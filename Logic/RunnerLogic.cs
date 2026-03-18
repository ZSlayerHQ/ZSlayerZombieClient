using DrakiaXYZ.BigBrain.Brains;
using EFT;
using UnityEngine;
using ZSlayerZombieClient.Core;

namespace ZSlayerZombieClient.Logic;

/// <summary>
/// Fast zombie with sprint burst / recovery cycle.
/// Terrifying because of the screaming sprint charges that close distance fast.
/// Zig-zags during sprint to make headshots harder.
/// Recovery phase is brief — just catching breath before the next charge.
/// </summary>
public class RunnerLogic : CustomLogic
{
    private float _nextPathTime;
    private float _nextLookTime;
    private float _phaseEndTime;
    private bool _isSprinting;
    private float _zigzagOffset;
    private float _zigzagTimer;

    public RunnerLogic(BotOwner botOwner) : base(botOwner) { }

    public override void Start()
    {
        BotOwner.Mover.SetPose(1f);
        _nextLookTime = 0f;
        _zigzagOffset = 0f;
        _zigzagTimer = 0f;

        // Start with a scream and sprint
        BotOwner.BotTalk?.Say(EPhraseTrigger.OnFight);
        StartSprintPhase();
    }

    public override void Update(CustomLayer.ActionData data)
    {
        var enemy = BotOwner.Memory.GoalEnemy;
        if (enemy == null) return;

        float time = Time.time;
        var targetPos = enemy.CurrPosition;
        float distance = (BotOwner.Position - targetPos).magnitude;

        // Head tracking — always face the target
        if (time >= _nextLookTime)
        {
            _nextLookTime = time + ZombieConstants.LookUpdateInterval;
            try { BotOwner.Steering?.LookToPoint(targetPos); }
            catch { }
        }

        // Toggle sprint/recovery phases
        if (time >= _phaseEndTime)
        {
            if (_isSprinting)
                StartRecoveryPhase();
            else
            {
                // Scream when starting a new sprint charge
                BotOwner.BotTalk?.Say(EPhraseTrigger.OnFight);
                StartSprintPhase();
            }
        }

        // Vocalize — screaming during sprint, growling during recovery
        float vocChance = _isSprinting
            ? ZombieConstants.RunnerVocalizationChance
            : ZombieConstants.VocalizationChance;
        if (Random.value < vocChance)
        {
            var trigger = _isSprinting ? EPhraseTrigger.OnEnemyConversation : EPhraseTrigger.OnMutter;
            BotOwner.BotTalk?.Say(trigger);
        }

        // Update path
        if (time < _nextPathTime) return;
        _nextPathTime = time + (_isSprinting
            ? ZombieConstants.FastPathUpdateInterval
            : ZombieConstants.PathUpdateInterval);

        if (distance > 4f)
        {
            if (_isSprinting && distance > 8f)
            {
                // Zig-zag during sprint — harder to headshot
                _zigzagTimer += Time.deltaTime * 3f;
                _zigzagOffset = Mathf.Sin(_zigzagTimer) * 2.5f;

                var direction = (targetPos - BotOwner.Position).normalized;
                var perpendicular = new Vector3(-direction.z, 0, direction.x);
                var zigzagTarget = targetPos + perpendicular * _zigzagOffset;

                BotOwner.Mover.GoToPoint(zigzagTarget, false, 1f);
            }
            else
            {
                // Direct approach when close or during recovery
                BotOwner.Mover.GoToPoint(targetPos, false, 1f);
            }
        }
        else
        {
            // Close range — controlled melee approach
            BotOwner.Mover.Sprint(false);
            BotOwner.Mover.SetTargetMoveSpeed(0.85f);
            BotOwner.Mover.GoToPoint(targetPos, false, 0.3f);
        }
    }

    public override void Stop()
    {
        BotOwner.Mover.Sprint(false);
    }

    private void StartSprintPhase()
    {
        _isSprinting = true;
        _phaseEndTime = Time.time +
            Plugin.ClientConfig.RunnerSprintDuration.Value +
            Random.Range(-1f, 1.5f);
        BotOwner.Mover.Sprint(true);
        BotOwner.Mover.SetTargetMoveSpeed(1f);
    }

    private void StartRecoveryPhase()
    {
        _isSprinting = false;
        // Short recovery — they don't rest long
        _phaseEndTime = Time.time +
            Plugin.ClientConfig.RunnerRecoveryDuration.Value +
            Random.Range(-0.5f, 1f);
        BotOwner.Mover.Sprint(false);
        BotOwner.Mover.SetTargetMoveSpeed(0.6f);
    }
}
