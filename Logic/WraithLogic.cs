using DrakiaXYZ.BigBrain.Brains;
using EFT;
using UnityEngine;
using UnityEngine.AI;
using ZSlayerZombieClient.Animation;
using ZSlayerZombieClient.Core;

namespace ZSlayerZombieClient.Logic;

/// <summary>
/// The psychological horror archetype. Sneaks up on players silently
/// when they aren't looking. The moment the player turns to face it,
/// the wraith freezes briefly in shock, then sprints away screaming.
/// After a short cooldown hiding somewhere, it comes back.
///
/// The terror: you keep catching glimpses of something fleeing.
/// You know it's behind you. You can't watch every direction at once.
/// In groups, while you watch one flee, another creeps up behind you.
///
/// Detection uses dot product between enemy's forward direction and
/// the vector from enemy to zombie. If positive = player facing us.
/// </summary>
public class WraithLogic : CustomLogic
{
    private enum WraithState
    {
        Stalking,   // Silent approach while player looks away
        Spotted,    // Brief freeze when player turns to face us
        Fleeing,    // Sprint away from player
        Cooldown    // Wait hidden, then restart cycle
    }

    private WraithState _state;
    private float _stateEndTime;
    private float _nextPathTime;
    private float _nextLookTime;
    private float _nextSightCheck;
    private Vector3 _fleeTarget;
    private bool _hasFleeTarget;

    // How wide the "player is looking at me" cone is (0.35 ≈ 70° cone)
    private const float SightDotThreshold = 0.35f;
    // How close before the wraith commits to attack instead of fleeing
    private const float CommitDistance = 3f;

    public WraithLogic(BotOwner botOwner) : base(botOwner) { }

    public override void Start()
    {
        _state = WraithState.Stalking;
        _stateEndTime = 0f;
        _nextPathTime = 0f;
        _nextLookTime = 0f;
        _nextSightCheck = 0f;
        _hasFleeTarget = false;

        BotOwner.Mover.Sprint(false);
        BotOwner.Mover.SetTargetMoveSpeed(0.7f);
        BotOwner.Mover.SetPose(1f);

        ZombieAnimationController.SetState(BotOwner, ZombieAnimState.Normal);
        ZombieDebug.LogLogicStart("Wraith", BotOwner, "state=Stalking");
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

        // If we're extremely close, commit to melee — no more games
        if (distance < CommitDistance && _state != WraithState.Fleeing)
        {
            BotOwner.Mover.Sprint(false);
            BotOwner.Mover.SetTargetMoveSpeed(0.9f);
            BotOwner.Mover.SetPose(1f);
            if (time >= _nextPathTime)
            {
                _nextPathTime = time + ZombieConstants.FastPathUpdateInterval;
                BotOwner.Mover.GoToPoint(targetPos, false, 0.3f);
            }
            if (time >= _nextLookTime)
            {
                _nextLookTime = time + ZombieConstants.LookUpdateInterval;
                try { BotOwner.Steering?.LookToPoint(targetPos); }
                catch { }
            }
            return;
        }

        switch (_state)
        {
            case WraithState.Stalking:
                UpdateStalking(time, targetPos, distance, enemy);
                break;
            case WraithState.Spotted:
                UpdateSpotted(time, targetPos);
                break;
            case WraithState.Fleeing:
                UpdateFleeing(time, targetPos, distance);
                break;
            case WraithState.Cooldown:
                UpdateCooldown(time, targetPos, distance);
                break;
        }
    }

    private void UpdateStalking(float time, Vector3 targetPos, float distance, EnemyInfo enemy)
    {
        // Check if player is looking at us
        if (time >= _nextSightCheck)
        {
            _nextSightCheck = time + 0.2f; // Check 5x/sec

            if (IsPlayerLookingAtMe(enemy, targetPos))
            {
                // SPOTTED — freeze in shock
                ZombieDebug.LogStateChange("Wraith", BotOwner, "Stalking", "Spotted",
                    $"player looking at us! dist={distance:F1}m");
                TransitionTo(WraithState.Spotted, 0.4f);
                BotOwner.Mover.SetTargetMoveSpeed(0f);
                ZombieAnimationController.SetState(BotOwner, ZombieAnimState.Frozen);
                return;
            }
        }

        // Head tracking — stare at player while sneaking
        if (time >= _nextLookTime)
        {
            _nextLookTime = time + ZombieConstants.LookUpdateInterval;
            try { BotOwner.Steering?.LookToPoint(targetPos); }
            catch { }
        }

        // Complete silence while stalking — no sound at all
        // (the absence of sound is what makes it terrifying)

        // Move toward player — faster when further, slower when close
        if (time >= _nextPathTime)
        {
            _nextPathTime = time + ZombieConstants.PathUpdateInterval;

            if (distance > 15f)
            {
                // Far away — move quickly, player won't hear
                BotOwner.Mover.Sprint(true);
                BotOwner.Mover.SetTargetMoveSpeed(1f);
            }
            else
            {
                // Getting close — slow down, be careful
                BotOwner.Mover.Sprint(false);
                float speed = Mathf.Lerp(0.3f, 0.6f, (distance - CommitDistance) / 12f);
                BotOwner.Mover.SetTargetMoveSpeed(speed);
            }

            // Approach from behind/side — offset away from player's facing
            Vector3 approachOffset = GetFlankOffset(targetPos, distance);
            BotOwner.Mover.GoToPoint(targetPos + approachOffset, false, 1f);
        }
    }

