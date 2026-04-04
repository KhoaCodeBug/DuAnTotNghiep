using UnityEngine;
using UnityEngine.AI;
using Fusion; // BẮT BUỘC THÊM THƯ VIỆN NÀY

public class ZombieHealth : NetworkBehaviour // Đổi từ MonoBehaviour sang NetworkBehaviour
{
    [Header("Chỉ số Sinh tồn")]
    public int maxHealth = 100;

    // Biến Mạng: Máu và Trạng thái chết sẽ tự động đồng bộ cho mọi người chơi
    [Networked] public int currentHealth { get; set; }
    [Networked] public NetworkBool isDead { get; set; }

    private Animator anim;
    private NavMeshAgent agent;
    private Collider2D coll;
    private ZombieAI aiScript;

    // Thay Start() bằng Spawned() trong Photon
    public override void Spawned()
    {
        currentHealth = maxHealth;
        isDead = false;

        anim = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();
        coll = GetComponent<Collider2D>();
        aiScript = GetComponent<ZombieAI>();
    }

    // Cầu nối: Player gọi hàm này như bình thường, nó sẽ tự gửi tín hiệu lên Server
    public void TakeDamage(int damageTaken)
    {
        RPC_TakeDamage(damageTaken);
    }

    // Lệnh thực thi trên Server
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_TakeDamage(int damageTaken)
    {
        if (isDead) return;

        currentHealth -= damageTaken;

        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            RPC_PlayHitEffect(); // Gọi tất cả máy trạm phát hoạt ảnh bị chém
            if (aiScript != null) aiScript.OnTakeDamageStun();
        }
    }

    // Lệnh phát hoạt ảnh bị thương cho TẤT CẢ người chơi cùng thấy
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayHitEffect()
    {
        if (anim != null) anim.SetTrigger("TakeDamage");
    }

    void Die()
    {
        isDead = true;

        if (agent != null)
        {
            agent.isStopped = true;
            agent.enabled = false;
        }
        if (coll != null) coll.enabled = false;
        if (aiScript != null) aiScript.enabled = false;

        RPC_PlayDeathAnimation();
    }

    // Lệnh phát hoạt ảnh chết cho TẤT CẢ người chơi cùng thấy
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayDeathAnimation()
    {
        if (anim != null)
        {
            anim.SetBool("isDead", true);
            int randomDeath = Random.Range(0, 2);
            anim.SetInteger("DeathType", randomDeath);
        }
    }
}