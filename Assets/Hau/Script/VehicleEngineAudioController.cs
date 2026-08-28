using Fusion;
using UnityEngine;

/// <summary>
/// Runtime-only engine presentation for <see cref="VehicleControllerFusion"/>.
/// It deliberately owns separate sources for idle, transition and driving audio
/// so clips can overlap during state changes instead of producing audible gaps.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(VehicleControllerFusion), typeof(Rigidbody2D))]
public sealed class VehicleEngineAudioController : MonoBehaviour
{
    public enum PlaybackState
    {
        Off,
        Starting,
        Idle,
        AccelerationIntro,
        AccelerationFollowThrough,
        Driving
    }

    private const string AudioResourceRoot = "Intro/VehicleAudio/";
    private const float DefaultCrossfadeSeconds = 0.18f;
    private const float DefaultLoopCrossfadeSeconds = 0.32f;
    private const float ScheduledLeadSeconds = 0.05f;

    private VehicleControllerFusion vehicle;
    private Rigidbody2D body;

    private AudioClip starterClip;
    private AudioClip idleLoopClip;
    private AudioClip accelerationIntroClip;
    private AudioClip accelerationFollowClip;
    private AudioClip drivingLoopClip;

    private AudioSource transitionSource;
    private AudioSource starterConfirmationSource;
    private AudioSource accelerationFollowSource;
    private AudioSource idleSource;
    private AudioSource drivingSourceA;
    private AudioSource drivingSourceB;

    private PlaybackState state;
    private double transitionEndsAt;
    private double loopFadeStartsAt;
    private double accelerationFollowEndsAt;
    private double cruiseFadeStartsAt;
    private float transitionGain;
    private float accelerationFollowGain;
    private float idleGain;
    private float drivingGain;
    private float transitionTarget;
    private float accelerationFollowTarget;
    private float idleTarget;
    private float drivingTarget;
    private bool drivingLoopScheduled;
    private bool drivingAIsCurrent;
    private double drivingCurrentEndsAt;
    private double drivingNextStartsAt;
    private float drivingLoopAGain;
    private float drivingLoopBGain;
    private bool wasEngineRunning;
    private bool initialized;
    private float nextNoiseMeterPulseAt;

    [SerializeField, Min(0.01f)] private float movementEnterSpeed = 0.22f;
    [SerializeField, Min(0.01f)] private float movementExitSpeed = 0.09f;
    [SerializeField, Min(0.02f)] private float crossfadeSeconds = DefaultCrossfadeSeconds;
    [SerializeField, Min(0.02f)] private float loopCrossfadeSeconds = DefaultLoopCrossfadeSeconds;
    [SerializeField, Range(0f, 1f)] private float engineVolume = 0.78f;

    public PlaybackState State => state;
    public float StarterDurationSeconds => starterClip != null
        ? Mathf.Max(0.05f, starterClip.length)
        : 0.05f;

    public static VehicleEngineAudioController Attach(VehicleControllerFusion owner)
    {
        if (owner == null) return null;
        VehicleEngineAudioController controller = owner.GetComponent<VehicleEngineAudioController>();
        if (controller == null) controller = owner.gameObject.AddComponent<VehicleEngineAudioController>();
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
        body = GetComponent<Rigidbody2D>();
        crossfadeSeconds = Mathf.Max(0.02f, crossfadeSeconds);
        loopCrossfadeSeconds = Mathf.Max(0.02f, loopCrossfadeSeconds);
        movementExitSpeed = Mathf.Min(movementExitSpeed, movementEnterSpeed);

        starterClip = Resources.Load<AudioClip>(AudioResourceRoot + "CarStart");
        idleLoopClip = Resources.Load<AudioClip>(AudioResourceRoot + "CarStart2");
        accelerationIntroClip = Resources.Load<AudioClip>(AudioResourceRoot + "CarAcce1");
        accelerationFollowClip = Resources.Load<AudioClip>(AudioResourceRoot + "CarAcce");
        drivingLoopClip = Resources.Load<AudioClip>(AudioResourceRoot + "CarAcce2");
        if (drivingLoopClip == null)
        {
            // Backward-compatible fallback while CarAcce2 is being authored.
            drivingLoopClip = accelerationFollowClip;
            accelerationFollowClip = null;
        }

        transitionSource = CreateSource("Engine Transition Audio");
        starterConfirmationSource = CreateSource("Engine Starter Confirmation Audio");
        accelerationFollowSource = CreateSource("Engine Acceleration Follow Audio");
        idleSource = CreateSource("Engine Idle Audio");
        drivingSourceA = CreateSource("Engine Driving Audio A");
        drivingSourceB = CreateSource("Engine Driving Audio B");
        state = PlaybackState.Off;
        initialized = true;
    }

