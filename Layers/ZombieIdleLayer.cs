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

        // Log first activation
        if (!_loggedActivation)
        {
            _loggedActivation = true;
            Plugin.Log.LogInfo("[ZSlayerHQ] ZombieIdleLayer ACTIVATED — BigBrain idle layer is working");
        }

        return true;
    }

    public override Action GetNextAction()
    {
        return new Action(typeof(IdleWanderLogic), "idle-wander");
    }

    public override bool IsCurrentActionEnding() => false;
}
