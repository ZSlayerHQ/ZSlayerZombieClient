using EFT;
using HarmonyLib;
using ZSlayerZombieClient.Core;

namespace ZSlayerZombieClient.Patches;

/// <summary>
/// Register infected bots on spawn, detect brain name for BigBrain matching,
/// and trigger runtime re-registration if names don't match.
/// </summary>
[HarmonyPatch(typeof(BotOwner), nameof(BotOwner.CalcGoal))]
public class BotSpawnPatch
{
    private static bool _firstSpawnLogged;

    [HarmonyPostfix]
    public static void Postfix(BotOwner __instance)
    {
        if (!ZombieIdentifier.IsInfected(__instance)) return;

        // Register zombie (lazy — may already be registered via layer activation)
        var entry = ZombieRegistry.GetOrRegister(__instance);

        // Detect actual brain name via BaseBrain.ShortName() — this is what BigBrain matches
        try
        {
            var brainName = __instance.Brain?.BaseBrain?.ShortName() ?? "null";
            var role = __instance.Profile.Info.Settings.Role;

            // Always log the first infected spawn for brain name verification
            if (!_firstSpawnLogged)
            {
                _firstSpawnLogged = true;
                Plugin.Log.LogWarning($"[ZSlayerHQ] First infected bot brain detected: '{brainName}' (role={role}, archetype={entry.Archetype.Type})");

                // Trigger runtime re-registration if brain name doesn't match
                Plugin.OnInfectedBrainDetected(brainName);
            }
            else if (Plugin.ClientConfig.DebugLogging.Value)
            {
                Plugin.Log.LogInfo($"[ZSlayerHQ] Infected spawn: role={role}, brain='{brainName}', archetype={entry.Archetype.Type}");
            }
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogError($"[ZSlayerHQ] Failed to detect brain name: {ex.Message}");

            // Fallback: try GetType().Name on the brain's base brain object
            try
            {
                var baseBrain = __instance.Brain?.BaseBrain;
                var typeName = baseBrain?.GetType()?.Name ?? "unknown";
                Plugin.Log.LogWarning($"[ZSlayerHQ] Brain type fallback: {typeName}");
            }
            catch { }
        }
    }
}
