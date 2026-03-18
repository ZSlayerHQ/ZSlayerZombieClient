using System.Collections.Generic;
using UnityEngine;
using ZSlayerZombieClient.Core;

namespace ZSlayerZombieClient.Horde;

/// <summary>
/// Horde rush coordination and flanking position assignment.
///
/// Rush trigger: alpha is Aggressive, 4+ group members, target within 30m.
/// When rushing: all members sprint at max speed toward the target for 10s.
/// Group vocalization intensifies during rush.
///
/// Flanking: during rush, members get offset positions around the target
/// so they converge from multiple angles, not a single file line.
///   - 2-3 approach from front
///   - 1-2 from the sides
///   - 1 from behind (groups of 6+)
/// </summary>
public static class HordeCoordinator
{
    private static bool _loggedFirstRush;
    private static int _rushCount;

    /// <summary>
    /// Check rush conditions and activate rushes for eligible groups.
    /// </summary>
    public static void CheckRush(List<HordeGroup> groups, int minCount, float maxDistance, float duration)
    {
        float time = Time.time;

        for (int g = 0; g < groups.Count; g++)
        {
            var group = groups[g];

            // End expired rushes
            if (group.IsRushing && time >= group.RushEndTime)
            {
                EndRush(group);
                continue;
            }

            // Don't start a new rush if already rushing
            if (group.IsRushing) continue;

            // Check rush conditions
            if (group.Alpha?.Bot == null) continue;
            if (group.Alpha.Alert != AlertState.Aggressive) continue;
            if (group.Size < minCount) continue;

            var alphaEnemy = group.Alpha.Bot.Memory?.GoalEnemy;
            if (alphaEnemy == null) continue;

            float targetDist = alphaEnemy.Distance;
            if (targetDist > maxDistance) continue;

            // All conditions met — trigger rush
            StartRush(group, alphaEnemy.CurrPosition, duration, time);
        }
    }

    private static void StartRush(HordeGroup group, Vector3 targetPos, float duration, float time)
    {
        group.IsRushing = true;
        group.RushEndTime = time + duration;
        group.SharedTarget = targetPos;
        _rushCount++;

        if (!_loggedFirstRush)
        {
            _loggedFirstRush = true;
            Plugin.Log.LogWarning($"[ZSlayerHQ] Horde: FIRST RUSH triggered — {group.Size} zombies charging!");
        }

        ZombieDebug.Warn($"Horde RUSH #{_rushCount}: {group.Size} zombies, alpha={ZombieDebug.BotId(group.Alpha.Bot)}");

        for (int i = 0; i < group.Members.Count; i++)
        {
            var member = group.Members[i];
            if (member.Bot == null || member.Bot.IsDead) continue;

            member.IsRushing = true;
            member.RushEndTime = group.RushEndTime;

            // Assign flanking position offset
            member.HordeTargetPosition = GetFlankPosition(targetPos, member.Bot.Position, i, group.Size);
            member.HordeTargetTime = time;

            // Promote all members to Aggressive during rush
            if (member.Alert != AlertState.Aggressive)
            {
                member.Alert = AlertState.Aggressive;
                member.LastAlertTime = time;
            }

            // Scream during rush
            try { member.Bot.BotTalk?.Say(EPhraseTrigger.OnFight); }
            catch { }
        }
    }

    private static void EndRush(HordeGroup group)
    {
        group.IsRushing = false;

        for (int i = 0; i < group.Members.Count; i++)
        {
            var member = group.Members[i];
            member.IsRushing = false;
        }

        ZombieDebug.Log($"Horde rush ended (group of {group.Size})");
    }

    /// <summary>
    /// Calculate a flanking position offset for this member during a rush.
    /// Distributes zombies around the target to converge from multiple angles.
    /// </summary>
    public static Vector3 GetFlankPosition(Vector3 targetPos, Vector3 memberPos, int memberIndex, int groupSize)
    {
        // Small groups: mostly direct approach with slight offsets
        if (groupSize <= 3)
        {
            float angle = memberIndex * (360f / groupSize);
            float rad = angle * Mathf.Deg2Rad;
            return targetPos + new Vector3(Mathf.Cos(rad) * 2f, 0, Mathf.Sin(rad) * 2f);
        }

        // Larger groups: distribute around target
        // First 2-3 approach direct, rest fan out
        if (memberIndex < 3)
        {
            // Front approach — slight lateral spread
            var dir = (targetPos - memberPos).normalized;
            var perp = new Vector3(-dir.z, 0, dir.x);
            float offset = (memberIndex - 1) * 3f; // -3, 0, +3
            return targetPos + perp * offset;
        }

        // Remaining members circle around target
        float circleAngle = 90f + (memberIndex - 3) * (180f / Mathf.Max(1, groupSize - 3));
        float circleRad = circleAngle * Mathf.Deg2Rad;
        float radius = 4f;

        return targetPos + new Vector3(Mathf.Cos(circleRad) * radius, 0, Mathf.Sin(circleRad) * radius);
    }
}
