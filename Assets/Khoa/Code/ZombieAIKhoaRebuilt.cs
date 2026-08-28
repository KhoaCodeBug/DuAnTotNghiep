using System.Collections;
using Fusion;
using Pathfinding;
using UnityEngine;

/// <summary>
/// A clean, state-driven replacement for ZOmbieAI_Khoa.
/// Gameplay values intentionally mirror the original zombie; only decision,
/// steering, path lifetime and target-memory logic are new.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(Animator))]
[RequireComponent(typeof(Seeker))]
public sealed class ZombieAIKhoaRebuilt : NetworkBehaviour
{
    private enum BrainState : byte
    {
        Idle,
        Investigating,
        Chasing,
        Searching,
        Attacking,
        Stunned,
        Dead
    }

    [Header("=== Movement (same gameplay values) ===")]
    [SerializeField] private float speed = 2.5f;
    public float ChaseMovementSpeed => speed;
    [SerializeField] private float nextWaypointDistance = 0.5f;
    [SerializeField] private float trackingDuration = 3f;

    [Header("=== Flocking (same gameplay values) ===")]
    [SerializeField] private LayerMask zombieMask;
    [SerializeField] private float separationRadius = 0.4f;
    [SerializeField] private float separationWeight = 1.5f;

    [Header("=== Damage ===")]
    [SerializeField] private float zombieDamage = 10f;

    [Header("=== Vision ===")]
    [SerializeField] private float detectionRange = 5f;
    [SerializeField] private float viewAngle = 90f;
    [SerializeField, Range(90f, 360f)] private float alertViewAngle = 200f;
    [SerializeField] private float closeAwarenessRange = 0.25f;
    [SerializeField, Range(0.1f, 1f)] private float crouchDetectionMultiplier = 0.55f;
    [SerializeField] private LayerMask obstacleMask;

    [Header("=== Hearing & Search (same gameplay values) ===")]
    [SerializeField] private int searchPointCount = 3;
    [SerializeField] private float searchRadius = 2.25f;
    [SerializeField] private float searchWaitDuration = 1.1f;

    [Header("=== Attack (same clips and timing) ===")]
    [SerializeField] private float attackRange = 0.12f;
    [SerializeField, Range(30f, 180f)] private float attackHitAngle = 120f;
    [SerializeField] private float attack1Duration = 1.0833334f;
    [SerializeField] private float attack2Duration = 1.25f;
    [SerializeField] private float attackCooldown = 1.5f;

    [Header("=== Zombie Stats ===")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float stunDuration = 5f;
    [SerializeField] private Color hurtColor = Color.red;

    [Header("=== Stable steering (logic only) ===")]
    [SerializeField] private float perceptionInterval = 0.15f;
    [SerializeField] private float pathRefreshInterval = 0.4f;
    [SerializeField] private float pathTargetMoveThreshold = 0.35f;
    [SerializeField] private float turnResponsiveness = 9f;
    [SerializeField] private float obstacleProbeDistance = 0.8f;
    [SerializeField] private float obstacleAvoidanceWeight = 1.8f;
    [SerializeField] private float stuckRepathDelay = 0.75f;

    [Networked] public float CurrentHealth { get; set; }
    [Networked] public NetworkBool NetIsDead { get; set; }
    [Networked] public NetworkBool NetIsAttacking { get; set; }
    [Networked] public int NetAttackIndex { get; set; }
    [Networked] public float NetSpeed { get; set; }
    [Networked] public Vector2 NetMoveDir { get; set; }
    [Networked] public NetworkBool TutorialStationary { get; set; }
    [Networked] public NetworkBool TutorialForceVisible { get; set; }

    private readonly Collider2D[] nearbyZombies = new Collider2D[10];
    private ContactFilter2D zombieFilter;
    private Rigidbody2D body;
    private Collider2D bodyCollider;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private Seeker seeker;
    private Color originalColor;

    private BrainState state;
    private BrainState resumeAfterStun;
    private Transform target;
    private Collider2D targetCollider;
    private PlayerHealth targetHealth;

    private Vector2 facing = Vector2.down;
    private Vector2 lastSeenPosition;
    private Vector2 previousSeenPosition;
    private Vector2 observedVelocity;
    private Vector2 movementGoal;
    private float memoryRemaining;
    private float perceptionTimer;
    private bool targetVisible;

    private Path path;
    private int waypointIndex;
    private int pathGeneration;
    private Vector2 requestedGoal;
    private float pathRefreshTimer;
    private Vector2 lastProgressPosition;
    private float stuckTimer;
    private float crowdSidePreference;

    private Vector2 searchCenter;
    private Vector2 searchTarget;
    private int searchPointIndex;
    private float searchWaitTimer;
    private float searchPhase;
    private float investigateWaitTimer;

    private float attackTimer;
    private float attackImpactTimer;
    private float cooldownTimer;
    private bool attackDamageApplied;
    private Vector2 attackOrigin;
    private Vector2 lockedAttackDirection;
    private PlayerHealth lockedTargetHealth;
    private Collider2D lockedTargetCollider;
    private float stunTimer;
    private float activeSoundUrgency;
    private float soundPriorityTimer;

    private bool tutorialConfigured;
    private bool tutorialStationary;
    private Vector2 tutorialFacing;
    private float tutorialHealth;

    private float renderMoveX;
    private float renderMoveY;
    private float renderSpeed;
    private bool renderedDead;
    private bool renderedAttacking;
    private int renderedAttackIndex;
    private Coroutine flashRoutine;

    private float Delta => Runner != null ? Runner.DeltaTime : Time.fixedDeltaTime;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        seeker = GetComponent<Seeker>();
        if (spriteRenderer != null) originalColor = spriteRenderer.color;

        zombieFilter = new ContactFilter2D { useLayerMask = true, useTriggers = false };
        zombieFilter.SetLayerMask(zombieMask);
    }

