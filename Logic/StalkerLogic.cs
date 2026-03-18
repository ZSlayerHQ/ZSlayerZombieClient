using DrakiaXYZ.BigBrain.Brains;
using EFT;
using UnityEngine;
using ZSlayerZombieClient.Core;

namespace ZSlayerZombieClient.Logic;

/// <summary>
/// The creepy one. Maintains distance, circles around the player,
/// tries to approach from behind or the side. When the player is
/// looking away or distracted, rushes in fast. Silent until the attack.
/// </summary>
public class StalkerLogic : CustomLogic
{
    private float _nextPathTime;
    private float _nextLookTime;
    private bool _isRushing;
    private float _rushEndTime;
    private float _circleAngle;

    public StalkerLogic(BotOwner botOwner) : base(botOwner) { }

    public override void Start()
    {
        BotOwner.Mover.Sprint(false);
        BotOwner.Mover.SetTargetMoveSpeed(0.5f);
        BotOwner.Mover.SetPose(1f); // Upright — zombie animations
        _nextPathTime = 0f;
        _nextLookTime = 0f;
        _isRushing = false;
        _circleAngle = Random.Range(0f, 360f);
    }

    public override void Update(CustomLayer.ActionData data)
    {
        var enemy = BotOwner.Memory.GoalEnemy;
        if (enemy == null) return;

        float time = Time.time;
        var targetPos = enemy.CurrPosition;
        float distance = (BotOwner.Position - targetPos).magnitude;

        // Head tracking
        if (time >= _nextLookTime)
        {
            _nextLookTime = time + ZombieConstants.LookUpdateInterval;
            try { BotOwner.Steering?.LookToPoint(targetPos); }
            catch { }
        }

        // Rush state — closing in for the kill
        if (_isRushing)
        {
            if (time >= _rushEndTime || distance < 3f)
            {
                _isRushing = false;
                BotOwner.Mover.Sprint(false);
                BotOwner.Mover.SetTargetMoveSpeed(0.7f);
            }
            else
            {
                if (time >= _nextPathTime)
                {
                    _nextPathTime = time + ZombieConstants.FastPathUpdateInterval;
                    BotOwner.Mover.GoToPoint(targetPos, false, 0.5f);
                }
                return;
            }
        }

        // Very quiet — only occasional low sounds
        if (Random.value < 0.001f)
            BotOwner.BotTalk?.Say(EPhraseTrigger.OnMutter);

        if (time < _nextPathTime) return;
        _nextPathTime = time + ZombieConstants.PathUpdateInterval;

        if (distance > 25f)
        {
            // Too far — approach normally but keep offset
            BotOwner.Mover.SetTargetMoveSpeed(0.6f);
            BotOwner.Mover.GoToPoint(targetPos, false, 15f);
        }
        else if (distance > 8f)
        {
            // Circling distance — orbit around the player
            _circleAngle += Random.Range(15f, 45f);
            float rad = _circleAngle * Mathf.Deg2Rad;
            float orbitRadius = Random.Range(12f, 18f);
            var orbitPos = targetPos + new Vector3(
                Mathf.Cos(rad) * orbitRadius,
                0,
                Mathf.Sin(rad) * orbitRadius);

            BotOwner.Mover.SetTargetMoveSpeed(0.5f);
            BotOwner.Mover.SetPose(1f);
            BotOwner.Mover.GoToPoint(orbitPos, false, 2f);

            // 8% chance per path update to initiate rush
            if (Random.value < 0.08f)
            {
                _isRushing = true;
                _rushEndTime = time + Random.Range(2f, 4f);
                BotOwner.Mover.Sprint(true);
                BotOwner.Mover.SetTargetMoveSpeed(1f);
                BotOwner.Mover.SetPose(1f);
                BotOwner.BotTalk?.Say(EPhraseTrigger.OnFight);
            }
        }
        else
        {
            // Close range — rush attack
            _isRushing = true;
            _rushEndTime = time + Random.Range(1.5f, 3f);
            BotOwner.Mover.Sprint(true);
            BotOwner.Mover.SetTargetMoveSpeed(1f);
            BotOwner.Mover.SetPose(1f);
            BotOwner.BotTalk?.Say(EPhraseTrigger.OnFight);
            BotOwner.Mover.GoToPoint(targetPos, false, 0.3f);
        }
    }

    public override void Stop()
    {
        _isRushing = false;
        BotOwner.Mover.Sprint(false);
    }
}
