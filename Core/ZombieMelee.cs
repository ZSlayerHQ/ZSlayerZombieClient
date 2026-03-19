using System.Collections.Generic;
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
/// and fall back to direct damage if RunToEnemyUpdate fails at close range.
/// </summary>
public static class ZombieMelee
{
    /// <summary>
    /// Distance at which we start delegating to vanilla melee system.
    /// RunToEnemyUpdate handles both approach and attack, so we give it
    /// enough room to build up its state machine (approach → close → swing).
    /// </summary>
    private const float MeleeEngageDistance = 10f;

    /// <summary>
    /// Maximum range for direct damage fallback (when no melee weapon).
    /// Zombie must be very close — this simulates a grab/scratch attack.
    /// </summary>
    private const float DirectAttackRange = 2.5f;

    /// <summary>Seconds between direct damage hits per bot.</summary>
    private const float DirectAttackCooldown = 1.5f;

    /// <summary>Base damage per direct attack hit.</summary>
    private const float DirectAttackBaseDamage = 25f;

    private static bool _loggedFirstMelee;
    private static bool _loggedFirstEquip;
    private static bool _loggedFirstFail;
    private static bool _loggedFirstNoMelee;
    private static bool _loggedFirstDirectAttack;
    private static int _meleeCallCount;
    private static int _equipCount;
    private static int _failCount;
    private static int _noMeleeCallCount;
    private static int _directAttackCount;

    /// <summary>Per-bot cooldown tracker for direct damage attacks.</summary>
    private static readonly Dictionary<string, float> _lastDirectAttackTime = new();

    /// <summary>Body parts to target with direct attacks (no head — zombies swipe at torso/limbs).</summary>
    private static readonly EBodyPart[] _directAttackBodyParts =
    {
        EBodyPart.Chest, EBodyPart.Stomach,
        EBodyPart.LeftArm, EBodyPart.RightArm,
        EBodyPart.LeftLeg, EBodyPart.RightLeg
    };

    /// <summary>
    /// Attempts vanilla melee attack if the zombie is close enough to the enemy.
    /// Falls back to direct damage application for zombies without melee weapons.
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
            // The vanilla brain forces this every frame — without it, RunToEnemyUpdate may exit immediately
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
                    _noMeleeCallCount++;

