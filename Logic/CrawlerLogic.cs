using DrakiaXYZ.BigBrain.Brains;
using EFT;
using UnityEngine;
using ZSlayerZombieClient.Core;

namespace ZSlayerZombieClient.Logic;

/// <summary>
/// The lurker. Extremely slow, almost motionless approach with long pauses.
/// Barely makes a sound. You might not even notice it until it's right next
/// to you — then sudden burst to close the last few meters.
/// Zombie stays upright (uses zombie walk animations) but at minimum speed.
/// </summary>
public class CrawlerLogic : CustomLogic
{
    private float _nextPathTime;
    private float _nextLookTime;
    private bool _isLunging;
    private float _lungeEndTime;
    private bool _isPaused;
    private float _pauseEndTime;

    public CrawlerLogic(BotOwner botOwner) : base(botOwner) { }

    public override void Start()
    {
        BotOwner.Mover.Sprint(false);
        BotOwner.Mover.SetTargetMoveSpeed(0.15f); // Barely moving
        BotOwner.Mover.SetPose(1f); // Upright — zombie animations
        _nextPathTime = 0f;
        _nextLookTime = 0f;
        _isLunging = false;
        _isPaused = false;
    }

    public override void Update(CustomLayer.ActionData data)
    {
        var enemy = BotOwner.Memory.GoalEnemy;
        if (enemy == null) return;

        float time = Time.time;
        var targetPos = enemy.CurrPosition;
        float distance = (BotOwner.Position - targetPos).magnitude;

        // Head tracking — constant stare
        if (time >= _nextLookTime)
        {
            _nextLookTime = time + ZombieConstants.LookUpdateInterval;
            try { BotOwner.Steering?.LookToPoint(targetPos); }
            catch { }
        }

        // Lunge state — sudden burst to close distance
        if (_isLunging)
        {
            if (time >= _lungeEndTime || distance < 2f)
            {
                _isLunging = false;
                BotOwner.Mover.Sprint(false);
                BotOwner.Mover.SetTargetMoveSpeed(0.15f);
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

        // Long pauses — stands still, just staring
        if (_isPaused)
        {
            if (time < _pauseEndTime) return;
            _isPaused = false;
            BotOwner.Mover.SetTargetMoveSpeed(0.15f);
        }

        // Almost silent — rare quiet breath
        if (Random.value < 0.0008f)
            BotOwner.BotTalk?.Say(EPhraseTrigger.OnBreath);

        if (time < _nextPathTime) return;
        _nextPathTime = time + ZombieConstants.PathUpdateInterval;

        if (distance > 5f)
        {
            // Painfully slow approach with frequent long pauses
            BotOwner.Mover.SetTargetMoveSpeed(Random.Range(0.1f, 0.2f));

            // 25% chance to just stop and stare for several seconds
            if (Random.value < 0.25f)
            {
                _isPaused = true;
                _pauseEndTime = time + Random.Range(2f, 6f);
                BotOwner.Mover.SetTargetMoveSpeed(0f);
                return;
            }

            // Slight lateral drift
            float lateralOffset = Random.Range(-0.5f, 0.5f);
            var direction = (targetPos - BotOwner.Position).normalized;
            var perpendicular = new Vector3(-direction.z, 0, direction.x);
            BotOwner.Mover.GoToPoint(targetPos + perpendicular * lateralOffset, false, 1f);
        }
        else
        {
            // Close enough — sudden burst
            _isLunging = true;
            _lungeEndTime = time + 1.5f;
            BotOwner.Mover.Sprint(true);
            BotOwner.Mover.SetTargetMoveSpeed(1f);
            BotOwner.BotTalk?.Say(EPhraseTrigger.OnFight);
            BotOwner.Mover.GoToPoint(targetPos, false, 0.3f);
        }
    }

    public override void Stop()
    {
        _isLunging = false;
        _isPaused = false;
        BotOwner.Mover.Sprint(false);
    }
}
