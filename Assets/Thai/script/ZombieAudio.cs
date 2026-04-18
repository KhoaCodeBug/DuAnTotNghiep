using UnityEngine;
using Fusion;

[RequireComponent(typeof(AudioSource), typeof(ZombieAI))]
public class ZombieAudio : NetworkBehaviour
{
    [Header("--- Cấu hình Âm thanh ---")]
    public AudioClip wanderSound; // Âm thanh rên rỉ tản bộ
    public AudioClip chaseSound;  // 💡 Âm thanh gầm thét khi rượt đuổi

    private AudioSource audioSource;
    private ZombieAI aiScript;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        aiScript = GetComponent<ZombieAI>();
    }

    public override void Render()
    {
        if (aiScript == null || audioSource == null) return;

        // 1. ƯU TIÊN 1: Nếu đang rượt đuổi Player -> Phát tiếng gầm thét
        if (aiScript.NetIsChasing && chaseSound != null)
        {
            // Kiểm tra xem loa có đang phát ĐÚNG bài chaseSound chưa, chưa thì đổi bài và bật loa
            if (audioSource.clip != chaseSound || !audioSource.isPlaying)
            {
                audioSource.clip = chaseSound;
                audioSource.Play();
            }
        }
        // 2. ƯU TIÊN 2: Nếu không rượt mà đang tản bộ -> Phát tiếng rên rỉ
        else if (aiScript.NetIsWandering && wanderSound != null)
        {
            if (audioSource.clip != wanderSound || !audioSource.isPlaying)
            {
                audioSource.clip = wanderSound;
                audioSource.Play();
            }
        }
        // 3. NGHỈ NGƠI: Nếu không làm gì cả (Idle/Bị choáng) -> Tắt tiếng
        else
        {
            if (audioSource.isPlaying)
            {
                audioSource.Stop();
            }
        }
    }
}