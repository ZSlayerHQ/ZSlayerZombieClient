using DrakiaXYZ.BigBrain.Brains;
using EFT;
using UnityEngine;
using ZSlayerZombieClient.Core;

namespace ZSlayerZombieClient.Logic;

/// <summary>
/// The most terrifying archetype. Max speed at all times, never pauses,
/// never stops, constant screaming. Direct path to target — they're too
/// enraged to bother with zig-zagging. Used for infectedTagilla (always)
/// and any zombie that takes massive damage quickly.
/// </summary>
public class BerserkerLogic : CustomLogic
{
    private float _nextPathTime;
    private float _nextLookTime;
    private float _nextScreamTime;

    public BerserkerLogic(BotOwner botOwner) : base(botOwner) { }

    public override void Start()
    {
        // Max aggression from the start
        BotOwner.Mover.SetPose(1f);
        BotOwner.Mover.Sprint(true);
        BotOwner.Mover.SetTargetMoveSpeed(1f);
        _nextPathTime = 0f;
        _nextLookTime = 0f;
        _nextScreamTime = 0f;

        // Opening scream
        BotOwner.BotTalk?.Say(EPhraseTrigger.OnFight);

        ZombieDebug.LogLogicStart("Berserker", BotOwner, "MAX AGGRESSION");
    }

    public override void Update(CustomLayer.ActionData data)
    {
        var enemy = BotOwner.Memory.GoalEnemy;
        if (enemy == null) return;

        float time = Time.time;
        var targetPos = enemy.CurrPosition;
        float distance = (BotOwner.Position - targetPos).magnitude;

        // Delegate to vanilla melee when in close combat range
        if (ZombieMelee.TryMeleeAttack(BotOwner, distance))
            return;

        // Horde rush overrides archetype behavior (berserker is already max aggro, but rush provides target sharing)
        if (ZombieRush.HandleRush(BotOwner, distance))
            return;

        // Constant head tracking
        if (time >= _nextLookTime)
        {
            _nextLookTime = time + ZombieConstants.LookUpdateInterval;
            try { BotOwner.Steering?.LookToPoint(targetPos); }
            catch { }
        }

        // Constant screaming — periodic war cries
        if (time >= _nextScreamTime)
        {
            _nextScreamTime = time + Random.Range(1.5f, 3f);
            BotOwner.BotTalk?.Say(EPhraseTrigger.OnFight);
        }

        // Continuous vocalization between screams (reduced when far)
        if (Random.value < ZombieConstants.BerserkerVocalizationChance * ZombieHelper.GetVocalizationMultiplier(distance))
            BotOwner.BotTalk?.Say(EPhraseTrigger.OnEnemyConversation);

        // Path update — tightest tracking, distance-throttled for performance
        if (time < _nextPathTime) return;
        _nextPathTime = time + (distance > 50f ? 1f : distance > 20f ? 0.4f : ZombieConstants.BerserkerPathUpdateInterval);

        if (distance > 3f)
        {
            // Full sprint, direct path, no deviations
            BotOwner.Mover.Sprint(true);
            BotOwner.Mover.SetTargetMoveSpeed(1f);
            BotOwner.Mover.GoToPoint(targetPos, false, 0.5f);
        }
        else
        {
            // Very close — stop sprinting for melee but keep max walk speed
            BotOwner.Mover.Sprint(false);
            BotOwner.Mover.SetTargetMoveSpeed(1f);
            BotOwner.Mover.GoToPoint(targetPos, false, 0.2f);
        }
    }

    public override void Stop()
    {
        BotOwner.Mover.Sprint(false);
    }
}