    /// <summary>
    /// Repair-completion feedback only. This never starts the idle loop or
    /// changes the networked engine state; entering the driver seat owns the
    /// complete startup-to-driving flow.
    /// </summary>
    public bool PlayStarterConfirmation(NetworkObject sourcePlayer)
    {
        if (!initialized) Initialize(GetComponent<VehicleControllerFusion>());
        if (starterConfirmationSource == null || starterClip == null) return false;
        starterConfirmationSource.Stop();
        starterConfirmationSource.clip = starterClip;
        starterConfirmationSource.loop = false;
        starterConfirmationSource.volume = engineVolume *
            Mathf.Clamp01(PlayerPrefs.GetFloat("GameSFXVolume", 0.8f));
        starterConfirmationSource.Play();
        if (sourcePlayer != null && sourcePlayer.HasInputAuthority)
            AutoNoiseMeter.ReportTransientNoise(0.78f, "ĐỀ MÁY XE");
        return true;
    }

    private AudioSource CreateSource(string sourceName)
    {
        GameObject sourceObject = new GameObject(sourceName);
        sourceObject.transform.SetParent(transform, false);
        AudioSource source = sourceObject.AddComponent<AudioSource>();
        GameplayAudioSpatializer.Configure(source, GameplayAudioSpatializer.Profile.Body);
        source.playOnAwake = false;
        source.loop = false;
        source.volume = 0f;
        return source;
    }

    private void Update()
    {
        if (!initialized || vehicle == null) return;

        // Fusion throws when a generated [Networked] getter is read before
        // NetworkBehaviour.Spawned(). Runtime-added presentation components can
        // receive Update earlier than that callback, especially for scene cars.
        if (!vehicle.IsNetworkSpawned)
        {
            if (wasEngineRunning || state != PlaybackState.Off) StopImmediately();
            wasEngineRunning = false;
            return;
        }

        bool engineRunning = vehicle.IsEngineRunning;
        if (!engineRunning)
        {
            if (wasEngineRunning || state != PlaybackState.Off) StopImmediately();
            wasEngineRunning = false;
            return;
        }

        if (!wasEngineRunning)
        {
            BeginStartup();
            wasEngineRunning = true;
        }

        double dspTime = AudioSettings.dspTime;
        float speed = body != null ? body.linearVelocity.magnitude : 0f;
        UpdateDrivingLoop(dspTime);

        switch (state)
        {
            case PlaybackState.Starting:
                if (dspTime >= loopFadeStartsAt)
                    SetTargets(0f, 0f, 1f, 0f);
                if (dspTime >= transitionEndsAt)
                {
                    transitionSource.Stop();
                    state = PlaybackState.Idle;
                    if (speed >= movementEnterSpeed) BeginAcceleration();
                }
                break;

            case PlaybackState.Idle:
                if (speed >= movementEnterSpeed) BeginAcceleration();
                break;

            case PlaybackState.AccelerationIntro:
                if (speed <= movementExitSpeed)
                {
                    ReturnToIdle();
                }
                else
                {
                    if (dspTime >= loopFadeStartsAt)
                        SetTargets(0f, accelerationFollowClip != null ? 1f : 0f, 0f,
                            accelerationFollowClip == null ? 1f : 0f);
                    if (dspTime >= transitionEndsAt)
                    {
                        transitionSource.Stop();
                        state = accelerationFollowClip != null
                            ? PlaybackState.AccelerationFollowThrough
                            : PlaybackState.Driving;
                    }
                }
                break;

            case PlaybackState.AccelerationFollowThrough:
                if (speed <= movementExitSpeed)
                {
                    ReturnToIdle();
                }
                else
                {
                    if (dspTime >= cruiseFadeStartsAt)
                        SetTargets(0f, 0f, 0f, 1f);
                    if (dspTime >= accelerationFollowEndsAt)
                    {
                        accelerationFollowSource.Stop();
                        state = PlaybackState.Driving;
                    }
                }
                break;

            case PlaybackState.Driving:
                if (speed <= movementExitSpeed) ReturnToIdle();
                break;
        }

        ApplyCrossfade();
        UpdateLocalNoiseMeter();
    }

