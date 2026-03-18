using DrakiaXYZ.BigBrain.Brains;
using EFT;
using UnityEngine;
using ZSlayerZombieClient.Core;

namespace ZSlayerZombieClient.Logic;

/// <summary>
/// The smart one. Retained enough intelligence to be tactical.
/// Crouches while circling, stays at distance, uses cover-like behavior.
/// Approaches from flanks and behind. Silent until the rush.
/// When rushing: stands upright, full sprint, attack scream.
/// The most unsettling archetype — a zombie that THINKS.
/// </summary>
public class StalkerLogic : CustomLogic
{
    private float _nextPathTime;
    private float _nextLookTime;
    private bool _isRushing;
    private float _rushEndTime;
    private float _circleAngle;
    private float _nextPeekTime;
    private bool _isPeeking;
    private float _peekEndTime;

    public StalkerLogic(BotOwner botOwner) : base(botOwner) { }

    public override void Start()
    {
        BotOwner.Mover.Sprint(false);
        BotOwner.Mover.SetTargetMoveSpeed(0.4f);
        BotOwner.Mover.SetPose(0.3f); // Crouched — stalkers are smarter
        _nextPathTime = 0f;
        _nextLookTime = 0f;
        _isRushing = false;
        _isPeeking = false;
        _circleAngle = Random.Range(0f, 360f);
        _nextPeekTime = Time.time + Random.Range(3f, 6f);

        ZombieDebug.LogLogicStart("Stalker", BotOwner, "crouched, circling");
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

        // Head tracking — always watching
        if (time >= _nextLookTime)
        {
            _nextLookTime = time + ZombieConstants.LookUpdateInterval;
            try { BotOwner.Steering?.LookToPoint(targetPos); }
            catch { }
        }

        // Rush state — stand up, full sprint, attack
        if (_isRushing)
        {
            if (time >= _rushEndTime || distance < 2f)
            {
                _isRushing = false;
                BotOwner.Mover.Sprint(false);
                BotOwner.Mover.SetPose(0.3f); // Back to crouch
                BotOwner.Mover.SetTargetMoveSpeed(0.4f);
            }
            else
            {
                if (time >= _nextPathTime)
                {
                    _nextPathTime = time + ZombieConstants.FastPathUpdateInterval;
                    BotOwner.Mover.GoToPoint(targetPos, false, 0.3f);
                }
                return;
            }
        }

        // Peek behavior — stop, watch, assess (like an intelligent predator)
        if (_isPeeking)
        {
            if (time >= _peekEndTime)
            {
                _isPeeking = false;
                BotOwner.Mover.SetTargetMoveSpeed(0.4f);
                _nextPeekTime = time + Random.Range(4f, 8f);
            }
            else
            {
                // Frozen, crouched, staring — very unsettling
                BotOwner.Mover.SetTargetMoveSpeed(0f);
                return;
            }
        }

        // Trigger peek at intervals (stop and observe)
        if (time >= _nextPeekTime && distance > 10f)
        {
            _isPeeking = true;
            _peekEndTime = time + Random.Range(1.5f, 3.5f);
            BotOwner.Mover.SetTargetMoveSpeed(0f);
            BotOwner.Mover.SetPose(0.2f); // Crouch lower while peeking
            ZombieDebug.LogStateChange("Stalker", BotOwner, "Circling", "Peeking", $"dist={distance:F1}m");
            return;
        }

        // Almost silent — only rare quiet sounds
        if (Random.value < 0.0008f)
            BotOwner.BotTalk?.Say(EPhraseTrigger.OnMutter);

        if (time < _nextPathTime) return;
        _nextPathTime = time + ZombieConstants.PathUpdateInterval;

        if (distance > 25f)
        {
            // Too far — approach crouched, moderate speed
            BotOwner.Mover.SetTargetMoveSpeed(0.5f);
            BotOwner.Mover.SetPose(0.5f);
            BotOwner.Mover.GoToPoint(targetPos, false, 15f);
        }
        else if (distance > 8f)
        {
            // Circling distance — orbit around the player, crouched
            _circleAngle += Random.Range(20f, 50f);
            float rad = _circleAngle * Mathf.Deg2Rad;
            float orbitRadius = Random.Range(12f, 18f);
            var orbitPos = targetPos + new Vector3(
                Mathf.Cos(rad) * orbitRadius,
                0,
                Mathf.Sin(rad) * orbitRadius);

            BotOwner.Mover.SetTargetMoveSpeed(0.35f);
            BotOwner.Mover.SetPose(0.3f); // Low crouch while circling
            BotOwner.Mover.GoToPoint(orbitPos, false, 2f);

            // 10% chance per path update to initiate rush from flank
            if (Random.value < 0.10f)
            {
                _isRushing = true;
                _rushEndTime = time + Random.Range(2.5f, 4.5f);
                BotOwner.Mover.Sprint(true);
                BotOwner.Mover.SetTargetMoveSpeed(1f);
                BotOwner.Mover.SetPose(1f); // Stand up for the charge
                BotOwner.BotTalk?.Say(EPhraseTrigger.OnFight);
                ZombieDebug.LogCombatEvent("Stalker", BotOwner, "FLANK RUSH", distance);
            }
        }
        else
        {
            // Close range — commit to the rush
            _isRushing = true;
            _rushEndTime = time + Random.Range(2f, 3.5f);
            BotOwner.Mover.Sprint(true);
            BotOwner.Mover.SetTargetMoveSpeed(1f);
            BotOwner.Mover.SetPose(1f); // Stand up for attack
            BotOwner.BotTalk?.Say(EPhraseTrigger.OnFight);
            BotOwner.Mover.GoToPoint(targetPos, false, 0.3f);
            ZombieDebug.LogCombatEvent("Stalker", BotOwner, "CLOSE RANGE RUSH", distance);
        }
    }

    public override void Stop()
    {
        _isRushing = false;
        _isPeeking = false;
        BotOwner.Mover.Sprint(false);
        BotOwner.Mover.SetPose(1f);
    }
}
