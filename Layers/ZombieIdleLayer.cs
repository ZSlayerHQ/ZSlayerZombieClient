using DrakiaXYZ.BigBrain.Brains;
using EFT;
using ZSlayerZombieClient.Core;
using ZSlayerZombieClient.Logic;

namespace ZSlayerZombieClient.Layers;

public class ZombieIdleLayer : CustomLayer
{
    public ZombieIdleLayer(BotOwner botOwner, int priority) : base(botOwner, priority) { }

    public override string GetName() => "ZSlayerZombieIdle";

    public override bool IsActive()
    {
        // Ensure zombie is registered on first check
        ZombieRegistry.GetOrRegister(BotOwner);
        return true;
    }

    public override Action GetNextAction()
    {
        return new Action(typeof(IdleWanderLogic), "idle-wander");
    }

    public override bool IsCurrentActionEnding() => false;
}
