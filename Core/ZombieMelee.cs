using System.Collections.Generic;
using System.Text;
using Comfort.Common;
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
    /// Maximum range for direct damage. Wider than vanilla melee to ensure
    /// damage lands — vanilla brain's damage callback is lost when we replace the brain.
    /// </summary>
    private const float DirectAttackRange = 3.5f;

    /// <summary>Seconds between direct damage hits per bot.</summary>
    private const float DirectAttackCooldown = 1.2f;

    /// <summary>Base damage per direct attack hit.</summary>
    private const float DirectAttackBaseDamage = 25f;

    private static bool _loggedFirstMelee;
    private static bool _loggedFirstEquip;
    private static bool _loggedFirstNoMelee;
    private static bool _loggedFirstDirectAttack;
    private static bool _loggedFirstKnifeKick;
    private static int _meleeCallCount;
    private static int _equipCount;
    private static int _failCount;
    private static int _noMeleeCallCount;
    private static int _directAttackCount;
    private static int _knifeKickCount;

    /// <summary>Per-bot cooldown tracker for direct damage attacks.</summary>
    private static readonly Dictionary<string, float> _lastDirectAttackTime = new();

    /// <summary>Per-bot last animator state hash — for discovering attack animations.</summary>
    private static readonly Dictionary<string, int> _lastAnimStateHash = new();

    /// <summary>Known animator state hashes seen during melee (for discovery).</summary>
    private static readonly HashSet<int> _discoveredAnimStates = new();

    /// <summary>Whether we've logged the animator state discovery summary.</summary>
    private static bool _loggedAnimDiscovery;

    /// <summary>Body parts to target with direct attacks (no head — zombies swipe at torso/limbs).</summary>
    private static readonly EBodyPart[] _directAttackBodyParts =
    {
        EBodyPart.Chest, EBodyPart.Stomach,
        EBodyPart.LeftArm, EBodyPart.RightArm,
        EBodyPart.LeftLeg, EBodyPart.RightLeg
    };

    /// <summary>
    /// Attempts melee attack: triggers vanilla animation + applies direct damage.
    ///
    /// Key insight: replacing the vanilla brain with BigBrain loses the damage callback
    /// that was tied to GClass324's state machine. RunToEnemyUpdate() triggers the
    /// animation/approach but damage never lands. We apply damage ourselves when in range.
    ///
    /// Returns true if melee is handling combat — caller should skip its own movement.
    /// </summary>
    public static bool TryMeleeAttack(BotOwner bot, float distance)
    {
        if (distance > MeleeEngageDistance) return false;
        if (bot.Memory?.GoalEnemy == null) return false;

        try
        {
            var melee = bot.WeaponManager.Melee;
            var botId = ZombieDebug.BotId(bot);
            bool hasMelee = bot.WeaponManager.IsMelee;

            // Step 1: Force melee weapon equipped (replicates vanilla ManualUpdate)
            if (!hasMelee && bot.WeaponManager.Selector.CanChangeToMeleeWeapons)
            {
                bot.WeaponManager.Selector.ChangeToMelee();
                hasMelee = true;
                _equipCount++;
                if (!_loggedFirstEquip)
                {
                    _loggedFirstEquip = true;
                    Plugin.Log.LogWarning($"[ZSlayerHQ] ZombieMelee: Forced ChangeToMelee for {botId}");
                }
            }

            // Step 2: Call RunToEnemyUpdate if melee is equipped — handles approach movement
            bool vanillaResult = false;
            if (hasMelee && bot.Memory?.GoalEnemy != null)
            {
                vanillaResult = melee.RunToEnemyUpdate();
                _meleeCallCount++;

                if (!_loggedFirstMelee)
                {
                    _loggedFirstMelee = true;
                    Plugin.Log.LogWarning($"[ZSlayerHQ] ZombieMelee: FIRST RunToEnemyUpdate for {botId} " +
                        $"at {distance:F1}m (result={vanillaResult}, KnifeCtrl={melee.KnifeController != null})");
                }

                if (!vanillaResult) _failCount++;
            }
            else
            {
                _noMeleeCallCount++;
            }

            // Step 3: ATTACK — try KnifeController.MakeKnifeKick() first (triggers swipe animation + damage)
            // The vanilla brain's GClass98 returns BotLogicDecision.oneMeleeAttack which calls method_2()
            // → KnifeController.MakeKnifeKick(). Since we replaced the brain with BigBrain, the decision
            // system never returns oneMeleeAttack. We call MakeKnifeKick() directly.
            if (distance <= DirectAttackRange)
            {
                bool kicked = TryKnifeKick(bot, melee);
                if (!kicked)
                {
                    // Fallback: direct damage for zombies without KnifeController
                    TryDirectAttack(bot, distance);
                }
                return true; // We're handling combat
            }

            // Step 4: Log animator state for attack animation discovery (only when close)
            if (distance <= DirectAttackRange + 2f)
                LogAnimatorState(bot, distance, vanillaResult);

            // Stats logging
            if ((_meleeCallCount + _noMeleeCallCount) % 500 == 0 && (_meleeCallCount + _noMeleeCallCount) > 0)
            {
                Plugin.Log.LogInfo($"[ZSlayerHQ] ZombieMelee stats: {_meleeCallCount} melee, {_noMeleeCallCount} no-weapon, " +
                    $"{_knifeKickCount} kicks, {_directAttackCount} direct hits, {_discoveredAnimStates.Count} anim states");
            }

            // Return true if vanilla melee is driving approach, false so logic class handles movement
            return vanillaResult;
        }
        catch (System.Exception ex)
        {
            ZombieDebug.LogThrottled($"melee-err-{ZombieDebug.BotId(bot)}", 10f,
                $"ZombieMelee error for {ZombieDebug.BotId(bot)}: {ex.Message}\n{ex.StackTrace}");
            return false;
        }
    }

    /// <summary>
    /// Trigger the actual swipe animation + damage via KnifeController.MakeKnifeKick().
    /// This is what the vanilla brain's BotLogicDecision.oneMeleeAttack → method_2() calls.
    /// We call it directly since BigBrain layers bypass the vanilla decision system.
    /// Returns true if the kick fired, false if KnifeController is null (no melee weapon).
    /// </summary>
    private static bool TryKnifeKick(BotOwner bot, BotMeleeWeaponData melee)
    {
        var knifeCtrl = melee.KnifeController;
        if (knifeCtrl == null) return false;

        var botId = ZombieDebug.BotId(bot);
        float time = Time.time;

        // Use the same per-bot cooldown as direct attacks
        if (_lastDirectAttackTime.TryGetValue(botId, out float lastTime) && time - lastTime < DirectAttackCooldown)
            return true; // Still in cooldown — return true so we don't fall through to direct damage

        _lastDirectAttackTime[botId] = time;

        try
        {
            // THE KEY CALL — triggers swipe animation + vanilla hit detection/damage
            bool result = knifeCtrl.MakeKnifeKick();
            _knifeKickCount++;

            // Vocalization (same as vanilla method_2)
            bot.BotTalk?.DropNextSayPeriod();
            bot.BotTalk?.Say(EPhraseTrigger.KnifeKill, sayImmediately: true);

            if (!_loggedFirstKnifeKick)
            {
                _loggedFirstKnifeKick = true;
                Plugin.Log.LogWarning($"[ZSlayerHQ] ZombieMelee: FIRST MakeKnifeKick by {botId} (result={result})");
            }

            return true;
        }
        catch (System.Exception ex)
        {
            ZombieDebug.LogThrottled($"kick-err-{botId}", 10f,
                $"KnifeKick error for {botId}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Log animator state transitions during melee combat.
    /// Used to discover attack animation state hashes for precise timing in future.
    /// </summary>
    private static void LogAnimatorState(BotOwner bot, float distance, bool vanillaResult)
    {
        try
        {
            var player = bot.GetPlayer;
            if (player == null) return;

            var animator = player.GetComponent<Animator>();
            if (animator == null) animator = player.GetComponentInChildren<Animator>();
            if (animator == null) return;

            var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            var botId = ZombieDebug.BotId(bot);
            int currentHash = stateInfo.fullPathHash;

            // Track state transitions
            _lastAnimStateHash.TryGetValue(botId, out int lastHash);

            if (currentHash != lastHash)
            {
                _lastAnimStateHash[botId] = currentHash;
                _discoveredAnimStates.Add(currentHash);

                // Log the transition — this helps us identify attack states
                if (distance <= DirectAttackRange + 2f)
                {
                    Plugin.Log.LogInfo($"[ZSlayerHQ] [ANIM] {botId} state={currentHash} norm={stateInfo.normalizedTime:F2} " +
                        $"len={stateInfo.length:F2}s speed={stateInfo.speed:F2} dist={distance:F1}m vanilla={vanillaResult} " +
                        $"isMelee={bot.WeaponManager.IsMelee}");
                }
            }

            // Periodic summary of discovered states
            if (!_loggedAnimDiscovery && _discoveredAnimStates.Count >= 5 && _meleeCallCount > 100)
            {
                _loggedAnimDiscovery = true;
                var sb = new StringBuilder();
                sb.Append($"[ZSlayerHQ] [ANIM-DISCOVERY] {_discoveredAnimStates.Count} unique states seen during melee: ");
                foreach (var hash in _discoveredAnimStates)
                    sb.Append($"{hash}, ");
                Plugin.Log.LogInfo(sb.ToString());
            }
        }
        catch { } // Don't let logging break combat
    }

    /// <summary>
    /// Direct damage fallback for zombies without melee weapons.
    /// Applies DamageInfoStruct with EDamageType.Melee directly to the target player.
    /// Cooldown-gated to prevent damage spam.
    /// </summary>
    private static bool TryDirectAttack(BotOwner bot, float distance)
    {
        var enemy = bot.Memory?.GoalEnemy;
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

            // IPlayerOwner is a bridge, NOT the Player class itself.
            // Obtain via GameWorld.GetAlivePlayerBridgeByProfileID()
            IPlayerOwner playerOwner = null;
            try
            {
                var gameWorld = Singleton<GameWorld>.Instance;
                if (gameWorld != null)
                    playerOwner = gameWorld.GetAlivePlayerBridgeByProfileID(bot.Profile.Id);
            }
            catch { }

            var damageInfo = new DamageInfoStruct
            {
                DamageType = EDamageType.Melee,
                Damage = damage,
                Player = playerOwner,
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
        _lastAnimStateHash.Remove(botId);
    }
}
