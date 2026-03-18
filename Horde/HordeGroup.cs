using System.Collections.Generic;
using UnityEngine;
using ZSlayerZombieClient.Core;

namespace ZSlayerZombieClient.Horde;

/// <summary>
/// Ephemeral data for a spatial group of zombies. Recalculated each tick
/// by HordeManager. Groups form when zombies are within GroupRadius of
/// each other (connected components via BFS).
/// </summary>
public class HordeGroup
{
    public List<ZombieEntry> Members { get; } = new();
    public ZombieEntry Alpha { get; set; }
    public Vector3? SharedTarget { get; set; }
    public bool IsRushing { get; set; }
    public float RushEndTime { get; set; }

    public int Size => Members.Count;

    /// <summary>
    /// Number of members currently in Aggressive alert state.
    /// Used for rush threshold checks and alert radius scaling.
    /// </summary>
    public int AggressiveCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < Members.Count; i++)
                if (Members[i].Alert == AlertState.Aggressive) count++;
            return count;
        }
    }
}
