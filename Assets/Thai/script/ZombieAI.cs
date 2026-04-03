using UnityEngine;
using UnityEngine.AI;

public class ZombieAI : MonoBehaviour
{
    [Header("Mục tiêu & Tốc độ")]
    [Tooltip("Hệ thống sẽ tự động tìm Player, không cần kéo tay")]
    public Transform player;

    [Tooltip("Tốc độ chạy đuổi theo Player")]
    public float moveSpeed = 3.5f; // BIẾN MỚI: QUẢN LÝ TỐC ĐỘ

    [Header("Tầm nhìn & Phạm vi")]
    public float chaseRange = 10f; // Tầm nhìn Radar
    public float attackRange = 1.2f;  // Khoảng cách dừng lại để đánh
    public float damageRadius = 1.5f; // Tầm tay thực tế vung tới 

    [Header("Cài đặt Tấn công")]
    public float attackCooldown = 1.5f; // Thời gian nghỉ giữa các đòn đánh
    public float damageAtk1 = 10f;
    public float damageAtk2 = 15f;
    public float damageAtk3 = 20f;
    public float damageAtk4 = 30f;

    private NavMeshAgent agent;
    private Animator anim;
    private ZombieHealth healthScript; // Tham chiếu đến cơ thể để kiểm tra xem đã chết chưa

    private float attackTimer = 0f;
    private float searchTimer = 0f;
    private float searchInterval = 0.5f;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();
        healthScript = GetComponent<ZombieHealth>();

        agent.updateRotation = false;
        agent.updateUpAxis = false;

        // Gán tốc độ đã cài vào NavMesh
        agent.speed = moveSpeed;
    }

    void Update()
    {
        // Kiểm tra xem cơ thể đã bị tiêu diệt chưa
        if (healthScript != null && healthScript.isDead) return;

        // 1. HỆ THỐNG RADAR
        searchTimer -= Time.deltaTime;
        if (searchTimer <= 0)
        {
            FindClosestPlayer();
            searchTimer = searchInterval;
        }

        // 2. Không có ai -> Đứng im
        if (player == null)
        {
            agent.isStopped = true;
            anim.SetBool("isRunning", false);
            return;
        }

        // 3. XỬ LÝ 3 TRẠNG THÁI AI
        float distanceToPlayer = Vector2.Distance(transform.position, player.position);
        if (attackTimer > 0) attackTimer -= Time.deltaTime;

        // Trạng thái 1: Ngoài tầm nhìn
        if (distanceToPlayer > chaseRange)
        {
            agent.isStopped = true;
            anim.SetBool("isRunning", false);
        }
        // Trạng thái 2: Rượt đuổi (Áp dụng Tốc độ)
        else if (distanceToPlayer <= chaseRange && distanceToPlayer > attackRange)
        {
            agent.isStopped = false;
            agent.speed = moveSpeed; // Đảm bảo tốc độ chạy luôn được cập nhật
            agent.SetDestination(player.position);

            anim.SetBool("isRunning", true);
            UpdateAnimatorDirection(agent.velocity.normalized);
        }
        // Trạng thái 3: Tấn công
        else
        {
            agent.isStopped = true;
            anim.SetBool("isRunning", false);

            Vector2 dirToPlayer = (player.position - transform.position).normalized;
            UpdateAnimatorDirection(dirToPlayer);

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

    // -------------------------------------------------------------------
    // 4 HÀM MỒI CHO ANIMATION EVENT
    // -------------------------------------------------------------------
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

    // Hàm này được gọi từ ZombieHealth khi bị chém trúng
    public void OnTakeDamageStun()
    {
        attackTimer = 1f; // AI bị choáng 1 giây
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
