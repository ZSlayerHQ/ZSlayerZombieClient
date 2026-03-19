using DrakiaXYZ.BigBrain.Brains;
using EFT;
using UnityEngine;
using ZSlayerZombieClient.Core;

namespace ZSlayerZombieClient.Logic;

/// <summary>
/// Slow, shambling zombie. Erratic movement with stumbles and pauses.
/// Creates tension through unpredictability — false pauses that lull you
/// into thinking they stopped, then sudden lurches forward.
/// Head always locked on the player. Vocalizations increase as they close in.
/// </summary>
public class ShamblerLogic : CustomLogic
{
    private float _nextPathTime;
    private float _nextStumbleTime;
    private float _nextLookTime;
    private bool _isStumbling;
    private float _stumbleEndTime;
    private float _speed;
    private bool _isLunging;
    private float _lungeEndTime;
    private float _lastDistance;
    private float _speedMul;

    public ShamblerLogic(BotOwner botOwner) : base(botOwner) { }

    public override void Start()
    {
        // Get per-zombie speed multiplier for variance
        _speedMul = 1f;
        if (ZombieRegistry.TryGet(BotOwner, out var entry))
            _speedMul = entry.SpeedMultiplier;

        BotOwner.Mover.Sprint(false);
        _speed = ZombieHelper.ApplySpeedVariance(
            Random.Range(Plugin.ClientConfig.ShamblerMinSpeed.Value, Plugin.ClientConfig.ShamblerMaxSpeed.Value),
            _speedMul);
        BotOwner.Mover.SetTargetMoveSpeed(_speed);
        BotOwner.Mover.SetPose(1f);
        _nextStumbleTime = Time.time + Random.Range(
            ZombieConstants.StumbleMinInterval, ZombieConstants.StumbleMaxInterval);
        _nextPathTime = 0f;
        _nextLookTime = 0f;
        _isLunging = false;
        _lastDistance = 999f;

        ZombieDebug.LogLogicStart("Shambler", BotOwner, $"speed={_speed:F2}");
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

        // Head tracking — always stare at the player (creepy)
        if (time >= _nextLookTime)
        {
            _nextLookTime = time + ZombieConstants.LookUpdateInterval;
            try { BotOwner.Steering?.LookToPoint(targetPos); }
            catch { }
        }

        // Lunge state (close-range burst)
        if (_isLunging)
        {
            if (time >= _lungeEndTime)
            {
                _isLunging = false;
                BotOwner.Mover.Sprint(false);
                BotOwner.Mover.SetTargetMoveSpeed(_speed);
            }
            else
            {
                // During lunge: sprint directly at target
                if (time >= _nextPathTime)
                {
                    _nextPathTime = time + ZombieConstants.FastPathUpdateInterval;
                    BotOwner.Mover.GoToPoint(targetPos, false, 0.5f);
                }
                return;
            }
        }

        // Stumble mechanic — random pauses create false sense of safety
        if (_isStumbling)
        {
            if (time < _stumbleEndTime)
            {
                // During stumble: still look at player (menacing stare while paused)
                return;
            }

            _isStumbling = false;
            _speed = ZombieHelper.ApplySpeedVariance(
                Random.Range(Plugin.ClientConfig.ShamblerMinSpeed.Value, Plugin.ClientConfig.ShamblerMaxSpeed.Value),
                _speedMul);
            BotOwner.Mover.SetTargetMoveSpeed(_speed);
            _nextStumbleTime = time + Random.Range(
                ZombieConstants.StumbleMinInterval, ZombieConstants.StumbleMaxInterval);

            // 20% chance: lurch forward after stumble (scary burst)
            if (distance < ZombieConstants.LungeDistance * 2f && Random.value < 0.2f)
            {
                _speed = Mathf.Min(_speed + 0.2f, 0.8f);
                BotOwner.Mover.SetTargetMoveSpeed(_speed);
            }
        }

        if (time >= _nextStumbleTime && !_isLunging)
        {
            _isStumbling = true;
            // Longer stumbles at distance (patient), shorter up close (urgent)
            float stumbleDuration = distance > 15f
                ? Random.Range(1f, 2.5f)
                : Random.Range(ZombieConstants.StumbleMinDuration, ZombieConstants.StumbleMaxDuration);
            _stumbleEndTime = time + stumbleDuration;
            BotOwner.Mover.SetTargetMoveSpeed(0.05f);
            ZombieDebug.LogThrottled($"shambler-stumble-{ZombieDebug.BotId(BotOwner)}", 10f,
                $"Shambler [{ZombieDebug.BotId(BotOwner)}]: STUMBLE for {stumbleDuration:F1}s at {distance:F1}m");
            return;
        }

        // Vocalize — frequency increases as zombie gets closer, reduced when far
        float vocChance = ZombieConstants.VocalizationChance * ZombieHelper.GetVocalizationMultiplier(distance);
        if (distance < 10f) vocChance *= 3f;
        else if (distance < 20f) vocChance *= 2f;
        if (Random.value < vocChance)
        {
            var trigger = distance < 5f ? EPhraseTrigger.OnFight : EPhraseTrigger.OnEnemyConversation;
            BotOwner.BotTalk?.Say(trigger);
        }

        // Update path — distance-based throttling for performance
        if (time < _nextPathTime) return;
        _nextPathTime = time + ZombieHelper.GetPathInterval(distance);

        // Trigger lunge when entering close range
        if (distance < ZombieConstants.LungeDistance && _lastDistance >= ZombieConstants.LungeDistance)
        {
            _isLunging = true;
            _lungeEndTime = time + ZombieConstants.LungeDuration;
            BotOwner.Mover.Sprint(true);
            BotOwner.Mover.SetTargetMoveSpeed(1f);
            BotOwner.BotTalk?.Say(EPhraseTrigger.OnFight);
            _lastDistance = distance;
            BotOwner.Mover.GoToPoint(targetPos, false, 0.5f);
            ZombieDebug.LogCombatEvent("Shambler", BotOwner, "LUNGE", distance);
            return;
        }

        _lastDistance = distance;

        if (distance > 4f)
        {
            // Erratic lateral offset — shambling gait, not a straight line
            var offset = new Vector3(
                Random.Range(-1.5f, 1.5f),
                0,
                Random.Range(-1.5f, 1.5f));
            BotOwner.Mover.GoToPoint(targetPos + offset, false, 1f);
        }
        else
        {
            // Very close — direct aggressive approach
            BotOwner.Mover.SetTargetMoveSpeed(Mathf.Min(_speed + ZombieConstants.LungeSpeedBoost, 0.9f));
            BotOwner.Mover.GoToPoint(targetPos, false, 0.3f);
        }
    }

    public override void Stop()
    {
        _isStumbling = false;
        _isLunging = false;
        BotOwner.Mover.Sprint(false);
    }
}
