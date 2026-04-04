using UnityEngine;
using UnityEngine.AI;

public class ZombieHealth : MonoBehaviour
{
    [Header("Chỉ số Sinh tồn")]
    public int maxHealth = 100;
    private int currentHealth;

    // Biến này để AI có thể đọc xem bản thân đã chết chưa
    public bool isDead { get; private set; } = false;

    private Animator anim;
    private NavMeshAgent agent;
    private Collider2D coll;

    // Tham chiếu đến cái não (AI) của Zombie
    private ZombieAI aiScript;

    void Start()
    {
        currentHealth = maxHealth;

        anim = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();
        coll = GetComponent<Collider2D>();
        aiScript = GetComponent<ZombieAI>();
    }

    // -------------------------------------------------------------------
    // HÀM NÀY GỌI KHI PLAYER CHÉM TRÚNG ZOMBIE
    // -------------------------------------------------------------------
    public void TakeDamage(int damageTaken)
    {
        if (isDead) return;

        currentHealth -= damageTaken;

        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            // Bị chém thì bật hoạt ảnh khựng lại
            anim.SetTrigger("TakeDamage");

            // Báo cho bộ não AI biết là đang bị choáng để ngừng tấn công 1 giây
            if (aiScript != null)
            {
                aiScript.OnTakeDamageStun();
            }
        }
    }

    // Xử lý cái chết
    void Die()
    {
        isDead = true;

        // Bật trạng thái chết ở Animator
        anim.SetBool("isDead", true);
        int randomDeath = Random.Range(0, 2);
        anim.SetInteger("DeathType", randomDeath);

        // TẮT MỌI HOẠT ĐỘNG
        if (agent != null)
        {
            agent.isStopped = true;
            agent.enabled = false;
        }

        if (coll != null) coll.enabled = false;

        // Tắt luôn Script AI để nó không rà quét Radar nữa
        if (aiScript != null) aiScript.enabled = false;
    }
}