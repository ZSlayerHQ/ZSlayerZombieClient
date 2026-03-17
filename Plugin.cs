using BepInEx;
using BepInEx.Logging;

namespace ZSlayerZombieClient;

[BepInPlugin("com.zslayerhq.zombieclient", "ZSlayer Zombie Client", "0.1.0")]
[BepInDependency("xyz.drakia.bigbrain", BepInDependency.DependencyFlags.HardDependency)]
[BepInDependency("me.sol.sain", BepInDependency.DependencyFlags.SoftDependency)]
public class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log;
    internal static bool SainAvailable;

    private void Awake()
    {
        Log = Logger;

        // Check if SAIN is loaded
        SainAvailable = IsSainLoaded();

        Log.LogInfo($"[ZSlayerHQ] ZSlayer Zombie Client v{Info.Metadata.Version}");
        Log.LogInfo($"[ZSlayerHQ] BigBrain: required (loaded)");
        Log.LogInfo($"[ZSlayerHQ] SAIN: {(SainAvailable ? "detected — enhanced AI active" : "not found — using BigBrain layers only")}");

        // TODO: Register BigBrain brain layers for zombie types
        // TODO: Register custom zombie behaviors (sprint, lunge, group aggro)
        // TODO: If SAIN available, register SAIN personality overrides
    }

    private static bool IsSainLoaded()
    {
        try
        {
            foreach (var plugin in BepInEx.Bootstrap.Chainloader.PluginInfos)
            {
                if (plugin.Key == "me.sol.sain")
                    return true;
            }
        }
        catch
        {
            // Chainloader not ready or SAIN not present
        }

        return false;
    }
}