    private void SetBodyCollisionEnabled(bool enabled)
    {
        if (bodyCollider != null && bodyCollider.enabled != enabled)
        {
            bodyCollider.enabled = enabled;
        }
    }

    public override void Spawned()
    {
        // Collider2D.enabled is local-only in Fusion. Restore it when this
        // network object is spawned/reused; Render keeps it synced afterward.
        SetBodyCollisionEnabled(!NetIsDead);

        ResetAnimator();

        if (!HasStateAuthority)
        {
            body.bodyType = RigidbodyType2D.Kinematic;
            return;
        }

        CurrentHealth = tutorialConfigured ? tutorialHealth : maxHealth;
        TutorialStationary = tutorialConfigured && tutorialStationary;
        facing = tutorialConfigured ? tutorialFacing : Random.insideUnitCircle.normalized;
        if (facing.sqrMagnitude < 0.001f) facing = Vector2.down;
        NetMoveDir = facing;
        renderMoveX = facing.x;
        renderMoveY = facing.y;
        state = BrainState.Idle;
        lastProgressPosition = body.position;
        crowdSidePreference = Random.value < 0.5f ? -1f : 1f;
        searchPhase = Random.Range(0f, Mathf.PI * 2f);
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || state == BrainState.Dead || NetIsDead) return;

        float dt = Delta;
        cooldownTimer = Mathf.Max(0f, cooldownTimer - dt);
        soundPriorityTimer = Mathf.Max(0f, soundPriorityTimer - dt);
        if (soundPriorityTimer <= 0f) activeSoundUrgency = 0f;

        if (TutorialStationary)
        {
            StopMovement();
            NetMoveDir = facing;
            return;
        }

        if (state == BrainState.Attacking)
        {
            TickAttack(dt);
            return;
        }

        if (state == BrainState.Stunned)
        {
            TickStun(dt);
            return;
        }

        perceptionTimer -= dt;
        if (perceptionTimer <= 0f)
        {
            perceptionTimer = Mathf.Max(0.05f, perceptionInterval);
            UpdatePerception(perceptionTimer);
        }

        switch (state)
        {
            case BrainState.Chasing: TickChase(dt); break;
            case BrainState.Investigating: TickInvestigation(dt); break;
            case BrainState.Searching: TickSearch(dt); break;
            default: StopMovement(); break;
        }

