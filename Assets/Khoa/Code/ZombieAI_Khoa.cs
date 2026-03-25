using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class ZOmbieAI_Khoa : MonoBehaviour
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
    [SerializeField] private float attackDuration = 1.4f;     // Zombie đứng yên đánh 1.4s
    [SerializeField] private float attackCooldown = 1.5f;

    private float attackTimer;
    private float cooldownTimer;
    private bool isAttacking;
    private bool hasAppliedDamage;

    [Header("=== Zombie Stats ===")]
    [SerializeField] private float maxHealth = 100f;
    private float currentHealth;
    [SerializeField] private float stunDuration = 5f;         // ← ĐÃ SET 5 GIÂY
    [SerializeField] private Color hurtColor = Color.red;

    public bool isDead { get; private set; }
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

    private void Start()
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
        currentHealth = maxHealth;
    }

    private void Update()
    {
        if (isDead) return;

        // 1. Tìm Player multiplayer
        searchTargetTimer -= Time.deltaTime;
        if (searchTargetTimer <= 0f)
        {
            UpdateTargetMultiplayer();
            searchTargetTimer = 0.5f;
        }

        if (player == null)
        {
            if (agent.isOnNavMesh) agent.isStopped = true;
            anim.SetFloat("Speed", 0f);
            return;
        }

        // 2. STUN LOGIC (đã kiểm tra kỹ - sẽ dừng đúng 5 giây)
        if (stunTimer > 0f)
        {
            stunTimer -= Time.deltaTime;
            isStunned = stunTimer > 0f;
            if (isStunned)
            {
                agent.isStopped = true;
                return;
            }
        }

        if (cooldownTimer > 0f) cooldownTimer -= Time.deltaTime;

        if (isAttacking)
        {
            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0f)
            {
                isAttacking = false;
                anim.SetBool("IsAttacking", false);
            }
        }

        // 3. Vision + Chase
        if (CanSeePlayer())
        {
            isChasing = true;
            isInvestigating = false;
            loseTimer = loseTime;
        }
        else if (isChasing)
        {
            loseTimer -= Time.deltaTime;
            if (loseTimer <= 0f) isChasing = false;
        }

        ColliderDistance2D collDist = Physics2D.Distance(myCol, playerCol);
        float distance = Mathf.Max(collDist.distance, 0f);

        Vector2 targetPos = playerCol.bounds.center;
        Vector2 myPos = myCol.bounds.center;
        Vector2 dirToPlayer = (targetPos - myPos).normalized;

        RaycastHit2D wallCheck = Physics2D.Raycast(myPos, dirToPlayer, distance, obstacleMask);
        bool noWallInBetween = wallCheck.collider == null;

        // 4. Attack hoặc Chase
        if (distance <= attackRange && noWallInBetween && isChasing && !isAttacking && cooldownTimer <= 0f)
        {
            int attackIndex = Random.Range(1, 3);
            anim.SetInteger("AttackIndex", attackIndex);
            anim.SetBool("IsAttacking", true);

            isAttacking = true;
            hasAppliedDamage = false;
            attackTimer = attackDuration;
            cooldownTimer = attackCooldown;

            agent.isStopped = true;
        }
        else if (isChasing && !isAttacking)
        {
            agent.isStopped = false;
            agent.speed = speed;

            pathRecalcTimer -= Time.deltaTime;
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
                investigateTimer -= Time.deltaTime;
                if (investigateTimer <= 0f) isInvestigating = false;
            }
        }
        else if (!isAttacking)
        {
            agent.isStopped = true;
        }

        // 5. Xoay mặt
        if (isAttacking)
        {
            lastMoveDirection = Vector2.Lerp(lastMoveDirection, dirToPlayer, 20f * Time.deltaTime);
        }
        else if (agent.velocity.sqrMagnitude > 0.01f)
        {
            lastMoveDirection = Vector2.Lerp(lastMoveDirection, agent.velocity.normalized, 15f * Time.deltaTime);
        }

        // 6. Animation
        anim.SetFloat("MoveX", lastMoveDirection.x);
        anim.SetFloat("MoveY", lastMoveDirection.y);
        anim.SetFloat("Speed", isAttacking ? 0f : agent.velocity.magnitude);
    }

    private void FixedUpdate()
    {
        if (isDead || isStunned || isAttacking || agent.isStopped)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    // ====================== MULTIPLAYER & VISION (giữ nguyên) ======================
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

    // ====================== PUBLIC ======================
    public void HearSound(Vector2 soundPos)
    {
        if (isDead || isChasing) return;
        isInvestigating = true;
        investigateTarget = soundPos;
        investigateTimer = 3f;
    }

    public void TakeDamage(float damage)
    {
        if (isDead) return;

        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

        if (currentHealth <= 0f)
        {
            Die();
            return;
        }

        stunTimer = stunDuration;           // ← Set lại 5 giây mỗi lần bị đạn
        isStunned = true;
        isAttacking = false;
        anim.SetBool("IsAttacking", false);
        agent.isStopped = true;

        if (anim != null)
        {
            anim.SetTrigger("TakeDamage");
        }

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
        if (!isDead) spriteRend.color = originalColor;
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        anim.SetBool("IsDead", true);
        anim.SetBool("IsAttacking", false);

        rb.linearVelocity = Vector2.zero;
        myCol.enabled = false;
        agent.enabled = false;

        StopAllCoroutines();
        StartCoroutine(VanishRoutine());
    }

    private IEnumerator VanishRoutine()
    {
        yield return new WaitForSeconds(5f);
        Destroy(gameObject);
    }

    public void TriggerAttackDamage()
    {
        if (hasAppliedDamage || isDead || playerCol == null) return;

        if (Physics2D.Distance(myCol, playerCol).distance <= attackRange + 0.2f)
        {
            Vector2 dirToPlayer = (playerCol.bounds.center - myCol.bounds.center).normalized;
            float angle = Vector2.Angle(lastMoveDirection.normalized, dirToPlayer);

            if (angle <= 60f && playerHealth != null)
            {
                playerHealth.TakeDamage(zombieDamage);
                hasAppliedDamage = true;
            }
        }
    }
}