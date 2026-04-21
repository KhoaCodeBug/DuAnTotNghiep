using UnityEngine;
using Pathfinding; // A* Pathfinding Project
using Fusion;

[RequireComponent(typeof(Rigidbody2D), typeof(Seeker))]
public class ZombieAI : NetworkBehaviour
{
    public enum AIMode { Idle, Wander, Investigate, Chase }
    private AIMode currentMode = AIMode.Idle;
    private AIMode pathRequestMode = AIMode.Idle;

    [Header("--- A* Pathfinding ---")]
    public float nextWaypointDistance = 0.5f;
    private Seeker seeker;
    private Path path;
    private int currentWaypoint = 0;
    private Rigidbody2D rb;

    [Header("--- Né Vật Cản (Local) ---")]
    [SerializeField] private LayerMask obstacleMask; // Gán layer Obstacle (Layer 6) vào đây
    [SerializeField] private float zombieRadius = 0.4f; // Bán kính vòng tròn dò tường của zombie

    [Header("--- Phạm vi Phát hiện (Detection) ---")]
    public float detectionRange = 10f;
    [Tooltip("Khoảng cách bỏ qua A* để lao thẳng vào Player nếu không có tường")]
    public float directChaseRange = 3f;

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

    // 🔊 HỆ THỐNG LẮNG NGHE
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

    [Networked] public Vector2 NetMoveDir { get; set; }
    [Networked] public NetworkBool NetIsRunning { get; set; }
    [Networked] public NetworkBool NetIsWandering { get; set; }
    [Networked] public NetworkBool NetIsChasing { get; set; }

    private void Awake()
    {
        seeker = GetComponent<Seeker>();
        rb = GetComponent<Rigidbody2D>();
    }

    public override void Spawned()
    {
        transform.rotation = Quaternion.identity;
        transform.position = new Vector3(transform.position.x, transform.position.y, 0f);

        anim = GetComponent<Animator>();
        healthScript = GetComponent<ZombieHealth>();
        wanderTimer = Random.Range(1f, 3f);

        if (!HasStateAuthority)
        {
            if (rb != null) rb.bodyType = RigidbodyType2D.Kinematic;
        }

        if (healthScript != null)
        {
            healthScript.OnStunRequested += ApplyStun;
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (healthScript != null)
        {
            healthScript.OnStunRequested -= ApplyStun;
        }
    }

    // --- HÀM KIỂM TRA TẦM NHÌN THẲNG ---
    private bool CanSeePlayer()
    {
        if (player == null) return false;

        Vector2 direction = (player.position - transform.position).normalized;
        float distance = Vector2.Distance(transform.position, player.position);

        // Bắn Raycast check vật cản
        RaycastHit2D hit = Physics2D.Raycast(transform.position, direction, distance, obstacleMask);

        // Nếu không chạm gì (null) nghĩa là nhìn thấy trực tiếp
        return hit.collider == null;
    }

    // --- HÀM TRƯỢT TƯỜNG (CẢI TIẾN) ---
    // Trả về TRUE nếu đụng tường, FALSE nếu đường đi thông thoáng
    private bool SafeMove(Vector2 targetDir, float currentSpeed)
    {
        float distanceToMove = currentSpeed * Runner.DeltaTime;

        // Bắn CircleCast dò tường phía trước
        RaycastHit2D hit = Physics2D.CircleCast(rb.position, zombieRadius, targetDir, distanceToMove, obstacleMask);

        if (hit.collider == null)
        {
            // Trống trải -> Đi bình thường
            rb.MovePosition(rb.position + targetDir * distanceToMove);
            NetMoveDir = targetDir;
            return false;
        }
        else
        {
            // Đụng tường -> Tính toán hướng trượt dọc theo mặt tường
            Vector2 slideDirection = targetDir - Vector2.Dot(targetDir, hit.normal) * hit.normal;
            slideDirection.Normalize();

            if (slideDirection.sqrMagnitude > 0.01f)
            {
                // Trượt nhẹ (giảm tốc) khi cọ vào tường
                rb.MovePosition(rb.position + slideDirection * (currentSpeed * 0.8f) * Runner.DeltaTime);
                NetMoveDir = slideDirection;
            }
            else
            {
                StopMovement();
            }
            return true; // Báo hiệu là đã va phải tường
        }
    }

    private void CalculatePath(Vector2 targetPos, AIMode mode)
    {
        if (seeker.IsDone())
        {
            pathRequestMode = mode;
            path = null;
            seeker.StartPath(rb.position, targetPos, OnPathComplete);
        }
    }

    private void OnPathComplete(Path p)
    {
        if (!p.error && currentMode == pathRequestMode)
        {
            path = p;
            currentWaypoint = 0;
            if (path.vectorPath.Count > 1)
            {
                currentWaypoint = 1;
            }
        }
    }

    // Trả về TRUE nếu bị kẹt tường trong lúc đi dọc theo A* Path
    private bool MoveAlongPath(float currentSpeed)
    {
        if (path == null || currentWaypoint >= path.vectorPath.Count) return false;

        while (currentWaypoint < path.vectorPath.Count &&
               Vector2.Distance(rb.position, path.vectorPath[currentWaypoint]) < nextWaypointDistance)
        {
            currentWaypoint++;
        }

        if (currentWaypoint >= path.vectorPath.Count) return false;

        Vector2 currentWp = (Vector2)path.vectorPath[currentWaypoint];
        Vector2 targetMoveDir = (currentWp - rb.position).normalized;

        return SafeMove(targetMoveDir, currentSpeed);
    }

    private void StopMovement()
    {
        if (rb != null) rb.linearVelocity = Vector2.zero;
        path = null;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || (healthScript != null && healthScript.isDead))
        {
            StopMovement();
            NetIsRunning = false;
            NetIsWandering = false;
            NetIsChasing = false;
            return;
        }

        if (stunTimer > 0)
        {
            stunTimer -= Runner.DeltaTime;
            StopMovement();
            NetIsRunning = false;
            NetIsWandering = false;
            NetIsChasing = false;
            return;
        }

        if (attackTimer > 0) attackTimer -= Runner.DeltaTime;

        if (hasHeardSound)
        {
            hearMemoryTimer -= Runner.DeltaTime;
            if (hearMemoryTimer <= 0) hasHeardSound = false;
        }

        if (player != null)
        {
            PlayerHealth pHealth = player.GetComponent<PlayerHealth>();
            if (pHealth == null || pHealth.isDead || !player.gameObject.activeInHierarchy) player = null;
        }

        searchTimer -= Runner.DeltaTime;
        if (searchTimer <= 0)
        {
            FindClosestPlayerInRange();
            searchTimer = 0.2f;
        }

        AIMode previousMode = currentMode;
        if (player != null) currentMode = AIMode.Chase;
        else if (hasHeardSound) currentMode = AIMode.Investigate;
        else currentMode = AIMode.Wander;

        NetIsWandering = (currentMode == AIMode.Wander);
        NetIsChasing = (currentMode == AIMode.Chase);

        if (currentMode != previousMode)
        {
            StopMovement();
            pathUpdateTimer = 0f;
            isWandering = false;
            if (currentMode == AIMode.Wander) wanderTimer = 0f;
        }

        switch (currentMode)
        {
            case AIMode.Chase: HandleChaseState(); break;
            case AIMode.Investigate: HandleInvestigateState(); break;
            case AIMode.Wander: HandleWanderState(); break;
        }
    }

