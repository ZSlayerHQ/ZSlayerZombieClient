namespace ZSlayerZombieClient.Archetypes;

public enum ZombieArchetype
{
    Shambler,
    Runner,
    Crawler,
    Stalker,
    Berserker,
    Wraith
}

public class ArchetypeData
{
    public ZombieArchetype Type { get; }
    public float MinSpeed { get; }
    public float MaxSpeed { get; }
    public float Pose { get; }
    public float MeleeRange { get; }

    public ArchetypeData(ZombieArchetype type, float minSpeed, float maxSpeed, float pose, float meleeRange)
    {
        Type = type;
        MinSpeed = minSpeed;
        MaxSpeed = maxSpeed;
        Pose = pose;
        MeleeRange = meleeRange;
    }

    public static readonly ArchetypeData Shambler = new(ZombieArchetype.Shambler, 0.3f, 0.5f, 1.0f, 4.0f);
    public static readonly ArchetypeData Runner = new(ZombieArchetype.Runner, 0.6f, 1.0f, 1.0f, 3.0f);
    public static readonly ArchetypeData Crawler = new(ZombieArchetype.Crawler, 0.3f, 0.4f, 1.0f, 2.0f);
    public static readonly ArchetypeData Stalker = new(ZombieArchetype.Stalker, 0.5f, 0.7f, 1.0f, 3.5f);
    public static readonly ArchetypeData Berserker = new(ZombieArchetype.Berserker, 0.9f, 1.0f, 1.0f, 3.0f);
    public static readonly ArchetypeData Wraith = new(ZombieArchetype.Wraith, 0.6f, 1.0f, 1.0f, 3.0f);
}
