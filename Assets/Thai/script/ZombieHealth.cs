using UnityEngine;
using UnityEngine.AI;
using Fusion;
using System.Collections;

public class ZombieHealth : NetworkBehaviour
{
    [Header("Chỉ số Sinh tồn")]
    public float maxHealth = 100f;
    public float stunDuration = 0.5f; // Thời gian choáng khi bị bắn
    public float meleeStunDuration = 1.5f; // [MỚI] Thời gian choáng khi bị đập báng súng
    public Color hurtColor = Color.red;

    [Networked] public float currentHealth { get; set; }
    [Networked] public NetworkBool isDead { get; set; }

    private Animator anim;
    private NavMeshAgent agent;
    private Collider2D coll;
    private ZombieAI aiScript;
    private SpriteRenderer spriteRend;
    private Color originalColor;

    public override void Spawned()
    {
        currentHealth = maxHealth;
        isDead = false;

        anim = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();
        coll = GetComponent<Collider2D>();
        aiScript = GetComponent<ZombieAI>();

        spriteRend = GetComponentInChildren<SpriteRenderer>();
        if (spriteRend != null) originalColor = spriteRend.color;
    }

    // =======================================================
    // 🔥 HÀM NHẬN SÁT THƯƠNG
    // Đã thêm biến "isMelee" để phân biệt đánh xa hay đánh gần
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

        if (aiScript != null)
        {
            aiScript.ApplyStun(currentStunTime);
            // Hàm ApplyStun bên ZombieAI đã tự động khóa tốc độ chạy và khóa sát thương
        }

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

        if (agent != null)
        {
            agent.isStopped = true;
            agent.enabled = false;
        }
        if (coll != null) coll.enabled = false;
        if (aiScript != null) aiScript.enabled = false;

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
            int randomDeath = Random.Range(0, 2);
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