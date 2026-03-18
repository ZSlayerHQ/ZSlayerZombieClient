using System.Collections.Generic;
using UnityEngine;
using ZSlayerZombieClient.Core;

namespace ZSlayerZombieClient.Horde;

/// <summary>
/// Alert state propagation within zombie groups.
///
/// 3-tier alert system:
///   Unaware → zombie idles/wanders (no stimulus)
///   Alerted → moves toward alert source, investigating
///   Aggressive → full pursuit, combat logic active
///
/// Propagation rules:
/// - An Aggressive zombie alerts Unaware neighbors within alertRadius → Alerted
/// - Multiple Aggressive zombies expand the effective radius (+25% per extra)
/// - Alerted zombies don't propagate further (prevents infinite cascade)
/// - Zombies that already have an enemy aren't affected (they're already Aggressive)
/// </summary>
public static class AlertPropagation
{
    private static bool _loggedFirstSpread;
    private static int _totalSpreads;

    public static void Propagate(List<HordeGroup> groups, float baseRadius)
    {
        for (int g = 0; g < groups.Count; g++)
        {
            var group = groups[g];
            if (group.Size < 2) continue;

            // Find all aggressive members as alert sources
            for (int i = 0; i < group.Members.Count; i++)
            {
                var source = group.Members[i];
                if (source.Alert != AlertState.Aggressive) continue;
                if (source.Bot == null || source.Bot.IsDead) continue;

                // Scale radius based on how many in the group are already aggressive
                float effectiveRadius = baseRadius * (1f + (group.AggressiveCount - 1) * 0.25f);
                float effectiveRadiusSqr = effectiveRadius * effectiveRadius;

                var sourcePos = source.Bot.Position;

                // Get the target position for alerted zombies to investigate
                Vector3? targetPos = null;
                var sourceEnemy = source.Bot.Memory?.GoalEnemy;
                if (sourceEnemy != null)
                    targetPos = sourceEnemy.CurrPosition;

                for (int j = 0; j < group.Members.Count; j++)
                {
                    var target = group.Members[j];
                    if (target == source) continue;
                    if (target.Alert != AlertState.Unaware) continue;
                    if (target.Bot == null || target.Bot.IsDead) continue;

                    float distSqr = (target.Bot.Position - sourcePos).sqrMagnitude;
                    if (distSqr > effectiveRadiusSqr) continue;

                    // Promote to Alerted
                    target.Alert = AlertState.Alerted;
                    target.LastAlertTime = Time.time;

                    if (targetPos.HasValue)
                    {
                        target.HordeTargetPosition = targetPos.Value;
                        target.HordeTargetTime = Time.time;
                    }

                    _totalSpreads++;

                    if (!_loggedFirstSpread)
                    {
                        _loggedFirstSpread = true;
                        float dist = Mathf.Sqrt(distSqr);
                        Plugin.Log.LogWarning($"[ZSlayerHQ] Horde: First alert spread — " +
                            $"{ZombieDebug.BotId(source.Bot)} (Aggressive) alerted " +
                            $"{ZombieDebug.BotId(target.Bot)} (was Unaware) at {dist:F1}m");
                    }

                    ZombieDebug.LogThrottled("alert-spread", 5f,
                        $"Alert spread: {ZombieDebug.BotId(source.Bot)} -> {ZombieDebug.BotId(target.Bot)} " +
                        $"(total spreads: {_totalSpreads})");
                }
            }
        }
    }
}
