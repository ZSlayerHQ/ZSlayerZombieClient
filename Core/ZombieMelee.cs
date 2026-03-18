using EFT;

namespace ZSlayerZombieClient.Core;

/// <summary>
/// Handles melee attack delegation for zombie logic classes.
///
/// Our BigBrain layers replace the vanilla zombie brain entirely, which means
/// the vanilla melee method (RunToEnemyUpdate) never gets called. This utility
/// bridges the gap: when a zombie is close enough, it delegates to the vanilla
/// melee system for approach + attack, while our custom logic handles everything
/// at range (archetype-specific movement patterns).
///
/// RunToEnemyUpdate handles: weapon equip, pose, steering, sprint, pathfinding,
/// and the actual hit attempt (KnifeController.MakeKnifeKick). We don't want to
/// duplicate any of that — just call it when in range and let it work.
/// </summary>
public static class ZombieMelee
{
    /// <summary>
    /// Distance threshold at which we delegate to vanilla melee logic.
    /// Below this distance, RunToEnemyUpdate controls movement + attacks.
    /// Above this, our archetype logic handles movement.
    /// </summary>
    private const float MeleeEngageDistance = 5f;

    private static bool _loggedFirstMelee;
    private static int _meleeCallCount;

    /// <summary>
    /// Attempts vanilla melee attack if the zombie is close enough to the enemy.
    /// Returns true if melee is handling combat — caller should skip its own movement.
    /// </summary>
    public static bool TryMeleeAttack(BotOwner bot, float distance)
    {
        if (distance > MeleeEngageDistance) return false;
        if (bot.Memory?.GoalEnemy == null) return false;

        try
        {
            bot.WeaponManager.Melee.RunToEnemyUpdate();
            _meleeCallCount++;

            if (!_loggedFirstMelee)
            {
                _loggedFirstMelee = true;
                Plugin.Log.LogWarning($"[ZSlayerHQ] ZombieMelee: FIRST melee delegation for {ZombieDebug.BotId(bot)} at {distance:F1}m");
            }

            if (_meleeCallCount % 100 == 0)
            {
                ZombieDebug.Log($"ZombieMelee: {_meleeCallCount} total melee delegations");
            }

            return true;
        }
        catch (System.Exception ex)
        {
            ZombieDebug.LogThrottled($"melee-err-{ZombieDebug.BotId(bot)}", 10f,
                $"ZombieMelee error for {ZombieDebug.BotId(bot)}: {ex.Message}");
            return false;
        }
    }
}
