using UnityEngine;
using UnityEngine.AI;
using Fusion;

public class ZombieAI : NetworkBehaviour
{
    [Header("--- Phạm vi Phát hiện (Detection) ---")]
    [Tooltip("Khoảng cách Zombie có thể phát hiện người chơi")]
    public float detectionRange = 10f;

    [Header("--- Tấn công & Tốc độ ---")]
    public float moveSpeed = 3.5f;
    public float attackRange = 1.5f;
    public float damageRadius = 1.8f;
    public float attackCooldown = 1.5f;

    [Header("--- Sát thương các chiêu ---")]
    public float damageAtk1 = 10f;
    public float damageAtk2 = 15f;
    public float damageAtk3 = 20f;
    public float damageAtk4 = 30f;

    private Transform player;
    private NavMeshAgent agent;
    private Animator anim;
    private ZombieHealth healthScript;

    private float attackTimer = 0f;
    private float searchTimer = 0f;
    private float stunTimer = 0f;
    private int currentAttackIndex = 1;

    [Networked] public Vector2 NetMoveDir { get; set; }
    [Networked] public NetworkBool NetIsRunning { get; set; }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.updateRotation = false;
            agent.updateUpAxis = false;
            // Ép phanh bằng 0 ngay từ lúc khởi tạo để tránh lỗi đứng từ xa
            agent.stoppingDistance = 0f;
        }
    }

    public override void Spawned()
    {
        transform.rotation = Quaternion.identity;
        transform.position = new Vector3(transform.position.x, transform.position.y, 0f);

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

            // 🔥 CHỐT CHẶN CUỐI CÙNG: Đảm bảo NavMesh không bao giờ tự dừng lại sớm
            agent.stoppingDistance = 0f;

            agent.Warp(transform.position);
        }
    }

    public override void FixedUpdateNetwork()
    {
        // Nếu đã chết thì ngừng mọi hoạt động
        if (!HasStateAuthority || (healthScript != null && healthScript.isDead))
        {
            if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
            NetIsRunning = false;
            return;
        }

        // Xử lý bị khựng (Stun)
        if (stunTimer > 0)
        {
            stunTimer -= Runner.DeltaTime;
            if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
            NetIsRunning = false;
            return;
        }

        // Cứ mỗi 0.5 giây mới quét tìm Player một lần để tiết kiệm CPU
        searchTimer -= Runner.DeltaTime;
        if (searchTimer <= 0)
        {
            FindClosestPlayerInRange();
            searchTimer = 0.5f;
        }

        // TRẠNG THÁI 1: KHÔNG có người chơi trong tầm mắt -> Đứng im (Idle)
        if (player == null || agent == null || !agent.isOnNavMesh)
        {
            NetIsRunning = false;
            if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
            return;
        }

        // Tính khoảng cách thực tế để quyết định Đuổi hay Đánh
        float distanceToPlayer = Vector2.Distance(transform.position, player.position);
        if (attackTimer > 0) attackTimer -= Runner.DeltaTime;

        // TRẠNG THÁI 2: Player ở xa nhưng vẫn trong tầm mắt -> Đuổi theo (Chase)
        if (distanceToPlayer > attackRange)
        {
            agent.isStopped = false;
            agent.speed = moveSpeed;
            agent.SetDestination(player.position);

            NetIsRunning = true;
            NetMoveDir = (agent.steeringTarget - transform.position).normalized;
        }
        // TRẠNG THÁI 3: Player đã lọt vào tầm đánh -> Tấn công (Attack)
        else
        {
            agent.isStopped = true;
            NetIsRunning = false;
            NetMoveDir = (player.position - transform.position).normalized;

            if (attackTimer <= 0)
            {
                int randomAtk = Random.Range(1, 5);
                currentAttackIndex = randomAtk;
                RPC_TriggerAttack(randomAtk);
                attackTimer = attackCooldown;
            }
        }
    }

    public override void Render()
    {
        if (anim == null) return;
        anim.SetBool("isRunning", NetIsRunning);
        if (NetMoveDir != Vector2.zero)
        {
            anim.SetFloat("DirX", NetMoveDir.x);
            anim.SetFloat("DirY", NetMoveDir.y);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_TriggerAttack(int atkIndex)
    {
        if (anim != null)
        {
            anim.ResetTrigger("Atk1");
            anim.ResetTrigger("Atk2");
            anim.ResetTrigger("Atk3");
            anim.ResetTrigger("Atk4");
            anim.SetTrigger("Atk" + atkIndex);
        }

        // 🔥 THÔNG BÁO BỐC THĂM CHIÊU THỨC ĐÃ TRỞ LẠI
        Debug.Log($"<color=orange><b>[BÁO ĐỘNG] Zombie đang tung chiêu: ĐÒN SỐ {atkIndex}</b></color>");
    }

    // 🔥 HÀM TÌM PLAYER TRONG PHẠM VI VÒNG TRÒN
    void FindClosestPlayerInRange()
    {
        GameObject[] allPlayers = GameObject.FindGameObjectsWithTag("Player");
        float closestDistance = detectionRange; // Chỉ tìm trong phạm vi detectionRange
        player = null;

        foreach (GameObject p in allPlayers)
        {
            float dist = Vector2.Distance(transform.position, p.transform.position);
            if (dist < closestDistance)
            {
                closestDistance = dist;
                player = p.transform;
            }
        }
    }

    public void DealDamage()
    {
        if (currentAttackIndex == 1) ExecuteDamage(damageAtk1);
        else if (currentAttackIndex == 2) ExecuteDamage(damageAtk2);
        else if (currentAttackIndex == 3) ExecuteDamage(damageAtk3);
        else if (currentAttackIndex == 4) ExecuteDamage(damageAtk4);
    }

    private void ExecuteDamage(float damageAmount)
    {
        if (!HasStateAuthority || player == null) return;
        if (Vector2.Distance(transform.position, player.position) <= damageRadius)
        {
            PlayerHealth pHealth = player.GetComponent<PlayerHealth>();
            if (pHealth != null) pHealth.TakeDamage(damageAmount);
        }
    }

    public void ApplyStun(float duration)
    {
        stunTimer = duration;
        attackTimer = duration;
    }

    // 🔥 VẼ VÒNG TRÒN PHẠM VI TRONG SCENE ĐỂ BẠN DỄ CHỈNH
    private void OnDrawGizmosSelected()
    {
        // Vòng tròn màu Xanh lá: Phạm vi phát hiện (Detection)
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Vòng tròn màu Vàng: Phạm vi tấn công (Attack)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // Vòng tròn màu Đỏ: Phạm vi gây sát thương thực tế
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, damageRadius);
    }
}