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

    // Timing
    public const float PathUpdateInterval = 0.5f;
    public const float VocalizationChance = 0.005f;
    public const float StumbleMinInterval = 3f;
    public const float StumbleMaxInterval = 8f;
    public const float StumbleMinDuration = 0.5f;
    public const float StumbleMaxDuration = 1.5f;
}
