using UnityEngine;
using UnityEngine.AI; // 🔥 1. THƯ VIỆN BẮT BUỘC CỦA NAVMESH
using System.Collections;

public class ZOmbieAI_Khoa : MonoBehaviour
{
    [Header("Movement (NavMesh lo)")]
    public float speed = 2f;
    Collider2D playerCol;
    Collider2D myCol;

    [Header("Damage")]
    public float zombieDamage = 5f;
    private PlayerHealth playerHealth;

    [Header("Vision")]
    public float detectionRange = 3f;
    public float viewAngle = 90f;
    public LayerMask obstacleMask;

    [Header("Chase Memory")]
    public float loseTime = 5f;
    float loseTimer = 0f;
    bool isChasing = false;

    [Header("--- Hearing ---")]
    public bool isInvestigating = false;
    private Vector2 investigateTarget;
    private float investigateTimer = 0f;

    [Header("Attack")]
    public float attackRange = 1.2f;
    public float attackDuration = 0.8f;
    public float attackCooldown = 1.5f;

    float attackTimer = 0f;
    float cooldownTimer = 0f;
    bool isAttacking = false;

    bool hasAppliedDamage = false;
    int attackIndex = 0;

    Transform player;
    Rigidbody2D rb;
    Animator anim;

    // 🔥 2. KHAI BÁO BỘ NÃO AI
    NavMeshAgent agent;
    Vector2 lastMove;

    [Header("--- Zombie Stats ---")]
    public float maxHealth = 100f;
    public float currentHealth;
    public float stunDuration = 0.3f;
    public Color hurtColor = Color.red;

    public bool isDead { get; private set; } = false;
    private bool isStunned = false;
    private float stunTimer = 0f;
    private SpriteRenderer spriteRend;
    private Color originalColor;

    public enum ZombieState
    {
        Idle, Wander, Chase, Investigate, Attack, Stunned, Dead
    }

    public ZombieState currentState = ZombieState.Idle;

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player").transform;
        playerHealth = player.GetComponent<PlayerHealth>();
        playerCol = player.GetComponent<Collider2D>();
        myCol = GetComponent<Collider2D>();

        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();

        // 🔥 3. KHỞI TẠO NÃO AI VÀ KHÓA TRỤC 3D (Y HỆT TUTORIAL)
        agent = GetComponent<NavMeshAgent>();
        agent.updateRotation = false;
        agent.updateUpAxis = false;
        agent.speed = speed; // Đồng bộ tốc độ

