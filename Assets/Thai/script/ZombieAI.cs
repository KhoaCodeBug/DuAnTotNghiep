using UnityEngine;
using Pathfinding; // A* Pathfinding Project
using Fusion;

[RequireComponent(typeof(Rigidbody2D), typeof(Seeker))]
public class ZombieAI : NetworkBehaviour
{
    // CÁC CHẾ ĐỘ CỦA AI ĐỂ QUẢN LÝ LOGIC TÁCH BIỆT
    public enum AIMode { Idle, Wander, Investigate, Chase }
    private AIMode currentMode = AIMode.Idle;
    private AIMode pathRequestMode = AIMode.Idle;

    [Header("--- A* Pathfinding ---")]
    public float nextWaypointDistance = 0.5f;
    private Seeker seeker;
    private Path path;
    private int currentWaypoint = 0;
    private Rigidbody2D rb;

    [Header("--- Phạm vi Phát hiện (Detection) ---")]
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

    [Header("--- Đi lang thang (Wander) ---")]
    public float wanderRadius = 6f;
    public float wanderWaitTimeMin = 2f;
    public float wanderWaitTimeMax = 5f;
    public float wanderSpeedMultiplier = 0.5f;
    private float wanderTimer = 0f;
    private bool isWandering = false;
    private Vector2 wanderTarget;

    // 🔊 HỆ THỐNG ÂM THANH
    private Vector3 lastHeardPosition;
    private bool hasHeardSound = false;
    private float hearMemoryTimer = 0f;
    public float hearMemoryDuration = 3f;

    private Transform player;
    private Animator anim;
    private ZombieHealth healthScript;

    private float attackTimer = 0f;
    private float searchTimer = 0f;
    private float stunTimer = 0f;
    private float pathUpdateTimer = 0f;

    private int currentAttackIndex = 1;

    // BIẾN MẠNG (PHOTON FUSION)
    [Networked] public Vector2 NetMoveDir { get; set; }
    [Networked] public NetworkBool NetIsRunning { get; set; }

    private void Awake()
    {
        seeker = GetComponent<Seeker>();
        rb = GetComponent<Rigidbody2D>();
    }

    public override void Spawned()
    {
        // Đảm bảo không bị xoay trục Z trong môi trường 2D/2.5D
        transform.rotation = Quaternion.identity;
        transform.position = new Vector3(transform.position.x, transform.position.y, 0f);

        anim = GetComponent<Animator>();
        healthScript = GetComponent<ZombieHealth>();

        wanderTimer = Random.Range(1f, 3f);

        if (!HasStateAuthority)
        {
            if (rb != null) rb.bodyType = RigidbodyType2D.Kinematic;
        }
    }

    // ==========================================
    // XỬ LÝ A* PATHFINDING
    // ==========================================
    private void CalculatePath(Vector2 targetPos, AIMode mode)
    {
        if (seeker.IsDone())
        {
            pathRequestMode = mode;
            path = null; // Xóa đường cũ để chờ đường mới
            seeker.StartPath(rb.position, targetPos, OnPathComplete);
        }
    }

    private void OnPathComplete(Path p)
    {
        // Chỉ nhận kết quả nếu trạng thái hiện tại vẫn khớp với mục đích lúc yêu cầu đường đi
        if (!p.error && currentMode == pathRequestMode)
        {
            path = p;
            currentWaypoint = 0;
        }
    }

    private void MoveAlongPath(float currentSpeed)
    {
        if (path == null || currentWaypoint >= path.vectorPath.Count) return;

        Vector2 currentWp = (Vector2)path.vectorPath[currentWaypoint];
        Vector2 targetMoveDir = (currentWp - rb.position).normalized;

        rb.MovePosition(rb.position + targetMoveDir * currentSpeed * Runner.DeltaTime);

        NetMoveDir = targetMoveDir;

        float distToWp = Vector2.Distance(rb.position, currentWp);
        if (distToWp < nextWaypointDistance)
        {
            currentWaypoint++;
        }
    }

    private void StopMovement()
    {
        if (rb != null) rb.linearVelocity = Vector2.zero;
        path = null;
    }

