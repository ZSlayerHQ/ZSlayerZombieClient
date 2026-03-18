using DrakiaXYZ.BigBrain.Brains;
using EFT;
using ZSlayerZombieClient.Archetypes;
using ZSlayerZombieClient.Core;
using ZSlayerZombieClient.Logic;

namespace ZSlayerZombieClient.Layers;

public class ZombieMainLayer : CustomLayer
{
    private static bool _loggedActivation;
    private static int _activationCount;

    public ZombieMainLayer(BotOwner botOwner, int priority) : base(botOwner, priority) { }

    public override string GetName() => "ZSlayerZombieCombat";

    public override bool IsActive()
    {
        if (!BotOwner.Memory.HaveEnemy) return false;

        // Force melee weapon equipped (replicates vanilla brain's ManualUpdate)
        // Without this, zombies may have guns/nothing equipped and can't attack
        ZombieMelee.EnsureMeleeEquipped(BotOwner);

        // Set zombie to aggressive when enemy detected
        if (ZombieRegistry.TryGet(BotOwner, out var entry))
        {
            if (entry.Alert != AlertState.Aggressive)
            {
                ZombieDebug.LogStateChange("MainLayer", BotOwner,
                    entry.Alert.ToString(), "Aggressive", "enemy detected");
            }
            entry.Alert = AlertState.Aggressive;
            entry.LastAlertTime = UnityEngine.Time.time;
        }

        // Log first activation as warning (always visible)
        if (!_loggedActivation)
        {
            _loggedActivation = true;
            Plugin.Log.LogWarning("[ZSlayerHQ] ZombieMainLayer ACTIVATED — BigBrain combat layer is working!");
        }

        _activationCount++;
        ZombieDebug.LogLayerActive("Combat", BotOwner);

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
            ZombieArchetype.Wraith => typeof(WraithLogic),
            _ => typeof(ShamblerLogic),
        };

        var id = ZombieDebug.BotId(BotOwner);
        ZombieDebug.Log($"Combat GetNextAction: bot={id} -> {entry.Archetype.Type} (logicType={logicType.Name})");

        return new Action(logicType, $"{entry.Archetype.Type}-combat");
    }

    public override bool IsCurrentActionEnding()
    {
        bool ending = !BotOwner.Memory.HaveEnemy;
        if (ending)
        {
            ZombieDebug.Log($"Combat action ending: bot={ZombieDebug.BotId(BotOwner)} (lost enemy)");
        }
        return ending;
    }
}
