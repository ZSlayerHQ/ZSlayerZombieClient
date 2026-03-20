using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ZSlayerZombieClient.Core;

namespace ZSlayerZombieClient.Horde;

/// <summary>
/// MonoBehaviour singleton that runs the horde coordination system.
///
/// Every 0.5s (configurable):
/// 1. Collects all living registered zombies
/// 2. Groups them by spatial proximity (BFS connected components)
/// 3. Elects an alpha per group (target broadcaster)
/// 4. Propagates alert states (Aggressive → nearby Unaware becomes Alerted)
/// 5. Checks rush conditions (4+ grouped + target close = coordinated charge)
///
/// Performance: O(n²) grouping with n = registered zombie count.
/// At 50 zombies, that's 2500 distance checks every 0.5s — negligible.
/// Dead/null bots are filtered out at the start of each tick.
/// </summary>
public class HordeManager : MonoBehaviour
{
    public static HordeManager Instance { get; private set; }

    private float _nextTickTime;
    private List<HordeGroup> _groups = new();
    private readonly List<ZombieEntry> _liveZombies = new();

    private static bool _loggedFirstTick;
    private static bool _loggedFirstGroup;

    private void Awake()
    {
        Instance = this;
        Plugin.Log.LogInfo("[ZSlayerHQ] HordeManager initialized");
    }

    private void Update()
    {
        if (!Plugin.ClientConfig.HordeEnabled.Value) return;

        float time = Time.time;
        if (time < _nextTickTime) return;
        _nextTickTime = time + Plugin.ClientConfig.HordeTickRate.Value;

        // Collect living zombies
        _liveZombies.Clear();
        int totalRegistered = ZombieRegistry.Count;
        int nullBots = 0;
        int deadBots = 0;
        foreach (var entry in ZombieRegistry.GetAll())
        {
            if (entry.Bot == null) { nullBots++; continue; }
            if (entry.Bot.IsDead) { deadBots++; continue; }
            _liveZombies.Add(entry);
        }

        if (_liveZombies.Count < 2)
        {
            ZombieDebug.LogThrottled("horde-nogroup", 15f,
                $"Horde: not enough live zombies ({_liveZombies.Count} alive, " +
                $"{totalRegistered} registered, {nullBots} null, {deadBots} dead)");
            return;
        }

        if (!_loggedFirstTick)
        {
            _loggedFirstTick = true;
            Plugin.Log.LogWarning($"[ZSlayerHQ] HordeManager: First tick with {_liveZombies.Count} live zombies");
        }

        // 1. Form spatial groups
        _groups = FormGroups(_liveZombies, Plugin.ClientConfig.HordeGroupRadius.Value);

        if (!_loggedFirstGroup && _groups.Any(g => g.Size >= 2))
        {
            _loggedFirstGroup = true;
            var biggest = _groups.OrderByDescending(g => g.Size).First();
            Plugin.Log.LogWarning($"[ZSlayerHQ] Horde: First group formed — {biggest.Size} zombies " +
                $"(total groups: {_groups.Count}, zombies: {_liveZombies.Count})");
        }

        // 2. Elect alphas and broadcast targets
        AlphaZombie.ProcessGroups(_groups);

        // 3. Propagate alerts
        AlertPropagation.Propagate(_groups, Plugin.ClientConfig.HordeAlertRadius.Value);

        // 4. Check rush conditions
        HordeCoordinator.CheckRush(_groups,
            Plugin.ClientConfig.HordeRushMinCount.Value,
            Plugin.ClientConfig.HordeRushDistance.Value,
            Plugin.ClientConfig.HordeRushDuration.Value);

        // Periodic summary
        ZombieDebug.LogThrottled("horde-summary", 30f,
            $"Horde tick: {_liveZombies.Count} live, {_groups.Count} groups, " +
            $"largest={(_groups.Count > 0 ? _groups.Max(g => g.Size) : 0)}, " +
            $"rushing={_groups.Count(g => g.IsRushing)}");
    }

    /// <summary>
    /// BFS connected components grouping.
    /// Two zombies are connected if within groupRadius of each other.
    /// </summary>
    private static List<HordeGroup> FormGroups(List<ZombieEntry> zombies, float groupRadius)
    {
        var groups = new List<HordeGroup>();
        float radiusSqr = groupRadius * groupRadius;
        var assigned = new bool[zombies.Count];

        for (int i = 0; i < zombies.Count; i++)
        {
            if (assigned[i]) continue;

            var group = new HordeGroup();
            var queue = new Queue<int>();

            queue.Enqueue(i);
            assigned[i] = true;

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                group.Members.Add(zombies[current]);

                var currentPos = zombies[current].Bot.Position;

                for (int j = 0; j < zombies.Count; j++)
                {
                    if (assigned[j]) continue;

                    float distSqr = (currentPos - zombies[j].Bot.Position).sqrMagnitude;
                    if (distSqr <= radiusSqr)
                    {
                        assigned[j] = true;
                        queue.Enqueue(j);
                    }
                }
            }

            groups.Add(group);
        }

        return groups;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