    // ==========================================
    // VÒNG LẶP LOGIC CHÍNH (FIXED UPDATE NETWORK)
    // ==========================================
    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || (healthScript != null && healthScript.isDead))
        {
            StopMovement();
            NetIsRunning = false;
            return;
        }

        // Xử lý khi bị Choáng (Stun)
        if (stunTimer > 0)
        {
            stunTimer -= Runner.DeltaTime;
            StopMovement();
            NetIsRunning = false;
            return;
        }

        // Đếm ngược bộ nhớ âm thanh
        if (hasHeardSound)
        {
            hearMemoryTimer -= Runner.DeltaTime;
            if (hearMemoryTimer <= 0) hasHeardSound = false;
        }

        // Tìm kiếm mục tiêu định kỳ
        searchTimer -= Runner.DeltaTime;
        if (searchTimer <= 0)
        {
            FindClosestPlayerInRange();
            searchTimer = 0.2f;
        }

        // Lấy trạng thái trước đó để so sánh
        AIMode previousMode = currentMode;

        // 1. QUYẾT ĐỊNH TRẠNG THÁI HIỆN TẠI
        if (player != null)
            currentMode = AIMode.Chase;
        else if (hasHeardSound)
            currentMode = AIMode.Investigate;
        else
            currentMode = AIMode.Wander;

        // 2. XỬ LÝ CHUYỂN ĐỔI TRẠNG THÁI (TRANSITIONS)
        if (currentMode != previousMode)
        {
            StopMovement();       // Dừng ngay đường cũ
            pathUpdateTimer = 0f; // Yêu cầu tính đường mới lập tức
            isWandering = false;

            if (currentMode == AIMode.Wander)
                wanderTimer = 0f; // Bắt đầu đi dạo ngay không cần đứng chờ
        }

        // 3. THỰC THI LOGIC THEO TRẠNG THÁI
        switch (currentMode)
        {
            case AIMode.Chase:
                HandleChaseState();
                break;
            case AIMode.Investigate:
                HandleInvestigateState();
                break;
            case AIMode.Wander:
                HandleWanderState();
                break;
        }
    }

    private void HandleChaseState()
    {
        float distanceToPlayer = Vector2.Distance(transform.position, player.position);
        if (attackTimer > 0) attackTimer -= Runner.DeltaTime;

        if (distanceToPlayer > attackRange) // Đang đuổi theo
        {
            pathUpdateTimer -= Runner.DeltaTime;
            if (pathUpdateTimer <= 0 && seeker.IsDone())
            {
                CalculatePath(player.position, AIMode.Chase);
                pathUpdateTimer = 0.2f;
            }

            MoveAlongPath(moveSpeed);
            NetIsRunning = true;
        }
        else // Đang trong tầm đánh
        {
            StopMovement();
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

    private void HandleInvestigateState()
    {
        pathUpdateTimer -= Runner.DeltaTime;
        if (pathUpdateTimer <= 0 && seeker.IsDone())
        {
            CalculatePath(lastHeardPosition, AIMode.Investigate);
            pathUpdateTimer = 0.2f;
        }

        MoveAlongPath(moveSpeed * 0.8f);
        NetIsRunning = true;

        if (Vector2.Distance(transform.position, lastHeardPosition) < 0.5f)
        {
            hasHeardSound = false;
            StopMovement();
        }
    }

    private void HandleWanderState()
    {
        wanderTimer -= Runner.DeltaTime;

        if (wanderTimer <= 0f && !isWandering)
        {
            // Tìm điểm ngẫu nhiên trong bán kính
            Vector2 randomDir = Random.insideUnitCircle * wanderRadius;
            Vector3 rawTarget = (Vector3)rb.position + (Vector3)randomDir;

            // TỐI ƯU: Ép điểm đến ra khỏi vật cản bằng GetNearest
            if (AstarPath.active != null)
            {
                var nearestNode = AstarPath.active.GetNearest(rawTarget, NNConstraint.Default);
                wanderTarget = nearestNode.position;
            }
            else
            {
                wanderTarget = rawTarget;
            }

            CalculatePath(wanderTarget, AIMode.Wander);
            isWandering = true;
        }

        if (isWandering)
        {
            if (path != null)
            {
                MoveAlongPath(moveSpeed * wanderSpeedMultiplier);
                NetIsRunning = true;

                if (Vector2.Distance(rb.position, wanderTarget) <= nextWaypointDistance * 2f || currentWaypoint >= path.vectorPath.Count)
                {
                    isWandering = false;
                    StopMovement();
                    NetIsRunning = false;
                    wanderTimer = Random.Range(wanderWaitTimeMin, wanderWaitTimeMax);
                }
            }
            else if (seeker.IsDone()) // Nếu A* tính xong mà vẫn ko có path -> Điểm kẹt
            {
                isWandering = false;
                StopMovement();
                wanderTimer = 1f;
            }
        }
        else
        {
            StopMovement();
            NetIsRunning = false;
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

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_HearSound(Vector3 pos)
    {
        if (!HasStateAuthority || player != null) return;
        lastHeardPosition = pos;
        hasHeardSound = true;
        hearMemoryTimer = hearMemoryDuration;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_TriggerAttack(int atkIndex)
    {
        if (anim != null)
        {
            for (int i = 1; i <= 4; i++) anim.ResetTrigger("Atk" + i);
            anim.SetTrigger("Atk" + atkIndex);
        }
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
        ExecuteDamage(currentAttackIndex == 1 ? damageAtk1 : currentAttackIndex == 2 ? damageAtk2 : currentAttackIndex == 3 ? damageAtk3 : damageAtk4, currentAttackIndex);
    }

    private void ExecuteDamage(float damageAmount, int attackIndex)
    {
        if (!HasStateAuthority || player == null) return;
        if (Vector2.Distance(transform.position, player.position) <= damageRadius)
        {
            PlayerHealth pHealth = player.GetComponent<PlayerHealth>();
            if (pHealth != null)
            {
                pHealth.TakeDamage(damageAmount, false, true);
                if (attackIndex == 2) pHealth.SetBitten();
            }
        }
    }

    public void ApplyStun(float duration) { stunTimer = duration; attackTimer = duration; }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green; Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = Color.red; Gizmos.DrawWireSphere(transform.position, damageRadius);
        Gizmos.color = Color.white; Gizmos.DrawWireSphere(transform.position, wanderRadius);
        if (hasHeardSound) { Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(lastHeardPosition, 0.3f); }
    }
}