        NetMoveDir = facing;
    }

    private void UpdatePerception(float sampleTime)
    {
        ValidateTarget();
        GameObject best = FindBestVisiblePlayer();

        if (best != null)
        {
            AssignTarget(best);
            Vector2 seen = targetCollider.bounds.center;
            observedVelocity = (seen - previousSeenPosition) / Mathf.Max(sampleTime, 0.001f);
            observedVelocity = Vector2.ClampMagnitude(observedVelocity, speed * 2f);
            previousSeenPosition = seen;
            lastSeenPosition = seen;
            memoryRemaining = trackingDuration;
            targetVisible = true;
            if (state != BrainState.Attacking && state != BrainState.Stunned)
                ChangeState(BrainState.Chasing);
            return;
        }

        targetVisible = false;
        if (state == BrainState.Chasing)
            memoryRemaining = Mathf.Max(0f, memoryRemaining - sampleTime);
    }

    private GameObject FindBestVisiblePlayer()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        Vector2 origin = bodyCollider.bounds.center;
        float bestScore = float.NegativeInfinity;
        GameObject best = null;

        for (int i = 0; i < players.Length; i++)
        {
            GameObject candidate = players[i];
            if (candidate.TryGetComponent(out Skill_StealthCrouch stealth) && stealth.IsInvisible) continue;
            if (!candidate.TryGetComponent(out PlayerHealth health) || health.Object == null || !health.Object.IsValid || health.isDead) continue;
            if (PlayerInteraction.IsProtectedOccupant(health)) continue;
            Collider2D candidateCollider = candidate.GetComponent<Collider2D>();
            if (candidateCollider == null) continue;

            Vector2 candidatePosition = candidateCollider.bounds.center;
            float distance = Vector2.Distance(origin, candidatePosition);
            bool current = target == candidate.transform;
            PlayerMovement movement = candidate.GetComponent<PlayerMovement>();
            bool crouching = movement != null && movement.NetIsCrouching;
            float activeCone = current ? alertViewAngle : viewAngle;
            Vector2 forward = facing.sqrMagnitude > 0.001f ? facing : Vector2.up;
            float angleFromFacing = Vector2.Angle(forward, (candidatePosition - origin).normalized);

            // Crouching directly behind this sight cone has exactly zero visual
            // detection. Standing players keep the normal rear/alert behaviour.
            if (crouching && angleFromFacing > activeCone * 0.5f) continue;

            float effectiveRange = crouching ? detectionRange * crouchDetectionMultiplier : detectionRange;
            if (!CanSee(distance, origin, candidatePosition, activeCone, health, effectiveRange)) continue;

            float score = -distance + (current ? 0.25f : 0f);
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }

    private void TickChase(float dt)
    {
        ValidateTarget();

        if (targetVisible && targetCollider != null)
        {
            movementGoal = targetCollider.bounds.center;
            if (CanBeginAttack())
            {
                StopMovement();
                if (cooldownTimer <= 0f) BeginAttack();
                return;
            }
        }
        else if (memoryRemaining > 0f)
        {
            float predictionTime = Mathf.Min(trackingDuration - memoryRemaining, 0.75f);
            movementGoal = lastSeenPosition + observedVelocity * predictionTime;
        }
        else
        {
            BeginSearch(lastSeenPosition);
            return;
        }

        MoveTo(movementGoal, 1f, targetVisible);
    }

    private void TickInvestigation(float dt)
    {
        if (Vector2.Distance(bodyCollider.bounds.center, movementGoal) > nextWaypointDistance + 0.1f)
        {
            MoveTo(movementGoal, 0.7f, false);
            return;
        }

        StopMovement();
        investigateWaitTimer -= dt;
        if (investigateWaitTimer <= 0f) BeginSearch(movementGoal);
    }

    private void BeginSearch(Vector2 center)
    {
        searchCenter = center;
        searchPointIndex = 0;
        searchWaitTimer = 0f;
        ChangeState(BrainState.Searching);
        SelectNextSearchPoint();
    }

    private void TickSearch(float dt)
    {
        if (Vector2.Distance(bodyCollider.bounds.center, searchTarget) > nextWaypointDistance + 0.1f)
        {
            MoveTo(searchTarget, 0.55f, false);
            return;
        }

        StopMovement();
        searchWaitTimer -= dt;
        if (searchWaitTimer > 0f) return;

        if (searchPointIndex >= searchPointCount)
        {
            ChangeState(BrainState.Idle);
            return;
        }

        searchWaitTimer = searchWaitDuration;
        SelectNextSearchPoint();
    }

    private void SelectNextSearchPoint()
    {
        if (searchPointIndex >= searchPointCount) return;
        const float goldenAngle = 2.39996323f;
        float angle = searchPhase + searchPointIndex * goldenAngle;
        float radius = searchRadius * Mathf.Sqrt((searchPointIndex + 1f) / Mathf.Max(searchPointCount, 1));
        searchTarget = searchCenter + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        searchPointIndex++;
        InvalidatePath();
    }

    private void MoveTo(Vector2 goal, float speedMultiplier, bool allowDirect)
    {
        pathRefreshTimer -= Delta;
        bool direct = allowDirect && HasLineOfSight(bodyCollider.bounds.center, goal, Vector2.Distance(bodyCollider.bounds.center, goal));
        Vector2 desired;

        if (direct)
        {
            desired = goal - body.position;
        }
        else
        {
            RequestPathIfNeeded(goal);
            if (path == null || waypointIndex >= path.vectorPath.Count)
            {
                StopMovement();
                return;
            }

            while (waypointIndex < path.vectorPath.Count - 1 &&
                   Vector2.Distance(body.position, path.vectorPath[waypointIndex]) <= nextWaypointDistance)
                waypointIndex++;

            desired = (Vector2)path.vectorPath[waypointIndex] - body.position;
        }

        if (desired.sqrMagnitude < 0.0001f)
        {
            StopMovement();
            return;
        }

        float remainingDistance = desired.magnitude;
        desired = GetSteeredDirection(desired.normalized);
        facing = Vector2.MoveTowards(facing, desired, turnResponsiveness * Delta).normalized;
        if (facing.sqrMagnitude < 0.001f) facing = desired;

        float moveSpeed = speed * speedMultiplier;
        float moveDistance = Mathf.Min(moveSpeed * Delta, remainingDistance);
        body.MovePosition(body.position + facing * moveDistance);
        NetSpeed = Delta > 0f ? moveDistance / Delta : 0f;
        CheckProgress(goal);
    }

    private void RequestPathIfNeeded(Vector2 goal)
    {
        bool goalMoved = Vector2.Distance(goal, requestedGoal) >= pathTargetMoveThreshold;
        bool exhausted = path == null || waypointIndex >= path.vectorPath.Count;
        if (!goalMoved && !exhausted && pathRefreshTimer > 0f) return;
        if (seeker == null || !seeker.IsDone()) return;

        requestedGoal = goal;
        pathRefreshTimer = pathRefreshInterval;
        int generation = ++pathGeneration;
        seeker.StartPath(body.position, goal, result => AcceptPath(result, generation));
    }

    private void AcceptPath(Path result, int generation)
    {
        if (!HasStateAuthority || result.error || generation != pathGeneration) return;
        path = result;
        waypointIndex = FindClosestForwardWaypoint(result);
    }

    private int FindClosestForwardWaypoint(Path candidate)
    {
        if (candidate == null || candidate.vectorPath.Count == 0) return 0;
        int best = 0;
        float bestDistance = float.PositiveInfinity;
        int limit = Mathf.Min(candidate.vectorPath.Count, 5);
        for (int i = 0; i < limit; i++)
        {
            float distance = Vector2.SqrMagnitude((Vector2)candidate.vectorPath[i] - body.position);
            if (distance < bestDistance) { bestDistance = distance; best = i; }
        }
        return best;
    }

    private void CheckProgress(Vector2 goal)
    {
        if (Vector2.Distance(body.position, lastProgressPosition) < 0.025f)
            stuckTimer += Delta;
        else
        {
            stuckTimer = 0f;
            lastProgressPosition = body.position;
        }

        if (stuckTimer < stuckRepathDelay) return;
        InvalidatePath();
        requestedGoal = goal + Vector2.one * (pathTargetMoveThreshold + 0.01f);
        stuckTimer = 0f;
        lastProgressPosition = body.position;
    }

    private Vector2 GetSteeredDirection(Vector2 desired)
    {
        Vector2 separation = GetSeparation();
        float reversePressure = Vector2.Dot(separation, desired);
        Vector2 lateral = separation - desired * reversePressure;
        if (reversePressure < 0f && lateral.sqrMagnitude < 0.001f)
            lateral = new Vector2(-desired.y, desired.x) * crowdSidePreference * -reversePressure;

        Vector2 crowd = (lateral + desired * Mathf.Max(0f, reversePressure)) * separationWeight;
        Vector2 avoidance = GetObstacleAvoidance(desired) * obstacleAvoidanceWeight;
        Vector2 combined = desired + crowd + avoidance;
        return combined.sqrMagnitude > 0.001f ? combined.normalized : desired;
    }

    private Vector2 GetSeparation()
    {
        Vector2 force = Vector2.zero;
        int count = Physics2D.OverlapCircle(body.position, separationRadius, zombieFilter, nearbyZombies);
        int contributors = 0;
        for (int i = 0; i < count; i++)
        {
            Collider2D other = nearbyZombies[i];
            if (other == null || other.transform.root == transform.root) continue;
            Vector2 delta = body.position - (Vector2)other.bounds.center;
            float distance = delta.magnitude;
            if (distance <= 0f || distance >= separationRadius) continue;
            force += delta.normalized * (1f - distance / separationRadius);
            contributors++;
        }
        return contributors > 0 ? force / contributors : Vector2.zero;
    }

    private Vector2 GetObstacleAvoidance(Vector2 forward)
    {
        if (obstacleProbeDistance <= 0f) return Vector2.zero;
        RaycastHit2D front = Physics2D.Raycast(body.position, forward, obstacleProbeDistance, obstacleMask);
        if (front.collider == null) return Vector2.zero;

        Vector2 left = new Vector2(-forward.y, forward.x);
        Vector2 right = -left;
        float probe = obstacleProbeDistance * 0.9f;
        RaycastHit2D leftHit = Physics2D.Raycast(body.position, (forward + left * 0.65f).normalized, probe, obstacleMask);
        RaycastHit2D rightHit = Physics2D.Raycast(body.position, (forward + right * 0.65f).normalized, probe, obstacleMask);
        float leftClear = leftHit.collider == null ? probe : leftHit.distance;
        float rightClear = rightHit.collider == null ? probe : rightHit.distance;
        float urgency = 1f - Mathf.Clamp01(front.distance / obstacleProbeDistance);
        return (leftClear >= rightClear ? left : right) * urgency;
    }

    private bool CanBeginAttack()
    {
        if (targetHealth == null || targetCollider == null || targetHealth.isDead) return false;
        ColliderDistance2D gap = Physics2D.Distance(bodyCollider, targetCollider);
        float distance = Mathf.Max(0f, gap.distance);
        return distance <= attackRange && IsTargetVisibleNow(targetHealth, targetCollider, alertViewAngle);
    }

    private void BeginAttack()
    {
        if (!CanBeginAttack()) return;
        attackOrigin = bodyCollider.bounds.center;
        lockedAttackDirection = ((Vector2)targetCollider.bounds.center - attackOrigin).normalized;
        if (lockedAttackDirection.sqrMagnitude < 0.001f) lockedAttackDirection = facing;
        lockedTargetHealth = targetHealth;
        lockedTargetCollider = targetCollider;
        facing = lockedAttackDirection;
        InvalidatePath();
        StopMovement();

        NetAttackIndex = Random.Range(1, 3);
        NetIsAttacking = true;
        attackDamageApplied = false;
        attackTimer = NetAttackIndex == 1 ? attack1Duration : attack2Duration;
        attackImpactTimer = GetAttackImpactDelay(NetAttackIndex, lockedAttackDirection);
        cooldownTimer = attackCooldown;
        ChangeState(BrainState.Attacking);
    }

    private void TickAttack(float dt)
    {
        StopMovement();
        facing = lockedAttackDirection;
        NetMoveDir = facing;
        attackTimer -= dt;
        attackImpactTimer -= dt;
        if (attackImpactTimer <= 0f) TryApplyAttackDamage();
        if (attackTimer > 0f) return;
        NetIsAttacking = false;
        ClearLockedAttack();
        ChangeState(target != null || memoryRemaining > 0f ? BrainState.Chasing : BrainState.Idle);
    }

    private void TickStun(float dt)
    {
        StopMovement();
        stunTimer -= dt;
        if (stunTimer > 0f) return;
        ChangeState(resumeAfterStun == BrainState.Attacking ? BrainState.Chasing : resumeAfterStun);
    }

    private bool CanSee(float distance, Vector2 origin, Vector2 point, float cone, PlayerHealth expected, float maxRange = -1f)
    {
        float allowedRange = maxRange > 0f ? maxRange : detectionRange;
        if (distance > allowedRange || !HasLineOfSight(origin, point, distance, expected)) return false;
        if (distance <= closeAwarenessRange) return true;
        Vector2 forward = facing.sqrMagnitude > 0.001f ? facing : Vector2.up;
        return Vector2.Angle(forward, (point - origin).normalized) <= cone * 0.5f;
    }

    private bool IsTargetVisibleNow(PlayerHealth health, Collider2D collider, float cone)
    {
        if (health == null || collider == null || health.isDead) return false;
        Vector2 origin = bodyCollider.bounds.center;
        Vector2 point = collider.bounds.center;
        return CanSee(Vector2.Distance(origin, point), origin, point, cone, health);
    }

    private bool HasLineOfSight(Vector2 from, Vector2 to, float distance, PlayerHealth expected = null)
    {
        if (distance <= 0.01f) return true;
        RaycastHit2D hit = Physics2D.Raycast(from, (to - from).normalized, distance, obstacleMask);
        if (hit.collider == null) return true;
        PlayerHealth health = expected != null ? expected : targetHealth;
        return health != null && hit.collider.GetComponentInParent<PlayerHealth>() == health;
    }

    private void AssignTarget(GameObject playerObject)
    {
        PlayerHealth candidateHealth = playerObject != null ? playerObject.GetComponent<PlayerHealth>() : null;
        if (PlayerInteraction.IsProtectedOccupant(candidateHealth)) return;
        target = playerObject.transform;
        targetCollider = playerObject.GetComponent<Collider2D>();
        targetHealth = playerObject.GetComponent<PlayerHealth>();
        if (previousSeenPosition == Vector2.zero && targetCollider != null)
            previousSeenPosition = targetCollider.bounds.center;
    }

    private void ValidateTarget()
    {
        if (target == null || targetHealth == null || targetHealth.Object == null || !targetHealth.Object.IsValid || targetHealth.isDead ||
            PlayerInteraction.IsProtectedOccupant(targetHealth) ||
            (target.TryGetComponent(out Skill_StealthCrouch stealth) && stealth.IsInvisible))
        {
            target = null;
            targetCollider = null;
            targetHealth = null;
            targetVisible = false;
        }
    }

    private void ChangeState(BrainState next)
    {
        if (state == next) return;
        state = next;
        InvalidatePath();
        if (next != BrainState.Chasing && next != BrainState.Attacking) targetVisible = false;
    }

    private void InvalidatePath()
    {
        pathGeneration++;
        path = null;
        waypointIndex = 0;
        pathRefreshTimer = 0f;
    }

    private void StopMovement()
    {
        if (body != null) body.linearVelocity = Vector2.zero;
        NetSpeed = 0f;
    }

    public override void Render()
    {
        // The Host's Collider2D.enabled change is not replicated automatically.
        // Apply the replicated death state locally on every peer.
        SetBodyCollisionEnabled(!NetIsDead);

        if (animator == null) return;
        renderMoveX = Mathf.Lerp(renderMoveX, NetMoveDir.x, Time.deltaTime * 12f);
        renderMoveY = Mathf.Lerp(renderMoveY, NetMoveDir.y, Time.deltaTime * 12f);
        renderSpeed = Mathf.Lerp(renderSpeed, NetSpeed, Time.deltaTime * 15f);
        animator.SetFloat("MoveX", renderMoveX);
        animator.SetFloat("MoveY", renderMoveY);
        animator.SetFloat("Speed", renderSpeed);

        if (renderedDead != NetIsDead)
        {
            animator.SetBool("IsDead", NetIsDead);
            renderedDead = NetIsDead;
        }
        if (NetIsAttacking && renderedAttackIndex != NetAttackIndex)
        {
            animator.SetInteger("AttackIndex", NetAttackIndex);
            renderedAttackIndex = NetAttackIndex;
        }
        if (renderedAttacking != NetIsAttacking)
        {
            animator.SetBool("IsAttacking", NetIsAttacking);
            renderedAttacking = NetIsAttacking;
        }
    }

    public void ConfigureTutorialSpawn(Vector2 initialFacing, float health, bool stationary)
    {
        tutorialConfigured = true;
        tutorialFacing = initialFacing.sqrMagnitude > 0.001f ? initialFacing.normalized : Vector2.down;
        tutorialHealth = Mathf.Max(1f, health);
        tutorialStationary = stationary;
    }

    public void SetTutorialForceVisible(bool visible)
    {
        if (HasStateAuthority) TutorialForceVisible = visible;
    }

    public void ReleaseTutorialStationary(Vector2 alertPosition)
    {
        if (!HasStateAuthority || NetIsDead) return;
        ReleaseTutorialStationary();
        movementGoal = alertPosition;
        investigateWaitTimer = 3f;
        ChangeState(BrainState.Investigating);
    }

    public void ReleaseTutorialStationary()
    {
        if (!HasStateAuthority || NetIsDead) return;
        TutorialStationary = false;
        tutorialStationary = false;
        InvalidatePath();
    }

    public void ForceSiegeTarget(PlayerHealth player)
    {
        if (!HasStateAuthority || NetIsDead || player == null || player.isDead ||
            PlayerInteraction.IsProtectedOccupant(player)) return;
        Collider2D collider = player.GetComponent<Collider2D>();
        if (collider == null) return;

        TutorialStationary = false;
        tutorialStationary = false;
        AssignTarget(player.gameObject);
        Vector2 targetPosition = collider.bounds.center;
        previousSeenPosition = targetPosition;
        lastSeenPosition = targetPosition;
        movementGoal = targetPosition;
        memoryRemaining = trackingDuration;
        targetVisible = true;
        ChangeState(BrainState.Chasing);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_HearSound(Vector2 soundPosition)
    {
        ProcessHeardSound(soundPosition, 1f);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_HearSoundWithUrgency(Vector2 soundPosition, float urgency)
    {
        ProcessHeardSound(soundPosition, Mathf.Clamp01(urgency));
    }

    private void ProcessHeardSound(Vector2 soundPosition, float urgency)
    {
        if (NetIsDead || urgency <= 0f) return;
        // An attack remains animation-locked, exactly like the original timing contract.
        if (state == BrainState.Attacking) return;

        // Vision always wins over hearing. A weak/repeated footstep also cannot
        // keep replacing a stronger recent sound such as a gunshot.
        if (state == BrainState.Chasing && targetVisible) return;
        if (soundPriorityTimer > 0f && urgency + 0.05f < activeSoundUrgency) return;

        // Quiet footsteps do not travel through walls. Running and gunshots can,
        // with responder limits enforced by PlayerMovement.MakeNoise.
        bool blocked = Physics2D.Linecast(bodyCollider.bounds.center, soundPosition, obstacleMask).collider != null;
        if (blocked && urgency < 0.75f) return;

        activeSoundUrgency = urgency;
        soundPriorityTimer = Mathf.Lerp(0.75f, 2.5f, urgency);

        if (state == BrainState.Chasing && memoryRemaining > 0f && urgency < 0.8f)
        {
            return;
        }

        if (state == BrainState.Stunned)
        {
            movementGoal = soundPosition;
            investigateWaitTimer = 3f;
            resumeAfterStun = BrainState.Investigating;
            InvalidatePath();
            return;
        }

        movementGoal = soundPosition;
        investigateWaitTimer = 3f;
        ChangeState(BrainState.Investigating);
        InvalidatePath();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_TakeDamage(float damage, PlayerRef shooter = default)
    {
        if (NetIsDead) return;
        BrainState stateBeforeHit = state;
        CurrentHealth = Mathf.Clamp(CurrentHealth - damage, 0f, maxHealth);
        if (CurrentHealth <= 0f)
        {
            Die(shooter);
            return;
        }

        resumeAfterStun = DeterminePostHitState(shooter, stateBeforeHit);
        stunTimer = stunDuration;
        NetIsAttacking = false;
        ClearLockedAttack();
        ChangeState(BrainState.Stunned);
        StopMovement();
        RPC_PlayHitEffect();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_PlayHitEffect()
    {
        if (animator != null) animator.SetTrigger("TakeDamage");
        if (spriteRenderer == null) return;
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashRedRoutine());
    }

    private IEnumerator FlashRedRoutine()
    {
        spriteRenderer.color = hurtColor;
        yield return new WaitForSeconds(0.12f);
        if (!NetIsDead) spriteRenderer.color = originalColor;
        flashRoutine = null;
    }

    private void Die(PlayerRef shooter)
    {
        if (NetIsDead) return;
        float deathAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        facing = new Vector2(Mathf.Cos(deathAngle), Mathf.Sin(deathAngle));
        NetMoveDir = facing;
        NetIsDead = true;
        NetIsAttacking = false;
        state = BrainState.Dead;
        ClearLockedAttack();
        InvalidatePath();
        StopMovement();
        SetBodyCollisionEnabled(false);

        if (shooter != PlayerRef.None)
        {
            Skill_WeaponMaster[] masters = FindObjectsByType<Skill_WeaponMaster>(FindObjectsSortMode.None);
            for (int i = 0; i < masters.Length; i++)
            {
                if (masters[i].Object == null || masters[i].Object.InputAuthority != shooter) continue;
                masters[i].AddKill();
                break;
            }
        }
        GetComponent<ZombieCorpseLoot>()?.MarkAsCorpse();
        StartCoroutine(VanishRoutine());
    }

    private IEnumerator VanishRoutine()
    {
        if (GetComponent<ZombieCorpseLoot>() != null) yield break;
        yield return new WaitForSeconds(5f);
        if (HasStateAuthority && Object != null && Object.IsValid) Runner.Despawn(Object);
    }

    // Called by the copied attack clips at their original impact frame.
    public void TriggerAttackDamage()
    {
        TryApplyAttackDamage();
    }

    private void TryApplyAttackDamage()
    {
        if (!HasStateAuthority || state != BrainState.Attacking || !NetIsAttacking || attackDamageApplied || NetIsDead ||
            lockedTargetHealth == null || lockedTargetCollider == null) return;

        Vector2 from = bodyCollider.bounds.center;
        Vector2 to = lockedTargetCollider.bounds.center;
        float distance = Mathf.Max(0f, Physics2D.Distance(bodyCollider, lockedTargetCollider).distance);
        Vector2 directionFromOrigin = (to - attackOrigin).normalized;
        // Consume exactly one impact window even when the player genuinely
        // dodges; the authoritative timer and Animation Event share this guard.
        attackDamageApplied = true;
        if (distance <= attackRange &&
            HasLineOfSight(from, to, distance, lockedTargetHealth) &&
            Vector2.Angle(lockedAttackDirection, directionFromOrigin) <= attackHitAngle * 0.5f)
        {
            lockedTargetHealth.TakeDamageNetworked(zombieDamage, false, true);
        }
    }

    private static float GetAttackImpactDelay(int attackIndex, Vector2 direction)
    {
        // Copied clips: every Attack 2 event is at 0.5s. Attack 1 East is
        // at 0.5s; its other seven directional clips fire at frame 7/12.
        if (attackIndex == 2) return 0.5f;
        bool east = direction.x > 0f && Mathf.Abs(direction.y) <= direction.x * 0.41421356f;
        return east ? 0.5f : 0.5833333f;
    }

    private BrainState DeterminePostHitState(PlayerRef shooter, BrainState previousState)
    {
        if (TryRememberDamageSource(shooter, out bool canSeeShooter))
            return canSeeShooter ? BrainState.Chasing : BrainState.Investigating;

        if (previousState == BrainState.Investigating || previousState == BrainState.Searching)
            return previousState;
        return target != null || memoryRemaining > 0f ? BrainState.Chasing : BrainState.Idle;
    }

    private bool TryRememberDamageSource(PlayerRef shooter, out bool canSeeShooter)
    {
        canSeeShooter = false;
        if (shooter == PlayerRef.None) return false;

        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        for (int i = 0; i < players.Length; i++)
        {
            PlayerHealth health = players[i].GetComponent<PlayerHealth>();
            Collider2D playerCollider = players[i].GetComponent<Collider2D>();
            if (health == null || playerCollider == null || health.Object == null || !health.Object.IsValid ||
                health.Object.InputAuthority != shooter || health.isDead || PlayerInteraction.IsProtectedOccupant(health)) continue;

            Vector2 source = playerCollider.bounds.center;
            movementGoal = source;
            investigateWaitTimer = 3f;
            activeSoundUrgency = 1f;
            soundPriorityTimer = 2.5f;

            Vector2 origin = bodyCollider.bounds.center;
            float distance = Vector2.Distance(origin, source);
            canSeeShooter = CanSee(distance, origin, source, alertViewAngle, health);
            if (canSeeShooter)
            {
                AssignTarget(players[i]);
                previousSeenPosition = source;
                lastSeenPosition = source;
                observedVelocity = Vector2.zero;
                memoryRemaining = trackingDuration;
                targetVisible = true;
            }
            return true;
        }
        return false;
    }

    private void ClearLockedAttack()
    {
        lockedTargetHealth = null;
        lockedTargetCollider = null;
        attackDamageApplied = true;
    }

    private void ResetAnimator()
    {
        if (animator == null) return;
        animator.SetBool("IsDead", false);
        animator.SetBool("IsAttacking", false);
        renderedDead = false;
        renderedAttacking = false;
    }
}