    private void UpdateLocalNoiseMeter()
    {
        if (!vehicle.HasLocalOccupant || Time.unscaledTime < nextNoiseMeterPulseAt) return;
        float intensity;
        string label;
        switch (state)
        {
            case PlaybackState.Starting:
                intensity = 0.78f;
                label = "ĐỀ MÁY XE";
                break;
            case PlaybackState.AccelerationIntro:
            case PlaybackState.AccelerationFollowThrough:
                intensity = 0.84f;
                label = "XE TĂNG TỐC";
                break;
            case PlaybackState.Driving:
                intensity = 0.68f;
                label = "ĐỘNG CƠ XE";
                break;
            default:
                intensity = 0.42f;
                label = "ĐỘNG CƠ XE";
                break;
        }
        AutoNoiseMeter.ReportTransientNoise(intensity, label);
        nextNoiseMeterPulseAt = Time.unscaledTime + 0.22f;
    }

    private void BeginStartup()
    {
        StopImmediately();
        if (starterClip == null)
        {
            StartIdleImmediately();
            return;
        }

        double startsAt = AudioSettings.dspTime + ScheduledLeadSeconds;
        float overlap = GetOverlap(starterClip);
        transitionEndsAt = startsAt + starterClip.length;
        loopFadeStartsAt = transitionEndsAt - overlap;

        transitionSource.clip = starterClip;
        transitionSource.loop = false;
        transitionGain = 1f;
        transitionTarget = 1f;
        transitionSource.PlayScheduled(startsAt);

        if (idleLoopClip != null)
        {
            idleSource.clip = idleLoopClip;
            idleSource.loop = true;
            idleGain = 0f;
            idleTarget = 0f;
            idleSource.PlayScheduled(loopFadeStartsAt);
        }

        state = PlaybackState.Starting;
        ApplySourceVolumes();
    }

    private void StartIdleImmediately()
    {
        StopImmediately();
        if (idleLoopClip != null)
        {
            idleSource.clip = idleLoopClip;
            idleSource.loop = true;
            idleSource.Play();
            idleGain = 1f;
            idleTarget = 1f;
        }
        state = PlaybackState.Idle;
        ApplySourceVolumes();
    }

