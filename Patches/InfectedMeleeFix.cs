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
    private static bool _loggedOnce;

    [HarmonyAfter("me.sol.sain")]
    [HarmonyPrefix]
    public static void Prefix(BotMeleeWeaponData __instance, ref bool __runOriginal)
    {
        // If the original was already going to run, nothing to fix
        if (__runOriginal) return;

        // Only override for infected bots — let SAIN manage everything else
        var botOwner = __instance.BotOwner_0;
        if (botOwner != null && ZombieIdentifier.IsInfected(botOwner))
        {
            __runOriginal = true;

            // Log once to confirm the fix is working
            if (!_loggedOnce)
            {
                _loggedOnce = true;
                Plugin.Log.LogInfo("[ZSlayerHQ] InfectedMeleeFix: restored RunToEnemyUpdate for infected bot (SAIN override active)");
            }
        }
    }
}
