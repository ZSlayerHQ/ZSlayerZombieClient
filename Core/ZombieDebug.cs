using System.Collections.Generic;
using EFT;
using UnityEngine;

namespace ZSlayerZombieClient.Core;

/// <summary>
/// Centralized debug logging for zombie behavior.
/// All methods are no-ops when DebugLogging is disabled.
/// Includes throttling and one-shot helpers to prevent log spam.
/// </summary>
public static class ZombieDebug
{
    private static readonly HashSet<string> _loggedOnce = new();
    private static readonly Dictionary<string, float> _throttleTimers = new();

    /// <summary>Short bot ID for readable logs (first 8 chars of ProfileId).</summary>
    public static string BotId(BotOwner bot)
    {
        var id = bot?.Profile?.Id;
        if (string.IsNullOrEmpty(id)) return "null";
        return id.Substring(0, System.Math.Min(8, id.Length));
    }

    /// <summary>Log debug message (only when debug enabled).</summary>
    public static void Log(string msg)
    {
        if (!Plugin.ClientConfig.DebugLogging.Value) return;
        Plugin.Log.LogInfo($"[ZSlayerHQ] {msg}");
    }

    /// <summary>Log warning-level message (only when debug enabled).</summary>
    public static void Warn(string msg)
    {
        if (!Plugin.ClientConfig.DebugLogging.Value) return;
        Plugin.Log.LogWarning($"[ZSlayerHQ] {msg}");
    }

    /// <summary>Log message only once per unique key (gated by debug).</summary>
    public static void LogOnce(string key, string msg)
    {
        if (!Plugin.ClientConfig.DebugLogging.Value) return;
        if (!_loggedOnce.Add(key)) return;
        Plugin.Log.LogInfo($"[ZSlayerHQ] {msg}");
    }

    /// <summary>Log message at most once per intervalSec for a given key.</summary>
    public static void LogThrottled(string key, float intervalSec, string msg)
    {
        if (!Plugin.ClientConfig.DebugLogging.Value) return;
        float time = Time.time;
        if (_throttleTimers.TryGetValue(key, out var next) && time < next) return;
        _throttleTimers[key] = time + intervalSec;
        Plugin.Log.LogInfo($"[ZSlayerHQ] {msg}");
    }

    /// <summary>Log a layer activation with bot details.</summary>
    public static void LogLayerActive(string layerName, BotOwner bot)
    {
        if (!Plugin.ClientConfig.DebugLogging.Value) return;
        var id = BotId(bot);
        var archetype = "unknown";
        var alert = "unknown";
        if (ZombieRegistry.TryGet(bot, out var entry))
        {
            archetype = entry.Archetype.Type.ToString();
            alert = entry.Alert.ToString();
        }
        LogThrottled($"layer-{layerName}-{id}", 5f,
            $"Layer {layerName} active: bot={id} archetype={archetype} alert={alert}");
    }

    /// <summary>Log a logic class starting with full bot context.</summary>
    public static void LogLogicStart(string logicName, BotOwner bot, string extraInfo = "")
    {
        if (!Plugin.ClientConfig.DebugLogging.Value) return;
        var id = BotId(bot);
        var speedMul = 1f;
        var archetype = "unknown";
        if (ZombieRegistry.TryGet(bot, out var entry))
        {
            speedMul = entry.SpeedMultiplier;
            archetype = entry.Archetype.Type.ToString();
        }

        var enemyInfo = "no enemy";
        var enemy = bot.Memory?.GoalEnemy;
        if (enemy != null)
        {
            var dist = (bot.Position - enemy.CurrPosition).magnitude;
            enemyInfo = $"enemy at {dist:F1}m";
        }

        var extra = string.IsNullOrEmpty(extraInfo) ? "" : $" | {extraInfo}";
        Plugin.Log.LogInfo($"[ZSlayerHQ] >> {logicName} START: bot={id} ({archetype}, speed={speedMul:F2}x) {enemyInfo}{extra}");
    }

    /// <summary>Log a state transition in a logic class.</summary>
    public static void LogStateChange(string logicName, BotOwner bot, string fromState, string toState, string reason = "")
    {
        if (!Plugin.ClientConfig.DebugLogging.Value) return;
        var id = BotId(bot);
        var reasonStr = string.IsNullOrEmpty(reason) ? "" : $" ({reason})";
        Plugin.Log.LogInfo($"[ZSlayerHQ] {logicName} [{id}]: {fromState} -> {toState}{reasonStr}");
    }

    /// <summary>Log a combat event (lunge, rush, attack, etc).</summary>
    public static void LogCombatEvent(string logicName, BotOwner bot, string eventName, float distance)
    {
        if (!Plugin.ClientConfig.DebugLogging.Value) return;
        var id = BotId(bot);
        Plugin.Log.LogInfo($"[ZSlayerHQ] {logicName} [{id}]: {eventName} at {distance:F1}m");
    }
}