    private void BeginAcceleration()
    {
        if (accelerationIntroClip == null)
        {
            StartDrivingLoop();
            return;
        }

        transitionSource.Stop();
        accelerationFollowSource.Stop();
        StopDrivingLoopImmediately();
        double startsAt = AudioSettings.dspTime + ScheduledLeadSeconds;
        float overlap = GetOverlap(accelerationIntroClip);
        transitionEndsAt = startsAt + accelerationIntroClip.length;
        loopFadeStartsAt = transitionEndsAt - overlap;

        transitionSource.clip = accelerationIntroClip;
        transitionSource.loop = false;
        transitionGain = 0f;
        transitionTarget = 1f;
        transitionSource.PlayScheduled(startsAt);

        double drivingStartsAt = loopFadeStartsAt;
        if (accelerationFollowClip != null)
        {
            accelerationFollowSource.clip = accelerationFollowClip;
            accelerationFollowSource.loop = false;
            accelerationFollowGain = 0f;
            accelerationFollowTarget = 0f;
            accelerationFollowSource.PlayScheduled(loopFadeStartsAt);
            accelerationFollowEndsAt = loopFadeStartsAt + accelerationFollowClip.length;
            cruiseFadeStartsAt = accelerationFollowEndsAt - GetOverlap(accelerationFollowClip);
            drivingStartsAt = cruiseFadeStartsAt;
        }

        if (drivingLoopClip != null)
        {
            drivingGain = 0f;
            drivingTarget = 0f;
            ScheduleDrivingLoop(drivingStartsAt);
        }

        idleTarget = 0f;
        state = PlaybackState.AccelerationIntro;
    }

    private void StartDrivingLoop()
    {
        if (drivingLoopClip == null)
        {
            state = PlaybackState.Idle;
            return;
        }

        StopDrivingLoopImmediately();
        ScheduleDrivingLoop(AudioSettings.dspTime + ScheduledLeadSeconds);
        SetTargets(0f, 0f, 0f, 1f);
        state = PlaybackState.Driving;
    }

    private void ScheduleDrivingLoop(double startsAt)
    {
        if (drivingLoopClip == null) return;

        StopDrivingLoopImmediately();
        drivingLoopScheduled = true;
        drivingAIsCurrent = true;
        drivingLoopAGain = 1f;
        drivingLoopBGain = 0f;

        ScheduleDrivingSource(drivingSourceA, startsAt);
        drivingCurrentEndsAt = startsAt + drivingLoopClip.length;
        drivingNextStartsAt = drivingCurrentEndsAt - GetLoopOverlap();
        ScheduleDrivingSource(drivingSourceB, drivingNextStartsAt);
    }

    private void ScheduleDrivingSource(AudioSource source, double startsAt)
    {
        if (source == null || drivingLoopClip == null) return;
        source.Stop();
        source.clip = drivingLoopClip;
        source.loop = false;
        source.timeSamples = 0;
        source.PlayScheduled(startsAt);
    }

    private void UpdateDrivingLoop(double dspTime)
    {
        if (!drivingLoopScheduled || drivingLoopClip == null) return;

        // Each pass overlaps the end of one copy with the beginning of the
        // other. This masks MP3 encoder padding and mismatched waveform edges,
        // which AudioSource.loop alone exposes as a short hitch/click.
        int safety = 0;
        while (dspTime >= drivingCurrentEndsAt && safety++ < 4)
        {
            AudioSource finished = drivingAIsCurrent ? drivingSourceA : drivingSourceB;
            finished.Stop();
            drivingAIsCurrent = !drivingAIsCurrent;
            drivingCurrentEndsAt = drivingNextStartsAt + drivingLoopClip.length;
            drivingNextStartsAt = drivingCurrentEndsAt - GetLoopOverlap();
            ScheduleDrivingSource(finished, drivingNextStartsAt);
        }

        float nextGain = dspTime <= drivingNextStartsAt
            ? 0f
            : Mathf.Clamp01((float)((dspTime - drivingNextStartsAt) /
                                    Mathf.Max(0.02f, GetLoopOverlap())));
        float currentGain = 1f - nextGain;
        if (drivingAIsCurrent)
        {
            drivingLoopAGain = currentGain;
            drivingLoopBGain = nextGain;
        }
        else
        {
            drivingLoopAGain = nextGain;
            drivingLoopBGain = currentGain;
        }
    }

