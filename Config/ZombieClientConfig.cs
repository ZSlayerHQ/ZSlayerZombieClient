using BepInEx.Configuration;

namespace ZSlayerZombieClient.Config;

public class ZombieClientConfig
{
    // Archetype weights
    public ConfigEntry<int> ShamblerWeight { get; }
    public ConfigEntry<int> RunnerWeight { get; }
    public ConfigEntry<int> CrawlerWeight { get; }
    public ConfigEntry<int> StalkerWeight { get; }
    public ConfigEntry<int> BerserkerWeight { get; }
    public ConfigEntry<int> WraithWeight { get; }

    // Movement
    public ConfigEntry<float> ShamblerMinSpeed { get; }
    public ConfigEntry<float> ShamblerMaxSpeed { get; }
    public ConfigEntry<float> RunnerSprintDuration { get; }
    public ConfigEntry<float> RunnerRecoveryDuration { get; }

    // Brain
    public ConfigEntry<string> InfectedBrainNames { get; }

    // Horde
    public ConfigEntry<bool> HordeEnabled { get; }
    public ConfigEntry<float> HordeGroupRadius { get; }
    public ConfigEntry<float> HordeAlertRadius { get; }
    public ConfigEntry<int> HordeRushMinCount { get; }
    public ConfigEntry<float> HordeRushDistance { get; }
    public ConfigEntry<float> HordeRushDuration { get; }
    public ConfigEntry<float> HordeTickRate { get; }

    // Debug
    public ConfigEntry<bool> DebugLogging { get; }

    public ZombieClientConfig(ConfigFile config)
    {
        ShamblerWeight = config.Bind("Archetypes", "ShamblerWeight", 40, "Weight for Shambler archetype");
        RunnerWeight = config.Bind("Archetypes", "RunnerWeight", 25, "Weight for Runner archetype");
        CrawlerWeight = config.Bind("Archetypes", "CrawlerWeight", 10, "Weight for Crawler archetype — slow lurker, surprise burst");
        StalkerWeight = config.Bind("Archetypes", "StalkerWeight", 15, "Weight for Stalker archetype — crouched flanker, tactical");
        BerserkerWeight = config.Bind("Archetypes", "BerserkerWeight", 10, "Weight for Berserker archetype");
        WraithWeight = config.Bind("Archetypes", "WraithWeight", 5, "Weight for Wraith archetype — sneaks up silently, flees when spotted");

        ShamblerMinSpeed = config.Bind("Movement", "ShamblerMinSpeed", 0.3f, "Shambler minimum move speed (0-1)");
        ShamblerMaxSpeed = config.Bind("Movement", "ShamblerMaxSpeed", 0.5f, "Shambler maximum move speed (0-1)");
        RunnerSprintDuration = config.Bind("Movement", "RunnerSprintDuration", 4.5f, "Runner sprint burst duration (seconds)");
        RunnerRecoveryDuration = config.Bind("Movement", "RunnerRecoveryDuration", 3.0f, "Runner recovery duration (seconds)");

        InfectedBrainNames = config.Bind("Brain", "InfectedBrainNames", "InfectedSlow,InfectedFast,InfectedShooting",
            "Comma-separated brain names for infected bots (may change between SPT versions)");

        HordeEnabled = config.Bind("Horde", "Enabled", true, "Enable horde coordination (alert spreading, alpha election, rush attacks)");
        HordeGroupRadius = config.Bind("Horde", "GroupRadius", 30f, "Max distance between zombies to be considered part of the same group");
        HordeAlertRadius = config.Bind("Horde", "AlertRadius", 20f, "Radius within which an aggressive zombie alerts unaware ones");
        HordeRushMinCount = config.Bind("Horde", "RushMinCount", 4, "Minimum group size to trigger a coordinated rush attack");
        HordeRushDistance = config.Bind("Horde", "RushDistance", 30f, "Max target distance to trigger rush (closer = more likely)");
        HordeRushDuration = config.Bind("Horde", "RushDuration", 10f, "Duration of rush attack in seconds");
        HordeTickRate = config.Bind("Horde", "TickRate", 0.5f, "Seconds between horde system updates (lower = more responsive but more CPU)");

        DebugLogging = config.Bind("Debug", "DebugLogging", true, "Enable verbose debug logging (disable for production)");
    }
}
