using UnityEngine;
using UnityEngine.AI; // Bắt buộc khai báo để dùng NavMesh

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

        // 2 Cài đặt TỐI QUAN TRỌNG để NavMesh 3D chạy được trong game 2D
        if (agent != null)
        {
            agent.updateRotation = false;
            agent.updateUpAxis = false;
            agent.speed = moveSpeed;
        }
    }

    void Update()
    {
        // 1. Chết thì đứng im
        if (healthScript != null && healthScript.isDead)
        {
            if (agent.isOnNavMesh) agent.isStopped = true;
            return;
        }

        // 2. RADAR
        searchTimer -= Time.deltaTime;
        if (searchTimer <= 0)
        {
            FindClosestPlayer();
            searchTimer = searchInterval;
        }

        // 3. Không có Player -> Thở
        if (player == null)
        {
            if (agent.isOnNavMesh) agent.isStopped = true;
            anim.SetBool("isRunning", false);
            return;
        }

        // Lấy hướng và khoảng cách
        float distanceToPlayer = Vector2.Distance(transform.position, player.position);
        Vector2 dirToPlayer = (player.position - transform.position).normalized;

        if (attackTimer > 0) attackTimer -= Time.deltaTime;

        // ---------------------------------------------------------
        // CHỐT CHẶN AN TOÀN: BẢO VỆ GAME KHỎI LỖI ĐỎ CỦA NAVMESH
        // ---------------------------------------------------------
        if (!agent.isOnNavMesh)
        {
            // Bị lỗi đường đi? Vẫn cho phép xoay mặt nhìn Player nếu ở gần!
            anim.SetBool("isRunning", false);
            if (distanceToPlayer <= chaseRange) UpdateAnimatorDirection(dirToPlayer);

            // Dừng code tại đây, tuyệt đối không gọi SetDestination để tránh văng lỗi đỏ
            return;
        }

        // 4. XỬ LÝ 3 TRẠNG THÁI AI (Khi NavMesh hoạt động tốt)
        if (distanceToPlayer > chaseRange)
        {
            agent.isStopped = true;
            anim.SetBool("isRunning", false);
        }
        else if (distanceToPlayer <= chaseRange && distanceToPlayer > attackRange)
        {
            agent.isStopped = false;
            agent.speed = moveSpeed;
            agent.SetDestination(player.position); // NavMesh tìm đường

            anim.SetBool("isRunning", true);
            UpdateAnimatorDirection(agent.velocity.normalized);
        }
        else
        {
            agent.isStopped = true;
            anim.SetBool("isRunning", false);
            UpdateAnimatorDirection(dirToPlayer); // Luôn xoay mặt theo Player

            if (attackTimer <= 0)
            {
                TriggerRandomAttack();
            }
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