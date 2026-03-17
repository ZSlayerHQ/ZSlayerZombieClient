using DrakiaXYZ.BigBrain.Brains;
using EFT;
using UnityEngine;
using ZSlayerZombieClient.Core;

namespace ZSlayerZombieClient.Logic;

public class RunnerLogic : CustomLogic
{
    private float _nextPathTime;
    private float _phaseEndTime;
    private bool _isSprinting;

    public RunnerLogic(BotOwner botOwner) : base(botOwner) { }

    public override void Start()
    {
        BotOwner.Mover.SetPose(1f);
        StartSprintPhase();

        try { BotOwner.WeaponManager?.Selector?.ChangeToMelee(); }
        catch { }
    }

    public override void Update(CustomLayer.ActionData data)
    {
        var enemy = BotOwner.Memory.GoalEnemy;
        if (enemy == null) return;

        float time = Time.time;

        // Toggle sprint/recovery phases
        if (time >= _phaseEndTime)
        {
            if (_isSprinting)
                StartRecoveryPhase();
            else
                StartSprintPhase();
        }

        // Vocalize (more frequent during sprint)
        float vocChance = _isSprinting
            ? ZombieConstants.VocalizationChance * 3f
            : ZombieConstants.VocalizationChance;
        if (Random.value < vocChance)
            BotOwner.BotTalk?.Say(EPhraseTrigger.OnEnemyConversation);

        // Update path
        if (time < _nextPathTime) return;
        _nextPathTime = time + (_isSprinting ? 0.3f : ZombieConstants.PathUpdateInterval);

        var targetPos = enemy.CurrPosition;
        float distance = (BotOwner.Position - targetPos).magnitude;

        if (distance > 3f)
        {
            BotOwner.Mover.GoToPoint(targetPos, false, 1f);
        }
        else
        {
            // Close range — stop sprinting for melee
            BotOwner.Mover.Sprint(false);
            BotOwner.Mover.SetTargetMoveSpeed(0.8f);
            BotOwner.Mover.GoToPoint(targetPos, false, 0.5f);
        }
    }

    public override void Stop()
    {
        BotOwner.Mover.Sprint(false);
    }

    private void StartSprintPhase()
    {
        _isSprinting = true;
        _phaseEndTime = Time.time +
            Plugin.ClientConfig.RunnerSprintDuration.Value +
            Random.Range(-1f, 1.5f);
        BotOwner.Mover.Sprint(true);
        BotOwner.Mover.SetTargetMoveSpeed(1f);
    }

    private void StartRecoveryPhase()
    {
        _isSprinting = false;
        _phaseEndTime = Time.time +
            Plugin.ClientConfig.RunnerRecoveryDuration.Value +
            Random.Range(-0.5f, 1f);
        BotOwner.Mover.Sprint(false);
        BotOwner.Mover.SetTargetMoveSpeed(0.6f);
    }
}
