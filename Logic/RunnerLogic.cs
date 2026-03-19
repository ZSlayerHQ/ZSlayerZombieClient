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
    private float _speedMul;

    public RunnerLogic(BotOwner botOwner) : base(botOwner) { }

    public override void Start()
    {
        _speedMul = 1f;
        if (ZombieRegistry.TryGet(BotOwner, out var entry))
            _speedMul = entry.SpeedMultiplier;

        BotOwner.Mover.SetPose(1f);
        _nextLookTime = 0f;
        _zigzagOffset = 0f;
        _zigzagTimer = 0f;

        // Start with a scream and sprint
        BotOwner.BotTalk?.Say(EPhraseTrigger.OnFight);
        StartSprintPhase();

        ZombieDebug.LogLogicStart("Runner", BotOwner);
    }

    public override void Update(CustomLayer.ActionData data)
    {
        var enemy = BotOwner.Memory.GoalEnemy;
        if (enemy == null) return;

        float time = Time.time;
        var targetPos = enemy.CurrPosition;
        float distance = (BotOwner.Position - targetPos).magnitude;
        ZombieHelper.FaceTarget(BotOwner, targetPos);

        // Delegate to vanilla melee when in close combat range
        if (ZombieMelee.TryMeleeAttack(BotOwner, distance))
            return;

        // Horde rush overrides archetype behavior
        if (ZombieRush.HandleRush(BotOwner, distance))
            return;

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

        // Vocalize — screaming during sprint, growling during recovery (reduced when far)
        float vocChance = (_isSprinting
            ? ZombieConstants.RunnerVocalizationChance
            : ZombieConstants.VocalizationChance) * ZombieHelper.GetVocalizationMultiplier(distance);
        if (Random.value < vocChance)
        {
            var trigger = _isSprinting ? EPhraseTrigger.OnEnemyConversation : EPhraseTrigger.OnMutter;
            BotOwner.BotTalk?.Say(trigger);
        }

        // Update path — distance-based throttling for performance
        if (time < _nextPathTime) return;
        float baseInterval = _isSprinting ? ZombieConstants.FastPathUpdateInterval : ZombieConstants.PathUpdateInterval;
        _nextPathTime = time + (distance > 50f ? baseInterval * 3f : distance > 20f ? baseInterval * 1.5f : baseInterval);

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
        float duration = Plugin.ClientConfig.RunnerSprintDuration.Value + Random.Range(-1f, 1.5f);
        _phaseEndTime = Time.time + duration;
        BotOwner.Mover.Sprint(true);
        BotOwner.Mover.SetTargetMoveSpeed(ZombieHelper.ApplySpeedVariance(1f, _speedMul));

        ZombieDebug.LogStateChange("Runner", BotOwner, "Recovery", "Sprint", $"duration={duration:F1}s");
    }

    private void StartRecoveryPhase()
    {
        _isSprinting = false;
        float duration = Plugin.ClientConfig.RunnerRecoveryDuration.Value + Random.Range(-0.5f, 1f);
        _phaseEndTime = Time.time + duration;
        BotOwner.Mover.Sprint(false);
        BotOwner.Mover.SetTargetMoveSpeed(ZombieHelper.ApplySpeedVariance(0.6f, _speedMul));

        ZombieDebug.LogStateChange("Runner", BotOwner, "Sprint", "Recovery", $"duration={duration:F1}s");
    }
}
