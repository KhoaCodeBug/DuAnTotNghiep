using UnityEngine;
using System.Collections;
using Fusion;
using Pathfinding;

public class ZOmbieAI_Khoa : NetworkBehaviour
{
    [Header("=== Movement (A* Pathfinding) ===")]
    [SerializeField] private float speed = 2.5f;
    public float ChaseMovementSpeed => speed;
    [SerializeField] private float nextWaypointDistance = 0.5f;

    private Seeker seeker;
    private Path path;
    private int currentWaypoint = 0;
    private float pathRecalcTimer = 0f;

    [Header("=== Tracking (MỚI) ===")]
    [SerializeField] private float trackingDuration = 3f;
    private float currentTrackingTimer;

    [Header("=== Flocking (Tách bầy) ===")]
    [SerializeField] private LayerMask zombieMask;
    [SerializeField] private float separationRadius = 0.4f;
    [SerializeField] private float separationWeight = 1.5f;

    // Mảng tĩnh tối ưu RAM cho FixedUpdateNetwork
    private Collider2D[] nearbyZombies = new Collider2D[10];
    private ContactFilter2D zombieFilter; // Thêm dòng này để fix lỗi Obsolete


    [Header("=== Damage ===")]
    [SerializeField] private float zombieDamage = 10f;
    private PlayerHealth playerHealth;

    [Header("=== Vision ===")]
    [SerializeField] private float detectionRange = 5f;
    [SerializeField] private float viewAngle = 90f;
    [SerializeField, Range(90f, 360f)] private float alertViewAngle = 200f;
    [SerializeField] private float closeAwarenessRange = 2.25f;
    [SerializeField] private LayerMask obstacleMask;

    [Header("=== Hearing & Memory ===")]
    private Vector2 lastKnownPlayerPos;
    private bool isChasing;
    private bool isInvestigating;
    private Vector2 investigateTarget;
    private float investigateTimer;
    private bool isSearching;
    [SerializeField] private int searchPointCount = 3;
    [SerializeField] private float searchRadius = 2.25f;
    [SerializeField] private float searchWaitDuration = 1.1f;
    private int currentSearchPoint;
    private float searchWaitTimer;
    private Vector2 searchCenter;
    private Vector2 lastObservedPosition;
    private Vector2 lastObservedVelocity;
    private bool hasObservedTarget;

    [Header("=== Attack ===")]
    // Collider-distance: chỉ bắt đầu vung tay khi gần chạm nhau, tránh đánh hẫng từ xa.
    [SerializeField] private float attackRange = 0.12f;
    [SerializeField, Range(30f, 180f)] private float attackHitAngle = 120f;
    // Đọc trực tiếp từ clip: Atk 1 = 13 frames @12fps, Atk 2 = 15 frames @12fps.
    [SerializeField] private float attack1Duration = 1.0833334f;
    [SerializeField] private float attack2Duration = 1.25f;
    [SerializeField] private float attackCooldown = 1.5f;

    private float attackTimer;
    private float cooldownTimer;
    private bool isAttacking;
    private bool hasAppliedDamage;
    private Vector2 attackOrigin;
    private Vector2 lockedAttackDirection;
    private PlayerHealth attackTargetHealth;
    private Collider2D attackTargetCollider;