                    if (!_loggedFirstNoMelee)
                    {
                        _loggedFirstNoMelee = true;
                        Plugin.Log.LogWarning($"[ZSlayerHQ] ZombieMelee: Cannot ChangeToMelee for {ZombieDebug.BotId(bot)} " +
                            $"(IsMelee={bot.WeaponManager.IsMelee}, CanChange={bot.WeaponManager.Selector.CanChangeToMeleeWeapons}, " +
                            $"MeleeEquipped={melee.MeleeWeaponEquipped}, KnifeCtrl={melee.KnifeController != null}) " +
                            $"— will try RunToEnemyUpdate then direct attack fallback");
                    }
                }
            }

            // Step 2: Call RunToEnemyUpdate — handles approach movement + hit attempt
            bool result = melee.RunToEnemyUpdate();
            _meleeCallCount++;

            if (!_loggedFirstMelee)
            {
                _loggedFirstMelee = true;
                Plugin.Log.LogWarning($"[ZSlayerHQ] ZombieMelee: FIRST RunToEnemyUpdate for {ZombieDebug.BotId(bot)} " +
                    $"at {distance:F1}m (result={result}, IsMelee={bot.WeaponManager.IsMelee}, " +
                    $"MeleeEquipped={melee.MeleeWeaponEquipped}, KnifeCtrl={melee.KnifeController != null}, " +
                    $"ShallEndRun={melee.ShallEndRun})");
            }

            if (_meleeCallCount % 500 == 0)
            {
                ZombieDebug.Log($"ZombieMelee stats: {_meleeCallCount} calls, {_equipCount} equips, " +
                    $"{_noMeleeCallCount} no-melee, {_failCount} fails, {_directAttackCount} direct attacks");
            }

            // RunToEnemyUpdate succeeded — vanilla melee is handling combat
            if (result) return true;

            // RunToEnemyUpdate failed — try direct damage if close enough
            _failCount++;

            if (!_loggedFirstFail)
            {
                _loggedFirstFail = true;
                Plugin.Log.LogWarning($"[ZSlayerHQ] ZombieMelee: RunToEnemyUpdate FAILED for {ZombieDebug.BotId(bot)} " +
                    $"(result=false, ShallEndRun={melee.ShallEndRun}, IsMelee={bot.WeaponManager.IsMelee}) " +
                    $"— will try direct attack if within {DirectAttackRange}m");
            }

            ZombieDebug.LogThrottled("melee-fail", 30f,
                $"ZombieMelee: {_failCount} RunToEnemyUpdate failures, {_directAttackCount} direct attacks");

            // Fallback: direct damage for zombies at very close range without melee weapons
            if (distance <= DirectAttackRange)
                return TryDirectAttack(bot, distance);

            return false;
        }
        catch (System.Exception ex)
        {
            ZombieDebug.LogThrottled($"melee-err-{ZombieDebug.BotId(bot)}", 10f,
                $"ZombieMelee error for {ZombieDebug.BotId(bot)}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Direct damage fallback for zombies without melee weapons.
    /// Applies DamageInfoStruct with EDamageType.Melee directly to the target player.
    /// Cooldown-gated to prevent damage spam.
    /// </summary>
    private static bool TryDirectAttack(BotOwner bot, float distance)
    {
        var enemy = bot.Memory.GoalEnemy;
        if (enemy?.Person == null) return false;

        var botId = ZombieDebug.BotId(bot);
        float time = Time.time;

        // Cooldown — return true during cooldown so logic class doesn't override with movement
        if (_lastDirectAttackTime.TryGetValue(botId, out float lastTime) && time - lastTime < DirectAttackCooldown)
            return true;

        try
        {
            var targetPlayer = enemy.Person as Player;
            if (targetPlayer == null) return false;

            _lastDirectAttackTime[botId] = time;

            // Random body part (no head — zombies swipe/grab at torso and limbs)
            var bodyPart = _directAttackBodyParts[Random.Range(0, _directAttackBodyParts.Length)];

            // Slight damage variance for feel
            float damage = DirectAttackBaseDamage + Random.Range(-5f, 10f);

            var direction = (targetPlayer.Position - bot.Position).normalized;

            var damageInfo = new DamageInfoStruct
            {
                DamageType = EDamageType.Melee,
                Damage = damage,
                Player = (IPlayerOwner)bot.GetPlayer,
                Direction = direction,
                HitPoint = targetPlayer.Position + Vector3.up * 0.8f,
                MasterOrigin = bot.Position,
                HitNormal = Vector3.up,
                IsForwardHit = true,
                PenetrationPower = 0f,
                ArmorDamage = 0f,
                StaminaBurnRate = 0.15f,
                LightBleedingDelta = 0.15f,
                HeavyBleedingDelta = 0.05f,
            };

            targetPlayer.ApplyDamageInfo(damageInfo, bodyPart, EBodyPartColliderType.None, 0f);

            _directAttackCount++;

            // Attack vocalization
            bot.BotTalk?.Say(EPhraseTrigger.OnFight);

            if (!_loggedFirstDirectAttack)
            {
                _loggedFirstDirectAttack = true;
                Plugin.Log.LogWarning($"[ZSlayerHQ] ZombieMelee: FIRST direct attack by {botId} — " +
                    $"{damage:F0} dmg to {bodyPart} at {distance:F1}m");
            }

            ZombieDebug.LogThrottled("direct-atk", 30f,
                $"ZombieMelee: {_directAttackCount} direct attacks applied");

            return true;
        }
        catch (System.Exception ex)
        {
            ZombieDebug.LogThrottled("direct-atk-err", 10f,
                $"Direct attack error for {botId}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Force melee weapon equipped on a zombie bot. Called periodically
    /// to replicate the vanilla brain's ManualUpdate behavior.
    /// Should be called from ZombieMainLayer.IsActive() every frame.
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

    /// <summary>
    /// Clean up tracking data for a bot that died/despawned.
    /// Called from BotDeathPatch.
    /// </summary>
    public static void OnBotRemoved(string botId)
    {
        _lastDirectAttackTime.Remove(botId);
    }
}
