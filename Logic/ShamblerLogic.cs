using DrakiaXYZ.BigBrain.Brains;
using EFT;
using UnityEngine;
using ZSlayerZombieClient.Core;

namespace ZSlayerZombieClient.Logic;

public class ShamblerLogic : CustomLogic
{
    private float _nextPathTime;
    private float _nextStumbleTime;
    private bool _isStumbling;
    private float _stumbleEndTime;
    private float _speed;

    public ShamblerLogic(BotOwner botOwner) : base(botOwner) { }

    public override void Start()
    {
        BotOwner.Mover.Sprint(false);
        _speed = Random.Range(
            Plugin.ClientConfig.ShamblerMinSpeed.Value,
            Plugin.ClientConfig.ShamblerMaxSpeed.Value);
        BotOwner.Mover.SetTargetMoveSpeed(_speed);
        BotOwner.Mover.SetPose(1f);
        _nextStumbleTime = Time.time + Random.Range(
            ZombieConstants.StumbleMinInterval, ZombieConstants.StumbleMaxInterval);
        _nextPathTime = 0f;

        try { BotOwner.WeaponManager?.Selector?.ChangeToMelee(); }
        catch { /* Some bots may not have melee */ }
    }

    public override void Update(CustomLayer.ActionData data)
    {
        var enemy = BotOwner.Memory.GoalEnemy;
        if (enemy == null) return;

        float time = Time.time;

        // Stumble mechanic — random pauses for shambling effect
        if (_isStumbling)
        {
            if (time < _stumbleEndTime) return;
            _isStumbling = false;
            _speed = Random.Range(
                Plugin.ClientConfig.ShamblerMinSpeed.Value,
                Plugin.ClientConfig.ShamblerMaxSpeed.Value);
            BotOwner.Mover.SetTargetMoveSpeed(_speed);
            _nextStumbleTime = time + Random.Range(
                ZombieConstants.StumbleMinInterval, ZombieConstants.StumbleMaxInterval);
        }

        if (time >= _nextStumbleTime)
        {
            _isStumbling = true;
            _stumbleEndTime = time + Random.Range(
                ZombieConstants.StumbleMinDuration, ZombieConstants.StumbleMaxDuration);
            BotOwner.Mover.SetTargetMoveSpeed(0.05f);
            return;
        }

        // Vocalize
        if (Random.value < ZombieConstants.VocalizationChance)
            BotOwner.BotTalk?.Say(EPhraseTrigger.OnEnemyConversation);

        // Update path periodically
        if (time < _nextPathTime) return;
        _nextPathTime = time + ZombieConstants.PathUpdateInterval;

        var targetPos = enemy.CurrPosition;
        float distance = (BotOwner.Position - targetPos).magnitude;

        if (distance > 4f)
        {
            // Erratic offset for shambling effect
            var offset = new Vector3(Random.Range(-1.5f, 1.5f), 0, Random.Range(-1.5f, 1.5f));
            BotOwner.Mover.GoToPoint(targetPos + offset, false, 1f);
        }
        else
        {
            // Close range — direct approach for melee
            BotOwner.Mover.SetTargetMoveSpeed(_speed + 0.15f);
            BotOwner.Mover.GoToPoint(targetPos, false, 0.5f);
        }
    }

    public override void Stop()
    {
        _isStumbling = false;
    }
}