    [Header("=== Zombie Stats ===")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float stunDuration = 5f;
    [SerializeField] private Color hurtColor = Color.red;

    // CÁC BIẾN MẠNG
    [Networked] public float CurrentHealth { get; set; }
    [Networked] public NetworkBool NetIsDead { get; set; }
    [Networked] public NetworkBool NetIsAttacking { get; set; }
    [Networked] public int NetAttackIndex { get; set; }
    [Networked] public float NetSpeed { get; set; }
    [Networked] public Vector2 NetMoveDir { get; set; }
    [Networked] public NetworkBool TutorialStationary { get; set; }
    [Networked] public NetworkBool TutorialForceVisible { get; set; }

    private bool isStunned;
    private float stunTimer;

    // References
    private Transform player;
    private Collider2D playerCol;
    private Collider2D myCol;
    private Rigidbody2D rb;
    private Animator anim;
    private SpriteRenderer spriteRend;
    private Color originalColor;

    // Helpers
    private Vector2 lastMoveDirection;
    private float searchTargetTimer = 0f;
    private int pathRequestId;
    private Vector2 requestedPathTarget;
    private Vector2 lastStuckCheckPosition;
    private float stuckTimer;
    private float crowdSidePreference = 1f;
    private bool tutorialSpawnConfigured;
    private bool tutorialSpawnStationary;
    private Vector2 tutorialSpawnFacing;
    private float tutorialSpawnHealth;

    /// <summary>Called from Fusion's onBeforeSpawned callback for scripted tutorial actors.</summary>
    public void ConfigureTutorialSpawn(Vector2 facing, float health, bool stationary)
    {
        tutorialSpawnConfigured = true;
        tutorialSpawnFacing = facing.sqrMagnitude > 0.001f ? facing.normalized : Vector2.down;
        tutorialSpawnHealth = Mathf.Max(1f, health);
        tutorialSpawnStationary = stationary;
    }

    public void SetTutorialForceVisible(bool visible)
    {
        if (HasStateAuthority) TutorialForceVisible = visible;
    }

    /// <summary>
    /// Releases a staged tutorial zombie back into the same vision, hearing,
    /// pathfinding and attack loop used by normal zombies.
    /// </summary>
    public void ReleaseTutorialStationary(Vector2 alertPosition)
    {
        if (!HasStateAuthority || NetIsDead) return;

        ReleaseTutorialStationary();
        isInvestigating = true;
        isSearching = false;
        investigateTarget = alertPosition;
        investigateTimer = 3f;
        path = null;
        currentWaypoint = 0;
        pathRecalcTimer = 0f;
    }

    public void ReleaseTutorialStationary()
    {
        if (!HasStateAuthority || NetIsDead) return;
        TutorialStationary = false;
        tutorialSpawnStationary = false;
        path = null;
        currentWaypoint = 0;
        pathRecalcTimer = 0f;
    }

    public void ForceSiegeTarget(PlayerHealth targetHealth)
    {
        if (!HasStateAuthority || NetIsDead || targetHealth == null || targetHealth.isDead ||
            PlayerInteraction.IsProtectedOccupant(targetHealth)) return;
        Collider2D targetCollider = targetHealth.GetComponent<Collider2D>();
        if (targetCollider == null) return;

        TutorialStationary = false;
        tutorialSpawnStationary = false;
        player = targetHealth.transform;
        playerCol = targetCollider;
        playerHealth = targetHealth;
        lastKnownPlayerPos = targetCollider.bounds.center;
        lastObservedPosition = lastKnownPlayerPos;
        currentTrackingTimer = trackingDuration;
        isChasing = true;
        isInvestigating = false;
        isSearching = false;
        path = null;
        currentWaypoint = 0;
        pathRecalcTimer = 0f;
    }

    [Header("=== Local Avoidance ===")]
    [SerializeField] private float obstacleProbeDistance = 0.8f;
    [SerializeField] private float obstacleAvoidanceWeight = 1.8f;
    [SerializeField] private float stuckRepathDelay = 0.75f;

    // Biến làm mượt Animation
    private float smoothMoveX, smoothMoveY, smoothSpeed;
    private bool lastIsAttacking;
    private bool lastIsDead;
    private int lastAttackIndex;

    private void Awake()
    {
        myCol = GetComponent<Collider2D>();
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        spriteRend = GetComponentInChildren<SpriteRenderer>();

        seeker = GetComponent<Seeker>();

        if (spriteRend != null) originalColor = spriteRend.color;

        // Cài đặt Filter cho hàm quét bầy đàn
        zombieFilter = new ContactFilter2D();

        // THÊM DÒNG NÀY ĐỂ ZOMBIE CHỈ TÁCH NHAU RA, KHÔNG TÁCH TƯỜNG
        zombieFilter.useLayerMask = true;

        zombieFilter.SetLayerMask(zombieMask);
    }

    private void SetBodyCollisionEnabled(bool enabled)
    {
        if (myCol != null && myCol.enabled != enabled)
        {
            myCol.enabled = enabled;
        }
    }

    public override void Spawned()
    {
        // Collider2D.enabled is local-only in Fusion. Restore it when this
        // network object is spawned/reused; Render keeps it synced afterward.
        SetBodyCollisionEnabled(!NetIsDead);

        // Tutorial actors are pooled from the same prefab as normal zombies.
        // Explicitly clear the Animator's death parameter so a reused/default
        // controller state can never show the corpse pose on its first frame.
        if (anim != null)
        {
            anim.SetBool("IsDead", false);
            anim.SetBool("IsAttacking", false);
        }
        lastIsDead = false;

        if (!HasStateAuthority)
        {
            if (rb != null) rb.bodyType = RigidbodyType2D.Kinematic;
        }
        else
        {
            CurrentHealth = tutorialSpawnConfigured ? tutorialSpawnHealth : maxHealth;
            Vector2 randomDir;
            if (tutorialSpawnConfigured)
            {
                randomDir = tutorialSpawnFacing;
                TutorialStationary = tutorialSpawnStationary;
            }
            else
            {
                float randomAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                randomDir = new Vector2(Mathf.Cos(randomAngle), Mathf.Sin(randomAngle)).normalized;
            }

            // Gán hướng ngẫu nhiên này cho các biến mạng và biến helper
            NetMoveDir = randomDir;
            lastMoveDirection = randomDir;

            // Ép luôn giá trị smooth để animation quay mặt ngay lập tức, không bị "trượt" từ (0,0)
            smoothMoveX = randomDir.x;
            smoothMoveY = randomDir.y;
            lastStuckCheckPosition = rb.position;
            crowdSidePreference = Random.value < 0.5f ? -1f : 1f;
        }
    }

    private void CalculatePath(Vector2 targetPos)
    {
        if (seeker != null && seeker.IsDone())
        {
            int requestId = ++pathRequestId;
            requestedPathTarget = targetPos;
            seeker.StartPath(rb.position, targetPos, p => OnPathComplete(p, requestId, targetPos));
        }
    }

    private void OnPathComplete(Path p, int requestId, Vector2 targetAtRequest)
    {
        // A* trả path bất đồng bộ. Bỏ qua path cũ nếu mục tiêu đã đổi khi path đang tính.
        if (!p.error && requestId == pathRequestId && Vector2.Distance(targetAtRequest, requestedPathTarget) < 0.1f)
        {
            path = p;
            currentWaypoint = 0;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || NetIsDead) return;

        if (TutorialStationary)
        {
            StopMovement();
            NetMoveDir = lastMoveDirection;
            return;
        }

        // Attack là trạng thái khóa cứng: không target lại, không A*, không chase cho tới khi clip kết thúc hoàn toàn.
        if (isAttacking)
        {
            TickAttackLock();
            return;
        }

        searchTargetTimer -= Runner.DeltaTime;
        if (searchTargetTimer <= 0f)
        {
            UpdateTargetMultiplayer();
            searchTargetTimer = 0.5f;
        }

        if (stunTimer > 0f)
        {
            stunTimer -= Runner.DeltaTime;
            isStunned = stunTimer > 0f;
            if (isStunned)
            {
                StopMovement();
                return;
            }
        }

        if (cooldownTimer > 0f) cooldownTimer -= Runner.DeltaTime;

        Vector2 myPos = myCol.bounds.center;
        bool hasValidTarget = player != null && playerCol != null && playerHealth != null &&
            !playerHealth.isDead && !PlayerInteraction.IsProtectedOccupant(playerHealth);
        float distance = float.PositiveInfinity;
        Vector2 targetPos = myPos;
        Vector2 dirToPlayer = lastMoveDirection == Vector2.zero ? Vector2.up : lastMoveDirection.normalized;
        bool canSee = false;
        bool noWallInBetween = false;

        if (hasValidTarget)
        {
            ColliderDistance2D collDist = Physics2D.Distance(myCol, playerCol);
            distance = Mathf.Max(collDist.distance, 0f);
            targetPos = playerCol.bounds.center;
            dirToPlayer = (targetPos - myPos).normalized;
            noWallInBetween = HasLineOfSight(myPos, targetPos, distance);
            canSee = CanSeePlayer(distance, myPos, targetPos, dirToPlayer);

            if (canSee)
            {
                // Khi thấy thật sự, zombie ghi nhận điểm cuối và vận tốc quan sát được.
                if (hasObservedTarget)
                    lastObservedVelocity = (targetPos - lastObservedPosition) / Mathf.Max(Runner.DeltaTime, 0.001f);

                hasObservedTarget = true;
                lastObservedPosition = targetPos;
                lastKnownPlayerPos = targetPos;
                currentTrackingTimer = trackingDuration;
                isChasing = true;
                isInvestigating = false;
                isSearching = false;
            }
            else if (isChasing && currentTrackingTimer > 0f)
            {
                // Không dùng tọa độ realtime qua tường. Chỉ dự đoán ngắn theo hướng đã nhìn thấy.
                currentTrackingTimer -= Runner.DeltaTime;
                lastKnownPlayerPos = lastObservedPosition + lastObservedVelocity * Mathf.Min(trackingDuration - currentTrackingTimer, 0.75f);
            }
        }
        else if (isChasing && currentTrackingTimer > 0f)
        {
            // Nếu mục tiêu vừa bị despawn/đổi mạng, vẫn kết thúc việc truy đuổi theo trí nhớ thay vì đứng khựng lại.
            currentTrackingTimer -= Runner.DeltaTime;
        }

        if (isChasing)
        {
            if (hasValidTarget && distance <= attackRange && canSee && noWallInBetween)
            {
                StopMovement();

                if (cooldownTimer <= 0f)
                    BeginAttack();
            }
            else
            {
                if (!canSee && currentTrackingTimer <= 0f)
                {
                    isChasing = false;
                    StartSearch(lastKnownPlayerPos);
                }
                else
                {
                    RecalculatePathIfNeeded(lastKnownPlayerPos);
                    MoveAlongPath(1f, noWallInBetween);
                }
            }
        }
        else if (isInvestigating)
        {
            float distToSound = Vector2.Distance(myPos, investigateTarget);
            if (distToSound > 0.5f)
            {
                RecalculatePathIfNeeded(investigateTarget);
                MoveAlongPath(0.7f, false);
            }
            else
            {
                StopMovement();
                investigateTimer -= Runner.DeltaTime;
                if (investigateTimer <= 0f)
                {
                    isInvestigating = false;
                    StartSearch(investigateTarget);
                }
            }
        }
        else if (isSearching)
        {
            RunSearch(myPos);
        }
        else
        {
            StopMovement();
        }

        NetMoveDir = lastMoveDirection;
    }

    private void StopMovement()
    {
        rb.linearVelocity = Vector2.zero;
        NetSpeed = 0f;
    }

    private void TickAttackLock()
    {
        StopMovement();
        lastMoveDirection = lockedAttackDirection;
        NetMoveDir = lastMoveDirection;

        if (isAttacking)
        {
            attackTimer -= Runner.DeltaTime;
            if (attackTimer > 0f) return;

            isAttacking = false;
            NetIsAttacking = false;
        }
    }

    private void BeginAttack()
    {
        if (playerHealth == null || playerCol == null) return;

        attackOrigin = myCol.bounds.center;
        lockedAttackDirection = ((Vector2)playerCol.bounds.center - attackOrigin).normalized;
        if (lockedAttackDirection.sqrMagnitude < 0.001f)
            lockedAttackDirection = lastMoveDirection == Vector2.zero ? Vector2.up : lastMoveDirection.normalized;

        attackTargetHealth = playerHealth;
        attackTargetCollider = playerCol;
        lastMoveDirection = lockedAttackDirection;
        pathRequestId++;
        path = null;
        currentWaypoint = 0;
        pathRecalcTimer = 0f;
        StopMovement();

        NetAttackIndex = Random.Range(1, 3);
        NetIsAttacking = true;
        isAttacking = true;
        hasAppliedDamage = false;
        attackTimer = NetAttackIndex == 1 ? attack1Duration : attack2Duration;
        cooldownTimer = attackCooldown;
    }

    private void RecalculatePathIfNeeded(Vector2 target)
    {
        pathRecalcTimer -= Runner.DeltaTime;
        if (pathRecalcTimer <= 0f)
        {
            CalculatePath(target);
            pathRecalcTimer = 0.25f;
        }
    }

    private void StartSearch(Vector2 center)
    {
        isInvestigating = false;
        isSearching = true;
        currentSearchPoint = 0;
        searchWaitTimer = 0f;
        searchCenter = center;
        path = null;
        currentWaypoint = 0;
        pathRecalcTimer = 0f;
        AdvanceSearchPoint(center);
    }

    private void AdvanceSearchPoint(Vector2 center)
    {
        if (currentSearchPoint >= searchPointCount)
        {
            isSearching = false;
            StopMovement();
            return;
        }

        // Không biết chính xác người chơi đi đâu: kiểm tra vài điểm quanh vị trí/hướng cuối cùng nhìn thấy.
        // A* sẽ tự chọn node đi được gần nhất nếu điểm rơi trùng tường.
        investigateTarget = center + Random.insideUnitCircle * searchRadius;
        currentSearchPoint++;
        path = null;
        currentWaypoint = 0;
        pathRecalcTimer = 0f;
    }

    private void RunSearch(Vector2 myPos)
    {
        if (Vector2.Distance(myPos, investigateTarget) > nextWaypointDistance + 0.1f)
        {
            RecalculatePathIfNeeded(investigateTarget);
            MoveAlongPath(0.55f, false);
            return;
        }

        StopMovement();
        searchWaitTimer -= Runner.DeltaTime;
        if (searchWaitTimer <= 0f)
        {
            searchWaitTimer = searchWaitDuration;
            // Search sau tiếng động phải xoay quanh nguồn tiếng động, không dùng vị trí Player cũ/mặc định (0,0).
            AdvanceSearchPoint(searchCenter);
        }
    }

    // TÍNH TOÁN LỰC TÁCH BẦY
    private Vector2 GetSeparationForce()
    {
        Vector2 force = Vector2.zero;

        int count = Physics2D.OverlapCircle(rb.position, separationRadius, zombieFilter, nearbyZombies);

        int validCount = 0;
        for (int i = 0; i < count; i++)
        {
            Collider2D otherCol = nearbyZombies[i];
            if (otherCol.gameObject == gameObject) continue;

            Vector2 diff = rb.position - (Vector2)otherCol.bounds.center;
            float dist = diff.magnitude;

            if (dist > 0 && dist < separationRadius)
            {
                force += diff.normalized * (1f - (dist / separationRadius));
                validCount++;
            }
        }

        if (validCount > 0)
        {
            force /= validCount;
        }

        return force;
    }

    private Vector2 GetObstacleAvoidance(Vector2 desiredDirection)
    {
        if (desiredDirection.sqrMagnitude < 0.001f || obstacleProbeDistance <= 0f)
            return Vector2.zero;

        Vector2 forward = desiredDirection.normalized;
        RaycastHit2D frontHit = Physics2D.Raycast(rb.position, forward, obstacleProbeDistance, obstacleMask);
        if (frontHit.collider == null)
            return Vector2.zero;

        Vector2 left = new Vector2(-forward.y, forward.x);
        Vector2 right = -left;
        float diagonalDistance = obstacleProbeDistance * 0.9f;

        RaycastHit2D leftHit = Physics2D.Raycast(rb.position, (forward + left * 0.65f).normalized, diagonalDistance, obstacleMask);
        RaycastHit2D rightHit = Physics2D.Raycast(rb.position, (forward + right * 0.65f).normalized, diagonalDistance, obstacleMask);

        float leftClearance = leftHit.collider == null ? diagonalDistance : leftHit.distance;
        float rightClearance = rightHit.collider == null ? diagonalDistance : rightHit.distance;
        Vector2 steerSide = leftClearance >= rightClearance ? left : right;
        float urgency = 1f - Mathf.Clamp01(frontHit.distance / obstacleProbeDistance);
        return steerSide * urgency;
    }

    private Vector2 GetSteeredDirection(Vector2 desiredDirection)
    {
        Vector2 desired = desiredDirection.normalized;
        Vector2 separation = GetSeparationForce();

        // Khi cả bầy cùng ép từ phía trước, lực tách thuần túy sẽ triệt tiêu hướng đi và gây kẹt.
        // Đổi phần lực ngược hướng thành một bước tránh sang trái/phải ổn định cho từng zombie.
        float forwardPressure = Vector2.Dot(separation, desired);
        Vector2 lateralSeparation = separation - desired * forwardPressure;
        if (forwardPressure < 0f && lateralSeparation.sqrMagnitude < 0.001f)
            lateralSeparation = new Vector2(-desired.y, desired.x) * crowdSidePreference * -forwardPressure;

        separation = (lateralSeparation + desired * Mathf.Max(0f, forwardPressure)) * separationWeight;
        Vector2 avoidance = GetObstacleAvoidance(desiredDirection) * obstacleAvoidanceWeight;
        Vector2 result = desired + separation + avoidance;
        return result.sqrMagnitude > 0.001f ? result.normalized : desired;
    }

    private void CheckForStuck(Vector2 pathTarget)
    {
        float movedDistance = Vector2.Distance(rb.position, lastStuckCheckPosition);
        if (movedDistance < 0.025f)
        {
            stuckTimer += Runner.DeltaTime;
        }
        else
        {
            stuckTimer = 0f;
            lastStuckCheckPosition = rb.position;
        }

        if (stuckTimer >= stuckRepathDelay)
        {
            path = null;
            currentWaypoint = 0;
            pathRecalcTimer = 0f;
            CalculatePath(pathTarget);
            stuckTimer = 0f;
            lastStuckCheckPosition = rb.position;
        }
    }

    private void MoveAlongPath(float speedMultiplier, bool noWall)
    {
        bool hasReachedEnd = path == null || currentWaypoint >= path.vectorPath.Count;

        if (hasReachedEnd)
        {
            if (isChasing && playerCol != null && noWall)
            {
                Vector2 targetDir = (playerCol.bounds.center - myCol.bounds.center).normalized;
                targetDir = GetSteeredDirection(targetDir);

                lastMoveDirection = Vector2.Lerp(lastMoveDirection, targetDir, 8f * Runner.DeltaTime);

                rb.MovePosition(rb.position + lastMoveDirection * speed * speedMultiplier * Runner.DeltaTime);
                NetSpeed = speed * speedMultiplier;
                CheckForStuck(playerCol.bounds.center);
            }
            else
            {
                StopMovement();
            }
            return;
        }

        Vector2 currentWp = (Vector2)path.vectorPath[currentWaypoint];
        Vector2 targetMoveDir = (currentWp - rb.position).normalized;
        targetMoveDir = GetSteeredDirection(targetMoveDir);

        float currentSpeed = speed * speedMultiplier;

        lastMoveDirection = Vector2.Lerp(lastMoveDirection, targetMoveDir, 10f * Runner.DeltaTime);

        rb.MovePosition(rb.position + lastMoveDirection * currentSpeed * Runner.DeltaTime);
        NetSpeed = currentSpeed;
        CheckForStuck(currentWp);

        float distToWp = Vector2.Distance(rb.position, currentWp);

        if (distToWp < nextWaypointDistance)
        {
            currentWaypoint++;
        }
    }

    private bool CanSeePlayer(float distance, Vector2 myPos, Vector2 targetPos, Vector2 toPlayer)
    {
        if (distance > detectionRange || !HasLineOfSight(myPos, targetPos, distance)) return false;
        if (distance <= closeAwarenessRange) return true;

        Vector2 forward = lastMoveDirection == Vector2.zero ? Vector2.up : lastMoveDirection.normalized;
        float effectiveAngle = isChasing ? alertViewAngle : viewAngle;
        return Vector2.Angle(forward, toPlayer) <= effectiveAngle * 0.5f;
    }

    private bool HasLineOfSight(Vector2 from, Vector2 to, float distance, PlayerHealth expectedTarget = null)
    {
        if (distance <= 0.01f) return true;

        RaycastHit2D hit = Physics2D.Raycast(from, (to - from).normalized, distance, obstacleMask);
        if (hit.collider == null) return true;
        PlayerHealth targetToCheck = expectedTarget != null ? expectedTarget : playerHealth;
        return targetToCheck != null && hit.collider.GetComponentInParent<PlayerHealth>() == targetToCheck;
    }

    public override void Render()
    {
        // The Host's Collider2D.enabled change is not replicated automatically.
        // Apply the replicated death state locally on every peer.
        SetBodyCollisionEnabled(!NetIsDead);

        if (anim != null)
        {
            smoothMoveX = Mathf.Lerp(smoothMoveX, NetMoveDir.x, Time.deltaTime * 12f);
            smoothMoveY = Mathf.Lerp(smoothMoveY, NetMoveDir.y, Time.deltaTime * 12f);
            smoothSpeed = Mathf.Lerp(smoothSpeed, NetSpeed, Time.deltaTime * 15f);

            anim.SetFloat("MoveX", smoothMoveX);
            anim.SetFloat("MoveY", smoothMoveY);
            anim.SetFloat("Speed", smoothSpeed);

            if (lastIsDead != NetIsDead)
            {
                anim.SetBool("IsDead", NetIsDead);
                lastIsDead = NetIsDead;
            }

            if (NetIsAttacking && lastAttackIndex != NetAttackIndex)
            {
                anim.SetInteger("AttackIndex", NetAttackIndex);
                lastAttackIndex = NetAttackIndex;
            }

            // Index phải được set trước boolean; nếu không Animator có thể vào nhầm state trong một frame.
            if (lastIsAttacking != NetIsAttacking)
            {
                anim.SetBool("IsAttacking", NetIsAttacking);
                lastIsAttacking = NetIsAttacking;
            }
        }
    }

    private void UpdateTargetMultiplayer()
    {
        // Chỉ đổi mục tiêu khi thật sự nhìn/thấy gần. Tránh việc zombie tự biết người chơi xa nhất ở đâu.
        GameObject[] allPlayers = GameObject.FindGameObjectsWithTag("Player");
        Vector2 myPos = myCol != null ? myCol.bounds.center : (Vector2)transform.position;
        float bestScore = float.NegativeInfinity;
        GameObject bestCandidate = null;

        foreach (GameObject p in allPlayers)
        {
            if (p.TryGetComponent(out Skill_StealthCrouch stealth) && stealth.IsInvisible) continue;

            if (!p.TryGetComponent(out PlayerHealth pHealth) || pHealth.Object == null || !pHealth.Object.IsValid || pHealth.isDead)
                continue;
            if (PlayerInteraction.IsProtectedOccupant(pHealth)) continue;

            Collider2D candidateCol = p.GetComponent<Collider2D>();
            if (candidateCol == null) continue;

            Vector2 candidatePos = candidateCol.bounds.center;
            float dist = Vector2.Distance(myPos, candidatePos);
            bool isCurrentTarget = player == p.transform;
            bool lineOfSight = dist <= detectionRange && HasLineOfSight(myPos, candidatePos, dist, pHealth);
            bool closeAwareness = dist <= closeAwarenessRange;
            Vector2 forward = lastMoveDirection == Vector2.zero ? Vector2.up : lastMoveDirection.normalized;
            float effectiveAngle = isChasing ? alertViewAngle : viewAngle;
            bool inView = closeAwareness || Vector2.Angle(forward, (candidatePos - myPos).normalized) <= effectiveAngle * 0.5f;

            // Mục tiêu hiện tại được giữ trong lúc đang truy đuổi để không đổi qua lại giữa nhiều người chơi.
            // Mục tiêu mới phải nằm trong vùng nhìn hoặc rất gần; tiếng động sẽ đi qua RPC_HearSound riêng.
            if (!isCurrentTarget && (!lineOfSight || !inView)) continue;

            float score = -dist + (lineOfSight && inView ? 1000f : 0f) + (isCurrentTarget ? 25f : 0f);
            if (score > bestScore)
            {
                bestScore = score;
                bestCandidate = p;
            }
        }

        if (bestCandidate != null)
        {
            player = bestCandidate.transform;
            playerCol = bestCandidate.GetComponent<Collider2D>();
            playerHealth = bestCandidate.GetComponent<PlayerHealth>();
        }
        else if (player != null && player.TryGetComponent(out Skill_StealthCrouch currentStealth) && currentStealth.IsInvisible)
        {
            // Tàng hình cắt đứt nhận diện mới, nhưng zombie vẫn được phép kiểm tra điểm mất dấu cuối.
            player = null;
            playerCol = null;
            playerHealth = null;
            if (isChasing)
            {
                isChasing = false;
                StartSearch(lastKnownPlayerPos);
            }
        }
        else if (player != null && (playerHealth == null || playerHealth.Object == null || !playerHealth.Object.IsValid ||
            playerHealth.isDead || PlayerInteraction.IsProtectedOccupant(playerHealth)))
        {
            player = null;
            playerCol = null;
            playerHealth = null;
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_HearSound(Vector2 soundPos)
    {
        if (NetIsDead) return;

        // Khi đang dí theo một người, tiếng động chỉ cập nhật hướng lần cuối thay vì làm zombie quên mục tiêu.
        if (isChasing && currentTrackingTimer > 0f)
        {
            lastKnownPlayerPos = soundPos;
            return;
        }

        isInvestigating = true;
        isSearching = false;
        investigateTarget = soundPos;
        investigateTimer = 3f;

        pathRecalcTimer = 0f;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_TakeDamage(float damage, PlayerRef shooter = default)
    {
        if (NetIsDead) return;

        CurrentHealth -= damage;
        CurrentHealth = Mathf.Clamp(CurrentHealth, 0f, maxHealth);

        if (CurrentHealth <= 0f)
        {
            Die(shooter);
            return;
        }

        stunTimer = stunDuration;
        isStunned = true;
        isAttacking = false;
        NetIsAttacking = false;
        hasAppliedDamage = true;
        attackTargetHealth = null;
        attackTargetCollider = null;
        StopMovement();

        RPC_PlayHitEffect();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_PlayHitEffect()
    {
        if (anim != null) anim.SetTrigger("TakeDamage");
        if (spriteRend != null)
        {
            StopCoroutine(FlashRedRoutine());
            StartCoroutine(FlashRedRoutine());
        }
    }

    private IEnumerator FlashRedRoutine()
    {
        spriteRend.color = hurtColor;
        yield return new WaitForSeconds(0.12f);
        if (!NetIsDead) spriteRend.color = originalColor;
    }

    private void Die(PlayerRef shooter)
    {
        if (NetIsDead) return;
        float deathAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        NetMoveDir = new Vector2(Mathf.Cos(deathAngle), Mathf.Sin(deathAngle));
        NetIsDead = true;

        StopMovement();
        SetBodyCollisionEnabled(false);

        if (shooter != PlayerRef.None)
        {
            Skill_WeaponMaster[] allWeaponMasters = FindObjectsByType<Skill_WeaponMaster>(FindObjectsSortMode.None);

            foreach (var master in allWeaponMasters)
            {
                if (master.Object != null && master.Object.InputAuthority == shooter)
                {
                    master.AddKill();
                    break;
                }
            }
        }

        GetComponent<ZombieCorpseLoot>()?.MarkAsCorpse();
        StartCoroutine(VanishRoutine());
    }

    private IEnumerator VanishRoutine()
    {
        if (GetComponent<ZombieCorpseLoot>() != null) yield break;
        yield return new WaitForSeconds(5f);
        if (HasStateAuthority) Runner.Despawn(Object);
    }

    public void TriggerAttackDamage()
    {
        if (!HasStateAuthority) return;

        // Event animation bị gọi muộn sau khi đòn đã kết thúc sẽ không thể gây sát thương.
        if (!isAttacking || !NetIsAttacking || hasAppliedDamage || NetIsDead || attackTargetHealth == null || attackTargetCollider == null) return;

        Vector2 from = myCol.bounds.center;
        Vector2 to = attackTargetCollider.bounds.center;
        ColliderDistance2D hitDistance = Physics2D.Distance(myCol, attackTargetCollider);
        float currentDist = Mathf.Max(hitDistance.distance, 0f);
        Vector2 directionFromAttackOrigin = (to - attackOrigin).normalized;

        // Dùng đúng khoảng cách giữa collider như điều kiện bắt đầu animation, tránh đánh trúng từ xa.
        if (currentDist <= attackRange
            && HasLineOfSight(from, to, currentDist, attackTargetHealth)
            && Vector2.Angle(lockedAttackDirection, directionFromAttackOrigin) <= attackHitAngle * 0.5f)
        {
            attackTargetHealth.TakeDamage(zombieDamage, false, true);
            hasAppliedDamage = true;
        }
    }
}
