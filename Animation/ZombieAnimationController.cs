using EFT;
using UnityEngine;
using ZSlayerZombieClient.Archetypes;
using ZSlayerZombieClient.Core;

namespace ZSlayerZombieClient.Animation;

/// <summary>
/// Static API for the zombie animation system.
/// Handles initialization (attaching bone controller), state changes,
/// and cleanup. Called from BotSpawnPatch and logic classes.
/// </summary>
public static class ZombieAnimationController
{
    private static bool _loggedFirstInit;
    private static bool _loggedNoAnimator;
    private static bool _loggedNoPlayer;
    private static int _initCount;
    private static int _failCount;

    /// <summary>
    /// Initialize animation for a zombie bot. Finds the Animator,
    /// creates the bone controller MonoBehaviour, and sets initial speed.
    /// Called once per zombie from BotSpawnPatch.
    ///
    /// Safe to call multiple times — checks for existing controller.
    /// </summary>
    public static void InitializeAnimation(BotOwner bot, ZombieArchetype archetype)
    {
        try
        {
            var player = bot.GetPlayer;
            if (player == null)
            {
                if (!_loggedNoPlayer)
                {
                    _loggedNoPlayer = true;
                    Plugin.Log.LogWarning($"[ZSlayerHQ] ZombieAnimation: GetPlayer returned null for {ZombieDebug.BotId(bot)}");
                }
                _failCount++;
                return;
            }

            // Don't attach twice
            var existing = player.gameObject.GetComponent<ZombieBoneController>();
            if (existing != null) return;

            // Find the animator — try the player directly, then search children
            var animator = player.GetComponent<Animator>();
            if (animator == null)
                animator = player.GetComponentInChildren<Animator>();

            if (animator == null)
            {
                if (!_loggedNoAnimator)
                {
                    _loggedNoAnimator = true;
                    Plugin.Log.LogWarning($"[ZSlayerHQ] ZombieAnimation: No Animator found on {ZombieDebug.BotId(bot)} — " +
                        $"bone manipulation disabled, animator speed unavailable");
                }
                _failCount++;
                return;
            }

            var profile = ArchetypeAnimationProfile.Get(archetype);
            var controller = player.gameObject.AddComponent<ZombieBoneController>();
            controller.Initialize(animator, profile, ZombieDebug.BotId(bot));

            // Set initial animator speed
            animator.speed = profile.BaseAnimatorSpeed;

            _initCount++;

            if (!_loggedFirstInit)
            {
                _loggedFirstInit = true;
                Plugin.Log.LogWarning($"[ZSlayerHQ] ZombieAnimation: FIRST zombie animation initialized — " +
                    $"{ZombieDebug.BotId(bot)} ({archetype}), animSpeed={profile.BaseAnimatorSpeed:F2}");
            }
        }
        catch (System.Exception ex)
        {
            _failCount++;
            ZombieDebug.LogThrottled("anim-init-err", 10f,
                $"ZombieAnimation init error: {ex.Message}");
        }
    }

    /// <summary>
    /// Set the animation state for a zombie. Affects animator speed
    /// and bone manipulation intensity.
    /// </summary>
    public static void SetState(BotOwner bot, ZombieAnimState state)
    {
        try
        {
            var player = bot.GetPlayer;
            if (player == null) return;

            var controller = player.GetComponent<ZombieBoneController>();
            if (controller != null)
                controller.SetState(state);
        }
        catch { }
    }

    /// <summary>
    /// Clean up animation components when a zombie dies/despawns.
    /// Called from BotDeathPatch.
    /// </summary>
    public static void Cleanup(BotOwner bot)
    {
        try
        {
            var player = bot.GetPlayer;
            if (player == null) return;

            var controller = player.GetComponent<ZombieBoneController>();
            if (controller != null)
                Object.Destroy(controller);
        }
        catch { }
    }
}
