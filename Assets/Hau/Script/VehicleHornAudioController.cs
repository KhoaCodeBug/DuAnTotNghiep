using Fusion;
using UnityEngine;

/// <summary>Runtime horn presentation for every Fusion vehicle.</summary>
[DisallowMultipleComponent]
public sealed class VehicleHornAudioController : MonoBehaviour
{
    private const string AudioResourceRoot = "Intro/VehicleAudio/";
    private VehicleControllerFusion vehicle;
    private AudioClip singleClip;
    private AudioClip holdClip;
    private AudioSource singleSource;
    private AudioSource holdSource;
    private bool wasHeld;
    private bool initialized;
    private bool cinematicAlarmActive;
    private float cinematicAlarmVolumeScale = 1f;
    private float nextNoiseMeterPulseAt;

    [SerializeField, Range(0f, 1f)] private float hornVolume = 0.92f;

    public static VehicleHornAudioController Attach(VehicleControllerFusion owner)
    {
        if (owner == null) return null;
        VehicleHornAudioController controller = owner.GetComponent<VehicleHornAudioController>();
        if (controller == null) controller = owner.gameObject.AddComponent<VehicleHornAudioController>();
        controller.Initialize(owner);
        return controller;
    }

    private void Awake()
    {
        Initialize(GetComponent<VehicleControllerFusion>());
    }

    private void Initialize(VehicleControllerFusion owner)
    {
        if (initialized) return;
        vehicle = owner;
        singleClip = Resources.Load<AudioClip>(AudioResourceRoot + "CarHornSingle");
        holdClip = Resources.Load<AudioClip>(AudioResourceRoot + "CarHornHold");
        singleSource = CreateSource("Vehicle Horn Single Audio");
        holdSource = CreateSource("Vehicle Horn Hold Audio");
        initialized = true;
    }

    private AudioSource CreateSource(string sourceName)
    {
        GameObject sourceObject = new GameObject(sourceName);
        sourceObject.transform.SetParent(transform, false);
        AudioSource source = sourceObject.AddComponent<AudioSource>();
        GameplayAudioSpatializer.Configure(source, GameplayAudioSpatializer.Profile.Body);
        source.playOnAwake = false;
        source.loop = false;
        source.volume = EffectiveVolume;
        return source;
    }

    private float EffectiveVolume => hornVolume *
        Mathf.Clamp01(PlayerPrefs.GetFloat("GameSFXVolume", 0.8f));

    private void Update()
    {
        if (!initialized || vehicle == null) return;
        if (!vehicle.IsNetworkSpawned)
        {
            StopImmediately();
            return;
        }

        bool held = vehicle.IsHornHeld;
        if (held != wasHeld)
        {
            if (held) StartHold();
            else StopHold();
            wasHeld = held;
        }

        if (held && vehicle.HasLocalDriver && Time.unscaledTime >= nextNoiseMeterPulseAt)
        {
            AutoNoiseMeter.ReportTransientNoise(1f, "CÒI XE");
            nextNoiseMeterPulseAt = Time.unscaledTime + 0.22f;
        }
    }

    public void PlaySingle(NetworkObject sourcePlayer)
    {
        if (!initialized) Initialize(GetComponent<VehicleControllerFusion>());
        if (singleSource != null && singleClip != null)
        {
            singleSource.Stop();
            singleSource.clip = singleClip;
            singleSource.loop = false;
            singleSource.volume = EffectiveVolume;
            singleSource.Play();
        }
        if (sourcePlayer != null && sourcePlayer.HasInputAuthority)
            AutoNoiseMeter.ReportTransientNoise(1f, "CÒI XE");
    }

    public void SetCinematicAlarm(bool active)
    {
        if (!initialized) Initialize(GetComponent<VehicleControllerFusion>());
        cinematicAlarmActive = active;
        cinematicAlarmVolumeScale = 1f;
        if (active) StartHold();
        else StopHold();
    }

    public void SetCinematicAlarmBackground()
    {
        if (!initialized) Initialize(GetComponent<VehicleControllerFusion>());
        cinematicAlarmActive = true;
        cinematicAlarmVolumeScale = 0.2f;
        if (holdSource == null || holdClip == null) return;
        holdSource.volume = EffectiveVolume * cinematicAlarmVolumeScale;
        if (!holdSource.isPlaying) StartHold();
    }

    private void StartHold()
    {
        if (holdSource == null || holdClip == null) return;
        holdSource.Stop();
        holdSource.clip = holdClip;
        holdSource.loop = true;
        holdSource.volume = EffectiveVolume * (cinematicAlarmActive ? cinematicAlarmVolumeScale : 1f);
        holdSource.Play();
        nextNoiseMeterPulseAt = 0f;
    }

    private void StopHold()
    {
        if (holdSource != null) holdSource.Stop();
        cinematicAlarmActive = false;
        cinematicAlarmVolumeScale = 1f;
        nextNoiseMeterPulseAt = 0f;
    }

    private void StopImmediately()
    {
        if (singleSource != null) singleSource.Stop();
        StopHold();
        wasHeld = false;
    }

    public void NotifyNetworkDespawned() => StopImmediately();

    private void OnDisable() => StopImmediately();
}
