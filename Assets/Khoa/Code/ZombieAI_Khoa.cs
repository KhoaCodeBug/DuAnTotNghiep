using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using Fusion;

public class ZOmbieAI_Khoa : NetworkBehaviour
{
    [Header("=== Movement (NavMesh 2D) ===")]
    [SerializeField] private float speed = 2.5f;
    private NavMeshAgent agent;

    [Header("=== Damage ===")]
    [SerializeField] private float zombieDamage = 15f;
    private PlayerHealth playerHealth;

    [Header("=== Vision ===")]
    [SerializeField] private float detectionRange = 5f;
    [SerializeField] private float viewAngle = 90f;
    [SerializeField] private LayerMask obstacleMask;

    [Header("=== Chase Memory ===")]
    [SerializeField] private float loseTime = 8f;
    private float loseTimer;
    private bool isChasing;

    [Header("=== Hearing ===")]
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

    // 🔥 CÁC BIẾN MẠNG
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
    private float pathRecalcTimer = 0f;

    // Biến làm mượt Animation chống "cứng đờ"
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

        agent = GetComponent<NavMeshAgent>();
        agent.updateRotation = false;
        agent.updateUpAxis = false;
        agent.speed = speed;

        if (spriteRend != null) originalColor = spriteRend.color;
    }

    public override void Spawned()
    {
        CurrentHealth = maxHealth;

        if (!HasStateAuthority)
        {
            if (agent != null) agent.enabled = false;
            if (rb != null) rb.bodyType = RigidbodyType2D.Kinematic;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        if (NetIsDead) return;

        searchTargetTimer -= Runner.DeltaTime;
        if (searchTargetTimer <= 0f)
        {
            UpdateTargetMultiplayer();
            searchTargetTimer = 0.5f;
        }

        if (player == null)
        {
            if (agent.isOnNavMesh) agent.isStopped = true;
            NetSpeed = 0f;
            return;
        }

        if (stunTimer > 0f)
        {
            stunTimer -= Runner.DeltaTime;
            isStunned = stunTimer > 0f;
            if (isStunned)
            {
                agent.isStopped = true;
                rb.linearVelocity = Vector2.zero;
                return;
            }
        }

        if (cooldownTimer > 0f) cooldownTimer -= Runner.DeltaTime;

        if (isAttacking)
        {
            attackTimer -= Runner.DeltaTime;

            // XÓA TIMER TRỪ MÁU, ĐỂ DÀNH TRỪ MÁU CHO ANIMATION EVENT

            if (attackTimer <= 0f)
            {
                isAttacking = false;
                NetIsAttacking = false;
            }
        }

        if (CanSeePlayer())
        {
            isChasing = true;
            isInvestigating = false;
            loseTimer = loseTime;
        }
        else if (isChasing)
        {
            loseTimer -= Runner.DeltaTime;
            if (loseTimer <= 0f) isChasing = false;
        }

        ColliderDistance2D collDist = Physics2D.Distance(myCol, playerCol);
        float distance = Mathf.Max(collDist.distance, 0f);

        Vector2 targetPos = playerCol.bounds.center;
        Vector2 myPos = myCol.bounds.center;
        Vector2 dirToPlayer = (targetPos - myPos).normalized;

        RaycastHit2D wallCheck = Physics2D.Raycast(myPos, dirToPlayer, distance, obstacleMask);
        bool noWallInBetween = wallCheck.collider == null;

        if (distance <= attackRange && noWallInBetween && isChasing && !isAttacking && cooldownTimer <= 0f)
        {
            int attackIndex = Random.Range(1, 3);
            NetAttackIndex = attackIndex;
            NetIsAttacking = true;

            isAttacking = true;
            hasAppliedDamage = false; // Mở khóa sát thương cho cú cào mới
            attackTimer = attackDuration;
            cooldownTimer = attackCooldown;

            agent.isStopped = true;
            rb.linearVelocity = Vector2.zero;
        }
        else if (isChasing && !isAttacking)
        {
            agent.isStopped = false;
            agent.speed = speed;

            pathRecalcTimer -= Runner.DeltaTime;
            if (pathRecalcTimer <= 0f)
            {
                agent.SetDestination(targetPos);
                pathRecalcTimer = 0.2f;
            }
        }
        else if (isInvestigating && !isAttacking && !isChasing)
        {
            float distToSound = Vector2.Distance(myPos, investigateTarget);
            if (distToSound > 0.5f)
            {
                agent.isStopped = false;
                agent.speed = speed * 0.7f;
                agent.SetDestination(investigateTarget);
            }
            else
            {
                agent.isStopped = true;
                investigateTimer -= Runner.DeltaTime;
                if (investigateTimer <= 0f) isInvestigating = false;
            }
        }
        else if (!isAttacking)
        {
            agent.isStopped = true;
        }

        if (isAttacking)
        {
            lastMoveDirection = Vector2.Lerp(lastMoveDirection, dirToPlayer, 20f * Runner.DeltaTime);
        }
        else if (agent.velocity.sqrMagnitude > 0.01f)
        {
            lastMoveDirection = Vector2.Lerp(lastMoveDirection, agent.velocity.normalized, 15f * Runner.DeltaTime);
        }

        NetMoveDir = lastMoveDirection;
        NetSpeed = isAttacking ? 0f : agent.velocity.magnitude;
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
        if (isChasing && player != null && player.gameObject.activeInHierarchy) return;

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
    }

    private bool CanSeePlayer()
    {
        if (playerCol == null) return false;

        Vector2 myPos = myCol.bounds.center;
        Vector2 targetPos = playerCol.bounds.center;
        Vector2 toPlayer = targetPos - myPos;
        float distance = toPlayer.magnitude;

        if (distance > detectionRange) return false;

        Vector2 forward = isChasing ? toPlayer.normalized : (lastMoveDirection == Vector2.zero ? Vector2.up : lastMoveDirection.normalized);

        if (Vector2.Angle(forward, toPlayer) > viewAngle * 0.5f) return false;

        RaycastHit2D hit = Physics2D.Raycast(myPos, toPlayer.normalized, distance, obstacleMask);
        return hit.collider == null || hit.collider.gameObject == player.gameObject;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_HearSound(Vector2 soundPos)
    {
        if (NetIsDead || isChasing) return;
        isInvestigating = true;
        investigateTarget = soundPos;
        investigateTimer = 3f;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_TakeDamage(float damage)
    {
        if (NetIsDead) return;

        CurrentHealth -= damage;
        CurrentHealth = Mathf.Clamp(CurrentHealth, 0f, maxHealth);

        if (CurrentHealth <= 0f)
        {
            Die();
            return;
        }

        stunTimer = stunDuration;
        isStunned = true;
        isAttacking = false;
        NetIsAttacking = false;
        agent.isStopped = true;

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

    private void Die()
    {
        if (NetIsDead) return;
        NetIsDead = true;

        rb.linearVelocity = Vector2.zero;
        myCol.enabled = false;
        if (agent != null) agent.enabled = false;

        StartCoroutine(VanishRoutine());
    }

    private IEnumerator VanishRoutine()
    {
        yield return new WaitForSeconds(5f);
        if (HasStateAuthority) Runner.Despawn(Object);
    }

    // ========================================================
    // 🔥 ĐẠO DIỄN ĐÃ GỌI TỪ ANIMATION EVENT THÌ XÀI HÀM NÀY!
    // ========================================================
    public void TriggerAttackDamage()
    {
        // CHỈ SERVER ĐƯỢC PHÉP TRỪ MÁU ĐỂ TRÁNH CLONE TỰ TRỪ GÂY LỆCH MẠNG
        if (!HasStateAuthority) return;

        if (hasAppliedDamage || NetIsDead || playerHealth == null || playerCol == null) return;

        // Cộng thêm 0.5 khoảng cách để bù trừ trễ mạng (Ping)
        float currentDist = Vector2.Distance(myCol.bounds.center, playerCol.bounds.center);
        if (currentDist <= attackRange + 0.5f)
        {
            playerHealth.TakeDamage(zombieDamage);
            hasAppliedDamage = true; // 1 cú vung tay chỉ cắn 1 lần
        }
    }
}