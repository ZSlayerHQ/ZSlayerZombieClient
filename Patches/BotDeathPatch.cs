using EFT;
using HarmonyLib;
using ZSlayerZombieClient.Core;

namespace ZSlayerZombieClient.Patches;

/// <summary>
/// Cleanup zombie registry entries when bots are deactivated (death/despawn).
/// Also notifies nearby zombies of the death for horde awareness.
/// </summary>
[HarmonyPatch(typeof(BotOwner), nameof(BotOwner.Dispose))]
public class BotDeathPatch
{
    private static int _deathCount;

    [HarmonyPrefix]
    public static void Prefix(BotOwner __instance)
    {
        if (__instance == null) return;
        if (!ZombieIdentifier.IsInfected(__instance)) return;

        _deathCount++;

        if (ZombieRegistry.TryGet(__instance, out var entry))
        {
            ZombieDebug.Log($"Zombie death: {ZombieDebug.BotId(__instance)} " +
                $"archetype={entry.Archetype.Type} alert={entry.Alert} " +
                $"wasAlpha={entry.IsAlpha} (total deaths: {_deathCount})");

            // Death vocalization
            try { __instance.BotTalk?.Say(EPhraseTrigger.OnDeath); }
            catch { }
        }

        ZombieRegistry.Remove(__instance);
        ZombieMelee.OnBotRemoved(ZombieDebug.BotId(__instance));
    }
}