        currentHealth = maxHealth;
        spriteRend = GetComponentInChildren<SpriteRenderer>();
        if (spriteRend != null) originalColor = spriteRend.color;
    }

    void Update()
    {
        if (isDead) return;

        // ===== XỬ LÝ CHOÁNG =====
        if (stunTimer > 0)
        {
            stunTimer -= Time.deltaTime;
            isStunned = stunTimer > 0;
            if (isStunned)
            {
                agent.isStopped = true; // 🔥 Bị choáng thì phanh AI lại
                return;
            }
        }

        // ===== XỬ LÝ THỜI GIAN ĐÁNH =====
        if (cooldownTimer > 0) cooldownTimer -= Time.deltaTime;

        if (isAttacking)
        {
            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0)
            {
                isAttacking = false;
            }
        }

        // ===== 1. VISION =====
        if (CanSeePlayer())
        {
            isChasing = true;
            isInvestigating = false;
            loseTimer = loseTime;
        }
        else if (isChasing)
        {
            loseTimer -= Time.deltaTime;
            if (loseTimer <= 0) isChasing = false;
        }

        // ===== 2. DISTANCE =====
        ColliderDistance2D collDist = Physics2D.Distance(myCol, playerCol);
        float distance = collDist.distance;
        if (distance < 0) distance = 0f;

        Vector2 targetPos = playerCol.bounds.center;
        Vector2 myPos = myCol.bounds.center;

        // Xoay mặt nhìn thẳng Player HOẶC nhìn theo hướng di chuyển của AI
        if (isChasing || isAttacking)
        {
            Vector2 dirToPlayer = (targetPos - myPos).normalized;
            if (distance > 0.1f)
            {
                lastMove = Vector2.Lerp(lastMove, dirToPlayer, 15f * Time.deltaTime).normalized;
            }
        }
        else if (agent.velocity.sqrMagnitude > 0.1f)
        {
            // Khi không đuổi player (như lúc investigate), nhìn theo hướng AI đang đi
            lastMove = Vector2.Lerp(lastMove, agent.velocity.normalized, 15f * Time.deltaTime).normalized;
        }

        // ===== 3. ATTACK =====
        if (distance <= attackRange && isChasing && !isAttacking && cooldownTimer <= 0)
        {
            attackIndex = Random.Range(1, 3);
            anim.SetInteger("AttackIndex", attackIndex);
            anim.SetTrigger("Attack");

            isAttacking = true;
            hasAppliedDamage = false;
            attackTimer = attackDuration;
            cooldownTimer = attackCooldown;

            agent.isStopped = true; // 🔥 Đang đánh thì dừng đi
        }

        // ===== 4. MOVEMENT LOGIC (BÀN GIAO CHO AI) =====
        if (isChasing && !isAttacking)
        {
            if (distance > attackRange)
            {
                // 🔥 THẦN CHÚ TỪ TUTORIAL: Chỉ đường cho AI chạy
                agent.isStopped = false;
                agent.speed = speed;
                agent.SetDestination(targetPos);
            }
            else
            {
                agent.isStopped = true;
            }
        }
        else if (isInvestigating && !isAttacking && !isChasing)
        {
            float distToSound = Vector2.Distance(myPos, investigateTarget);
            if (distToSound > 0.5f)
            {
                agent.isStopped = false;
                agent.speed = speed * 0.5f; // Đi chậm khi đi tìm tiếng ồn
                agent.SetDestination(investigateTarget);
            }
            else
            {
                agent.isStopped = true;
                investigateTimer -= Time.deltaTime;
                if (investigateTimer <= 0)
                {
                    isInvestigating = false;
                }
            }
        }
        else if (!isAttacking)
        {
            agent.isStopped = true;
        }

        // ===== 5. ANIMATOR =====
        anim.SetFloat("MoveX", lastMove.x);
        anim.SetFloat("MoveY", lastMove.y);
        // Lấy chính tốc độ thực của AI truyền vào Animator
        anim.SetFloat("Speed", isAttacking ? 0f : agent.velocity.magnitude);
    }

    void FixedUpdate()
    {
        // 🔥 Đã dẹp bỏ hàm rb.MovePosition. NavMesh sẽ lo chuyện di chuyển!
        // Chỉ dùng FixedUpdate để triệt tiêu các lực đẩy vật lý bậy bạ
        if (isDead || isStunned || isAttacking || agent.isStopped)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

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
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        if (currentHealth <= 0)
        {
            Die();
            return;
        }

        stunTimer = stunDuration;
        isStunned = true;
        isAttacking = false;

        if (agent != null) agent.isStopped = true; // Trúng đạn thì khựng AI lại

        if (anim != null)
        {
            anim.ResetTrigger("Attack");
            anim.ResetTrigger("TakeDamage");
            anim.SetTrigger("TakeDamage");
        }

        if (spriteRend != null)
        {
            StopCoroutine("FlashRedRoutine");
            StartCoroutine("FlashRedRoutine");
        }
    }

    private IEnumerator FlashRedRoutine()
    {
        spriteRend.color = hurtColor;
        yield return new WaitForSeconds(0.1f);
        if (!isDead) spriteRend.color = originalColor;
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;
        if (anim != null) anim.SetBool("IsDead", true);

        rb.linearVelocity = Vector2.zero;
        if (myCol != null) myCol.enabled = false;

        if (agent != null) agent.enabled = false; // Tắt não đi để khỏi tính toán đường nữa

        StopAllCoroutines();
        if (spriteRend != null) spriteRend.color = originalColor;

        StartCoroutine(VanishRoutine());
    }

    private IEnumerator VanishRoutine()
    {
        yield return new WaitForSeconds(5f);
        Destroy(gameObject);
    }

    bool CanSeePlayer()
    {
        Vector2 myPos = myCol.bounds.center;
        Vector2 targetPos = playerCol.bounds.center;
        Vector2 toPlayer = targetPos - myPos;
        float distance = toPlayer.magnitude;

        if (distance > detectionRange) return false;

        Vector2 forward;
        if (isChasing) forward = toPlayer.normalized;
        else forward = lastMove == Vector2.zero ? Vector2.up : lastMove.normalized;

        float angle = Vector2.Angle(forward, toPlayer);
        if (angle > viewAngle * 0.5f) return false;

        RaycastHit2D hit = Physics2D.Raycast(myPos, toPlayer.normalized, distance, obstacleMask);
        if (hit.collider != null && hit.collider.gameObject != player.gameObject) return false;

        return true;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        if (Application.isPlaying && myCol != null) Gizmos.DrawWireSphere(myCol.bounds.center, attackRange);
        else Gizmos.DrawWireSphere(transform.position, attackRange);
    }

    public void TriggerAttackDamage()
    {
        if (hasAppliedDamage || isDead) return;

        ColliderDistance2D collDist = Physics2D.Distance(myCol, playerCol);
        if (collDist.distance <= attackRange + 0.2f)
        {
            Vector2 myPos = myCol.bounds.center;
            Vector2 targetPos = playerCol.bounds.center;

            Vector2 dirToPlayer = (targetPos - myPos).normalized;
            Vector2 zombieFacingDir = lastMove.normalized;
            float angle = Vector2.Angle(zombieFacingDir, dirToPlayer);

            if (angle <= 60f)
            {
                if (playerHealth != null)
                {
                    playerHealth.TakeDamage(zombieDamage);
                    hasAppliedDamage = true;
                }
            }
            else Debug.Log("Né đẹp! Zombie vồ hụt vào không khí!");
        }
        else Debug.Log("Lùi hay! Zombie cào hụt!");
    }
}