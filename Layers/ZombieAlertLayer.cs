using DrakiaXYZ.BigBrain.Brains;
using EFT;
using ZSlayerZombieClient.Core;
using ZSlayerZombieClient.Logic;

namespace ZSlayerZombieClient.Layers;

/// <summary>
/// Medium priority layer (85) — activates when the zombie heard something
/// or was recently in combat but lost sight. Zombie investigates the last
/// known position before dropping back to idle.
/// </summary>
public class ZombieAlertLayer : CustomLayer
{
    private static bool _loggedActivation;

    public ZombieAlertLayer(BotOwner botOwner, int priority) : base(botOwner, priority) { }

    public override string GetName() => "ZSlayerZombieAlert";

    public override bool IsActive()
    {
        // Don't activate if we have a direct enemy (main combat layer handles that)
        if (BotOwner.Memory.HaveEnemy) return false;

        // Activate if we're in alert state (heard something, lost sight of enemy)
        if (ZombieRegistry.TryGet(BotOwner, out var entry))
        {
            if (entry.Alert == AlertState.Alerted || entry.Alert == AlertState.Aggressive)
            {
                if (!_loggedActivation)
                {
                    _loggedActivation = true;
                    Plugin.Log.LogWarning("[ZSlayerHQ] ZombieAlertLayer ACTIVATED — BigBrain alert layer is working!");
                }
                ZombieDebug.LogLayerActive("Alert", BotOwner);
                return true;
            }
        }

        // Activate if we had an enemy recently (GoalEnemy still set but HaveEnemy is false)
        if (BotOwner.Memory.GoalEnemy != null)
        {
            // Check if zombie is registered; if not, register and check alert
            if (entry == null)
                entry = ZombieRegistry.GetOrRegister(BotOwner);

            // Transition to alerted if we just lost sight of enemy
            if (entry.Alert == AlertState.Unaware)
            {
                entry.Alert = AlertState.Alerted;
                entry.LastAlertTime = UnityEngine.Time.time;
                ZombieDebug.LogStateChange("AlertLayer", BotOwner,
                    "Unaware", "Alerted", "GoalEnemy present but HaveEnemy=false");
            }

            ZombieDebug.LogLayerActive("Alert", BotOwner);
            return true;
        }

        return false;
    }

    public override Action GetNextAction()
    {
        ZombieDebug.Log($"Alert GetNextAction: bot={ZombieDebug.BotId(BotOwner)} -> InvestigateLogic");
        return new Action(typeof(InvestigateLogic), "investigate");
    }

    public override bool IsCurrentActionEnding()
    {
        // End if we got an enemy again (combat layer takes over)
        if (BotOwner.Memory.HaveEnemy)
        {
            ZombieDebug.Log($"Alert action ending: bot={ZombieDebug.BotId(BotOwner)} (enemy re-acquired, combat takes over)");
            return true;
        }

        // End if alert has decayed
        if (ZombieRegistry.TryGet(BotOwner, out var entry))
        {
            if (entry.Alert == AlertState.Unaware)
            {
                ZombieDebug.Log($"Alert action ending: bot={ZombieDebug.BotId(BotOwner)} (alert decayed to Unaware)");
                return true;
            }

            // Auto-decay alert after timeout
            if (UnityEngine.Time.time - entry.LastAlertTime > ZombieConstants.AlertDecayTime)
            {
                ZombieDebug.LogStateChange("AlertLayer", BotOwner,
                    entry.Alert.ToString(), "Unaware", $"timeout ({ZombieConstants.AlertDecayTime}s)");
                entry.Alert = AlertState.Unaware;
                return true;
            }
        }

        return false;
    }
}
