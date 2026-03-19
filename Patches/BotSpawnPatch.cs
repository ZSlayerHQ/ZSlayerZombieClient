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
    private static int _infectedSpawnCount;

    [HarmonyPostfix]
    public static void Postfix(BotOwner __instance)
    {
        if (!ZombieIdentifier.IsInfected(__instance)) return;

        _infectedSpawnCount++;

        // Register zombie (lazy — may already be registered via layer activation)
        var entry = ZombieRegistry.GetOrRegister(__instance);

        // Set nickname to archetype name so kill feed / AmandsSense / telemetry shows "Stalker" instead of "???"
        // Profile.Nickname is a computed property (=> Info.Nickname), and Info.Nickname is a public string field.
        try
        {
            var archetypeName = entry.Archetype.Type.ToString();
            var nickname = __instance.Profile.Info.Nickname;
            if (string.IsNullOrEmpty(nickname) || nickname == "???" || nickname == "Infected")
            {
                __instance.Profile.Info.Nickname = archetypeName;
            }
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogWarning($"[ZSlayerHQ] Failed to set zombie nickname: {ex.Message}");
        }

        // Detect actual brain name via BaseBrain.ShortName() — this is what BigBrain matches
        try
        {
            var brainName = __instance.Brain?.BaseBrain?.ShortName() ?? "null";
            var role = __instance.Profile.Info.Settings.Role;

            // Always log the first infected spawn for brain name verification
            if (!_firstSpawnLogged)
            {
                _firstSpawnLogged = true;
                Plugin.Log.LogWarning($"[ZSlayerHQ] ========================================");
                Plugin.Log.LogWarning($"[ZSlayerHQ] FIRST INFECTED BOT SPAWNED");
                Plugin.Log.LogWarning($"[ZSlayerHQ]   Brain: '{brainName}'");
                Plugin.Log.LogWarning($"[ZSlayerHQ]   Role: {role}");
                Plugin.Log.LogWarning($"[ZSlayerHQ]   Archetype: {entry.Archetype.Type}");
                Plugin.Log.LogWarning($"[ZSlayerHQ]   SpeedMul: {entry.SpeedMultiplier:F2}x");
                Plugin.Log.LogWarning($"[ZSlayerHQ]   ProfileId: {ZombieDebug.BotId(__instance)}");
                Plugin.Log.LogWarning($"[ZSlayerHQ] ========================================");

                // Trigger runtime re-registration if brain name doesn't match
                Plugin.OnInfectedBrainDetected(brainName);
            }
            else
            {
                // Always log spawns (not just in debug) for now — we need visibility
                Plugin.Log.LogInfo($"[ZSlayerHQ] Infected #{_infectedSpawnCount}: role={role} brain='{brainName}' archetype={entry.Archetype.Type} speed={entry.SpeedMultiplier:F2}x id={ZombieDebug.BotId(__instance)}");
            }

            // Periodic summary
            if (_infectedSpawnCount % 10 == 0)
            {
                Plugin.Log.LogWarning($"[ZSlayerHQ] === {_infectedSpawnCount} infected bots spawned, {ZombieRegistry.Count} registered ===");
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
