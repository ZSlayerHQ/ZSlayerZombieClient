namespace ZSlayerZombieClient.Core;

public static class ZombieConstants
{
    // WildSpawnType enum values for infected bots
    public const int InfectedAssault = 60;
    public const int InfectedPmc = 61;
    public const int InfectedCivil = 62;
    public const int InfectedLaborant = 63;
    public const int InfectedTagilla = 64;

    // Default brain names (may change between SPT versions)
    public static readonly string[] DefaultBrainNames =
    {
        "InfectedSlow",
        "InfectedFast",
        "InfectedShooting"
    };

    // Path update intervals
    public const float PathUpdateInterval = 0.5f;
    public const float FastPathUpdateInterval = 0.3f;
    public const float BerserkerPathUpdateInterval = 0.2f;

    // Vocalization
    public const float VocalizationChance = 0.005f;
    public const float RunnerVocalizationChance = 0.015f;
    public const float BerserkerVocalizationChance = 0.025f;

    // Shambler stumble timing
    public const float StumbleMinInterval = 3f;
    public const float StumbleMaxInterval = 8f;
    public const float StumbleMinDuration = 0.5f;
    public const float StumbleMaxDuration = 1.5f;

    // Lunge (close-range burst)
    public const float LungeDistance = 6f;
    public const float LungeSpeedBoost = 0.3f;
    public const float LungeDuration = 0.8f;

    // Investigation
    public const float InvestigateTimeout = 15f;
    public const float AlertDecayTime = 20f;

    // Look tracking interval
    public const float LookUpdateInterval = 0.15f;
}
