using UnityEngine;
using UnityEngine.AI;

public class ZombieAI : MonoBehaviour
{
    [Header("Mục tiêu & Tốc độ")]
    public Transform player;
    public float moveSpeed = 3.5f;

    [Header("Tầm nhìn & Phạm vi")]
    public float chaseRange = 10f;
    public float attackRange = 1.2f;
    public float damageRadius = 1.5f;

    [Header("Cài đặt Tấn công")]
    public float attackCooldown = 1.5f;
    public float damageAtk1 = 10f;
    public float damageAtk2 = 15f;
    public float damageAtk3 = 20f;
    public float damageAtk4 = 30f;

    private NavMeshAgent agent;
    private Animator anim;
    private ZombieHealth healthScript;

    private float attackTimer = 0f;
    private float searchTimer = 0f;
    private float searchInterval = 0.5f;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();
        healthScript = GetComponent<ZombieHealth>();

        // THIẾT LẬP BẮT BUỘC ĐỂ NAVMESH CHẠY ĐƯỢC 2D
        if (agent != null)
        {
            agent.updateRotation = false; // Tắt tự xoay của 3D
            agent.updateUpAxis = false;   // Ép NavMesh hiểu hệ trục tọa độ XY của 2D
            agent.speed = moveSpeed;
        }
        else
        {
            Debug.LogError("QUÊN CHƯA GẮN NAVMESH AGENT CHO ZOMBIE KÌA THÁI ƠI!");
        }
    }

    void Update()
    {
        // 1. Nếu Zombie chết -> Ngưng hoạt động
        if (healthScript != null && healthScript.isDead)
        {
            if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
            return;
        }

        // 2. RADAR: Quét mục tiêu
        searchTimer -= Time.deltaTime;
        if (searchTimer <= 0)
        {
            FindClosestPlayer();
            searchTimer = searchInterval;
        }

        // 3. Nếu chưa có Player -> Thở
        if (player == null)
        {
            if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
            anim.SetBool("isRunning", false);
            return;
        }

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);
        Vector2 dirToPlayer = (player.position - transform.position).normalized;
        if (attackTimer > 0) attackTimer -= Time.deltaTime;

        // --- BẢO VỆ CHỐNG LỖI NAVMESH ---
        // Nếu chẳng may quên gắn Agent hoặc mặt sàn bị lọt, Zombie vẫn sẽ đứng tại chỗ và xoay mặt chém!
        if (agent == null || !agent.isOnNavMesh)
        {
            anim.SetBool("isRunning", false);
            if (distanceToPlayer <= chaseRange) UpdateAnimatorDirection(dirToPlayer);
            return;
        }

        // 4. XỬ LÝ 3 TRẠNG THÁI AI (Khi NavMesh đã ổn định)
        if (distanceToPlayer > chaseRange)
        {
            agent.isStopped = true;
            anim.SetBool("isRunning", false);
        }
        else if (distanceToPlayer <= chaseRange && distanceToPlayer > attackRange)
        {
            agent.isStopped = false;
            agent.speed = moveSpeed;
            agent.SetDestination(player.position);

            anim.SetBool("isRunning", true);
            UpdateAnimatorDirection(agent.velocity.normalized);
        }
        else
        {
            agent.isStopped = true;
            anim.SetBool("isRunning", false);
            UpdateAnimatorDirection(dirToPlayer);

            if (attackTimer <= 0) TriggerRandomAttack();
        }
    }

    void FindClosestPlayer()
    {
        GameObject[] allPlayers = GameObject.FindGameObjectsWithTag("Player");
        float closestDistance = chaseRange;
        Transform target = null;

        foreach (GameObject p in allPlayers)
        {
            float dist = Vector2.Distance(transform.position, p.transform.position);
            if (dist < closestDistance)
            {
                closestDistance = dist;
                target = p.transform;
            }
        }
        player = target;
    }

    void UpdateAnimatorDirection(Vector2 direction)
    {
        if (direction != Vector2.zero)
        {
            anim.SetFloat("DirX", direction.x);
            anim.SetFloat("DirY", direction.y);
        }
    }

    void TriggerRandomAttack()
    {
        int randomAtk = Random.Range(1, 5);
        anim.SetTrigger("Atk" + randomAtk);
        attackTimer = attackCooldown;
    }

    public void Event_HitATK1() { ExecuteDamage(damageAtk1); }
    public void Event_HitATK2() { ExecuteDamage(damageAtk2); }
    public void Event_HitATK3() { ExecuteDamage(damageAtk3); }
    public void Event_HitATK4() { ExecuteDamage(damageAtk4); }

    private void ExecuteDamage(float damageAmount)
    {
        if (player == null) return;
        float distanceToPlayer = Vector2.Distance(transform.position, player.position);
        if (distanceToPlayer <= damageRadius)
        {
            PlayerHealth pHealth = player.GetComponent<PlayerHealth>();
            if (pHealth != null) pHealth.TakeDamage(damageAmount);
        }
    }

    public void OnTakeDamageStun()
    {
        attackTimer = 1f;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, chaseRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, damageRadius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}