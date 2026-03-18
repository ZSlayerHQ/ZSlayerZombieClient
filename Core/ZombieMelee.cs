using EFT;
using UnityEngine;

namespace ZSlayerZombieClient.Core;

/// <summary>
/// Handles melee attack delegation for zombie logic classes.
///
/// The vanilla zombie brain (GClass324) does TWO critical things we must replicate:
/// 1. ManualUpdate() forces ChangeToMelee() every frame (except Shooting mode)
/// 2. GClass98's decision logic calls ChangeToMelee() before returning oneMeleeAttack
///
/// Without forcing melee weapon equipped first, RunToEnemyUpdate() fails silently
/// because IsMelee=false and CanChangeToMeleeWeapons may be false, causing it to
/// return false immediately — no movement, no attack, no animation.
///
/// Our fix: force ChangeToMelee() before every RunToEnemyUpdate() call,
/// and fall back to our own movement if RunToEnemyUpdate fails.
/// </summary>
public static class ZombieMelee
{
    private const float MeleeEngageDistance = 5f;

    private static bool _loggedFirstMelee;
    private static bool _loggedFirstEquip;
    private static bool _loggedFirstFail;
    private static int _meleeCallCount;
    private static int _equipCount;
    private static int _failCount;

    /// <summary>
    /// Attempts vanilla melee attack if the zombie is close enough to the enemy.
    /// Returns true if melee is handling combat — caller should skip its own movement.
    /// Returns false if melee failed — caller should continue its own movement.
    /// </summary>
    public static bool TryMeleeAttack(BotOwner bot, float distance)
    {
        if (distance > MeleeEngageDistance) return false;
        if (bot.Memory?.GoalEnemy == null) return false;

        try
        {
            var melee = bot.WeaponManager.Melee;

            // Step 1: Ensure melee weapon is equipped (replicates vanilla ManualUpdate behavior)
            // The vanilla brain forces this every frame — without it, RunToEnemyUpdate exits immediately
            if (!bot.WeaponManager.IsMelee)
            {
                if (bot.WeaponManager.Selector.CanChangeToMeleeWeapons)
                {
                    bot.WeaponManager.Selector.ChangeToMelee();
                    _equipCount++;

                    if (!_loggedFirstEquip)
                    {
                        _loggedFirstEquip = true;
                        Plugin.Log.LogWarning($"[ZSlayerHQ] ZombieMelee: Forced ChangeToMelee for {ZombieDebug.BotId(bot)} (wasn't in melee mode)");
                    }
                }
                else
                {
                    // Can't switch to melee — this zombie has no melee weapon
                    // Fall back to our own movement (don't block the logic class)
                    _failCount++;

                    if (!_loggedFirstFail)
                    {
                        _loggedFirstFail = true;
                        Plugin.Log.LogWarning($"[ZSlayerHQ] ZombieMelee: CANNOT equip melee for {ZombieDebug.BotId(bot)} " +
                            $"(IsMelee={bot.WeaponManager.IsMelee}, CanChange={bot.WeaponManager.Selector.CanChangeToMeleeWeapons}, " +
                            $"MeleeEquipped={melee.MeleeWeaponEquipped}, KnifeCtrl={melee.KnifeController != null})");
                    }

                    ZombieDebug.LogThrottled("melee-no-weapon", 15f,
                        $"ZombieMelee: {_failCount} bots couldn't equip melee (no melee weapon available)");

                    return false; // Let logic class handle movement
                }
            }

            // Step 2: Call RunToEnemyUpdate — handles approach movement + hit attempt
            bool result = melee.RunToEnemyUpdate();
            _meleeCallCount++;

            if (!_loggedFirstMelee)
            {
                _loggedFirstMelee = true;
                Plugin.Log.LogWarning($"[ZSlayerHQ] ZombieMelee: FIRST melee delegation for {ZombieDebug.BotId(bot)} " +
                    $"at {distance:F1}m (result={result}, IsMelee={bot.WeaponManager.IsMelee}, " +
                    $"MeleeEquipped={melee.MeleeWeaponEquipped}, KnifeCtrl={melee.KnifeController != null})");
            }

            if (_meleeCallCount % 500 == 0)
            {
                ZombieDebug.Log($"ZombieMelee stats: {_meleeCallCount} calls, {_equipCount} equips, {_failCount} fails");
            }

            // If RunToEnemyUpdate returned false (ShallEndRun), it couldn't path to enemy
            // Let our logic class handle movement in that case
            if (!result && melee.ShallEndRun)
            {
                return false;
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

    /// <summary>
    /// Force melee weapon equipped on a zombie bot. Called periodically
    /// to replicate the vanilla brain's ManualUpdate behavior.
    /// Should be called from BotSpawnPatch or a periodic tick.
    /// </summary>
    public static void EnsureMeleeEquipped(BotOwner bot)
    {
        try
        {
            if (!bot.WeaponManager.IsMelee && bot.WeaponManager.Selector.CanChangeToMeleeWeapons)
            {
                bot.WeaponManager.Selector.ChangeToMelee();
            }
        }
        catch { }
    }
}
