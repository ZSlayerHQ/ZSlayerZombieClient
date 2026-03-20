using UnityEngine;
using ZSlayerZombieClient.Core;

namespace ZSlayerZombieClient.Animation;

/// <summary>
/// MonoBehaviour attached to each zombie's Player GameObject.
/// Runs in LateUpdate (after animation system) to apply additive bone
/// rotations that give each archetype a distinct visual silhouette.
///
/// All rotations are ADDITIVE — they modify the current animation pose,
/// not replace it. This means existing walk/run/combat animations still
/// play normally, but with archetype-specific character layered on top.
///
/// Performance: ~6 cached quaternion multiplications per frame per zombie.
/// At 50 zombies that's 300 quaternion ops — well under 0.1ms total.
/// </summary>
public class ZombieBoneController : MonoBehaviour
{
    private Animator _animator;
    private ArchetypeAnimationProfile _profile;

    // Cached bone transforms (found once at init, reused every frame)
    private Transform _spine;
    private Transform _chest;
    private Transform _head;
    private Transform _leftUpperArm;
    private Transform _rightUpperArm;

    // Per-zombie sway phase offset (so zombies don't sway in unison)
    private float _swayPhase;

    // Current animation state (set by logic classes)
    private ZombieAnimState _currentState = ZombieAnimState.Normal;

    // Initialization flag
    private bool _initialized;

    // One-shot logging
    private static bool _loggedFirstInit;
    private static bool _loggedFirstLateUpdate;
    private static int _initCount;

    /// <summary>
    /// Called by ZombieAnimationController after AddComponent.
    /// Caches bone transforms from the animator's humanoid rig.
    /// </summary>
    public void Initialize(Animator animator, ArchetypeAnimationProfile profile, string botId)
    {
        _animator = animator;
        _profile = profile;
        _swayPhase = Random.Range(0f, Mathf.PI * 2f);

        // Cache bone transforms from the humanoid avatar
        _spine = TryGetBone(animator, HumanBodyBones.Spine);
        _chest = TryGetBone(animator, HumanBodyBones.Chest);
        _head = TryGetBone(animator, HumanBodyBones.Head);
        _leftUpperArm = TryGetBone(animator, HumanBodyBones.LeftUpperArm);
        _rightUpperArm = TryGetBone(animator, HumanBodyBones.RightUpperArm);

        _initialized = true;
        _initCount++;

        int boneCount = (_spine != null ? 1 : 0) + (_chest != null ? 1 : 0) +
                        (_head != null ? 1 : 0) + (_leftUpperArm != null ? 1 : 0) +
                        (_rightUpperArm != null ? 1 : 0);

        if (!_loggedFirstInit)
        {
            _loggedFirstInit = true;
            Plugin.Log.LogWarning($"[ZSlayerHQ] ZombieBoneController: FIRST init for {botId} — " +
                $"bones found: {boneCount}/5 (spine={_spine != null}, chest={_chest != null}, " +
                $"head={_head != null}, Larm={_leftUpperArm != null}, Rarm={_rightUpperArm != null}), " +
                $"animSpeed={_profile.BaseAnimatorSpeed:F2}");
        }

        ZombieDebug.LogThrottled("bone-init", 30f,
            $"ZombieBoneController: {_initCount} zombies initialized with bone control");
    }

    /// <summary>
    /// Set the current animation state. Called by logic classes on state transitions.
    /// </summary>
    public void SetState(ZombieAnimState state)
    {
        _currentState = state;
    }

    private void LateUpdate()
    {
        if (!_initialized || _animator == null || _profile == null) return;

        float time = Time.time;

        // --- Animator Speed ---
        float targetSpeed = _currentState switch
        {
            ZombieAnimState.Rushing => _profile.RushAnimatorSpeed,
            ZombieAnimState.Stumbling => _profile.StumbleAnimatorSpeed,
            ZombieAnimState.Frozen => 0f,
            ZombieAnimState.Lunging => _profile.LungeAnimatorSpeed,
            _ => _profile.BaseAnimatorSpeed,
        };
        _animator.speed = targetSpeed;

        // --- Sway Oscillation ---
        float sway = Mathf.Sin(time * _profile.SwaySpeed + _swayPhase) * _profile.SwayAmount;
        float headSway = Mathf.Sin(time * _profile.SwaySpeed * 1.3f + _swayPhase) * _profile.SwayAmount * 0.7f;

        // --- Spine Lean ---
        float spineLean = _currentState == ZombieAnimState.Rushing || _currentState == ZombieAnimState.Lunging
            ? _profile.SpineLeanRush
            : _profile.SpineLean;

        if (_spine != null)
        {
            _spine.localRotation *= Quaternion.Euler(spineLean * 0.5f, 0f, sway * 0.6f);
        }

        if (_chest != null)
        {
            _chest.localRotation *= Quaternion.Euler(spineLean * 0.5f, 0f, sway * 0.4f);
        }

        // --- Head ---
        if (_head != null)
        {
            _head.localRotation *= Quaternion.Euler(_profile.HeadNod, 0f, _profile.HeadTilt + headSway);
        }

        // --- Arms ---
        float armForward = _profile.ArmForward;
        float armSpread = _profile.ArmSpread;

        // During rush/lunge: arms back for runners, forward for others
        if (_currentState == ZombieAnimState.Rushing || _currentState == ZombieAnimState.Lunging)
        {
            armForward *= 1.3f;
        }

        if (_leftUpperArm != null)
        {
            _leftUpperArm.localRotation *= Quaternion.Euler(armForward, 0f, -armSpread);
        }

        if (_rightUpperArm != null)
        {
            _rightUpperArm.localRotation *= Quaternion.Euler(armForward, 0f, armSpread);
        }

        if (!_loggedFirstLateUpdate)
        {
            _loggedFirstLateUpdate = true;
            Plugin.Log.LogWarning($"[ZSlayerHQ] ZombieBoneController: FIRST LateUpdate executed — " +
                $"animSpeed={targetSpeed:F2}, spineLean={spineLean:F1}, sway={sway:F2}");
        }
    }

    /// <summary>
    /// Safely get a bone transform, returning null if not found.
    /// EFT's zombie models may not have all standard humanoid bones.
    /// </summary>
    private static Transform TryGetBone(Animator animator, HumanBodyBones bone)
    {
        try
        {
            return animator.GetBoneTransform(bone);
        }
        catch
        {
            return null;
        }
    }

    private void OnDestroy()
    {
        _initialized = false;
        _animator = null;
        _profile = null;
    }
}

/// <summary>
/// Animation states that logic classes can signal to the bone controller.
/// These affect animator speed and bone offset intensity.
/// </summary>
public enum ZombieAnimState
{
    /// <summary>Default archetype behavior.</summary>
    Normal,

    /// <summary>Horde rush or close-range charge — faster speed, more forward lean.</summary>
    Rushing,

    /// <summary>Shambler stumble or pause — very slow, exaggerated sway.</summary>
    Stumbling,

    /// <summary>Wraith spotted or stalker peek — complete freeze.</summary>
    Frozen,

    /// <summary>Close-range burst toward target — fast, aggressive lean.</summary>
    Lunging,
}
