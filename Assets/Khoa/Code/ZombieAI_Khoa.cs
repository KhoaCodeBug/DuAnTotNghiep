using UnityEngine;

public class ZOmbieAI_Khoa : MonoBehaviour
{
    [Header("Movement")]
    public float speed = 2f;
    Collider2D playerCol; // mới nè
    Collider2D myCol; // mới nè
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

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player").transform;

        playerCol = player.GetComponent<Collider2D>();
        myCol = GetComponent<Collider2D>();

        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
    }

    void Update()
    {
        // ===== TIMER =====
        if (cooldownTimer > 0)
            cooldownTimer -= Time.deltaTime;

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
            loseTimer = loseTime;
        }
        else if (isChasing)
        {
            loseTimer -= Time.deltaTime;

            if (loseTimer <= 0)
                isChasing = false;
        }
 
        // ===== 2. DISTANCE =====
        float distance = Vector2.Distance(myCol.ClosestPoint(player.position), playerCol.ClosestPoint(transform.position)); // mới nè

        // 🔥 FIX: luôn cập nhật hướng nhìn khi đang chase hoặc gần player
        if (distance > 0.2f) // 🔥 tránh jitter khi quá gần
        {
            Vector2 dir = (player.position - transform.position).normalized;
            if (dir != Vector2.zero)
                lastMove = dir;
        }

        // ===== 3. ATTACK =====
        if (distance <= attackRange && !isAttacking && cooldownTimer <= 0)
        {
            attackIndex = Random.Range(1, 3); // chỉ atk2, atk3

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
                Vector2 direction = (player.position - transform.position).normalized;
                movement = direction;
            }
            else
            {
                movement = Vector2.zero; // 🔥 ĐỨNG YÊN KHI ĐỦ TẦM
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
        anim.SetInteger("AttackIndex", attackIndex);
    }

    void FixedUpdate()
    {
        rb.MovePosition(rb.position + movement * speed * Time.fixedDeltaTime);
    }

    // ===== VISION SYSTEM =====
    bool CanSeePlayer()
    {
        Vector2 toPlayer = player.position - transform.position;
        float distance = toPlayer.magnitude;

        if (distance > detectionRange)
            return false;

        // 🔥 FIX: nếu đang chase thì luôn nhìn về player
        Vector2 forward;
        if (isChasing)
            forward = toPlayer.normalized;
        else
            forward = lastMove == Vector2.zero ? Vector2.up : lastMove.normalized;

        float angle = Vector2.Angle(forward, toPlayer);

        if (angle > viewAngle * 0.5f)
            return false;

        RaycastHit2D hit = Physics2D.Raycast(transform.position, toPlayer.normalized, distance, obstacleMask);

        if (hit.collider != null)
            return false;

        return true;
    }
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}