using UnityEngine;
using System.Collections;
using Fusion;
using Pathfinding;

public class ZOmbieAI_Khoa : NetworkBehaviour
{
    [Header("=== Movement (A* Pathfinding) ===")]
    [SerializeField] private float speed = 2.5f;
    [SerializeField] private float nextWaypointDistance = 0.5f;

    private Seeker seeker;
    private Path path;
    private int currentWaypoint = 0;
    private float pathRecalcTimer = 0f;

    [Header("=== Tracking (MỚI) ===")]
    [SerializeField] private float trackingDuration = 3f;
    private float currentTrackingTimer;

    [Header("=== Damage ===")]
    [SerializeField] private float zombieDamage = 10f;
    private PlayerHealth playerHealth;

    [Header("=== Vision ===")]
    [SerializeField] private float detectionRange = 5f;
    [SerializeField] private float viewAngle = 90f;
    [SerializeField] private LayerMask obstacleMask;

    [Header("=== Hearing & Memory ===")]
    private Vector2 lastKnownPlayerPos;
    private bool isChasing;
    private bool isInvestigating;
    private Vector2 investigateTarget;
    private float investigateTimer;

    [Header("=== Attack ===")]
    [SerializeField] private float attackRange = 1.2f;
    [SerializeField] private float attackDuration = 1.4f;
    [SerializeField] private float attackCooldown = 1.5f;