    private void UpdateSpotted(float time, Vector3 targetPos)
    {
        // Frozen in place, staring at player — brief moment of horror
        if (time >= _nextLookTime)
        {
            _nextLookTime = time + ZombieConstants.LookUpdateInterval;
            try { BotOwner.Steering?.LookToPoint(targetPos); }
            catch { }
        }

        if (time >= _stateEndTime)
        {
            // Transition to flee — scream and run
            ZombieDebug.LogStateChange("Wraith", BotOwner, "Spotted", "Fleeing", "freeze ended, screaming and running");
            TransitionTo(WraithState.Fleeing, Random.Range(2.5f, 4f));
            BotOwner.BotTalk?.Say(EPhraseTrigger.OnFight); // Scream as it flees
            ZombieAnimationController.SetState(BotOwner, ZombieAnimState.Rushing);
            _hasFleeTarget = false;
        }
    }

    private void UpdateFleeing(float time, Vector3 targetPos, float distance)
    {
        // Sprint AWAY from player
        BotOwner.Mover.Sprint(true);
        BotOwner.Mover.SetTargetMoveSpeed(1f);
        BotOwner.Mover.SetPose(1f);

        // Occasional panic vocalization while fleeing
        if (Random.value < 0.008f)
            BotOwner.BotTalk?.Say(EPhraseTrigger.OnEnemyConversation);

        if (time >= _nextPathTime)
        {
            _nextPathTime = time + ZombieConstants.FastPathUpdateInterval;

            if (!_hasFleeTarget || (BotOwner.Position - _fleeTarget).sqrMagnitude < 9f)
            {
                // Pick a flee point away from the player
                var awayDir = (BotOwner.Position - targetPos).normalized;
                // Add random lateral offset so they don't flee in a straight line
                var lateral = new Vector3(-awayDir.z, 0, awayDir.x) * Random.Range(-5f, 5f);
                var candidate = BotOwner.Position + awayDir * 20f + lateral;

                if (NavMesh.SamplePosition(candidate, out var hit, 10f, NavMesh.AllAreas))
                {
                    _fleeTarget = hit.position;
                    _hasFleeTarget = true;
                }
                else
                {
                    // Can't find flee point — just run in the away direction
                    _fleeTarget = BotOwner.Position + awayDir * 15f;
                    _hasFleeTarget = true;
                }
            }

            BotOwner.Mover.GoToPoint(_fleeTarget, false, 2f);
        }

        if (time >= _stateEndTime)
        {
            // Done fleeing — enter cooldown
            ZombieDebug.LogStateChange("Wraith", BotOwner, "Fleeing", "Cooldown", $"fled to {distance:F1}m away");
            TransitionTo(WraithState.Cooldown, Random.Range(3f, 5f));
            BotOwner.Mover.Sprint(false);
            BotOwner.Mover.SetTargetMoveSpeed(0f);
            ZombieAnimationController.SetState(BotOwner, ZombieAnimState.Stumbling);
        }
    }

    private void UpdateCooldown(float time, Vector3 targetPos, float distance)
    {
        // Standing still, waiting, silent
        BotOwner.Mover.SetTargetMoveSpeed(0f);

        // Look toward player occasionally (peeking)
        if (time >= _nextLookTime)
        {
            _nextLookTime = time + Random.Range(1f, 2f);
            try { BotOwner.Steering?.LookToPoint(targetPos); }
            catch { }
        }

        if (time >= _stateEndTime)
        {
            // Cooldown over — start stalking again
            ZombieDebug.LogStateChange("Wraith", BotOwner, "Cooldown", "Stalking", $"resuming approach, dist={distance:F1}m");
            TransitionTo(WraithState.Stalking, 0f);
            BotOwner.Mover.SetTargetMoveSpeed(0.5f);
            ZombieAnimationController.SetState(BotOwner, ZombieAnimState.Normal);
        }
    }

    /// <summary>
    /// Check if the enemy player is facing toward this zombie.
    /// Uses dot product between enemy's look direction and the
    /// vector from enemy to zombie. Positive = facing us.
    /// </summary>
    private bool IsPlayerLookingAtMe(EnemyInfo enemy, Vector3 enemyPos)
    {
        try
        {
            // Try to get the enemy's forward/look direction
            var person = enemy.Person;
            if (person == null) return false;

            // EFT's IPlayer has Transform which gives us position and forward
            var enemyTransform = person.Transform;
            if (enemyTransform == null) return false;

            // Get the actual position from the transform for accuracy
            var actualEnemyPos = enemyTransform.position;
            var dirToMe = (BotOwner.Position - actualEnemyPos).normalized;

            // Use the transform's forward direction
            var enemyForward = enemyTransform.forward;

            // Flatten to horizontal plane (ignore vertical look angle)
            dirToMe.y = 0;
            dirToMe = dirToMe.normalized;
            enemyForward.y = 0;
            enemyForward = enemyForward.normalized;

            float dot = Vector3.Dot(enemyForward, dirToMe);
            return dot > SightDotThreshold;
        }
        catch
        {
            // If we can't determine look direction, assume not looking
            // (safer to keep stalking than to flee for no reason)
            return false;
        }
    }

    /// <summary>
    /// Calculate an offset to approach from behind/side of the player,
    /// away from their facing direction.
    /// </summary>
    private Vector3 GetFlankOffset(Vector3 targetPos, float distance)
    {
        if (distance < 6f) return Vector3.zero; // Too close for flanking

        var dirToEnemy = (targetPos - BotOwner.Position).normalized;
        var perpendicular = new Vector3(-dirToEnemy.z, 0, dirToEnemy.x);

        // Offset to the side — approach from flank
        float sideOffset = Random.value > 0.5f ? 3f : -3f;
        return perpendicular * sideOffset;
    }

    private void TransitionTo(WraithState newState, float duration)
    {
        _state = newState;
        _stateEndTime = Time.time + duration;
    }

    public override void Stop()
    {
        BotOwner.Mover.Sprint(false);
        BotOwner.Mover.SetPose(1f);
    }
}
