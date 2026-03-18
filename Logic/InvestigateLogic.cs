using DrakiaXYZ.BigBrain.Brains;
using EFT;
using UnityEngine;
using UnityEngine.AI;
using ZSlayerZombieClient.Core;

namespace ZSlayerZombieClient.Logic;

/// <summary>
/// Investigation behavior when zombie is alerted but has no direct enemy.
/// Moves toward last known position, scanning around, growling.
/// Creates the "they heard you" tension without full combat.
/// </summary>
public class InvestigateLogic : CustomLogic
{
    private float _nextPathTime;
    private float _nextLookTime;
    private Vector3 _investigatePos;
    private bool _reachedTarget;
    private float _scanEndTime;
    private float _lookAngle;

    public InvestigateLogic(BotOwner botOwner) : base(botOwner) { }

    public override void Start()
    {
        BotOwner.Mover.Sprint(false);
        BotOwner.Mover.SetTargetMoveSpeed(0.55f);
        BotOwner.Mover.SetPose(1f);
        _nextPathTime = 0f;
        _nextLookTime = 0f;
        _reachedTarget = false;
        _lookAngle = 0f;

        // Try to get last known enemy position
        var enemy = BotOwner.Memory.GoalEnemy;
        if (enemy != null)
        {
            _investigatePos = enemy.CurrPosition;
        }
        else
        {
            // No enemy data — investigate nearby area
            var randomDir = Random.insideUnitSphere * 20f;
            randomDir.y = 0;
            var candidate = BotOwner.Position + randomDir;
            if (NavMesh.SamplePosition(candidate, out var hit, 5f, NavMesh.AllAreas))
                _investigatePos = hit.position;
            else
                _investigatePos = BotOwner.Position + BotOwner.LookDirection * 10f;
        }

        // Alert growl
        BotOwner.BotTalk?.Say(EPhraseTrigger.OnEnemyConversation);
    }

    public override void Update(CustomLayer.ActionData data)
    {
        float time = Time.time;
        float distance = (BotOwner.Position - _investigatePos).magnitude;

        // Head scanning — look around while investigating
        if (time >= _nextLookTime)
        {
            _nextLookTime = time + 0.4f;

            if (_reachedTarget)
            {
                // Scan around at the investigation point
                _lookAngle += Random.Range(30f, 90f);
                float rad = _lookAngle * Mathf.Deg2Rad;
                var scanDir = _investigatePos + new Vector3(Mathf.Cos(rad) * 5f, 0, Mathf.Sin(rad) * 5f);
                try { BotOwner.Steering?.LookToPoint(scanDir); }
                catch { }
            }
            else
            {
                // Look toward investigation target while moving
                try { BotOwner.Steering?.LookToPoint(_investigatePos); }
                catch { }
            }
        }

        // Vocalize — alert growling
        if (Random.value < 0.003f)
            BotOwner.BotTalk?.Say(EPhraseTrigger.OnEnemyConversation);

        if (_reachedTarget)
        {
            // Scan for a bit then calm down
            if (time >= _scanEndTime)
            {
                // Clear alert state — return to idle
                if (ZombieRegistry.TryGet(BotOwner, out var entry))
                    entry.Alert = AlertState.Unaware;
            }
            return;
        }

        // Move toward investigation point
        if (time < _nextPathTime) return;
        _nextPathTime = time + ZombieConstants.PathUpdateInterval;

        if (distance > 3f)
        {
            BotOwner.Mover.GoToPoint(_investigatePos, false, 2f);
        }
        else
        {
            // Arrived — scan the area
            _reachedTarget = true;
            _scanEndTime = time + Random.Range(4f, 8f);
            BotOwner.Mover.SetTargetMoveSpeed(0.1f);
        }
    }

    public override void Stop()
    {
    }
}
