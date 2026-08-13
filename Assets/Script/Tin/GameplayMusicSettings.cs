using UnityEngine;

/// <summary>Resource asset that keeps the gameplay theme reference out of scene files.</summary>
public sealed class GameplayMusicSettings : ScriptableObject
{
    [SerializeField] private AudioClip theme;
    [SerializeField, Range(0f, 1f)] private float relativeVolume = 0.28f;

    public AudioClip Theme => theme;
    public float RelativeVolume => relativeVolume;
}
