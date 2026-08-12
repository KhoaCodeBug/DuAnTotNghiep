using UnityEngine;
using Pathfinding; // A* Pathfinding Project
using Fusion;

[RequireComponent(typeof(Rigidbody2D), typeof(Seeker))]
public class ZombieAI : NetworkBehaviour
{
    // Bỏ Wander, chỉ còn 3 trạng thái
    public enum AIMode { Idle, Investigate, Search, Chase }
    private AIMode currentMode = AIMode.Idle;
    private AIMode pathRequestMode = AIMode.Idle;

    [Header("--- A* Pathfinding ---")]
    public float nextWaypointDistance = 0.5f;
    private Seeker seeker;
    private Path path;
    private int currentWaypoint = 0;
    private Rigidbody2D rb;
    private int pathRequestId = 0;
    private Vector2 requestedPathTarget;
    private Vector2 lastMoveDirection;
    private Vector2 lastStuckCheckPosition;
    private float stuckTimer = 0f;
    private float crowdSidePreference = 1f;
    [SerializeField] private LayerMask zombieMask;
    [SerializeField] private float separationRadius = 0.4f;
    [SerializeField] private float separationWeight = 1.5f;
    private readonly Collider2D[] nearbyZombies = new Collider2D[10];
    private ContactFilter2D zombieFilter;

    [Header("--- Né Vật Cản (Local) ---")]
    [SerializeField] private LayerMask obstacleMask; // Gán layer Obstacle (Layer 6) vào đây
    [SerializeField] private float zombieRadius = 0.4f; // Bán kính vòng tròn dò tường của zombie
    [SerializeField] private float obstacleProbeDistance = 0.8f;
    [SerializeField] private float obstacleAvoidanceWeight = 1.8f;
    [SerializeField] private float stuckRepathDelay = 0.75f;

    [Header("--- Cảm nhận cự ly gần (Zombie mù) ---")]
    [SerializeField] private float closeAwarenessRange = 0.6f;

    [Header("--- Search Memory ---")]
    [SerializeField] private int searchPointCount = 3;
    [SerializeField] private float searchRadius = 2f;
    [SerializeField] private float searchWaitDuration = 1f;
    [SerializeField] private float hearingRangeMultiplier = 2f;
    [SerializeField] private float hearingInvestigateSpeedMultiplier = 1.2f;
    [SerializeField, Range(0f, 1f)] private float loudHearingUrgencyThreshold = 0.75f;
    [SerializeField] private float quietHearingSpeedMultiplier = 0.65f;
    [SerializeField] private float normalHearingSpeedMultiplier = 1f;
    [SerializeField] private float quietSoundPositionUncertainty = 0.75f;
    [SerializeField] private float soundRepathDistance = 0.35f;

    [Header("--- Tấn công & Tốc độ ---")]
    public float moveSpeed = 2.5f;
    [SerializeField] private float chaseSpeedMultiplier = 1.7f;
    public float attackRange = 0.12f;
    public float damageRadius = 0.12f;
    public float attackCooldown = 1.5f;
    [SerializeField] private float attackCommitDuration = 1.25f;
    [SerializeField, Range(30f, 180f)] private float attackHitAngle = 120f;

    [Header("--- Sát thương các chiêu ---")]
    public float damageAtk1 = 10f;
    public float damageAtk2 = 15f;
    public float damageAtk3 = 20f;
    public float damageAtk4 = 30f;

    // 🔊 HỆ THỐNG LẮNG NGHE
    private Vector3 lastHeardPosition;
    private bool hasHeardSound = false;
    private float hearMemoryTimer = 0f;
    private float lastHeardUrgency = 1f;
    private PlayerRef heardPlayerRef = PlayerRef.None;
    public float hearMemoryDuration = 3f;
    public float HearingRangeMultiplier => Mathf.Max(1f, hearingRangeMultiplier);

    private Transform player;
    private PlayerHealth playerHealth;
    private Collider2D playerCollider;
    private Animator anim;
    private ZombieHealth healthScript;

