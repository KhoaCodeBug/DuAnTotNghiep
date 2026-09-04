using System;
using UnityEngine;

/// <summary>
/// Centralized runtime source of truth for audio volume settings.
/// Bridges PlayerPrefs persistence with real-time UI preview and cross-menu synchronization.
/// </summary>
public static class GameAudioSettings
{
    public const string MasterKey = "GameMasterVolume";
    public const string MusicKey = "GameMusicVolume";
    public const string SFXKey = "GameSFXVolume";

    public const float DefaultMaster = 1.0f;
    public const float DefaultMusic = 0.5f;
    public const float DefaultSFX = 0.8f;

    private static bool _initialized = false;
    private static float _masterVolume = DefaultMaster;
    private static float _musicVolume = DefaultMusic;
    private static float _sfxVolume = DefaultSFX;

    public static event Action<float, float, float> OnSettingsChanged;

    public static float MasterVolume
    {
        get
        {
            EnsureInitialized();
            return _masterVolume;
        }
    }

    public static float MusicVolume
    {
        get
        {
            EnsureInitialized();
            return _musicVolume;
        }
    }

    public static float SFXVolume
    {
        get
        {
            EnsureInitialized();
            return _sfxVolume;
        }
    }

    public static float EffectiveMusicVolume => MasterVolume * MusicVolume;
    public static float EffectiveSFXVolume => MasterVolume * SFXVolume;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Initialize()
    {
        _masterVolume = PlayerPrefs.GetFloat(MasterKey, DefaultMaster);
        _musicVolume = PlayerPrefs.GetFloat(MusicKey, DefaultMusic);
        _sfxVolume = PlayerPrefs.GetFloat(SFXKey, DefaultSFX);
        _initialized = true;

        ApplyToAudioListener();
        NotifyChanged();
    }

    private static void EnsureInitialized()
    {
        if (!_initialized)
        {
            Initialize();
        }
    }

    public static void SetMasterVolume(float value)
    {
        EnsureInitialized();
        _masterVolume = Mathf.Clamp01(value);
        ApplyToAudioListener();
        NotifyChanged();
    }

    public static void SetMusicVolume(float value)
    {
        EnsureInitialized();
        _musicVolume = Mathf.Clamp01(value);
        NotifyChanged();
    }

    public static void SetSFXVolume(float value)
    {
        EnsureInitialized();
        _sfxVolume = Mathf.Clamp01(value);
        NotifyChanged();
    }

    public static void SetPreview(float master, float music, float sfx)
    {
        EnsureInitialized();
        _masterVolume = Mathf.Clamp01(master);
        _musicVolume = Mathf.Clamp01(music);
        _sfxVolume = Mathf.Clamp01(sfx);
        ApplyToAudioListener();
        NotifyChanged();
    }

    public static void Save()
    {
        EnsureInitialized();
        PlayerPrefs.SetFloat(MasterKey, _masterVolume);
        PlayerPrefs.SetFloat(MusicKey, _musicVolume);
        PlayerPrefs.SetFloat(SFXKey, _sfxVolume);
        PlayerPrefs.Save();
    }

    public static void Revert()
    {
        _masterVolume = PlayerPrefs.GetFloat(MasterKey, DefaultMaster);
        _musicVolume = PlayerPrefs.GetFloat(MusicKey, DefaultMusic);
        _sfxVolume = PlayerPrefs.GetFloat(SFXKey, DefaultSFX);
        _initialized = true;
        ApplyToAudioListener();
        NotifyChanged();
    }

    public static void ResetToDefaults()
    {
        _masterVolume = DefaultMaster;
        _musicVolume = DefaultMusic;
        _sfxVolume = DefaultSFX;
        _initialized = true;
        ApplyToAudioListener();
        NotifyChanged();
    }

    public static bool HasUnsavedChanges()
    {
        EnsureInitialized();
        float savedMaster = PlayerPrefs.GetFloat(MasterKey, DefaultMaster);
        float savedMusic = PlayerPrefs.GetFloat(MusicKey, DefaultMusic);
        float savedSFX = PlayerPrefs.GetFloat(SFXKey, DefaultSFX);

        return Mathf.Abs(_masterVolume - savedMaster) > 0.001f ||
               Mathf.Abs(_musicVolume - savedMusic) > 0.001f ||
               Mathf.Abs(_sfxVolume - savedSFX) > 0.001f;
    }

    private static void ApplyToAudioListener()
    {
        AudioListener.volume = _masterVolume;
    }

    private static void NotifyChanged()
    {
        OnSettingsChanged?.Invoke(_masterVolume, _musicVolume, _sfxVolume);
    }
}
