using UnityEngine;

public class ZOmbieAI_Khoa : MonoBehaviour
{
    [Header("Movement")]
    public float speed = 2f;
    Collider2D playerCol;
    Collider2D myCol;

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
        // 🔥 FIX: Dùng Physics2D.Distance để lấy khoảng cách mép-mép chính xác nhất
        ColliderDistance2D collDist = Physics2D.Distance(myCol, playerCol);
        float distance = collDist.distance;
        if (distance < 0) distance = 0f; // Tránh lỗi số âm khi 2 collider vô tình đè lên nhau

        // Lấy tọa độ tâm của 2 collider để tính toán hướng đi chuẩn xác
        Vector2 targetPos = playerCol.bounds.center;
        Vector2 myPos = myCol.bounds.center;

        // Cập nhật hướng nhìn khi đang chase hoặc gần player
        if (distance > 0.2f) // Tránh jitter khi quá gần
        {
            Vector2 dir = (targetPos - myPos).normalized;
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
                // 🔥 FIX: Nhắm vào giữa người Player thay vì nhắm vào gót chân
                Vector2 direction = (targetPos - myPos).normalized;
                movement = direction;
            }
            else
            {
                movement = Vector2.zero; // ĐỨNG YÊN KHI ĐỦ TẦM
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
        // Đã xóa anim.SetInteger ở đây để tránh đè logic của Trigger Attack
    }

    void FixedUpdate()
    {
        rb.MovePosition(rb.position + movement * speed * Time.fixedDeltaTime);
    }

    // ===== VISION SYSTEM =====
    bool CanSeePlayer()
    {
        // 🔥 FIX: Mắt zombie (tâm) nhìn vào người player (tâm)
        Vector2 myPos = myCol.bounds.center;
        Vector2 targetPos = playerCol.bounds.center;

        Vector2 toPlayer = targetPos - myPos;
        float distance = toPlayer.magnitude;

        if (distance > detectionRange)
            return false;

        Vector2 forward;
        if (isChasing)
            forward = toPlayer.normalized;
        else
            forward = lastMove == Vector2.zero ? Vector2.up : lastMove.normalized;

        float angle = Vector2.Angle(forward, toPlayer);

        if (angle > viewAngle * 0.5f)
            return false;

        // Bắn tia raycast xem có bị tường cản không
        RaycastHit2D hit = Physics2D.Raycast(myPos, toPlayer.normalized, distance, obstacleMask);

        // Nếu tia ray đụng vật cản (và vật cản đó không phải là chính Player) thì ko nhìn thấy
        if (hit.collider != null && hit.collider.gameObject != player.gameObject)
            return false;

        return true;
    }

    void OnDrawGizmosSelected()
    {
        // Vẽ vòng tầm đánh từ tâm của Zombie để dễ nhìn hơn trên Scene
        Gizmos.color = Color.red;
        if (Application.isPlaying && myCol != null)
        {
            Gizmos.DrawWireSphere(myCol.bounds.center, attackRange);
        }
        else
        {
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }
}