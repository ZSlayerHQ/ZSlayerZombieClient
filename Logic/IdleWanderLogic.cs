using DrakiaXYZ.BigBrain.Brains;
using EFT;
using UnityEngine;
using UnityEngine.AI;
using ZSlayerZombieClient.Core;

namespace ZSlayerZombieClient.Logic;

public class IdleWanderLogic : CustomLogic
{
    private Vector3 _wanderTarget;
    private float _nextWanderTime;
    private float _pauseEndTime;
    private bool _isPaused;

    private const float WanderRadius = 15f;
    private const float ArrivalDistance = 2f;

    public IdleWanderLogic(BotOwner botOwner) : base(botOwner) { }

    public override void Start()
    {
        BotOwner.Mover.Sprint(false);
        BotOwner.Mover.SetTargetMoveSpeed(0.2f);
        BotOwner.Mover.SetPose(1f);
        _nextWanderTime = 0f;
        _isPaused = false;

        try { BotOwner.WeaponManager?.Selector?.ChangeToMelee(); }
        catch { }
    }

    public override void Update(CustomLayer.ActionData data)
    {
        float time = Time.time;

        // Paused at destination
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

        // Ambient groaning
        if (Random.value < 0.002f)
            BotOwner.BotTalk?.Say(EPhraseTrigger.OnMutter);
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
            BotOwner.Mover.SetTargetMoveSpeed(Random.Range(0.15f, 0.3f));
            BotOwner.Mover.GoToPoint(_wanderTarget, true, ArrivalDistance);
            _nextWanderTime = Time.time + Random.Range(8f, 20f);

            // Random pause chance before starting move
            if (Random.value < 0.3f)
            {
                _isPaused = true;
                _pauseEndTime = Time.time + Random.Range(3f, 8f);
            }
        }
        else
        {
            // Failed to find valid point — wait and retry
            _isPaused = true;
            _pauseEndTime = Time.time + Random.Range(2f, 5f);
            _nextWanderTime = _pauseEndTime;
        }
    }
}
