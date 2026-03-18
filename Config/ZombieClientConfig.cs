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

        DebugLogging = config.Bind("Debug", "DebugLogging", false, "Enable verbose debug logging");
    }
}
