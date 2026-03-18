using System.Collections.Concurrent;
using EFT;
using ZSlayerZombieClient.Archetypes;

namespace ZSlayerZombieClient.Core;

public static class ZombieRegistry
{
    private static readonly ConcurrentDictionary<string, ZombieEntry> _zombies = new();

    public static ZombieEntry GetOrRegister(BotOwner bot)
    {
        var id = bot.Profile.Id;
        if (_zombies.TryGetValue(id, out var existing))
            return existing;

        var archetype = Plugin.ArchetypeAssigner.Assign(bot);
        var entry = new ZombieEntry(bot, archetype);

        if (_zombies.TryAdd(id, entry))
        {
            if (Plugin.ClientConfig.DebugLogging.Value)
                Plugin.Log.LogInfo($"[ZSlayerHQ] Zombie registered: {id.Substring(0, System.Math.Min(8, id.Length))} -> {archetype.Type} (speed: {entry.SpeedMultiplier:F2}x)");
        }
        else
        {
            _zombies.TryGetValue(id, out entry);
        }

        return entry;
    }

    public static bool TryGet(BotOwner bot, out ZombieEntry entry)
    {
        return _zombies.TryGetValue(bot.Profile.Id, out entry);
    }

    public static bool Remove(BotOwner bot)
    {
        return _zombies.TryRemove(bot.Profile.Id, out _);
    }

    public static int Count => _zombies.Count;
    public static void Clear() => _zombies.Clear();
}

public class ZombieEntry
{
    public BotOwner Bot { get; }
    public ArchetypeData Archetype { get; }
    public AlertState Alert { get; set; } = AlertState.Unaware;
    public float LastAlertTime { get; set; }

    /// <summary>
    /// Per-zombie speed multiplier (0.8 - 1.3). Seeded from ProfileId
    /// for FIKA determinism. Makes each zombie feel unique — some
    /// shamblers are faster, some runners are slower.
    /// </summary>
    public float SpeedMultiplier { get; }

    public ZombieEntry(BotOwner bot, ArchetypeData archetype)
    {
        Bot = bot;
        Archetype = archetype;

        // Deterministic speed variance from ProfileId hash
        // Range: 0.8x to 1.3x (some zombies are notably faster)
        int hash = 0;
        var id = bot.Profile.Id;
        if (id != null)
        {
            // Use a different hash seed than archetype assignment
            foreach (char c in id)
                hash = hash * 37 + c;
        }
        float normalized = ((hash & 0x7FFFFFFF) % 1000) / 1000f; // 0.0 - 1.0
        SpeedMultiplier = 0.8f + normalized * 0.5f; // 0.8 - 1.3
    }
}

public enum AlertState
{
    Unaware,
    Alerted,
    Aggressive
}
