using System.Collections.Generic;
using ZSlayerZombieClient.Archetypes;

namespace ZSlayerZombieClient.Animation;

/// <summary>
/// Per-archetype animation parameters: animator speed, bone offsets, sway.
/// All angles in degrees. Bone offsets are additive (applied on top of
/// whatever the animation system produces each frame).
/// </summary>
public class ArchetypeAnimationProfile
{
    // Animator speed multipliers
    public float BaseAnimatorSpeed { get; set; }
    public float RushAnimatorSpeed { get; set; }
    public float StumbleAnimatorSpeed { get; set; }
    public float LungeAnimatorSpeed { get; set; }

    // Spine (forward lean = positive X rotation)
    public float SpineLean { get; set; }
    public float SpineLeanRush { get; set; }

    // Head (nod = X rotation, tilt = Z rotation)
    public float HeadNod { get; set; }
    public float HeadTilt { get; set; }

    // Arms (forward reach = X, outward spread = Z)
    public float ArmForward { get; set; }
    public float ArmSpread { get; set; }

    // Sway oscillation (applied to spine + head)
    public float SwayAmount { get; set; }
    public float SwaySpeed { get; set; }

    private static readonly Dictionary<ZombieArchetype, ArchetypeAnimationProfile> Profiles = new()
    {
        [ZombieArchetype.Shambler] = new ArchetypeAnimationProfile
        {
            BaseAnimatorSpeed = 0.7f,
            RushAnimatorSpeed = 1.0f,
            StumbleAnimatorSpeed = 0.3f,
            LungeAnimatorSpeed = 1.1f,
            SpineLean = 3f,
            SpineLeanRush = 8f,
            HeadNod = 0f,
            HeadTilt = 0f,
            ArmForward = 15f,
            ArmSpread = 5f,
            SwayAmount = 6f,
            SwaySpeed = 1.5f,
        },

        [ZombieArchetype.Runner] = new ArchetypeAnimationProfile
        {
            BaseAnimatorSpeed = 1.0f,
            RushAnimatorSpeed = 1.2f,
            StumbleAnimatorSpeed = 0.4f,
            LungeAnimatorSpeed = 1.3f,
            SpineLean = 10f,
            SpineLeanRush = 18f,
            HeadNod = 8f,
            HeadTilt = 0f,
            ArmForward = -10f,
            ArmSpread = 8f,
            SwayAmount = 2f,
            SwaySpeed = 3f,
        },

        [ZombieArchetype.Crawler] = new ArchetypeAnimationProfile
        {
            BaseAnimatorSpeed = 0.85f,
            RushAnimatorSpeed = 1.0f,
            StumbleAnimatorSpeed = 0.3f,
            LungeAnimatorSpeed = 1.2f,
            SpineLean = 25f,
            SpineLeanRush = 15f,
            HeadNod = -15f,
            HeadTilt = 0f,
            ArmForward = 20f,
            ArmSpread = 10f,
            SwayAmount = 3f,
            SwaySpeed = 2f,
        },

        [ZombieArchetype.Stalker] = new ArchetypeAnimationProfile
        {
            BaseAnimatorSpeed = 0.85f,
            RushAnimatorSpeed = 1.1f,
            StumbleAnimatorSpeed = 0.3f,
            LungeAnimatorSpeed = 1.2f,
            SpineLean = 8f,
            SpineLeanRush = 15f,
            HeadNod = 3f,
            HeadTilt = 12f,
            ArmForward = 5f,
            ArmSpread = -5f,
            SwayAmount = 1.5f,
            SwaySpeed = 1.2f,
        },

        [ZombieArchetype.Berserker] = new ArchetypeAnimationProfile
        {
            BaseAnimatorSpeed = 1.3f,
            RushAnimatorSpeed = 1.4f,
            StumbleAnimatorSpeed = 0.5f,
            LungeAnimatorSpeed = 1.4f,
            SpineLean = 10f,
            SpineLeanRush = 15f,
            HeadNod = -8f,
            HeadTilt = 0f,
            ArmForward = -5f,
            ArmSpread = 25f,
            SwayAmount = 4f,
            SwaySpeed = 6f,
        },

        [ZombieArchetype.Wraith] = new ArchetypeAnimationProfile
        {
            BaseAnimatorSpeed = 0.9f,
            RushAnimatorSpeed = 1.1f,
            StumbleAnimatorSpeed = 0.0f, // Freeze when spotted
            LungeAnimatorSpeed = 1.0f,
            SpineLean = 5f,
            SpineLeanRush = 3f,
            HeadNod = 5f,
            HeadTilt = 15f,
            ArmForward = 8f,
            ArmSpread = -8f,
            SwayAmount = 1f,
            SwaySpeed = 0.8f,
        },
    };

    public static ArchetypeAnimationProfile Get(ZombieArchetype archetype)
    {
        return Profiles.GetValueOrDefault(archetype, Profiles[ZombieArchetype.Shambler]);
    }
}
