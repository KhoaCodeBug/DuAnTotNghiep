using UnityEngine;
using UnityEngine.AI;
using Fusion; // THÊM THƯ VIỆN NÀY

public class ZombieAI : NetworkBehaviour // Đổi sang NetworkBehaviour
{
    [Header("Mục tiêu & Tốc độ")]
    public Transform player;
    public float moveSpeed = 3.5f;

    [Header("Tầm nhìn & Phạm vi")]
    public float chaseRange = 10f;
    public float attackRange = 1.5f;
    public float damageRadius = 1.8f;

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

    // CÁC BIẾN MẠNG ĐỂ ĐỒNG BỘ ANIMATION
    [Networked] public Vector2 NetMoveDir { get; set; }
    [Networked] public NetworkBool NetIsRunning { get; set; }

    public override void Spawned()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();
        healthScript = GetComponent<ZombieHealth>();

        if (!HasStateAuthority)
        {
            if (agent != null) agent.enabled = false;
            return;
        }

        if (agent != null)
        {
            agent.updateRotation = false;
            agent.updateUpAxis = false;
            agent.speed = moveSpeed;

            // DÒNG LỆNH MỚI: Ép con quái dính chặt vào lưới NavMesh gần nhất ngay khi xuất hiện
            agent.Warp(transform.position);
        }
    }

    // Đổi Update thành FixedUpdateNetwork của Photon
    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return; // Chỉ Host mới được tính toán AI

        if (healthScript != null && healthScript.isDead)
        {
            if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
            NetIsRunning = false;
            return;
        }

        searchTimer -= Runner.DeltaTime; // Dùng Runner.DeltaTime thay cho Time.deltaTime
        if (searchTimer <= 0)
        {
            FindClosestPlayer();
            searchTimer = searchInterval;
        }

        if (player == null)
        {
            if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
            NetIsRunning = false;
            return;
        }

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);
        Vector2 dirToPlayer = (player.position - transform.position).normalized;
        if (attackTimer > 0) attackTimer -= Runner.DeltaTime;

        if (agent == null || !agent.isOnNavMesh)
        {
            NetIsRunning = false;
            if (distanceToPlayer <= chaseRange) NetMoveDir = dirToPlayer;
            return;
        }

        // XỬ LÝ 3 TRẠNG THÁI AI
        if (distanceToPlayer > chaseRange)
        {
            agent.isStopped = true;
            NetIsRunning = false;
        }
        else if (distanceToPlayer <= chaseRange && distanceToPlayer > attackRange)
        {
            agent.isStopped = false;
            agent.speed = moveSpeed;
            agent.SetDestination(player.position);

            NetIsRunning = true;
            NetMoveDir = agent.velocity.normalized;
        }
        else // KHI VÀO TẦM ĐÁNH
        {
            agent.isStopped = true;
            NetIsRunning = false;
            NetMoveDir = dirToPlayer;

            if (attackTimer <= 0)
            {
                int randomAtk = Random.Range(1, 5);
                RPC_TriggerAttack(randomAtk); // Ra lệnh cho toàn Server phát đòn đánh
                attackTimer = attackCooldown;
            }
        }
    }

    // Hàm Render chạy trên mọi máy (Kể cả máy trạm) để cập nhật hình ảnh mượt mà
    public override void Render()
    {
        if (anim != null)
        {
            anim.SetBool("IsRunning", NetIsRunning); // Chữ I viết hoa
            if (NetMoveDir != Vector2.zero)
            {
                anim.SetFloat("DirX", NetMoveDir.x);
                anim.SetFloat("DirY", NetMoveDir.y);
            }
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_TriggerAttack(int atkIndex)
    {
        if (anim != null) anim.SetTrigger("Atk" + atkIndex);
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

    public void Event_HitATK1() { ExecuteDamage(damageAtk1); }
    public void Event_HitATK2() { ExecuteDamage(damageAtk2); }
    public void Event_HitATK3() { ExecuteDamage(damageAtk3); }
    public void Event_HitATK4() { ExecuteDamage(damageAtk4); }

    private void ExecuteDamage(float damageAmount)
    {
        if (!HasStateAuthority) return; // Chỉ máy chủ mới được quyền trừ máu
        if (player == null) return;

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);
        if (distanceToPlayer <= damageRadius)
        {
            PlayerHealth pHealth = player.GetComponent<PlayerHealth>();
            if (pHealth != null) pHealth.TakeDamage(damageAmount);
        }
    }

    public void OnTakeDamageStun() { attackTimer = 1f; }
}