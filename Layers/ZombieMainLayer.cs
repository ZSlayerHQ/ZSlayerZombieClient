using DrakiaXYZ.BigBrain.Brains;
using EFT;
using ZSlayerZombieClient.Archetypes;
using ZSlayerZombieClient.Core;
using ZSlayerZombieClient.Logic;

namespace ZSlayerZombieClient.Layers;

public class ZombieMainLayer : CustomLayer
{
    private static bool _loggedActivation;

    public ZombieMainLayer(BotOwner botOwner, int priority) : base(botOwner, priority) { }

    public override string GetName() => "ZSlayerZombieCombat";

    public override bool IsActive()
    {
        if (!BotOwner.Memory.HaveEnemy) return false;

        // Set zombie to aggressive when enemy detected
        if (ZombieRegistry.TryGet(BotOwner, out var entry))
        {
            entry.Alert = AlertState.Aggressive;
            entry.LastAlertTime = UnityEngine.Time.time;
        }

        // Log first activation to confirm BigBrain layers work
        if (!_loggedActivation)
        {
            _loggedActivation = true;
            Plugin.Log.LogWarning("[ZSlayerHQ] ZombieMainLayer ACTIVATED — BigBrain combat layer is working!");
        }

        return true;
    }

    public override Action GetNextAction()
    {
        if (!ZombieRegistry.TryGet(BotOwner, out var entry))
            entry = ZombieRegistry.GetOrRegister(BotOwner);

        var logicType = entry.Archetype.Type switch
        {
            ZombieArchetype.Runner => typeof(RunnerLogic),
            ZombieArchetype.Berserker => typeof(BerserkerLogic),
            ZombieArchetype.Stalker => typeof(StalkerLogic),
            ZombieArchetype.Crawler => typeof(CrawlerLogic),
            _ => typeof(ShamblerLogic),
        };

        if (Plugin.ClientConfig.DebugLogging.Value)
            Plugin.Log.LogInfo($"[ZSlayerHQ] Combat logic: {entry.Archetype.Type} for bot {BotOwner.Profile.Id.Substring(0, 8)}");

        return new Action(logicType, $"{entry.Archetype.Type}-combat");
    }

    public override bool IsCurrentActionEnding()
    {
        return !BotOwner.Memory.HaveEnemy;
    }
}
