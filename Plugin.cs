using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using DrakiaXYZ.BigBrain.Brains;
using EFT;
using HarmonyLib;
using ZSlayerZombieClient.Archetypes;
using ZSlayerZombieClient.Config;
using ZSlayerZombieClient.Core;
using ZSlayerZombieClient.Horde;
using ZSlayerZombieClient.Layers;

namespace ZSlayerZombieClient;

[BepInPlugin("com.zslayerhq.zombieclient", "ZSlayer SPT Zombies", "1.1.0")]
[BepInDependency("xyz.drakia.bigbrain", BepInDependency.DependencyFlags.HardDependency)]
[BepInDependency("me.sol.sain", BepInDependency.DependencyFlags.SoftDependency)]
public class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log;
    internal static bool SainAvailable;
    internal static ZombieClientConfig ClientConfig;
    internal static ArchetypeAssigner ArchetypeAssigner;

    // Brain name tracking for runtime auto-detection
    private static readonly HashSet<string> _registeredBrainNames = new();
    private static readonly List<WildSpawnType> _infectedRoles = new();
    private static bool _brainNameReRegistered;

    private void Awake()
    {
        Log = Logger;

        // Initialize config and archetype assigner
        ClientConfig = new ZombieClientConfig(Config);
        ArchetypeAssigner = new ArchetypeAssigner(ClientConfig);

        // Check if SAIN is loaded
        SainAvailable = IsSainLoaded();

        Log.LogInfo($"[ZSlayerHQ] ZSlayer SPT Zombies v{Info.Metadata.Version}");
        Log.LogInfo($"[ZSlayerHQ] BigBrain: required (loaded)");
        Log.LogInfo($"[ZSlayerHQ] SAIN: {(SainAvailable ? "detected — melee fix active" : "not found — using BigBrain layers only")}");

        // Parse brain names from config
        var brainNames = new List<string>();
        foreach (var name in ClientConfig.InfectedBrainNames.Value.Split(','))
        {
            var trimmed = name.Trim();
            if (!string.IsNullOrEmpty(trimmed))
                brainNames.Add(trimmed);
        }

        if (brainNames.Count == 0)
        {
            brainNames.AddRange(ZombieConstants.DefaultBrainNames);
            Log.LogWarning("[ZSlayerHQ] No brain names configured, using defaults");
        }

        foreach (var bn in brainNames)
            _registeredBrainNames.Add(bn);

        // Infected roles
        _infectedRoles.AddRange(new[]
        {
            (WildSpawnType)ZombieConstants.InfectedAssault,
            (WildSpawnType)ZombieConstants.InfectedPmc,
            (WildSpawnType)ZombieConstants.InfectedCivil,
            (WildSpawnType)ZombieConstants.InfectedLaborant,
            (WildSpawnType)ZombieConstants.InfectedTagilla,
        });

        // Register BigBrain layers (highest priority checked first)
        BrainManager.AddCustomLayer(typeof(ZombieMainLayer), brainNames, 95, _infectedRoles);
        BrainManager.AddCustomLayer(typeof(ZombieAlertLayer), brainNames, 85, _infectedRoles);
        BrainManager.AddCustomLayer(typeof(ZombieIdleLayer), brainNames, 75, _infectedRoles);

        Log.LogInfo($"[ZSlayerHQ] Registered 3 BigBrain layers for infected types (brains: {string.Join(", ", brainNames)})");

        // Apply Harmony patches
        new Harmony("com.zslayerhq.zombieclient").PatchAll();
        Log.LogInfo("[ZSlayerHQ] Harmony patches applied (InfectedMeleeFix, BotSpawnPatch)");

        // Create HordeManager MonoBehaviour
        if (ClientConfig.HordeEnabled.Value)
        {
            var hordeGo = new UnityEngine.GameObject("ZSlayerHordeManager");
            UnityEngine.Object.DontDestroyOnLoad(hordeGo);
            hordeGo.AddComponent<HordeManager>();
            Log.LogInfo("[ZSlayerHQ] HordeManager created (horde coordination active)");
        }
        else
        {
            Log.LogInfo("[ZSlayerHQ] Horde coordination disabled via config");
        }
    }

    /// <summary>
    /// Called from BotSpawnPatch when an infected bot spawns.
    /// Checks if the detected brain name matches our registered names.
    /// If not, re-registers layers with the correct name (one-time fix).
    /// </summary>
    internal static void OnInfectedBrainDetected(string detectedBrainName)
    {
        if (string.IsNullOrEmpty(detectedBrainName)) return;
        if (_registeredBrainNames.Contains(detectedBrainName)) return;
        if (_brainNameReRegistered) return;

        _brainNameReRegistered = true;

        Log.LogWarning($"[ZSlayerHQ] *** BRAIN NAME MISMATCH ***");
        Log.LogWarning($"[ZSlayerHQ] Expected: {string.Join(", ", _registeredBrainNames)}");
        Log.LogWarning($"[ZSlayerHQ] Detected: {detectedBrainName}");
        Log.LogWarning($"[ZSlayerHQ] Re-registering BigBrain layers with correct brain name...");

        var correctedNames = new List<string>(_registeredBrainNames) { detectedBrainName };
        _registeredBrainNames.Add(detectedBrainName);

        try
        {
            BrainManager.AddCustomLayer(typeof(ZombieMainLayer), correctedNames, 95, _infectedRoles);
            BrainManager.AddCustomLayer(typeof(ZombieAlertLayer), correctedNames, 85, _infectedRoles);
            BrainManager.AddCustomLayer(typeof(ZombieIdleLayer), correctedNames, 75, _infectedRoles);
            Log.LogWarning($"[ZSlayerHQ] Re-registered layers with brain name '{detectedBrainName}' — future zombies will use custom behavior");
            Log.LogWarning($"[ZSlayerHQ] Update BepInEx config 'InfectedBrainNames' to include '{detectedBrainName}' to avoid this on next launch");
        }
        catch (System.Exception ex)
        {
            Log.LogError($"[ZSlayerHQ] Failed to re-register layers: {ex.Message}");
        }
    }

    private static bool IsSainLoaded()
    {
        try
        {
            foreach (var plugin in BepInEx.Bootstrap.Chainloader.PluginInfos)
            {
                if (plugin.Key == "me.sol.sain")
                    return true;
            }
        }
        catch
        {
            // Chainloader not ready or SAIN not present
        }

        return false;
    }
}
