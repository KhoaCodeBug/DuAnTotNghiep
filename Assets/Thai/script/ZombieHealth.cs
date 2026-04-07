using UnityEngine;
using UnityEngine.AI;
using Fusion;
using System.Collections;

public class ZombieHealth : NetworkBehaviour
{
    [Header("Chỉ số Sinh tồn")]
    public float maxHealth = 100f;
    public float stunDuration = 0.5f;
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
    // 🔥 HÀM NHẬN SÁT THƯƠNG CHUẨN THEO BẢN GỐC CỦA KHOA
    // =======================================================
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_TakeDamage(float damage, PlayerRef shooter = default)
    {
        if (isDead) return;

        // Trừ máu và khóa không cho tụt số âm
        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

        Debug.Log($"<color=red><b>[TRÚNG ĐẠN] Zombie mất {damage} máu! Máu còn: {currentHealth}</b></color>");

        if (currentHealth <= 0f)
        {
            Die(shooter);
            return;
        }

        // Bị bắn trúng thì đứng hình
        if (aiScript != null) aiScript.ApplyStun(stunDuration);

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

        // Xử lý cộng điểm hạ gục (Kill) cho Player giống hệt code Khoa
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
            anim.SetBool("IsDead", true);
            int randomDeath = Random.Range(0, 2);
            //anim.SetInteger("DeathType", randomrandomDeath);
        }
    }

    private IEnumerator VanishRoutine()
    {
        yield return new WaitForSeconds(5f);
        if (HasStateAuthority) Runner.Despawn(Object);
    }
}