using System;
using System.Linq;
using System.Reflection;
using DrakiaXYZ.BigBrain.Brains;
using EFT;
using HarmonyLib;
using ZSlayerZombieClient.Core;

namespace ZSlayerZombieClient.Patches;

/// <summary>
/// Fix for BigBrain layers not activating on infected bots.
///
/// Root cause: BigBrain's BotBaseBrainActivatePatch is a PREFIX on Activate().
/// It calls ShortName() to match brain names. But for infected bots (GClass324),
/// ShortName() reads from Gclass98_0.CurrentZombieMode — which is only initialized
/// DURING Activate()'s body (not before). So BigBrain's prefix sees null/uninitialized
/// state, ShortName() fails, and our layers are never added.
///
/// Fix: Add our own POSTFIX on Activate() that runs AFTER the brain is fully initialized,
/// then manually injects our CustomLayerWrapper instances into the brain's layer list.
/// </summary>
[HarmonyPatch]
public class ZombieBrainPatch
{
    private static MethodInfo _addLayerMethod;
    private static FieldInfo _botOwnerField;
    private static Type _wrapperType;
    private static ConstructorInfo _wrapperCtor;
    private static bool _loggedFirstInjection;
    private static int _injectionCount;
    private static bool _initFailed;

    [HarmonyTargetMethod]
    public static MethodBase TargetMethod()
    {
        try
        {
            var baseBrainType = typeof(BaseBrain);
            var strategyType = baseBrainType.BaseType;
            Plugin.Log?.LogInfo($"[ZSlayerHQ] ZombieBrainPatch: BaseBrain={baseBrainType.FullName}, BaseType={strategyType?.FullName}");

            _botOwnerField = AccessTools.GetDeclaredFields(baseBrainType)
                .FirstOrDefault(f => f.FieldType == typeof(BotOwner));
            if (_botOwnerField == null)
            {
                Plugin.Log?.LogError("[ZSlayerHQ] ZombieBrainPatch: could not find BotOwner field on BaseBrain");
                return null;
            }

            _addLayerMethod = AccessTools.GetDeclaredMethods(strategyType)
                .FirstOrDefault(m =>
                {
                    var p = m.GetParameters();
                    return p.Length == 3 && p[0].Name == "index" && p[1].Name == "layer";
                });
            if (_addLayerMethod == null)
            {
                // Try alternative: look for method with (int, AICoreLayerClass, bool) signature
                _addLayerMethod = AccessTools.GetDeclaredMethods(strategyType)
                    .FirstOrDefault(m =>
                    {
                        var p = m.GetParameters();
                        return p.Length == 3
                               && p[0].ParameterType == typeof(int)
                               && p[2].ParameterType == typeof(bool);
                    });
            }
            if (_addLayerMethod == null)
            {
                Plugin.Log?.LogError("[ZSlayerHQ] ZombieBrainPatch: could not find AddLayer method on strategy type");
                // Log all methods for debugging
                foreach (var m in AccessTools.GetDeclaredMethods(strategyType))
                {
                    var ps = m.GetParameters();
                    Plugin.Log?.LogWarning($"[ZSlayerHQ]   method: {m.Name}({string.Join(", ", ps.Select(p => $"{p.ParameterType.Name} {p.Name}"))})");
                }
                return null;
            }

            // Find BigBrain's internal CustomLayerWrapper type and its constructor
            _wrapperType = typeof(BrainManager).Assembly.GetTypes()
                .FirstOrDefault(t => t.Name == "CustomLayerWrapper");
            if (_wrapperType != null)
            {
                _wrapperCtor = _wrapperType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .FirstOrDefault(c => c.GetParameters().Length == 3);
            }
            Plugin.Log?.LogInfo($"[ZSlayerHQ] ZombieBrainPatch: WrapperType={_wrapperType?.Name ?? "NULL"}, Ctor={(_wrapperCtor != null ? "found" : "NULL")}");

            var activateMethod = AccessTools.Method(strategyType, "Activate");
            if (activateMethod == null)
            {
                // Try finding Activate on all base types
                var searchType = strategyType;
                while (searchType != null && activateMethod == null)
                {
                    activateMethod = searchType.GetMethod("Activate",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                    if (activateMethod == null)
                        searchType = searchType.BaseType;
                }
                Plugin.Log?.LogWarning($"[ZSlayerHQ] ZombieBrainPatch: Activate not found via AccessTools, searched hierarchy: {(activateMethod != null ? $"found on {searchType.Name}" : "NOT FOUND")}");
            }

            if (activateMethod != null)
                Plugin.Log?.LogInfo($"[ZSlayerHQ] ZombieBrainPatch: target = {activateMethod.DeclaringType.Name}.{activateMethod.Name} — patch will apply");
            else
                Plugin.Log?.LogError("[ZSlayerHQ] ZombieBrainPatch: FAILED — could not find Activate method anywhere");

            return activateMethod;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[ZSlayerHQ] ZombieBrainPatch.TargetMethod FAILED: {ex}");
            return null;
        }
    }

    [HarmonyPostfix]
    public static void Postfix(object __instance)
    {
        if (_initFailed) return;

        try
        {
            var botOwner = (BotOwner)_botOwnerField.GetValue(__instance);
            if (botOwner == null) return;
            if (!ZombieIdentifier.IsInfected(botOwner)) return;

            if (_wrapperType == null || _wrapperCtor == null || _addLayerMethod == null)
            {
                _initFailed = true;
                Plugin.Log.LogError("[ZSlayerHQ] ZombieBrainPatch: failed to resolve BigBrain internals");
                return;
            }

            // ShortName() should work now (post-activation, brain fully initialized)
            var brain = (BaseBrain)__instance;
            string brainName;
            try { brainName = brain.ShortName(); }
            catch { brainName = "unknown"; }

            // Check if BigBrain already added our layers during its prefix
            bool alreadyHasLayers = false;
            try
            {
                // List_0 is the active layers list on AICoreStrategyAbstractClass<T>
                var listField = __instance.GetType().BaseType?
                    .GetField("List_0", BindingFlags.Public | BindingFlags.Instance);
                if (listField != null)
                {
                    var list = listField.GetValue(__instance) as System.Collections.IList;
                    if (list != null)
                    {
                        foreach (var item in list)
                        {
                            if (_wrapperType.IsInstanceOfType(item))
                            {
                                // It's a CustomLayerWrapper — check if it's one of ours
                                var nameMethod = _wrapperType.GetMethod("Name");
                                var name = nameMethod?.Invoke(item, null) as string;
                                if (name != null && name.StartsWith("ZSlayer"))
                                {
                                    alreadyHasLayers = true;
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            if (alreadyHasLayers)
            {
                ZombieDebug.Log($"ZombieBrainPatch: layers already present for {ZombieDebug.BotId(botOwner)}");
                return;
            }

            // Inject our layers — BigBrain's prefix missed them because ShortName wasn't ready
            int injected = 0;
            foreach (var layerInfo in BrainManager.CustomLayersReadOnly.Values)
            {
                if (!layerInfo.CustomLayerBrains.Contains(brainName)) continue;
                if (!layerInfo.CustomLayerRoles.Contains(botOwner.Profile.Info.Settings.Role)) continue;

                try
                {
                    var wrapper = _wrapperCtor.Invoke(new object[]
                    {
                        layerInfo.customLayerType,
                        botOwner,
                        layerInfo.customLayerPriority
                    });
                    _addLayerMethod.Invoke(__instance, new object[]
                    {
                        layerInfo.customLayerId,
                        wrapper,
                        true
                    });
                    injected++;
                    _injectionCount++;
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError($"[ZSlayerHQ] ZombieBrainPatch: inject failed: {ex.InnerException?.Message ?? ex.Message}");
                }
            }

            if (injected > 0 && !_loggedFirstInjection)
            {
                _loggedFirstInjection = true;
                Plugin.Log.LogWarning($"[ZSlayerHQ] ========================================");
                Plugin.Log.LogWarning($"[ZSlayerHQ] ZombieBrainPatch: FIRST LAYER INJECTION");
                Plugin.Log.LogWarning($"[ZSlayerHQ]   Bot: {ZombieDebug.BotId(botOwner)}");
                Plugin.Log.LogWarning($"[ZSlayerHQ]   Brain: '{brainName}'");
                Plugin.Log.LogWarning($"[ZSlayerHQ]   Layers injected: {injected}");
                Plugin.Log.LogWarning($"[ZSlayerHQ]   (BigBrain prefix couldn't read ShortName)");
                Plugin.Log.LogWarning($"[ZSlayerHQ] ========================================");
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"[ZSlayerHQ] ZombieBrainPatch error: {ex.Message}");
        }
    }
}
