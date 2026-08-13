using UnityEngine;
using Fusion;

[RequireComponent(typeof(AudioSource), typeof(ZombieAI))]
public class ZombieAudio : NetworkBehaviour
{
    [Header("--- Cấu hình Âm thanh ---")]
    [Tooltip("Âm thanh rên rỉ rảnh rỗi khi đứng yên (Idle)")]
    public AudioClip wanderSound;

    [Tooltip("Âm thanh gầm thét khi rượt đuổi (Chase)")]
    public AudioClip chaseSound;

    [Header("--- Cân bằng âm thanh theo đàn ---")]
    [Range(0f, 1f)] public float wanderVolume = 0.34f;
    [Range(0f, 1f)] public float chaseVolume = 0.78f;
    [Tooltip("Bỏ đoạn im lặng ở đầu file zombie_Run.mp3.")]
    [Min(0f)] public float chaseAudibleStart = 0.31f;
    [Tooltip("Loop trước đoạn im lặng dài ở cuối file zombie_Run.mp3.")]
    [Min(0.1f)] public float chaseAudibleEnd = 2.42f;
    [Min(0f)] public float initialWanderDelayMin = 2f;
    [Min(0f)] public float initialWanderDelayMax = 10f;
    [Min(0.1f)] public float wanderBurstDurationMin = 1.5f;
    [Min(0.1f)] public float wanderBurstDurationMax = 3.2f;
    [Min(0f)] public float wanderIntervalMin = 5f;
    [Min(0f)] public float wanderIntervalMax = 12f;

    private AudioSource audioSource;
    private ZombieAI aiScript;
    private float nextWanderTime;
    private float wanderStopTime;
    private bool wasEngaged;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        aiScript = GetComponent<ZombieAI>();

        GameplayAudioSpatializer.Configure(audioSource, GameplayAudioSpatializer.Profile.Zombie);
        audioSource.loop = false;
        audioSource.Stop();
        ScheduleNextWander(initialWanderDelayMin, initialWanderDelayMax);
    }

    public override void Render()
    {
        if (aiScript == null || audioSource == null) return;

        float sfxVolume = PlayerPrefs.GetFloat("GameSFXVolume", 0.8f);

        // Chase vẫn là loop, nhưng mỗi zombie bắt đầu ở một đoạn ngẫu nhiên
        // để cả đàn không gầm cùng đúng một nhịp.
        bool isEngaged = aiScript.NetIsChasing || aiScript.NetIsAttacking;

        if (isEngaged && chaseSound != null)
        {
            audioSource.volume = GameplayAudioSpatializer.GetAttenuatedVolume(
                audioSource,
                GameplayAudioSpatializer.Profile.Zombie,
                chaseVolume * sfxVolume);

            float audibleStart = Mathf.Clamp(chaseAudibleStart, 0f, Mathf.Max(0f, chaseSound.length - 0.1f));
            float audibleEnd = Mathf.Clamp(chaseAudibleEnd, audibleStart + 0.05f, chaseSound.length);
            bool reachedSilentTail = audioSource.clip == chaseSound
                && audioSource.isPlaying
                && audioSource.time >= audibleEnd;

            if (!wasEngaged || audioSource.clip != chaseSound || !audioSource.isPlaying || reachedSilentTail)
            {
                audioSource.Stop();
                audioSource.clip = chaseSound;
                audioSource.loop = false;

                if (!wasEngaged || audioSource.pitch < 0.9f || audioSource.pitch > 1.1f)
                    audioSource.pitch = Random.Range(0.94f, 1.06f);

                audioSource.time = reachedSilentTail
                    ? audibleStart
                    : Random.Range(audibleStart, audibleEnd - 0.02f);

                if (audioSource.volume > 0.001f)
                    audioSource.Play();
            }
        }
        // Idle là tiếng rên ngắt quãng, không còn một loop đồng loạt ngay lúc
        // vào game. Mỗi con có delay, khoảng nghỉ và pitch riêng.
        else if (!isEngaged && wanderSound != null)
        {
            if (wasEngaged)
            {
                audioSource.Stop();
                audioSource.loop = false;
                ScheduleNextWander(1f, 3f);
            }

            if (audioSource.clip == wanderSound && audioSource.isPlaying)
            {
                audioSource.volume = GameplayAudioSpatializer.GetAttenuatedVolume(
                    audioSource,
                    GameplayAudioSpatializer.Profile.Zombie,
                    wanderVolume * sfxVolume);

                if (Time.time >= wanderStopTime)
                {
                    audioSource.Stop();
                    ScheduleNextWander(wanderIntervalMin, wanderIntervalMax);
                }
            }
            else
            {
                // Nếu clip bị Unity dừng sớm (đổi scene/audio device), mở lại
                // lịch thay vì mắc kẹt ở PositiveInfinity.
                if (float.IsPositiveInfinity(nextWanderTime))
                    ScheduleNextWander(wanderIntervalMin, wanderIntervalMax);

                if (Time.time >= nextWanderTime)
                    PlayWanderSound(sfxVolume);
            }
        }
        else
        {
            if (audioSource.isPlaying)
            {
                audioSource.Stop();
            }
        }

        wasEngaged = isEngaged;
    }

    private void PlayWanderSound(float sfxVolume)
    {
        audioSource.Stop();
        audioSource.clip = wanderSound;
        audioSource.loop = false;
        audioSource.pitch = Random.Range(0.92f, 1.08f);
        audioSource.volume = GameplayAudioSpatializer.GetAttenuatedVolume(
            audioSource,
            GameplayAudioSpatializer.Profile.Zombie,
            wanderVolume * sfxVolume);

        if (audioSource.volume > 0.001f)
        {
            float minBurst = Mathf.Max(0.1f, wanderBurstDurationMin);
            float maxBurst = Mathf.Max(minBurst, wanderBurstDurationMax);
            float burstDuration = Random.Range(minBurst, maxBurst);
            float audioSecondsNeeded = burstDuration * audioSource.pitch;
            float latestStart = Mathf.Max(0f, wanderSound.length - audioSecondsNeeded - 0.05f);

            if (latestStart > 0f)
                audioSource.time = Random.Range(0f, latestStart);

            wanderStopTime = Time.time + burstDuration;
            nextWanderTime = float.PositiveInfinity;
            audioSource.Play();
        }
        else
        {
            ScheduleNextWander(wanderIntervalMin, wanderIntervalMax);
        }
    }

    private void ScheduleNextWander(float minimumDelay, float maximumDelay)
    {
        float min = Mathf.Max(0f, minimumDelay);
        float max = Mathf.Max(min, maximumDelay);
        nextWanderTime = Time.time + Random.Range(min, max);
    }
}