    private float attackTimer = 0f;
    private float searchTimer = 0f;
    private float stunTimer = 0f;
    private float pathUpdateTimer = 0f;

    private int currentAttackIndex = 1;
    private bool hasDealtDamageThisAttack = false;
    private bool isAttackLocked = false;
    private float attackCommitTimer = 0f;
    private Vector2 attackLockedPosition;
    private Vector2 attackOrigin;
    private Vector2 lockedAttackDirection;
    private PlayerHealth attackTargetHealth;
    private Collider2D attackTargetCollider;

    private bool isSearching = false;
    private Vector2 searchCenter;
    private Vector2 currentSearchTarget;
    private int currentSearchPoint = 0;
    private float searchWaitTimer = 0f;
    private float smoothMoveSpeed = 0f;

    [Networked] public Vector2 NetMoveDir { get; set; }
    [Networked] public NetworkBool NetIsRunning { get; set; }
    [Networked] public NetworkBool NetIsChasing { get; set; }
    [Networked] public float NetMoveSpeed { get; set; }

    private void Awake()
    {
        seeker = GetComponent<Seeker>();
        rb = GetComponent<Rigidbody2D>();

        zombieFilter = new ContactFilter2D();
        zombieFilter.useLayerMask = true;
        zombieFilter.SetLayerMask(zombieMask);
    }

