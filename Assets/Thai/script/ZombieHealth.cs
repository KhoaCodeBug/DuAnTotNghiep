using UnityEngine;
using Fusion;
using System.Collections;
using System; // BẮT BUỘC CÓ DÒNG NÀY ĐỂ DÙNG SỰ KIỆN (ACTION)

public class ZombieHealth : NetworkBehaviour
{
    [Header("Chỉ số Sinh tồn")]
    public float maxHealth = 100f;
    public float stunDuration = 0.5f; // Thời gian choáng khi bị bắn
    public float meleeStunDuration = 1.5f; // Thời gian choáng khi bị đập báng súng
    public Color hurtColor = Color.red;

    [Networked] public float currentHealth { get; set; }
    [Networked] public NetworkBool isDead { get; set; }

    // 💡 ĐÀI PHÁT THANH ĐỂ BÁO LỖI CS1061 BIẾN MẤT
    public event Action<float> OnStunRequested;

    private Animator anim;
    private Collider2D coll;
    // Đã xóa biến aiScript vì Health không cần biết AI là ai nữa
    private SpriteRenderer spriteRend;
    private Color originalColor;

    public override void Spawned()
    {
        currentHealth = maxHealth;
        isDead = false;

        anim = GetComponent<Animator>();
        coll = GetComponent<Collider2D>();

        spriteRend = GetComponentInChildren<SpriteRenderer>();
        if (spriteRend != null) originalColor = spriteRend.color;
    }

    // =======================================================
    // 🔥 HÀM NHẬN SÁT THƯƠNG
    // =======================================================
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_TakeDamage(float damage, PlayerRef shooter = default, bool isMelee = false)
    {
        if (isDead) return;

        // Trừ máu và khóa không cho tụt số âm
        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

        Debug.Log($"<color=red><b>[TRÚNG ĐÒN] Zombie mất {damage} máu! Máu còn: {currentHealth}</b></color>");

        if (currentHealth <= 0f)
        {
            Die(shooter);
            return;
        }

        // 💡 KIỂM TRA CHOÁNG: Nếu là cận chiến (isMelee) thì choáng 1.5s, nếu súng thì 0.5s
        float currentStunTime = isMelee ? meleeStunDuration : stunDuration;

        // 💡 PHÁT LOA THÔNG BÁO CHOÁNG TỚI ZOMBIE AI
        OnStunRequested?.Invoke(currentStunTime);

        RPC_PlayHitEffect();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_PlayHitEffect()
    {
        if (anim != null) anim.SetTrigger("TakeDamage");

        if (spriteRend != null)
        {
            StopCoroutine(FlashRedRoutine());
            StartCoroutine(FlashRedRoutine());
        }
    }

    private IEnumerator FlashRedRoutine()
    {
        spriteRend.color = hurtColor;
        yield return new WaitForSeconds(0.12f);
        if (!isDead) spriteRend.color = originalColor;
    }

    private void Die(PlayerRef shooter)
    {
        if (isDead) return;
        isDead = true;

        if (coll != null) coll.enabled = false;
        // Đã xóa lệnh tắt aiScript ở đây vì ZombieAI tự biết dừng lại khi isDead = true

        // Xử lý cộng điểm hạ gục (Kill) cho Player
        if (shooter != PlayerRef.None)
        {
            Skill_WeaponMaster[] allWeaponMasters = FindObjectsByType<Skill_WeaponMaster>(FindObjectsSortMode.None);

            foreach (var master in allWeaponMasters)
            {
                if (master.Object != null && master.Object.InputAuthority == shooter)
                {
                    master.AddKill();
                    break;
                }
            }
        }

        RPC_PlayDeathAnimation();
        StartCoroutine(VanishRoutine());
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayDeathAnimation()
    {
        if (anim != null)
        {
            // 💡 ĐÃ SỬA LỖI CS0104: Chỉ định rõ ràng dùng hàm Random của UnityEngine
            int randomDeath = UnityEngine.Random.Range(0, 2);
            anim.SetInteger("DeathType", randomDeath);
            anim.SetBool("isDead", true);

            Debug.Log($"<color=white><b>[TỬ TRẬN] Zombie ngã gục theo kiểu số: {randomDeath}</b></color>");
        }
    }

    private IEnumerator VanishRoutine()
    {
        yield return new WaitForSeconds(5f);
        if (HasStateAuthority) Runner.Despawn(Object);
    }
}