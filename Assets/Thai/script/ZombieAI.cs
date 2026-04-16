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

    // 🔥 [MỚI THÊM] Timer để tối ưu thuật toán A*
    private float pathUpdateTimer = 0f;

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
            agent.stoppingDistance = 0f;
            agent.Warp(transform.position);
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || (healthScript != null && healthScript.isDead))
        {
            if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
            NetIsRunning = false;
            return;
        }

        if (stunTimer > 0)
        {
            stunTimer -= Runner.DeltaTime;
            if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
            NetIsRunning = false;
            return;
        }

        searchTimer -= Runner.DeltaTime;
        if (searchTimer <= 0)
        {
            FindClosestPlayerInRange();
            searchTimer = 0.5f;
        }

        if (player == null || agent == null || !agent.isOnNavMesh)
        {
            NetIsRunning = false;
            if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
            return;
        }

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);
        if (attackTimer > 0) attackTimer -= Runner.DeltaTime;

        // TRẠNG THÁI 2: Đuổi theo (Chase) & Tránh vật cản bằng NavMesh A*
        if (distanceToPlayer > attackRange)
        {
            agent.isStopped = false;
            agent.speed = moveSpeed;

            // 🔥 [TỐI ƯU A*] Thay vì gọi mỗi frame, chỉ tính toán lại đường đi lách vật cản mỗi 0.2 giây
            pathUpdateTimer -= Runner.DeltaTime;
            if (pathUpdateTimer <= 0)
            {
                agent.SetDestination(player.position);
                pathUpdateTimer = 0.2f;
            }

            NetIsRunning = true;

            // Dùng agent.steeringTarget (mục tiêu ngắn hạn trên lưới) giúp animation hướng đi 
            // mượt mà hơn khi zombie đang lách qua vật cản, thay vì hướng thẳng vào player.
            NetMoveDir = (agent.steeringTarget - transform.position).normalized;
        }
        // TRẠNG THÁI 3: Tấn công
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
        // Debug.Log($"<color=orange><b>[BÁO ĐỘNG] Zombie đang tung chiêu: ĐÒN SỐ {atkIndex}</b></color>");
    }

    void FindClosestPlayerInRange()
    {
        GameObject[] allPlayers = GameObject.FindGameObjectsWithTag("Player");
        float closestDistance = detectionRange;
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
        if (currentAttackIndex == 1) ExecuteDamage(damageAtk1, 1);
        else if (currentAttackIndex == 2) ExecuteDamage(damageAtk2, 2);
        else if (currentAttackIndex == 3) ExecuteDamage(damageAtk3, 3);
        else if (currentAttackIndex == 4) ExecuteDamage(damageAtk4, 4);
    }

    private void ExecuteDamage(float damageAmount, int attackIndex)
    {
        if (!HasStateAuthority || player == null) return;

        if (Vector2.Distance(transform.position, player.position) <= damageRadius)
        {
            PlayerHealth pHealth = player.GetComponent<PlayerHealth>();
            if (pHealth != null)
            {
                // Gây sát thương cơ bản và báo cho PlayerHealth biết đây là Zombie tấn công (hiện UI máu)
                pHealth.TakeDamage(damageAmount, false, true);

                // ==========================================
                // 🔥 [Thái zombie hiệu ứng máu]
                // ==========================================
                if (attackIndex == 1)
                {
                    // Atk1 (Cào): Hàm TakeDamage ở trên đã tự động set isBleeding = true.
                    // Player sẽ bị tụt máu từ từ theo bleedDamagePerSecond, 
                    // nhưng hoàn toàn CÓ THỂ CHỮA TRỊ bằng cách gọi hàm SetGlobalBleeding(false) (ví dụ khi dùng băng gạc).
                    Debug.Log("<color=yellow>[Thái zombie hiệu ứng máu] Player trúng Atk1: Đang rỉ máu (Có thể băng bó)!</color>");
                }
                else if (attackIndex == 2)
                {
                    // Atk2 (Cắn/Vết thương chí mạng): Gọi thêm hàm SetBitten().
                    // Bật isBitten = true, kích hoạt infectionTimer rút máu đến chết và hóa Zombie.
                    // KHÔNG THỂ CHỮA TRỊ.
                    pHealth.SetBitten();
                    Debug.Log("<color=red>[Thái zombie hiệu ứng máu] Player trúng Atk2: Đã bị cắn! Án tử hình không thể chữa!</color>");
                }
                // ==========================================
            }
        }
    }

    public void ApplyStun(float duration)
    {
        stunTimer = duration;
        attackTimer = duration;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, damageRadius);
    }
}