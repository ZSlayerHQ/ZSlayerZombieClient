using EFT;
using HarmonyLib;
using ZSlayerZombieClient.Core;

namespace ZSlayerZombieClient.Patches;

/// <summary>
/// Register infected bots on spawn and log brain info for debugging.
/// CalcGoal is called during bot brain initialization.
/// If this method name changes between SPT versions, update the patch target.
/// </summary>
[HarmonyPatch(typeof(BotOwner), nameof(BotOwner.CalcGoal))]
public class BotSpawnPatch
{
    [HarmonyPostfix]
    public static void Postfix(BotOwner __instance)
    {
        if (!ZombieIdentifier.IsInfected(__instance)) return;

        // Register zombie (lazy — may already be registered via layer activation)
        var entry = ZombieRegistry.GetOrRegister(__instance);

        // Log brain name for debugging (helps identify correct brain names if they change)
        if (Plugin.ClientConfig.DebugLogging.Value)
        {
            try
            {
                var brainType = __instance.Brain?.GetType()?.Name ?? "unknown";
                Plugin.Log.LogInfo($"[ZSlayerHQ] Infected spawn: role={__instance.Profile.Info.Settings.Role}, brain={brainType}, archetype={entry.Archetype.Type}");
            }
            catch { /* Non-critical logging */ }
        }
    }
}
