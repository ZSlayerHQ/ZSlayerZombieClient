using DrakiaXYZ.BigBrain.Brains;
using EFT;
using ZSlayerZombieClient.Core;
using ZSlayerZombieClient.Logic;

namespace ZSlayerZombieClient.Layers;

public class ZombieIdleLayer : CustomLayer
{
    private static bool _loggedActivation;

    public ZombieIdleLayer(BotOwner botOwner, int priority) : base(botOwner, priority) { }

    public override string GetName() => "ZSlayerZombieIdle";

    public override bool IsActive()
    {
        // Ensure zombie is registered on first check
        ZombieRegistry.GetOrRegister(BotOwner);

        // Log first activation (always visible)
        if (!_loggedActivation)
        {
            _loggedActivation = true;
            Plugin.Log.LogWarning("[ZSlayerHQ] ZombieIdleLayer ACTIVATED — BigBrain idle layer is working");
        }

        ZombieDebug.LogLayerActive("Idle", BotOwner);
        return true;
    }

    public override Action GetNextAction()
    {
        ZombieDebug.Log($"Idle GetNextAction: bot={ZombieDebug.BotId(BotOwner)} -> IdleWanderLogic");
        return new Action(typeof(IdleWanderLogic), "idle-wander");
    }

    public override bool IsCurrentActionEnding() => false;
}
