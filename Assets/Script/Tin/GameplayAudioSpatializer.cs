using UnityEngine;

/// <summary>
/// Shared spatial-audio profiles for the top-down gameplay plane. Configure
/// keeps a safe native-3D fallback for existing callers; gameplay sounds that
/// call GetAttenuatedVolume use the real XY distance instead.
/// </summary>
public static class GameplayAudioSpatializer
{
    public enum Profile { Gunshot, Footstep, Body, Melee, Voice, Zombie }

    /// <summary>
    /// Player-owned presentation cues are intentionally separate from spatial
    /// profiles. A Body profile, for example, contains both an essential death
    /// cue and a muted-on-teammates hurt cue.
    /// </summary>
    public enum PlayerCue
    {
        Gunshot,
        Death,
        Footstep,
        Consumable,
        Hurt,
        Reload,
        DryFire,
        Melee,
        HeavyBreathing
    }

    private static Transform cachedListenerTransform;

    public static bool ShouldPlayPlayerCue(PlayerCue cue, bool isOwnedAvatar)
    {
        if (isOwnedAvatar) return true;
        return cue == PlayerCue.Gunshot || cue == PlayerCue.Death;
    }

    /// <summary>
    /// Client owners predict gunshot audio once; the later authority RPC must
    /// remain audible to teammates and Host owners without echoing locally.
    /// </summary>
    public static bool ShouldPlayAuthoritativeCue(
        PlayerCue cue,
        bool isOwnedAvatar,
        bool hasStateAuthority)
    {
        if (!ShouldPlayPlayerCue(cue, isOwnedAvatar)) return false;
        return cue != PlayerCue.Gunshot || !isOwnedAvatar || hasStateAuthority;
    }

    public static void Configure(AudioSource source, Profile profile)
    {
        if (source == null) return;

        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.dopplerLevel = 0f;
        source.spread = 0f;
        source.panStereo = 0f;
        source.playOnAwake = false;

        switch (profile)
        {
            case Profile.Gunshot:
                source.minDistance = 10f;
                source.maxDistance = 48f;
                break;
            case Profile.Footstep:
                source.minDistance = 10f;
                source.maxDistance = 25f;
                break;
            case Profile.Body:
                source.minDistance = 10f;
                source.maxDistance = 22f;
                break;
            case Profile.Melee:
                source.minDistance = 10f;
                source.maxDistance = 19f;
                break;
            case Profile.Voice:
                source.minDistance = 10f;
                source.maxDistance = 30f;
                break;
            case Profile.Zombie:
                source.minDistance = 3f;
                source.maxDistance = 8f;
                break;
        }
    }

    /// <summary>
    /// Returns a volume that fades smoothly by planar distance and becomes
    /// completely silent at the profile's maxDistance. The owning player may
    /// bypass attenuation so their own actions remain clear even with camera
    /// look-ahead enabled.
    /// </summary>
    public static float GetAttenuatedVolume(
        AudioSource source,
        Profile profile,
        float unattenuatedVolume,
        bool forceFullVolume = false)
    {
        if (source == null || unattenuatedVolume <= 0f) return 0f;

        if (forceFullVolume)
        {
            // Owner feedback is intentionally centered and unattenuated.
            source.spatialBlend = 0f;
            source.panStereo = 0f;
            return unattenuatedVolume;
        }

        // Remote emitters retain Unity's 3D direction, while a flat native
        // rolloff prevents double attenuation on top of the planar policy.
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Custom;
        source.SetCustomCurve(
            AudioSourceCurveType.CustomRolloff,
            AnimationCurve.Linear(0f, 1f, 1f, 1f));

        Transform listener = GetListenerTransform();
        if (listener == null) return unattenuatedVolume;

        float planarDistance = Vector2.Distance(source.transform.position, listener.position);
        return unattenuatedVolume * GetAttenuation(profile, planarDistance);
    }

    /// <summary>
    /// Pure distance policy used by runtime audio and narrow rule tests.
    /// </summary>
    public static float GetAttenuation(Profile profile, float planarDistance)
    {
        planarDistance = Mathf.Max(0f, planarDistance);
        if (profile == Profile.Gunshot)
        {
            if (planarDistance <= 3f) return 1f;
            if (planarDistance <= 10f) return LerpSegment(planarDistance, 3f, 10f, 1f, 0.72f);
            if (planarDistance <= 20f) return LerpSegment(planarDistance, 10f, 20f, 0.72f, 0.42f);
            if (planarDistance <= 32f) return LerpSegment(planarDistance, 20f, 32f, 0.42f, 0.20f);
            if (planarDistance <= 48f) return LerpSegment(planarDistance, 32f, 48f, 0.20f, 0f);
            return 0f;
        }

        GetPlanarRange(profile, out float fullVolumeRadius, out float audibleRadius);
        if (planarDistance <= fullVolumeRadius) return 1f;
        if (planarDistance >= audibleRadius) return 0f;

        float t = Mathf.InverseLerp(fullVolumeRadius, audibleRadius, planarDistance);
        return 1f - Mathf.SmoothStep(0f, 1f, t);
    }

    private static float LerpSegment(float distance, float fromDistance, float toDistance, float fromVolume, float toVolume)
    {
        float t = Mathf.InverseLerp(fromDistance, toDistance, distance);
        return Mathf.Lerp(fromVolume, toVolume, t);
    }

    private static Transform GetListenerTransform()
    {
        if (cachedListenerTransform != null && cachedListenerTransform.gameObject.activeInHierarchy)
            return cachedListenerTransform;

        Camera mainCamera = Camera.main;
        if (mainCamera != null && mainCamera.isActiveAndEnabled)
        {
            cachedListenerTransform = mainCamera.transform;
            return cachedListenerTransform;
        }

        AudioListener listener = Object.FindFirstObjectByType<AudioListener>();
        cachedListenerTransform = listener != null ? listener.transform : null;
        return cachedListenerTransform;
    }

    private static void GetPlanarRange(Profile profile, out float fullVolumeRadius, out float audibleRadius)
    {
        switch (profile)
        {
            case Profile.Gunshot:
                fullVolumeRadius = 3f;
                audibleRadius = 48f;
                break;
            case Profile.Footstep:
                fullVolumeRadius = 1.5f;
                audibleRadius = 18f;
                break;
            case Profile.Body:
                fullVolumeRadius = 2f;
                audibleRadius = 20f;
                break;
            case Profile.Melee:
                fullVolumeRadius = 1.5f;
                audibleRadius = 15f;
                break;
            case Profile.Voice:
                fullVolumeRadius = 2f;
                audibleRadius = 28f;
                break;
            case Profile.Zombie:
                // Idle breathing/groans should stay local to each zombie;
                // a large group must not be audible across the whole screen.
                fullVolumeRadius = 1.1f;
                audibleRadius = 7f;
                break;
            default:
                fullVolumeRadius = 2f;
                audibleRadius = 20f;
                break;
        }
    }
}
