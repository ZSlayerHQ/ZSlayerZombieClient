namespace ZSlayerZombieClient.Core;

/// <summary>
/// Shared utilities for zombie logic classes.
/// Performance-critical helpers that reduce CPU load with many zombies.
/// </summary>
public static class ZombieHelper
{
    /// <summary>
    /// Returns an appropriate path update interval based on distance to target.
    /// Distant zombies update their NavMesh path much less frequently,
    /// significantly reducing CPU load when many zombies are active.
    ///
    /// Close (&lt;20m):  0.4s — tight tracking, feels responsive
    /// Medium (20-50m): 0.8s — still approaches, less CPU
    /// Far (50-80m):    1.5s — coarse updates, player won't notice
    /// Very far (&gt;80m): 3.0s — minimal updates, just shuffling toward target
    /// </summary>
    public static float GetPathInterval(float distance)
    {
        if (distance < 20f) return 0.4f;
        if (distance < 50f) return 0.8f;
        if (distance < 80f) return 1.5f;
        return 3f;
    }

    /// <summary>
    /// Applies a zombie's per-instance speed multiplier to a base speed.
    /// Clamps result to 0-1 range (EFT speed bounds).
    /// </summary>
    public static float ApplySpeedVariance(float baseSpeed, float multiplier)
    {
        float result = baseSpeed * multiplier;
        if (result > 1f) return 1f;
        if (result < 0.05f) return 0.05f;
        return result;
    }

    /// <summary>
    /// Returns an appropriate vocalization frequency multiplier based on distance.
    /// Distant zombies vocalize less often — saves audio processing and
    /// the player can't hear them anyway.
    /// </summary>
    public static float GetVocalizationMultiplier(float distance)
    {
        if (distance < 30f) return 1f;
        if (distance < 60f) return 0.3f;
        return 0.05f; // Almost never vocalize when very far
    }
}