    public override void Spawned()
    {
        transform.rotation = Quaternion.identity;
        transform.position = new Vector3(transform.position.x, transform.position.y, 0f);

        anim = GetComponent<Animator>();
        healthScript = GetComponent<ZombieHealth>();
        lastMoveDirection = NetMoveDir == Vector2.zero ? Random.insideUnitCircle.normalized : NetMoveDir.normalized;
        if (lastMoveDirection.sqrMagnitude < 0.001f) lastMoveDirection = Vector2.up;
        NetMoveDir = lastMoveDirection;
        lastStuckCheckPosition = rb != null ? rb.position : (Vector2)transform.position;
        crowdSidePreference = Random.value < 0.5f ? -1f : 1f;

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

    // --- HÀM TRƯỢT TƯỜNG (CẢI TIẾN) ---
    private bool SafeMove(Vector2 targetDir, float currentSpeed, float maxMoveDistance = float.PositiveInfinity)
    {
        targetDir = GetSteeredDirection(targetDir);
        float distanceToMove = Mathf.Min(currentSpeed * Runner.DeltaTime, maxMoveDistance);
        if (distanceToMove <= 0f)
        {
            StopMovement();
            return false;
        }

        RaycastHit2D hit = Physics2D.CircleCast(rb.position, zombieRadius, targetDir, distanceToMove, obstacleMask);

        if (hit.collider == null)
        {
            lastMoveDirection = Vector2.Lerp(lastMoveDirection, targetDir, 10f * Runner.DeltaTime);
            if (lastMoveDirection.sqrMagnitude < 0.001f) lastMoveDirection = targetDir;
            lastMoveDirection.Normalize();
            rb.MovePosition(rb.position + lastMoveDirection * distanceToMove);
            NetMoveDir = lastMoveDirection;
            NetMoveSpeed = currentSpeed;
            return true;
        }
        else
        {
            Vector2 slideDirection = targetDir - Vector2.Dot(targetDir, hit.normal) * hit.normal;
            slideDirection.Normalize();

            if (slideDirection.sqrMagnitude > 0.01f)
            {
                float slideDistance = Mathf.Min((currentSpeed * 0.8f) * Runner.DeltaTime, maxMoveDistance);
                lastMoveDirection = Vector2.Lerp(lastMoveDirection, slideDirection, 10f * Runner.DeltaTime);
                if (lastMoveDirection.sqrMagnitude < 0.001f) lastMoveDirection = slideDirection;
                lastMoveDirection.Normalize();
                rb.MovePosition(rb.position + lastMoveDirection * slideDistance);
                NetMoveDir = lastMoveDirection;
                NetMoveSpeed = currentSpeed * 0.8f;
                return true;
            }
            else
            {
                StopMovement();
            }
            return false;
        }
    }

    private void CalculatePath(Vector2 targetPos, AIMode mode)
    {
        if (seeker != null && seeker.IsDone())
        {
            pathRequestMode = mode;
            int requestId = ++pathRequestId;
            requestedPathTarget = targetPos;
            path = null;
            seeker.StartPath(rb.position, targetPos, p => OnPathComplete(p, requestId, targetPos));
        }
    }

    private void OnPathComplete(Path p, int requestId, Vector2 targetAtRequest)
    {
        if (!p.error
            && currentMode == pathRequestMode
            && requestId == pathRequestId
            && Vector2.Distance(targetAtRequest, requestedPathTarget) < 0.1f)
        {
            path = p;
            currentWaypoint = 0;
            if (path.vectorPath.Count > 1)
            {
                currentWaypoint = 1;
            }
        }
    }

    private bool MoveAlongPath(float currentSpeed, bool allowDirectChaseFallback = false)
    {
        if (path == null || currentWaypoint >= path.vectorPath.Count)
        {
            if (allowDirectChaseFallback && HasValidTarget())
            {
                Vector2 targetDirection = (requestedPathTarget - rb.position).normalized;
                return SafeMove(targetDirection, currentSpeed);
            }

            NetMoveSpeed = 0f;
            return false;
        }

        while (currentWaypoint < path.vectorPath.Count &&
               Vector2.Distance(rb.position, path.vectorPath[currentWaypoint]) < nextWaypointDistance)
        {
            currentWaypoint++;
        }

        if (currentWaypoint >= path.vectorPath.Count)
        {
            NetMoveSpeed = 0f;
            return false;
        }

        Vector2 currentWp = (Vector2)path.vectorPath[currentWaypoint];
        Vector2 targetMoveDir = (currentWp - rb.position).normalized;

        return SafeMove(targetMoveDir, currentSpeed);
    }

    private Vector2 GetSeparationForce()
    {
        if (separationRadius <= 0f) return Vector2.zero;

        Vector2 force = Vector2.zero;
        int count = Physics2D.OverlapCircle(rb.position, separationRadius, zombieFilter, nearbyZombies);
        int validCount = 0;

        for (int i = 0; i < count; i++)
        {
            Collider2D otherCol = nearbyZombies[i];
            if (otherCol == null || otherCol.gameObject == gameObject) continue;

            Vector2 diff = rb.position - (Vector2)otherCol.bounds.center;
            float dist = diff.magnitude;
            if (dist <= 0f || dist >= separationRadius) continue;

            force += diff.normalized * (1f - dist / separationRadius);
            validCount++;
        }

        return validCount > 0 ? force / validCount : Vector2.zero;
    }

    private Vector2 GetSteeredDirection(Vector2 desiredDirection)
    {
        if (desiredDirection.sqrMagnitude < 0.001f) return desiredDirection;

        Vector2 desired = desiredDirection.normalized;
        Vector2 separation = GetSeparationForce();

        float forwardPressure = Vector2.Dot(separation, desired);
        Vector2 lateralSeparation = separation - desired * forwardPressure;
        if (forwardPressure < 0f && lateralSeparation.sqrMagnitude < 0.001f)
        {
            lateralSeparation = new Vector2(-desired.y, desired.x) * crowdSidePreference * -forwardPressure;
        }

        separation = (lateralSeparation + desired * Mathf.Max(0f, forwardPressure)) * separationWeight;
        Vector2 avoidance = GetObstacleAvoidance(desired) * obstacleAvoidanceWeight;
        Vector2 result = desired + separation + avoidance;
        return result.sqrMagnitude > 0.001f ? result.normalized : desired;
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
            pathUpdateTimer = 0f;
            CalculatePath(pathTarget, currentMode);
            stuckTimer = 0f;
            lastStuckCheckPosition = rb.position;
        }
    }

