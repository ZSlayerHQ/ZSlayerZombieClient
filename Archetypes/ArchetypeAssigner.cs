using EFT;
using ZSlayerZombieClient.Config;
using ZSlayerZombieClient.Core;

namespace ZSlayerZombieClient.Archetypes;

public class ArchetypeAssigner
{
    private readonly ZombieClientConfig _config;

    public ArchetypeAssigner(ZombieClientConfig config)
    {
        _config = config;
    }

    public ArchetypeData Assign(BotOwner bot)
    {
        var role = bot.Profile.Info.Settings.Role;

        // Special overrides
        if (role == (WildSpawnType)ZombieConstants.InfectedTagilla)
            return ArchetypeData.Berserker;

        if (role == (WildSpawnType)ZombieConstants.InfectedLaborant)
        {
            return SeededRandom(bot.Profile.Id, 1000) < 600
                ? ArchetypeData.Crawler
                : ArchetypeData.Shambler;
        }

        // Weighted random selection (deterministic per ProfileId for FIKA sync)
        var weights = new[]
        {
            (_config.ShamblerWeight.Value, ArchetypeData.Shambler),
            (_config.RunnerWeight.Value, ArchetypeData.Runner),
            (_config.CrawlerWeight.Value, ArchetypeData.Crawler),
            (_config.StalkerWeight.Value, ArchetypeData.Stalker),
            (_config.BerserkerWeight.Value, ArchetypeData.Berserker),
        };

        int total = 0;
        foreach (var (w, _) in weights) total += w;
        if (total <= 0) return ArchetypeData.Shambler;

        int roll = SeededRandom(bot.Profile.Id, total);
        int cumulative = 0;
        foreach (var (w, data) in weights)
        {
            cumulative += w;
            if (roll < cumulative) return data;
        }

        return ArchetypeData.Shambler;
    }

    /// <summary>
    /// Deterministic random from ProfileId hash — same result on all FIKA clients.
    /// </summary>
    private static int SeededRandom(string profileId, int max)
    {
        int hash = 0;
        if (profileId != null)
        {
            foreach (char c in profileId)
                hash = hash * 31 + c;
        }
        return (hash & 0x7FFFFFFF) % max;
    }
}
