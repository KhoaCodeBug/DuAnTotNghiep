using UnityEngine;
using System.Collections;

public class ZOmbieAI_Khoa : MonoBehaviour
{
    [Header("Movement")]
    public float speed = 2f;
    Collider2D playerCol;
    Collider2D myCol;

    [Header("Damage")]
    public float zombieDamage = 15f;
    private PlayerHealth playerHealth;

    [Header("Vision")]
    public float detectionRange = 3f;
    public float viewAngle = 90f;
    public LayerMask obstacleMask;

    [Header("Chase Memory")]
    public float loseTime = 5f;
    float loseTimer = 0f;
    bool isChasing = false;

    [Header("Attack")]
    public float attackRange = 1.2f;
    public float attackDuration = 0.8f;
    public float attackCooldown = 1.5f;

    float attackTimer = 0f;
    float cooldownTimer = 0f;

    bool isAttacking = false;
    int attackIndex = 0;

    Transform player;
    Rigidbody2D rb;
    Animator anim;

    Vector2 movement;
    Vector2 lastMove;

    // =======================================================
    // 🔥 MỚI: PHẦN CODE CỦA BẠN (MÁU, SÁT THƯƠNG, HIỆU ỨNG)
    // =======================================================
    [Header("--- Zombie Stats (MỚI) ---")]
    public float maxHealth = 100f;
    public float currentHealth;

    [Tooltip("Thời gian zombie bị khựng lại khi trúng đạn")]
    public float stunDuration = 0.3f;
    public Color hurtColor = Color.red;

    public bool isDead { get; private set; } = false;
    private bool isStunned = false;
    private float stunTimer = 0f;

    private SpriteRenderer spriteRend;
    private Color originalColor;
    private bool isFlashing = false;
    // =======================================================

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player").transform;
        playerHealth = player.GetComponent<PlayerHealth>();
        playerCol = player.GetComponent<Collider2D>();
        myCol = GetComponent<Collider2D>();

        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();

        // 🔥 MỚI: Khởi tạo máu và màu sắc cho Zombie
        currentHealth = maxHealth;
        spriteRend = GetComponentInChildren<SpriteRenderer>();
        if (spriteRend != null) originalColor = spriteRend.color;
    }

    void Update()
    {
        // 🔥 MỚI: Chết rồi thì không làm gì cả
        if (isDead) return;

        // 🔥 MỚI: Đếm lùi thời gian choáng. Nếu đang choáng thì đứng im chịu trận!
        if (stunTimer > 0)
        {
            stunTimer -= Time.deltaTime;
            isStunned = stunTimer > 0;
            if (isStunned)
            {
                movement = Vector2.zero;
                return; // Cắt ngang logic, không cho đuổi hay cắn
            }
        }

        // ===== TIMER =====
        if (cooldownTimer > 0) cooldownTimer -= Time.deltaTime;

        if (isAttacking)
        {
            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0) isAttacking = false;
        }

        // ===== 1. VISION =====
        if (CanSeePlayer())
        {
            isChasing = true;
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

        if (distance > 0.2f)
        {
            Vector2 dir = (targetPos - myPos).normalized;
            if (dir != Vector2.zero) lastMove = dir;
        }

        // ===== 3. ATTACK =====
        if (distance <= attackRange && !isAttacking && cooldownTimer <= 0)
        {
            attackIndex = Random.Range(1, 3);

            anim.SetInteger("AttackIndex", attackIndex);
            anim.SetTrigger("Attack");

            isAttacking = true;
            attackTimer = attackDuration;
            cooldownTimer = attackCooldown;
        }

        // ===== 4. MOVEMENT =====
        if (isChasing && !isAttacking)
        {
            if (distance > attackRange)
            {
                Vector2 direction = (targetPos - myPos).normalized;
                movement = direction;
            }
            else
            {
                movement = Vector2.zero;
            }
        }
        else
        {
            movement = Vector2.zero;
        }

        // ===== 5. ANIMATOR =====
        anim.SetFloat("MoveX", lastMove.x);
        anim.SetFloat("MoveY", lastMove.y);
        anim.SetFloat("Speed", movement.magnitude);
    }

    void FixedUpdate()
    {
        // 🔥 MỚI: Chết hoặc Choáng thì không di chuyển vật lý
        if (isDead || isStunned) return;

        rb.MovePosition(rb.position + movement * speed * Time.fixedDeltaTime);
    }

    // =======================================================
    // 🔥 MỚI: HÀM NHẬN SÁT THƯƠNG VÀ CHẾT DÀNH CHO ZOMBIE
    // =======================================================
    public void TakeDamage(float damage)
    {
        if (isDead) return;

        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        Debug.Log("Zombie trúng đạn! Máu còn: " + currentHealth);

        if (currentHealth <= 0)
        {
            Die();
            return;
        }

        // Gây choáng và gọi animation giật mình
        stunTimer = stunDuration;
        isStunned = true;
        if (anim != null) anim.SetTrigger("TakeDamage");

        if (spriteRend != null && !isFlashing)
        {
            StartCoroutine(FlashHurtRoutine());
        }
    }

    private IEnumerator FlashHurtRoutine()
    {
        isFlashing = true;
        spriteRend.color = hurtColor;
        yield return new WaitForSeconds(0.1f);
        spriteRend.color = originalColor;
        isFlashing = false;
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;
        Debug.Log("Zombie đã bị tiêu diệt!");

        if (anim != null) anim.SetBool("IsDead", true);

        // Nằm im, tắt va chạm để không cản đường Player
        rb.linearVelocity = Vector2.zero;
        if (myCol != null) myCol.enabled = false;

        StopAllCoroutines();
        if (spriteRend != null) spriteRend.color = originalColor;

        // Bắt đầu quy trình nằm chờ 5 giây rồi xóa xác
        StartCoroutine(VanishRoutine());
    }

    // ĐÃ SỬA: Bỏ chớp chớp, nằm im 5 giây rồi biến mất
    private IEnumerator VanishRoutine()
    {
        yield return new WaitForSeconds(5f); // Chờ 5 giây (Bạn có thể đổi số này tùy ý)
        Destroy(gameObject); // Bụp! Xác biến mất.
    }
    // =======================================================

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
        ColliderDistance2D collDist = Physics2D.Distance(myCol, playerCol);
        if (collDist.distance <= attackRange + 0.2f)
        {
            if (playerHealth != null) playerHealth.TakeDamage(zombieDamage);
        }
    }
}