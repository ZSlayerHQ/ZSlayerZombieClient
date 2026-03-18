using DrakiaXYZ.BigBrain.Brains;
using EFT;
using UnityEngine;
using UnityEngine.AI;
using ZSlayerZombieClient.Core;

namespace ZSlayerZombieClient.Logic;

/// <summary>
/// Ambient wandering when no enemies detected.
/// Zombies are restless — they shamble around at a moderate pace,
/// not standing still like statues. Occasional pauses to look around
/// but they keep moving. Speed varies per zombie.
/// </summary>
public class IdleWanderLogic : CustomLogic
{
    private Vector3 _wanderTarget;
    private float _nextWanderTime;
    private float _pauseEndTime;
    private bool _isPaused;
    private float _nextLookTime;
    private float _speedMul;

    private const float WanderRadius = 20f;
    private const float ArrivalDistance = 2f;

    public IdleWanderLogic(BotOwner botOwner) : base(botOwner) { }

    public override void Start()
    {
        _speedMul = 1f;
        if (ZombieRegistry.TryGet(BotOwner, out var entry))
            _speedMul = entry.SpeedMultiplier;

        BotOwner.Mover.Sprint(false);
        // Faster idle speed — zombies are restless, not sleeping
        BotOwner.Mover.SetTargetMoveSpeed(ZombieHelper.ApplySpeedVariance(0.4f, _speedMul));
        BotOwner.Mover.SetPose(1f);
        _nextWanderTime = 0f;
        _isPaused = false;
        _nextLookTime = 0f;
    }

    public override void Update(CustomLayer.ActionData data)
    {
        float time = Time.time;

        // Occasional random head movement (looking around)
        if (time >= _nextLookTime)
        {
            _nextLookTime = time + Random.Range(2f, 6f);
            try
            {
                var lookDir = BotOwner.Position + Random.insideUnitSphere * 10f;
                lookDir.y = BotOwner.Position.y;
                BotOwner.Steering?.LookToPoint(lookDir);
            }
            catch { }
        }

        // Paused at destination — shorter pauses, more restless
        if (_isPaused)
        {
            if (time < _pauseEndTime) return;
            _isPaused = false;
        }

        // Pick new wander target or check arrival
        if (time >= _nextWanderTime || HasArrived())
        {
            PickNewWanderTarget();
        }

        // Ambient groaning — quiet, atmospheric
        if (Random.value < 0.002f)
            BotOwner.BotTalk?.Say(EPhraseTrigger.OnMutter);

        // Occasional deep breath
        if (Random.value < 0.0005f)
            BotOwner.BotTalk?.Say(EPhraseTrigger.OnBreath);
    }

    private bool HasArrived()
    {
        if (_wanderTarget == Vector3.zero) return true;
        return (BotOwner.Position - _wanderTarget).sqrMagnitude < ArrivalDistance * ArrivalDistance;
    }

    private void PickNewWanderTarget()
    {
        var randomDir = Random.insideUnitSphere * WanderRadius;
        randomDir.y = 0;
        var candidatePos = BotOwner.Position + randomDir;

        if (NavMesh.SamplePosition(candidatePos, out var hit, 5f, NavMesh.AllAreas))
        {
            _wanderTarget = hit.position;
            // Varied idle speed — some zombies shamble faster than others
            BotOwner.Mover.SetTargetMoveSpeed(ZombieHelper.ApplySpeedVariance(
                Random.Range(0.3f, 0.5f), _speedMul));
            BotOwner.Mover.GoToPoint(_wanderTarget, false, ArrivalDistance);
            // Shorter intervals — zombies keep moving, don't stand around
            _nextWanderTime = Time.time + Random.Range(5f, 12f);

            // Less frequent pauses (15% vs 30%), shorter duration
            if (Random.value < 0.15f)
            {
                _isPaused = true;
                _pauseEndTime = Time.time + Random.Range(1.5f, 4f);
            }
        }
        else
        {
            _isPaused = true;
            _pauseEndTime = Time.time + Random.Range(1f, 3f);
            _nextWanderTime = _pauseEndTime;
        }
    }
}
