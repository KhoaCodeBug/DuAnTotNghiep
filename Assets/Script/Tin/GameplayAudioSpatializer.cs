using UnityEngine;

/// <summary>
/// Shared 2D-world spatial-audio profiles. The gameplay plane is at Z=0 while
/// the listener camera is normally near Z=-10, so positional sounds use a
/// 10-unit minimum distance to remain natural for the local player.
/// </summary>
public static class GameplayAudioSpatializer
{
    public enum Profile { Gunshot, Footstep, Body, Melee, Voice }

    public static void Configure(AudioSource source, Profile profile)
    {
        if (source == null) return;

        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.dopplerLevel = 0f;
        source.spread = 0f;
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
        }
    }
}
