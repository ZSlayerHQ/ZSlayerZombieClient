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
                Plugin.Log.LogInfo($"[ZSlayerHQ] Zombie registered: {id.Substring(0, System.Math.Min(8, id.Length))} -> {archetype.Type}");
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

    public ZombieEntry(BotOwner bot, ArchetypeData archetype)
    {
        Bot = bot;
        Archetype = archetype;
    }
}

public enum AlertState
{
    Unaware,
    Alerted,
    Aggressive
}
