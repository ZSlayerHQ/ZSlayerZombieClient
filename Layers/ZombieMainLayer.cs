using DrakiaXYZ.BigBrain.Brains;
using EFT;
using ZSlayerZombieClient.Archetypes;
using ZSlayerZombieClient.Core;
using ZSlayerZombieClient.Logic;

namespace ZSlayerZombieClient.Layers;

public class ZombieMainLayer : CustomLayer
{
    public ZombieMainLayer(BotOwner botOwner, int priority) : base(botOwner, priority) { }

    public override string GetName() => "ZSlayerZombieCombat";

    public override bool IsActive()
    {
        return BotOwner.Memory.HaveEnemy;
    }

    public override Action GetNextAction()
    {
        if (!ZombieRegistry.TryGet(BotOwner, out var entry))
            entry = ZombieRegistry.GetOrRegister(BotOwner);

        var logicType = entry.Archetype.Type switch
        {
            ZombieArchetype.Runner => typeof(RunnerLogic),
            // Phase 2 archetypes fall back to shambler for now
            _ => typeof(ShamblerLogic),
        };

        return new Action(logicType, $"{entry.Archetype.Type}-combat");
    }

    public override bool IsCurrentActionEnding()
    {
        return !BotOwner.Memory.HaveEnemy;
    }
}