    private void StopMovement()
    {
        if (rb != null) rb.linearVelocity = Vector2.zero;
        path = null;
        NetMoveSpeed = 0f;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || (healthScript != null && healthScript.isDead))
        {
            StopMovement();
            NetIsRunning = false;
            NetIsChasing = false;
            return;
        }

        if (stunTimer > 0)
        {
            stunTimer -= Runner.DeltaTime;
            StopMovement();
            NetIsRunning = false;
            NetIsChasing = false;
            return;
        }

        if (attackTimer > 0) attackTimer -= Runner.DeltaTime;

        if (hasHeardSound)
        {
            hearMemoryTimer -= Runner.DeltaTime;
            if (hearMemoryTimer <= 0f)
            {
                Vector2 expiredSoundPosition = lastHeardPosition;
                hasHeardSound = false;
                heardPlayerRef = PlayerRef.None;

                if (!isAttackLocked)
                {
                    ClearTarget(false);
                    StartSearch(expiredSoundPosition);
                }
            }
        }

        if (player != null)
        {
            PlayerHealth pHealth = player.GetComponent<PlayerHealth>();
            if (pHealth == null || pHealth.isDead || !player.gameObject.activeInHierarchy)
            {
                ClearTarget(false);
            }
        }

        if (HandleCommittedAttack()) return;

        searchTimer -= Runner.DeltaTime;
        if (searchTimer <= 0)
        {
            FindPlayerInCloseAwareness();
            searchTimer = 0.2f;
        }

        AIMode previousMode = currentMode;

        if (HasValidTarget() && (hasHeardSound || GetColliderDistance(playerCollider) <= closeAwarenessRange))
            currentMode = AIMode.Chase;
        else if (hasHeardSound)
            currentMode = AIMode.Investigate;
        else if (isSearching) currentMode = AIMode.Search;
        else currentMode = AIMode.Idle;

        NetIsChasing = (currentMode == AIMode.Chase);

        if (currentMode != previousMode)
        {
            StopMovement();
            pathUpdateTimer = 0f;
        }

