using UnityEngine;

/// <summary>
/// Shared spatial-audio profiles for the top-down gameplay plane. Configure
/// keeps a safe native-3D fallback for existing callers; gameplay sounds that
/// call GetAttenuatedVolume use the real XY distance instead.
/// </summary>
public static class GameplayAudioSpatializer
{
    public enum Profile { Gunshot, Footstep, Body, Melee, Voice, Zombie }

    private static Transform cachedListenerTransform;

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

        // The caller is opting into planar attenuation, so disable Unity's 3D
        // distance curve to avoid attenuating the same sound twice.
        source.spatialBlend = 0f;
        source.panStereo = 0f;
        if (forceFullVolume) return unattenuatedVolume;

        Transform listener = GetListenerTransform();
        if (listener == null) return unattenuatedVolume;

        GetPlanarRange(profile, out float fullVolumeRadius, out float audibleRadius);
        float planarDistance = Vector2.Distance(source.transform.position, listener.position);

        if (planarDistance <= fullVolumeRadius) return unattenuatedVolume;
        if (planarDistance >= audibleRadius) return 0f;

        float t = Mathf.InverseLerp(fullVolumeRadius, audibleRadius, planarDistance);
        float attenuation = 1f - Mathf.SmoothStep(0f, 1f, t);
        return unattenuatedVolume * attenuation;
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
                audibleRadius = 42f;
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
