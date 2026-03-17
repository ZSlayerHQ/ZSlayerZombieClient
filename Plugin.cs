using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using DrakiaXYZ.BigBrain.Brains;
using EFT;
using HarmonyLib;
using ZSlayerZombieClient.Archetypes;
using ZSlayerZombieClient.Config;
using ZSlayerZombieClient.Core;
using ZSlayerZombieClient.Layers;

namespace ZSlayerZombieClient;

[BepInPlugin("com.zslayerhq.zombieclient", "ZSlayer Zombie Client", "0.1.0")]
[BepInDependency("xyz.drakia.bigbrain", BepInDependency.DependencyFlags.HardDependency)]
[BepInDependency("me.sol.sain", BepInDependency.DependencyFlags.SoftDependency)]
public class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log;
    internal static bool SainAvailable;
    internal static ZombieClientConfig ClientConfig;
    internal static ArchetypeAssigner ArchetypeAssigner;

    private void Awake()
    {
        Log = Logger;

        // Initialize config and archetype assigner
        ClientConfig = new ZombieClientConfig(Config);
        ArchetypeAssigner = new ArchetypeAssigner(ClientConfig);

        // Check if SAIN is loaded
        SainAvailable = IsSainLoaded();

        Log.LogInfo($"[ZSlayerHQ] ZSlayer Zombie Client v{Info.Metadata.Version}");
        Log.LogInfo($"[ZSlayerHQ] BigBrain: required (loaded)");
        Log.LogInfo($"[ZSlayerHQ] SAIN: {(SainAvailable ? "detected — enhanced AI active" : "not found — using BigBrain layers only")}");

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

        // Infected roles
        var infectedRoles = new List<WildSpawnType>
        {
            (WildSpawnType)ZombieConstants.InfectedAssault,
            (WildSpawnType)ZombieConstants.InfectedPmc,
            (WildSpawnType)ZombieConstants.InfectedCivil,
            (WildSpawnType)ZombieConstants.InfectedLaborant,
            (WildSpawnType)ZombieConstants.InfectedTagilla,
        };

        // Register BigBrain layers (highest priority checked first)
        BrainManager.AddCustomLayer(typeof(ZombieMainLayer), brainNames, 95, infectedRoles);
        BrainManager.AddCustomLayer(typeof(ZombieIdleLayer), brainNames, 75, infectedRoles);

        Log.LogInfo($"[ZSlayerHQ] Registered 2 BigBrain layers for infected types (brains: {string.Join(", ", brainNames)})");

        // Apply Harmony patches
        new Harmony("com.zslayerhq.zombieclient").PatchAll();
        Log.LogInfo("[ZSlayerHQ] Harmony patches applied");
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
