using System.Collections.Generic;
using UnityEngine;
using ZSlayerZombieClient.Core;

namespace ZSlayerZombieClient.Horde;

/// <summary>
/// Alpha zombie election and target broadcasting.
///
/// Each group has one alpha — the leader that broadcasts its target
/// to group members. Election priority:
/// 1. First Aggressive member (already has a target)
/// 2. If tie, prefer non-Shambler archetypes (more aggressive types lead)
///
/// When an alpha dies, the nearest living member inherits.
/// The alpha's GoalEnemy position becomes the group's shared target.
/// </summary>
public static class AlphaZombie
{
    private static bool _loggedFirstElection;

    /// <summary>
    /// Elect an alpha for each group and broadcast targets.
    /// </summary>
    public static void ProcessGroups(List<HordeGroup> groups)
    {
        for (int i = 0; i < groups.Count; i++)
        {
            var group = groups[i];
            if (group.Size < 2) continue; // Solo zombies don't need alphas

            ElectAlpha(group);
            BroadcastTarget(group);
        }
    }

    private static void ElectAlpha(HordeGroup group)
    {
        // Clear previous alpha flags
        for (int i = 0; i < group.Members.Count; i++)
            group.Members[i].IsAlpha = false;

        ZombieEntry bestCandidate = null;
        int bestScore = -1;

        for (int i = 0; i < group.Members.Count; i++)
        {
            var member = group.Members[i];
            if (member.Bot == null || member.Bot.IsDead) continue;

            // Score: Aggressive > Alerted > Unaware, plus archetype bonus
            int score = member.Alert switch
            {
                AlertState.Aggressive => 100,
                AlertState.Alerted => 50,
                _ => 0
            };

            // Non-shambler archetypes get a small bonus (more aggressive types lead)
            if (member.Archetype.Type != Archetypes.ZombieArchetype.Shambler)
                score += 10;

            if (score > bestScore)
            {
                bestScore = score;
                bestCandidate = member;
            }
        }

        if (bestCandidate != null)
        {
            bestCandidate.IsAlpha = true;
            group.Alpha = bestCandidate;

            if (!_loggedFirstElection)
            {
                _loggedFirstElection = true;
                Plugin.Log.LogWarning($"[ZSlayerHQ] Horde: First alpha elected — {ZombieDebug.BotId(bestCandidate.Bot)} " +
                    $"({bestCandidate.Archetype.Type}) in group of {group.Size}");
            }
        }
    }

    /// <summary>
    /// Alpha broadcasts its GoalEnemy position to the group.
    /// Group members without their own enemy get a HordeTargetPosition
    /// to investigate, drawing them toward the action.
    /// </summary>
    private static void BroadcastTarget(HordeGroup group)
    {
        if (group.Alpha?.Bot == null) return;

        var alphaEnemy = group.Alpha.Bot.Memory?.GoalEnemy;
        if (alphaEnemy == null)
        {
            group.SharedTarget = null;
            return;
        }

        var targetPos = alphaEnemy.CurrPosition;
        group.SharedTarget = targetPos;

        float time = Time.time;

        for (int i = 0; i < group.Members.Count; i++)
        {
            var member = group.Members[i];
            if (member == group.Alpha) continue;
            if (member.Bot == null || member.Bot.IsDead) continue;

            // Only set horde target for members who don't already have their own enemy
            if (!member.Bot.Memory.HaveEnemy)
            {
                member.HordeTargetPosition = targetPos;
                member.HordeTargetTime = time;
            }
        }
    }
}
