using DrakiaXYZ.BigBrain.Brains;
using EFT;
using UnityEngine;
using ZSlayerZombieClient.Core;

namespace ZSlayerZombieClient.Logic;

/// <summary>
/// Fast prone crawler. Moves at 3x normal zombie speed while fully prone.
/// Terrifying because they're low, fast, and hard to hit. If their path
/// gets blocked (stuck detection), they stand up temporarily to navigate
/// around the obstacle, then drop back prone.
/// Almost silent until the final lunge at close range.
/// </summary>
public class CrawlerLogic : CustomLogic
{
    private float _nextPathTime;
    private float _nextLookTime;
    private float _nextStuckCheck;
    private Vector3 _lastStuckCheckPos;
    private bool _isStandingForObstacle;
    private float _standEndTime;
    private bool _isLunging;
    private float _lungeEndTime;

    private const float StuckCheckInterval = 2f;
    private const float StuckThreshold = 0.5f; // Moved less than 0.5m in 2s = stuck
    private const float StandDuration = 3f;
    private const float CrawlSpeed = 0.9f; // ~3x normal zombie speed (0.3)

    public CrawlerLogic(BotOwner botOwner) : base(botOwner) { }

    public override void Start()
    {
        BotOwner.Mover.Sprint(false);
        BotOwner.Mover.SetTargetMoveSpeed(CrawlSpeed);
        BotOwner.Mover.SetPose(0f); // Fully prone
        _nextPathTime = 0f;
        _nextLookTime = 0f;
        _nextStuckCheck = Time.time + StuckCheckInterval;
        _lastStuckCheckPos = BotOwner.Position;
        _isStandingForObstacle = false;
        _isLunging = false;

        ZombieDebug.LogLogicStart("Crawler", BotOwner, $"PRONE speed={CrawlSpeed}");
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

        // Horde rush overrides archetype behavior
        if (ZombieRush.HandleRush(BotOwner, distance))
            return;

        // Head tracking — stare at player from prone
        if (time >= _nextLookTime)
        {
            _nextLookTime = time + ZombieConstants.LookUpdateInterval;
            try { BotOwner.Steering?.LookToPoint(targetPos); }
            catch { }
        }

        // Lunge state — stand up for final attack
        if (_isLunging)
        {
            if (time >= _lungeEndTime || distance < 1.5f)
            {
                _isLunging = false;
                BotOwner.Mover.Sprint(false);
                BotOwner.Mover.SetPose(0f); // Back to prone
                BotOwner.Mover.SetTargetMoveSpeed(CrawlSpeed);
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

        // Stuck detection — if barely moved in 2 seconds, stand up to navigate
        if (time >= _nextStuckCheck)
        {
            float movedDist = (BotOwner.Position - _lastStuckCheckPos).magnitude;
            _lastStuckCheckPos = BotOwner.Position;
            _nextStuckCheck = time + StuckCheckInterval;

            if (movedDist < StuckThreshold && !_isStandingForObstacle && distance > 5f)
            {
                // Stuck — stand up to get around obstacle
                _isStandingForObstacle = true;
                _standEndTime = time + StandDuration;
                BotOwner.Mover.SetPose(1f);
                BotOwner.Mover.SetTargetMoveSpeed(0.6f);

                ZombieDebug.LogStateChange("Crawler", BotOwner, "Prone", "Standing",
                    $"stuck (moved {movedDist:F2}m in {StuckCheckInterval}s, dist={distance:F1}m)");
            }
        }

        // Standing for obstacle — check if we can go back to prone
        if (_isStandingForObstacle)
        {
            if (time >= _standEndTime)
            {
                _isStandingForObstacle = false;
                BotOwner.Mover.SetPose(0f); // Back to prone
                BotOwner.Mover.SetTargetMoveSpeed(CrawlSpeed);
            }
        }

        // Almost silent — rare breathing
        if (Random.value < 0.0008f)
            BotOwner.BotTalk?.Say(EPhraseTrigger.OnBreath);

        // Path updates — distance-based throttling for performance
        float pathInterval = ZombieHelper.GetPathInterval(distance);
        if (time < _nextPathTime) return;
        _nextPathTime = time + pathInterval;

        if (distance > 4f)
        {
            // Fast prone approach — slight lateral drift
            float lateralOffset = Random.Range(-0.8f, 0.8f);
            var direction = (targetPos - BotOwner.Position).normalized;
            var perpendicular = new Vector3(-direction.z, 0, direction.x);
            BotOwner.Mover.GoToPoint(targetPos + perpendicular * lateralOffset, false, 1f);
        }
        else
        {
            // Close range — stand up and lunge
            _isLunging = true;
            _lungeEndTime = time + 1.5f;
            BotOwner.Mover.SetPose(1f);
            BotOwner.Mover.Sprint(true);
            BotOwner.Mover.SetTargetMoveSpeed(1f);
            BotOwner.BotTalk?.Say(EPhraseTrigger.OnFight);
            BotOwner.Mover.GoToPoint(targetPos, false, 0.3f);
            ZombieDebug.LogCombatEvent("Crawler", BotOwner, "STAND-UP LUNGE", distance);
        }
    }

    public override void Stop()
    {
        _isLunging = false;
        _isStandingForObstacle = false;
        BotOwner.Mover.Sprint(false);
        BotOwner.Mover.SetPose(1f);
    }
}
