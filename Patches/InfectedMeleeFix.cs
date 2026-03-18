using HarmonyLib;
using ZSlayerZombieClient.Core;

namespace ZSlayerZombieClient.Patches;

/// <summary>
/// Fix for SAIN bug (GitHub: Solarint/SAIN#278) where RunToEnemyUpdatePatch
/// returns false for ALL bots, including infected bots that SAIN explicitly
/// excludes via StrictExclusionList. This prevents vanilla melee logic from
/// ever running for zombies — they chase but never swing or deal damage.
///
/// In HarmonyX (BepInEx), all prefixes always run regardless of return value.
/// The __runOriginal ref parameter is the final arbiter of whether the original
/// method executes. We run AFTER SAIN's patch and override __runOriginal back
/// to true for infected bots, restoring vanilla melee behavior.
///
/// When SAIN is not installed, __runOriginal is already true — this is a no-op.
///
/// Zombies use hands-based melee (no weapon needed). RunToEnemyUpdate handles
/// the approach + attack animation trigger for zombie bots automatically.
/// </summary>
[HarmonyPatch(typeof(BotMeleeWeaponData), nameof(BotMeleeWeaponData.RunToEnemyUpdate))]
public class InfectedMeleeFix
{
    private static bool _loggedFirstFix;
    private static bool _loggedFirstNoFix;
    private static int _fixCount;

    [HarmonyAfter("me.sol.sain")]
    [HarmonyPrefix]
    public static void Prefix(BotMeleeWeaponData __instance, ref bool __runOriginal)
    {
        var botOwner = __instance.BotOwner_0;
        if (botOwner == null) return;

        bool isInfected = ZombieIdentifier.IsInfected(botOwner);

        // For non-infected bots, don't interfere
        if (!isInfected) return;

        if (!__runOriginal)
        {
            // SAIN blocked the original — we need to restore it
            __runOriginal = true;
            _fixCount++;

            if (!_loggedFirstFix)
            {
                _loggedFirstFix = true;
                Plugin.Log.LogWarning($"[ZSlayerHQ] InfectedMeleeFix: RESTORED RunToEnemyUpdate for infected bot (SAIN was blocking melee)");
                Plugin.Log.LogWarning($"[ZSlayerHQ]   Bot: {ZombieDebug.BotId(botOwner)}, Role: {botOwner.Profile.Info.Settings.Role}");
            }

            ZombieDebug.LogThrottled("melee-fix", 10f,
                $"InfectedMeleeFix: restored melee for {ZombieDebug.BotId(botOwner)} (total fixes: {_fixCount})");
        }
        else
        {
            // Original was already going to run — SAIN not blocking (or SAIN not installed)
            if (!_loggedFirstNoFix)
            {
                _loggedFirstNoFix = true;
                ZombieDebug.Log($"InfectedMeleeFix: RunToEnemyUpdate already allowed for infected bot {ZombieDebug.BotId(botOwner)} (SAIN not blocking)");
            }
        }
    }
}