        switch (currentMode)
        {
            case AIMode.Chase: HandleChaseState(); break;
            case AIMode.Investigate: HandleInvestigateState(); break;
            case AIMode.Search: HandleSearchState(); break;
            case AIMode.Idle: HandleIdleState(); break; // Thay thế Wander bằng Idle
        }
    }

    private void HandleIdleState()
    {
        // Khi đứng yên thì không chạy và không di chuyển
        StopMovement();
        NetIsRunning = false;
    }

    private void HandleChaseState()
    {
        if (!HasValidTarget())
        {
            ClearTarget(false);
            return;
        }

        Vector2 targetPos = playerCollider.bounds.center;
        Collider2D myCollider = GetComponent<Collider2D>();
        ColliderDistance2D colliderDistance = myCollider != null
            ? Physics2D.Distance(myCollider, playerCollider)
            : default;
        float distanceToPlayer = myCollider != null
            ? Mathf.Max(colliderDistance.distance, 0f)
            : Vector2.Distance(rb.position, targetPos);
        float attackStartRange = GetEffectiveAttackRange();

        if (distanceToPlayer <= closeAwarenessRange)
        {
            lastHeardPosition = targetPos;
            hasHeardSound = true;
            hearMemoryTimer = Mathf.Max(hearMemoryTimer, 0.5f);
        }

        if (distanceToPlayer > attackStartRange)
        {
            Vector2 chaseDestination = lastHeardPosition;
            pathUpdateTimer -= Runner.DeltaTime;
            if (pathUpdateTimer <= 0 && seeker.IsDone())
            {
                CalculatePath(chaseDestination, AIMode.Chase);
                pathUpdateTimer = 0.25f;
            }

            bool moved = MoveAlongPath(moveSpeed * chaseSpeedMultiplier, true);
            CheckForStuck(chaseDestination);
            NetIsRunning = moved;

            if (Vector2.Distance(rb.position, chaseDestination) < 0.5f && distanceToPlayer > closeAwarenessRange)
            {
                hasHeardSound = false;
                heardPlayerRef = PlayerRef.None;
                ClearTarget(false);
                StartSearch(chaseDestination);
            }
        }
        else
        {
            StopMovement();
            NetIsRunning = false;
            NetMoveDir = (targetPos - rb.position).normalized;

            if (attackTimer <= 0 && HasLineOfSight(rb.position, targetPos, Vector2.Distance(rb.position, targetPos), playerHealth))
            {
                StartAttack();
            }
        }
    }

    private float GetColliderDistance(Collider2D targetCollider)
    {
        if (targetCollider == null) return float.PositiveInfinity;

        Collider2D myCollider = GetComponent<Collider2D>();
        if (myCollider != null)
        {
            return Mathf.Max(Physics2D.Distance(myCollider, targetCollider).distance, 0f);
        }

        Vector2 myPos = rb != null ? rb.position : (Vector2)transform.position;
        return Vector2.Distance(myPos, targetCollider.bounds.center);
    }

    private bool HandleCommittedAttack()
    {
        if (!isAttackLocked) return false;

        attackCommitTimer -= Runner.DeltaTime;
        StopMovement();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.MovePosition(attackLockedPosition);
        }
        NetIsRunning = false;
        NetIsChasing = player != null;

        lastMoveDirection = lockedAttackDirection;
        NetMoveDir = lockedAttackDirection;

        if (attackCommitTimer <= 0f)
        {
            isAttackLocked = false;
            attackTargetHealth = null;
            attackTargetCollider = null;

            if (!hasHeardSound && GetColliderDistance(playerCollider) > closeAwarenessRange)
            {
                Vector2 searchPosition = lastHeardPosition;
                ClearTarget(false);
                StartSearch(searchPosition);
            }
            else if (hasHeardSound)
            {
                TryBindPlayerFromSound(heardPlayerRef);
            }
        }

        return true;
    }

    private float GetEffectiveAttackRange()
    {
        return Mathf.Max(attackRange, damageRadius);
    }

    private void StartAttack()
    {
        if (!HasValidTarget()) return;

        PlayerHealth selectedHealth = playerHealth;
        Collider2D selectedCollider = playerCollider;
        if (selectedHealth == null || selectedCollider == null) return;

        int randomAtk = Random.Range(1, 5);
        currentAttackIndex = randomAtk;
        hasDealtDamageThisAttack = false;
        isAttackLocked = true;
        attackCommitTimer = attackCommitDuration;
        attackOrigin = rb != null ? rb.position : (Vector2)transform.position;
        attackLockedPosition = attackOrigin;
        attackTargetHealth = selectedHealth;
        attackTargetCollider = selectedCollider;
        lockedAttackDirection = ((Vector2)selectedCollider.bounds.center - attackOrigin).normalized;
        if (lockedAttackDirection.sqrMagnitude < 0.001f)
        {
            lockedAttackDirection = NetMoveDir == Vector2.zero ? Vector2.up : NetMoveDir.normalized;
        }
        lastMoveDirection = lockedAttackDirection;
        NetMoveDir = lockedAttackDirection;
        pathRequestId++;
        path = null;
        currentWaypoint = 0;
        pathUpdateTimer = 0f;
        StopMovement();
        NetIsRunning = false;
        NetIsChasing = true;
        RPC_TriggerAttack(randomAtk);
        attackTimer = attackCooldown;
    }

    private void HandleInvestigateState()
    {
        Vector2 investigatePos = lastHeardPosition;

        pathUpdateTimer -= Runner.DeltaTime;
        if (pathUpdateTimer <= 0 && seeker.IsDone())
        {
            CalculatePath(investigatePos, AIMode.Investigate);
            pathUpdateTimer = 0.5f;
        }

        float investigateSpeed = hasHeardSound
            ? moveSpeed * GetHearingInvestigateSpeedMultiplier()
            : moveSpeed * 0.8f;
        bool moved = MoveAlongPath(investigateSpeed);
        CheckForStuck(investigatePos);
        NetIsRunning = moved;

        if (Vector2.Distance(transform.position, investigatePos) < 0.5f)
        {
            hasHeardSound = false;
            heardPlayerRef = PlayerRef.None;
            StopMovement();
            StartSearch(investigatePos);
        }
    }

    private void StartSearch(Vector2 center)
    {
        isSearching = true;
        searchCenter = center;
        currentSearchPoint = 0;
        searchWaitTimer = 0f;
        AdvanceSearchPoint();
    }

    private float GetHearingInvestigateSpeedMultiplier()
    {
        if (lastHeardUrgency >= loudHearingUrgencyThreshold)
        {
            return hearingInvestigateSpeedMultiplier;
        }

        float t = loudHearingUrgencyThreshold <= 0f
            ? 1f
            : Mathf.Clamp01(lastHeardUrgency / loudHearingUrgencyThreshold);
        return Mathf.Lerp(quietHearingSpeedMultiplier, normalHearingSpeedMultiplier, t);
    }

    private void AdvanceSearchPoint()
    {
        if (currentSearchPoint >= searchPointCount)
        {
            isSearching = false;
            StopMovement();
            return;
        }

        currentSearchTarget = searchCenter + Random.insideUnitCircle * searchRadius;
        currentSearchPoint++;
        path = null;
        currentWaypoint = 0;
        pathUpdateTimer = 0f;
    }

    private void HandleSearchState()
    {
        if (!isSearching)
        {
            StopMovement();
            NetIsRunning = false;
            return;
        }

        if (Vector2.Distance(rb.position, currentSearchTarget) > nextWaypointDistance + 0.1f)
        {
            pathUpdateTimer -= Runner.DeltaTime;
            if (pathUpdateTimer <= 0 && seeker.IsDone())
            {
                CalculatePath(currentSearchTarget, AIMode.Search);
                pathUpdateTimer = 0.5f;
            }

            bool moved = MoveAlongPath(moveSpeed * 0.55f);
            CheckForStuck(currentSearchTarget);
            NetIsRunning = moved;
            return;
        }

        StopMovement();
        NetIsRunning = false;
        searchWaitTimer -= Runner.DeltaTime;
        if (searchWaitTimer <= 0f)
        {
            searchWaitTimer = searchWaitDuration;
            AdvanceSearchPoint();
        }
    }

    public override void Render()
    {
        if (anim == null) return;
        float targetSpeed = NetIsRunning ? NetMoveSpeed : 0f;
        smoothMoveSpeed = Mathf.Lerp(smoothMoveSpeed, targetSpeed, Time.deltaTime * 15f);
        if (smoothMoveSpeed < 0.03f) smoothMoveSpeed = 0f;

        anim.SetFloat("Speed", smoothMoveSpeed);
        if (NetMoveDir != Vector2.zero)
        {
            anim.SetFloat("DirX", NetMoveDir.x);
            anim.SetFloat("DirY", NetMoveDir.y);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_HearSound(Vector3 pos)
    {
        if (!HasStateAuthority) return;

        ApplyHeardSound(pos, 1f, PlayerRef.None);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_HearSoundWithUrgency(Vector3 pos, float urgency, PlayerRef sourcePlayer)
    {
        if (!HasStateAuthority) return;

        ApplyHeardSound(pos, urgency, sourcePlayer);
    }

    private void ApplyHeardSound(Vector3 pos, float urgency, PlayerRef sourcePlayer)
    {
        lastHeardUrgency = Mathf.Clamp01(urgency);
        float uncertainty = quietSoundPositionUncertainty * (1f - lastHeardUrgency);
        Vector2 offset = uncertainty > 0f ? Random.insideUnitCircle * uncertainty : Vector2.zero;
        Vector2 newHeardPosition = pos + (Vector3)offset;
        bool destinationChanged = Vector2.Distance(lastHeardPosition, newHeardPosition) >= soundRepathDistance;

        lastHeardPosition = newHeardPosition;
        hasHeardSound = true;
        hearMemoryTimer = hearMemoryDuration;
        isSearching = false;
        if (sourcePlayer != PlayerRef.None) heardPlayerRef = sourcePlayer;
        TryBindPlayerFromSound(sourcePlayer);

        if (!isAttackLocked)
        {
            currentMode = HasValidTarget() ? AIMode.Chase : AIMode.Investigate;
            if (destinationChanged)
            {
                pathRequestId++;
                pathUpdateTimer = 0f;
            }
        }
    }

    private void TryBindPlayerFromSound(PlayerRef sourcePlayer)
    {
        if (sourcePlayer == PlayerRef.None) return;
        if (HasValidTarget()
            && playerHealth.Object != null
            && playerHealth.Object.IsValid
            && playerHealth.Object.InputAuthority == sourcePlayer)
            return;
        if (isAttackLocked && attackTargetHealth != null) return;

        GameObject[] allPlayers = GameObject.FindGameObjectsWithTag("Player");
        foreach (GameObject candidate in allPlayers)
        {
            PlayerHealth candidateHealth = candidate.GetComponent<PlayerHealth>();
            if (candidateHealth == null || candidateHealth.Object == null || !candidateHealth.Object.IsValid) continue;
            if (candidateHealth.Object.InputAuthority != sourcePlayer || candidateHealth.isDead) continue;

            Collider2D candidateCollider = GetMainTargetCollider(candidate);
            if (candidateCollider == null) return;

            player = candidate.transform;
            playerHealth = candidateHealth;
            playerCollider = candidateCollider;
            heardPlayerRef = sourcePlayer;
            return;
        }
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

    private void FindPlayerInCloseAwareness()
    {
        GameObject[] allPlayers = GameObject.FindGameObjectsWithTag("Player");
        float bestDistance = closeAwarenessRange;
        GameObject bestCandidate = null;
        PlayerHealth bestHealth = null;
        Collider2D bestCollider = null;
        Vector2 myPos = rb != null ? rb.position : (Vector2)transform.position;

        foreach (GameObject p in allPlayers)
        {
            PlayerHealth pHealth = p.GetComponent<PlayerHealth>();
            if (pHealth == null || pHealth.Object == null || !pHealth.Object.IsValid || pHealth.isDead) continue;

            Collider2D candidateCollider = GetMainTargetCollider(p);
            if (candidateCollider == null) continue;

            float distance = GetColliderDistance(candidateCollider);
            Vector2 targetPos = candidateCollider.bounds.center;
            if (distance <= bestDistance && HasLineOfSight(myPos, targetPos, Vector2.Distance(myPos, targetPos), pHealth))
            {
                bestDistance = distance;
                bestCandidate = p;
                bestHealth = pHealth;
                bestCollider = candidateCollider;
            }
        }

        if (bestCandidate != null)
        {
            player = bestCandidate.transform;
            playerHealth = bestHealth;
            playerCollider = bestCollider;
            heardPlayerRef = bestHealth.Object.InputAuthority;
            lastHeardPosition = bestCollider.bounds.center;
            hasHeardSound = true;
            hearMemoryTimer = Mathf.Max(hearMemoryTimer, 0.5f);
            isSearching = false;
        }
    }

    public void DealDamage()
    {
        ExecuteDamage(GetAttackDamage(currentAttackIndex), currentAttackIndex);
    }

    public void DealDamage(int attackIndexFromAnimation)
    {
        int attackIndex = attackIndexFromAnimation >= 1 && attackIndexFromAnimation <= 4
            ? attackIndexFromAnimation
            : currentAttackIndex;
        ExecuteDamage(GetAttackDamage(attackIndex), attackIndex);
    }

    private float GetAttackDamage(int attackIndex)
    {
        return attackIndex switch { 1 => damageAtk1, 2 => damageAtk2, 3 => damageAtk3, _ => damageAtk4 };
    }

    private void ExecuteDamage(float damageAmount, int attackIndex)
    {
        if (hasDealtDamageThisAttack) return;
        if (!HasStateAuthority) return;

        if (TryGetPlayerInDamageRange(out PlayerHealth pHealth))
        {
            if (!pHealth.isDead)
            {
                pHealth.TakeDamage(damageAmount, false, true);
                hasDealtDamageThisAttack = true;
                if (attackIndex == 2) pHealth.SetBitten();
            }
        }
    }

    private bool TryGetPlayerInDamageRange(out PlayerHealth pHealth)
    {
        pHealth = null;
        PlayerHealth targetHealth = attackTargetHealth != null ? attackTargetHealth : playerHealth;
        Collider2D targetCollider = attackTargetCollider != null ? attackTargetCollider : playerCollider;
        if (targetHealth == null || targetCollider == null) return false;
        if (targetHealth.isDead || !targetCollider.enabled || !targetCollider.gameObject.activeInHierarchy) return false;

        pHealth = targetHealth;

        Vector2 attackCenter = rb != null ? rb.position : (Vector2)transform.position;
        Collider2D myCollider = GetComponent<Collider2D>();
        float currentDist = myCollider != null
            ? Mathf.Max(Physics2D.Distance(myCollider, targetCollider).distance, 0f)
            : Vector2.Distance(attackCenter, targetCollider.bounds.center);
        Vector2 targetCenter = targetCollider.bounds.center;
        Vector2 directionFromAttackOrigin = (targetCenter - attackOrigin).normalized;
        if (directionFromAttackOrigin.sqrMagnitude < 0.001f)
        {
            directionFromAttackOrigin = lockedAttackDirection.sqrMagnitude > 0.001f
                ? lockedAttackDirection.normalized
                : Vector2.up;
        }

        return currentDist <= GetEffectiveAttackRange()
            && HasLineOfSight(attackCenter, targetCenter, Vector2.Distance(attackCenter, targetCenter), targetHealth)
            && Vector2.Angle(lockedAttackDirection, directionFromAttackOrigin) <= attackHitAngle * 0.5f;
    }

    private bool HasValidTarget()
    {
        return player != null
            && playerHealth != null
            && playerCollider != null
            && !playerHealth.isDead
            && playerCollider.enabled
            && player.gameObject.activeInHierarchy;
    }

    private void ClearTarget(bool stopMovement = true)
    {
        player = null;
        playerHealth = null;
        playerCollider = null;
        if (stopMovement)
        {
            StopMovement();
            NetIsRunning = false;
        }
    }

    private Collider2D GetMainTargetCollider(GameObject target)
    {
        Collider2D[] colliders = target.GetComponentsInChildren<Collider2D>();
        foreach (Collider2D col in colliders)
        {
            if (col != null && col.enabled && !col.isTrigger) return col;
        }

        return target.GetComponent<Collider2D>();
    }

    private bool HasLineOfSight(Vector2 from, Vector2 to, float distance, PlayerHealth expectedTarget)
    {
        if (distance <= 0.01f) return true;

        RaycastHit2D hit = Physics2D.Raycast(from, (to - from).normalized, distance, obstacleMask);
        if (hit.collider == null) return true;
        return expectedTarget != null && hit.collider.GetComponentInParent<PlayerHealth>() == expectedTarget;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = transform.position;

        Gizmos.color = new Color(1f, 0.15f, 0.1f, 0.95f);
        Gizmos.DrawWireSphere(center, Mathf.Max(attackRange, damageRadius));
    }

    public void ApplyStun(float duration)
    {
        stunTimer = duration;
        attackTimer = duration;
        isAttackLocked = false;
        attackTargetHealth = null;
        attackTargetCollider = null;
        StopMovement();
        NetIsRunning = false;
    }
}