    private float GetLoopOverlap()
    {
        if (drivingLoopClip == null) return loopCrossfadeSeconds;
        return Mathf.Min(loopCrossfadeSeconds, Mathf.Max(0.02f, drivingLoopClip.length * 0.2f));
    }

    private void StopDrivingLoopImmediately()
    {
        if (drivingSourceA != null) drivingSourceA.Stop();
        if (drivingSourceB != null) drivingSourceB.Stop();
        drivingLoopScheduled = false;
        drivingLoopAGain = drivingLoopBGain = 0f;
    }

    private void ReturnToIdle()
    {
        if (idleLoopClip != null && !idleSource.isPlaying)
        {
            idleSource.clip = idleLoopClip;
            idleSource.loop = true;
            idleSource.Play();
        }

        SetTargets(0f, 0f, 1f, 0f);
        state = PlaybackState.Idle;
    }

    private void SetTargets(float transition, float accelerationFollow, float idle, float driving)
    {
        transitionTarget = transition;
        accelerationFollowTarget = accelerationFollow;
        idleTarget = idle;
        drivingTarget = driving;
    }

    private void ApplyCrossfade()
    {
        float step = Time.unscaledDeltaTime / crossfadeSeconds;
        transitionGain = Mathf.MoveTowards(transitionGain, transitionTarget, step);
        accelerationFollowGain = Mathf.MoveTowards(accelerationFollowGain, accelerationFollowTarget, step);
        idleGain = Mathf.MoveTowards(idleGain, idleTarget, step);
        drivingGain = Mathf.MoveTowards(drivingGain, drivingTarget, step);
        ApplySourceVolumes();

        // Let audible sources reach zero before stopping them. Stopping first
        // would reintroduce the exact hard cut this controller is meant to avoid.
        if (state == PlaybackState.Idle)
        {
            if (transitionTarget <= 0f && transitionGain <= 0.001f && transitionSource.isPlaying)
                transitionSource.Stop();
            if (accelerationFollowTarget <= 0f && accelerationFollowGain <= 0.001f &&
                accelerationFollowSource.isPlaying)
                accelerationFollowSource.Stop();
            if (drivingTarget <= 0f && drivingGain <= 0.001f)
                StopDrivingLoopImmediately();
        }
    }

    private void ApplySourceVolumes()
    {
        float sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("GameSFXVolume", 0.8f));
        float volume = engineVolume * sfxVolume;
        if (transitionSource != null) transitionSource.volume = transitionGain * volume;
        if (accelerationFollowSource != null)
            accelerationFollowSource.volume = accelerationFollowGain * volume;
        if (idleSource != null) idleSource.volume = idleGain * volume;
        if (drivingSourceA != null) drivingSourceA.volume = drivingGain * drivingLoopAGain * volume;
        if (drivingSourceB != null) drivingSourceB.volume = drivingGain * drivingLoopBGain * volume;
    }

    private float GetOverlap(AudioClip clip)
    {
        if (clip == null) return crossfadeSeconds;
        return Mathf.Min(crossfadeSeconds, Mathf.Max(0.02f, clip.length * 0.2f));
    }

    private void StopImmediately()
    {
        if (starterConfirmationSource != null) starterConfirmationSource.Stop();
        if (transitionSource != null) transitionSource.Stop();
        if (accelerationFollowSource != null) accelerationFollowSource.Stop();
        if (idleSource != null) idleSource.Stop();
        StopDrivingLoopImmediately();
        transitionGain = accelerationFollowGain = idleGain = drivingGain = 0f;
        transitionTarget = accelerationFollowTarget = idleTarget = drivingTarget = 0f;
        state = PlaybackState.Off;
        nextNoiseMeterPulseAt = 0f;
        ApplySourceVolumes();
    }

    public void NotifyNetworkDespawned()
    {
        if (initialized) StopImmediately();
        wasEngineRunning = false;
    }

    private void OnDisable()
    {
        if (initialized) StopImmediately();
        wasEngineRunning = false;
    }
}