    private float attackTimer;
    private float cooldownTimer;
    private bool isAttacking;
    private bool hasAppliedDamage;

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
    }

    public override void Spawned()
    {
        CurrentHealth = maxHealth;

        if (!HasStateAuthority)
        {
            if (rb != null) rb.bodyType = RigidbodyType2D.Kinematic;
        }
    }

    private void CalculatePath(Vector2 targetPos)
    {
        if (seeker.IsDone())
        {
            seeker.StartPath(rb.position, targetPos, OnPathComplete);
        }
    }

    private void OnPathComplete(Path p)
    {
        if (!p.error)
        {
            path = p;
            currentWaypoint = 0;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || NetIsDead) return;

        searchTargetTimer -= Runner.DeltaTime;
        if (searchTargetTimer <= 0f)
        {
            UpdateTargetMultiplayer();
            searchTargetTimer = 0.5f;
        }

        if (player == null)
        {
            StopMovement();
            return;
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

        if (isAttacking)
        {
            attackTimer -= Runner.DeltaTime;
            if (attackTimer <= 0f)
            {
                isAttacking = false;
                NetIsAttacking = false;
            }
        }

        ColliderDistance2D collDist = Physics2D.Distance(myCol, playerCol);
        float distance = Mathf.Max(collDist.distance, 0f);

        Vector2 targetPos = playerCol.bounds.center;
        Vector2 myPos = myCol.bounds.center;
        Vector2 dirToPlayer = (targetPos - myPos).normalized;

        // Bắn Raycast check Tường
        RaycastHit2D wallCheck = Physics2D.Raycast(myPos, dirToPlayer, distance, obstacleMask);
        bool noWallInBetween = wallCheck.collider == null;

        bool canSee = CanSeePlayer(distance, myPos, targetPos, dirToPlayer);

        if (canSee)
        {
            isChasing = true;
            isInvestigating = false;
            currentTrackingTimer = trackingDuration;
            lastKnownPlayerPos = targetPos;
        }
        else if (isChasing)
        {
            if (currentTrackingTimer > 0f)
            {
                currentTrackingTimer -= Runner.DeltaTime;
                lastKnownPlayerPos = targetPos;
            }
        }

        if (isAttacking)
        {
            StopMovement();
            lastMoveDirection = Vector2.Lerp(lastMoveDirection, dirToPlayer, 20f * Runner.DeltaTime);
        }
        else if (isChasing)
        {
            if (distance <= attackRange && canSee && noWallInBetween)
            {
                StopMovement();

                if (cooldownTimer <= 0f)
                {
                    int attackIndex = Random.Range(1, 3);
                    NetAttackIndex = attackIndex;
                    NetIsAttacking = true;
                    isAttacking = true;
                    hasAppliedDamage = false;
                    attackTimer = attackDuration;
                    cooldownTimer = attackCooldown;
                }
            }
            else
            {
                pathRecalcTimer -= Runner.DeltaTime;
                if (pathRecalcTimer <= 0f)
                {
                    CalculatePath(lastKnownPlayerPos);
                    pathRecalcTimer = 0.2f;

                    if (!canSee && currentTrackingTimer <= 0f && Vector2.Distance(myPos, lastKnownPlayerPos) < 0.5f)
                    {
                        isChasing = false;
                        isInvestigating = true;
                        investigateTarget = lastKnownPlayerPos;
                        investigateTimer = 3f;
                    }
                }

                // CHUYỂN GIAO BIẾN noWallInBetween ĐỂ KHÓA LỖI XUYÊN TƯỜNG
                MoveAlongPath(1f, noWallInBetween);
            }
        }
        else if (isInvestigating)
        {
            float distToSound = Vector2.Distance(myPos, investigateTarget);
            if (distToSound > 0.5f)
            {
                pathRecalcTimer -= Runner.DeltaTime;
                if (pathRecalcTimer <= 0f)
                {
                    CalculatePath(investigateTarget);
                    pathRecalcTimer = 0.2f;
                }
                MoveAlongPath(0.7f, false);
            }
            else
            {
                StopMovement();
                investigateTimer -= Runner.DeltaTime;
                if (investigateTimer <= 0f)
                {
                    isInvestigating = false;
                }
            }
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

    // ĐÃ FIX: Nhận thêm tham số noWall để cấm đi thẳng nếu bị kẹt hàng rào
    // ĐÃ FIX: Tích hợp Steering Behavior (Bẻ lái mượt) chống giật khi cua góc
    private void MoveAlongPath(float speedMultiplier, bool noWall)
    {
        bool hasReachedEnd = path == null || currentWaypoint >= path.vectorPath.Count;

        if (hasReachedEnd)
        {
            if (isChasing && playerCol != null && noWall)
            {
                Vector2 targetDir = (playerCol.bounds.center - myCol.bounds.center).normalized;

                // Steering: Trượt hướng từ từ thay vì quay ngoắt
                lastMoveDirection = Vector2.Lerp(lastMoveDirection, targetDir, 8f * Runner.DeltaTime);

                rb.MovePosition(rb.position + lastMoveDirection * speed * speedMultiplier * Runner.DeltaTime);
                NetSpeed = speed * speedMultiplier;
            }
            else
            {
                StopMovement();
            }
            return;
        }

        Vector2 currentWp = (Vector2)path.vectorPath[currentWaypoint];
        Vector2 targetMoveDir = (currentWp - rb.position).normalized;
        float currentSpeed = speed * speedMultiplier;

        // ==========================================
        // STEERING LOGIC (Thay thế Simple Smooth)
        // Hệ số Lerp 10f giúp bo cua mượt mà, không bị giật cục khi góc rẽ quá gắt
        // ==========================================
        lastMoveDirection = Vector2.Lerp(lastMoveDirection, targetMoveDir, 10f * Runner.DeltaTime);

        // Di chuyển theo hướng đã được làm cong
        rb.MovePosition(rb.position + lastMoveDirection * currentSpeed * Runner.DeltaTime);
        NetSpeed = currentSpeed;

        float distToWp = Vector2.Distance(rb.position, currentWp);

        // Giữ nextWaypointDistance khoảng 0.5f - 0.6f trên Inspector
        if (distToWp < nextWaypointDistance)
        {
            currentWaypoint++;
        }
    }

    private bool CanSeePlayer(float distance, Vector2 myPos, Vector2 targetPos, Vector2 toPlayer)
    {
        if (distance > detectionRange) return false;

        if (distance <= attackRange * 1.5f)
        {
            RaycastHit2D shortHit = Physics2D.Raycast(myPos, toPlayer, distance, obstacleMask);
            return shortHit.collider == null || shortHit.collider.gameObject == player.gameObject;
        }

        Vector2 forward = isChasing ? toPlayer : (lastMoveDirection == Vector2.zero ? Vector2.up : lastMoveDirection.normalized);

        if (Vector2.Angle(forward, toPlayer) > viewAngle * 0.5f) return false;

        RaycastHit2D hit = Physics2D.Raycast(myPos, toPlayer, distance, obstacleMask);
        return hit.collider == null || hit.collider.gameObject == player.gameObject;
    }

    public override void Render()
    {
        if (anim != null)
        {
            smoothMoveX = Mathf.Lerp(smoothMoveX, NetMoveDir.x, Time.deltaTime * 12f);
            smoothMoveY = Mathf.Lerp(smoothMoveY, NetMoveDir.y, Time.deltaTime * 12f);
            smoothSpeed = Mathf.Lerp(smoothSpeed, NetSpeed, Time.deltaTime * 15f);

            anim.SetFloat("MoveX", smoothMoveX);
            anim.SetFloat("MoveY", smoothMoveY);
            anim.SetFloat("Speed", smoothSpeed);

            if (lastIsAttacking != NetIsAttacking)
            {
                anim.SetBool("IsAttacking", NetIsAttacking);
                lastIsAttacking = NetIsAttacking;
            }

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
        }
    }

    private void UpdateTargetMultiplayer()
    {
        if (isChasing && player != null && player.gameObject.activeInHierarchy)
        {
            if (player.TryGetComponent(out Skill_StealthCrouch currentTargetStealth) && currentTargetStealth.IsInvisible)
            {
                isChasing = false;
                player = null;
            }
            else
            {
                return;
            }
        }

        GameObject[] allPlayers = GameObject.FindGameObjectsWithTag("Player");
        if (allPlayers.Length == 0)
        {
            player = null; playerCol = null; playerHealth = null;
            return;
        }

        Vector2 myPos = transform.position;
        float minDist = Mathf.Infinity;
        GameObject closest = null;

        foreach (GameObject p in allPlayers)
        {
            if (p.TryGetComponent(out Skill_StealthCrouch stealth) && stealth.IsInvisible) continue;

            float dist = Vector2.Distance(myPos, p.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                closest = p;
            }
        }

        if (closest != null && closest.transform != player)
        {
            player = closest.transform;
            playerCol = closest.GetComponent<Collider2D>();
            playerHealth = closest.GetComponent<PlayerHealth>();
        }
        else if (closest == null)
        {
            player = null;
            playerCol = null;
            playerHealth = null;
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_HearSound(Vector2 soundPos)
    {
        if (NetIsDead || isChasing) return;
        isInvestigating = true;
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
        NetIsDead = true;

        StopMovement();
        myCol.enabled = false;

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

        StartCoroutine(VanishRoutine());
    }

    private IEnumerator VanishRoutine()
    {
        yield return new WaitForSeconds(5f);
        if (HasStateAuthority) Runner.Despawn(Object);
    }

    public void TriggerAttackDamage()
    {
        if (!HasStateAuthority) return;

        if (hasAppliedDamage || NetIsDead || playerHealth == null || playerCol == null) return;

        float currentDist = Vector2.Distance(myCol.bounds.center, playerCol.bounds.center);

        if (currentDist <= attackRange + 0.5f)
        {
            playerHealth.TakeDamage(zombieDamage, false, true);
            hasAppliedDamage = true;
        }
    }
}