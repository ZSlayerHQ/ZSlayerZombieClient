using EFT;
using UnityEngine;

namespace ZSlayerZombieClient.Core;

/// <summary>
/// Handles horde rush behavior for zombie logic classes.
///
/// When a zombie is part of a coordinated rush (set by HordeCoordinator),
/// this overrides its normal archetype movement with max-speed direct charge.
/// All archetype personality is suspended during a rush — every zombie
/// becomes a berserker for the duration.
///
/// Called at the top of each logic's Update(), similar to ZombieMelee.
/// Returns true if rush is active (caller should skip normal behavior).
/// </summary>
public static class ZombieRush
{
    private static bool _loggedFirstRush;

    /// <summary>
    /// If this zombie is rushing, override movement to max-speed charge.
    /// Returns true if rush is handling movement (caller should return).
    /// </summary>
    public static bool HandleRush(BotOwner bot, float distance)
    {
        if (!ZombieRegistry.TryGet(bot, out var entry)) return false;
        if (!entry.IsRushing) return false;

        float time = Time.time;

        // Rush expired
        if (time >= entry.RushEndTime)
        {
            entry.IsRushing = false;
            return false;
        }

        // Determine target: use enemy position if available, otherwise horde target
        Vector3? target = null;
        var enemy = bot.Memory?.GoalEnemy;
        if (enemy != null)
            target = enemy.CurrPosition;
        else if (entry.HordeTargetPosition.HasValue)
            target = entry.HordeTargetPosition.Value;

        if (!target.HasValue) return false;

        if (!_loggedFirstRush)
        {
            _loggedFirstRush = true;
            Plugin.Log.LogWarning($"[ZSlayerHQ] ZombieRush: First rush behavior for {ZombieDebug.BotId(bot)}");
        }

        // Max speed, direct approach, constant screaming
        bot.Mover.Sprint(true);
        bot.Mover.SetTargetMoveSpeed(1f);
        bot.Mover.SetPose(1f);

        try { bot.Steering?.LookToPoint(target.Value); }
        catch { }

        bot.Mover.GoToPoint(target.Value, false, 0.5f);

        // Frequent screaming during rush
        if (Random.value < 0.02f)
            bot.BotTalk?.Say(EPhraseTrigger.OnFight);

        return true;
    }
}