    private void HandleChaseState()
    {
        float distanceToPlayer = Vector2.Distance(transform.position, player.position);

        if (distanceToPlayer > attackRange)
        {
            if (distanceToPlayer <= directChaseRange && CanSeePlayer())
            {
                Vector2 directDir = (player.position - transform.position).normalized;

                // Khi rượt ráo riết thì bỏ qua kết quả boolean, cứ ép trượt tường (SafeMove)
                SafeMove(directDir, moveSpeed);

                NetIsRunning = true;
                path = null;
            }
            else
            {
                pathUpdateTimer -= Runner.DeltaTime;
                if (pathUpdateTimer <= 0 && seeker.IsDone())
                {
                    CalculatePath(player.position, AIMode.Chase);
                    pathUpdateTimer = 0.3f;
                }
                MoveAlongPath(moveSpeed);
                NetIsRunning = true;
            }
        }
        else
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
            pathUpdateTimer = 0.5f;
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
            Vector2 randomDir = Random.insideUnitCircle * wanderRadius;
            Vector3 rawTarget = (Vector3)rb.position + (Vector3)randomDir;

            if (AstarPath.active != null)
            {
                var nearestNode = AstarPath.active.GetNearest(rawTarget, NNConstraint.Default);
                wanderTarget = nearestNode.position;
            }
            else wanderTarget = rawTarget;

            CalculatePath(wanderTarget, AIMode.Wander);
            isWandering = true;
        }

        if (isWandering)
        {
            if (path != null)
            {
                // Kiểm tra xem lúc đi dạo có lỡ đụng tường không
                bool isHittingWall = MoveAlongPath(moveSpeed * wanderSpeedMultiplier);
                NetIsRunning = true;

                // Nếu đụng tường HOẶC đi tới đích -> Lập tức hủy đường cũ, chuyển hướng mới
                if (isHittingWall || Vector2.Distance(rb.position, wanderTarget) <= nextWaypointDistance * 2f || currentWaypoint >= path.vectorPath.Count)
                {
                    isWandering = false;
                    StopMovement();
                    NetIsRunning = false;

                    // Tìm chỗ khác ngay lập tức (không cố cọ xát vào tường gây giật lag)
                    wanderTimer = Random.Range(wanderWaitTimeMin, wanderWaitTimeMax);
                }
            }
            else if (seeker.IsDone())
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
        float activeRange = (currentMode == AIMode.Chase) ? detectionRange * 1.5f : detectionRange;
        float closestDistance = activeRange;
        player = null;

        foreach (GameObject p in allPlayers)
        {
            PlayerHealth pHealth = p.GetComponent<PlayerHealth>();
            if (pHealth != null && pHealth.isDead) continue;

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
        float damage = currentAttackIndex switch { 1 => damageAtk1, 2 => damageAtk2, 3 => damageAtk3, _ => damageAtk4 };
        ExecuteDamage(damage, currentAttackIndex);
    }

    private void ExecuteDamage(float damageAmount, int attackIndex)
    {
        if (!HasStateAuthority || player == null) return;

        if (Vector2.Distance(transform.position, player.position) <= damageRadius)
        {
            PlayerHealth pHealth = player.GetComponent<PlayerHealth>();
            if (pHealth != null && !pHealth.isDead)
            {
                pHealth.TakeDamage(damageAmount, false, true);
                if (attackIndex == 2) pHealth.SetBitten();
            }
        }
    }

    public void ApplyStun(float duration)
    {
        stunTimer = duration;
        attackTimer = duration;
    }
}