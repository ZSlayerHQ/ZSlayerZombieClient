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
                return true;
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
            }

            return true;
        }

        return false;
    }

    public override Action GetNextAction()
    {
        return new Action(typeof(InvestigateLogic), "investigate");
    }

    public override bool IsCurrentActionEnding()
    {
        // End if we got an enemy again (combat layer takes over)
        if (BotOwner.Memory.HaveEnemy) return true;

        // End if alert has decayed
        if (ZombieRegistry.TryGet(BotOwner, out var entry))
        {
            if (entry.Alert == AlertState.Unaware) return true;

            // Auto-decay alert after timeout
            if (UnityEngine.Time.time - entry.LastAlertTime > ZombieConstants.AlertDecayTime)
            {
                entry.Alert = AlertState.Unaware;
                return true;
            }
        }

        return false;
    }
}
