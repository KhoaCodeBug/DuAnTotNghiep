using UnityEngine;
using System.Collections;

public class ZOmbieAI_Khoa : MonoBehaviour
/*{
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

    // 🔥 HỆ THỐNG THÍNH GIÁC (HEARING)
    [Header("--- Hearing ---")]
    public bool isInvestigating = false;
    private Vector2 investigateTarget;
    private float investigateTimer = 0f; // Thời gian đứng ngó nghiêng khi tới nơi

    [Header("Attack")]
    public float attackRange = 1.2f;
    [Tooltip("Phải set thời gian này BẰNG VỚI độ dài của clip animation Attack nhé!")]
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
        Idle,
        Wander,
        Chase,
        Investigate,
        Attack,
        Stunned,
        Dead
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
                movement = Vector2.zero;
                return; // Đang choáng thì bỏ qua mọi tư duy đuổi/đánh
            }
        }

        // ===== XỬ LÝ THỜI GIAN ĐÁNH =====
        if (cooldownTimer > 0) cooldownTimer -= Time.deltaTime;

        if (isAttacking)
        {
            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0)
            {
                isAttacking = false; // Hết thời gian vung tay, cho phép đi tiếp
            }
        }

        // ===== 1. VISION =====
        if (CanSeePlayer())
        {
            isChasing = true;
            isInvestigating = false; // Thấy rồi thì dẹp tò mò, dí luôn!
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

        // 🔥 FIX: HỆ THỐNG XOAY MẶT THÔNG MINH
        // Cập nhật hướng nhìn liên tục khi đang đuổi HOẶC đang đánh
        if (isChasing || isAttacking)
        {
            Vector2 dirToPlayer = (targetPos - myPos).normalized;

            // Chỉ cập nhật mặt nếu khoảng cách > 0.1 để tránh giật mặt khi đứng quá sát nhau đè lên nhau
            if (distance > 0.1f)
            {
                // Thêm tí "độ trễ" (Lerp) nhẹ để khi xoay mặt nó không bị giật cục đùng 1 cái
                lastMove = Vector2.Lerp(lastMove, dirToPlayer, 15f * Time.deltaTime).normalized;
            }
        }

        // ===== 3. ATTACK =====
        if (distance <= attackRange && isChasing && !isAttacking && cooldownTimer <= 0)
        {
            attackIndex = Random.Range(1, 3);
            anim.SetInteger("AttackIndex", attackIndex);
            anim.SetTrigger("Attack");

            isAttacking = true;
            attackTimer = attackDuration;
            cooldownTimer = attackCooldown;
        }

        // ===== 4. MOVEMENT LOGIC =====
        if (isChasing && !isAttacking)
        {
            if (distance > attackRange)
                movement = (targetPos - myPos).normalized;
            else
                movement = Vector2.zero;
        }
        else if (isInvestigating && !isAttacking && !isChasing)
        {
            float distToSound = Vector2.Distance(myPos, investigateTarget);
            if (distToSound > 0.5f) // Chưa tới nơi
            {
                movement = (investigateTarget - myPos).normalized;
                lastMove = movement;
            }
            else // Tới nơi rồi
            {
                movement = Vector2.zero;
                investigateTimer -= Time.deltaTime;
                if (investigateTimer <= 0)
                {
                    isInvestigating = false;
                }
            }
        }
        else
        {
            movement = Vector2.zero;
        }

        // ===== 5. ANIMATOR =====
        // Mặc dù đứng im đánh, nhưng truyền lastMove vào để Blend Tree xoay đúng hướng
        anim.SetFloat("MoveX", lastMove.x);
        anim.SetFloat("MoveY", lastMove.y);

        // Nếu đang đánh thì ép speed về 0 để chân không bước (không trượt)
        anim.SetFloat("Speed", isAttacking ? 0f : movement.magnitude);
    }

    void FixedUpdate()
    {
        // 🔥 FIX "TRƯỢT BĂNG/VỪA ĐI VỪA ĐÁNH":
        // Nếu Đang Chết OR Đang Choáng OR Đang Đánh -> Bắt chôn chân tại chỗ!
        if (isDead || isStunned || isAttacking)
        {
            rb.linearVelocity = Vector2.zero; // Cắt đứt hoàn toàn quán tính trôi dạt
            return;
        }

        float currentSpeed = isInvestigating ? (speed * 0.5f) : speed;
        rb.MovePosition(rb.position + movement * currentSpeed * Time.fixedDeltaTime);
    }

    // Hàm nghe tiếng động
    public void HearSound(Vector2 soundPos)
    {
        if (isDead || isChasing) return;

        isInvestigating = true;
        investigateTarget = soundPos;
        investigateTimer = 3f;
    }

    // Hàm nhận sát thương
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

        // Quá trình bị choáng sẽ ngắt luôn quá trình đang tấn công
        isAttacking = false;

        if (anim != null)
        {
            // 🔥 FIX: Hủy bỏ các Trigger cũ đang bị xếp hàng chờ
            anim.ResetTrigger("Attack");
            anim.ResetTrigger("TakeDamage");

            // Gọi Trigger mới
            anim.SetTrigger("TakeDamage");
        }

        // 🔥 THÊM: Gọi hiệu ứng chớp đỏ khi trúng đạn
        if (spriteRend != null)
        {
            StopCoroutine("FlashRedRoutine");
            StartCoroutine("FlashRedRoutine");
        }
    }

    // Coroutine xử lý chớp đỏ cực nhanh
    private IEnumerator FlashRedRoutine()
    {
        spriteRend.color = hurtColor; // Đổi sang màu đỏ
        yield return new WaitForSeconds(0.1f); // Giữ trong 0.1 giây

        // Trả lại màu gốc (nếu nó chưa chết)
        if (!isDead) spriteRend.color = originalColor;
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;
        if (anim != null) anim.SetBool("IsDead", true);

        rb.linearVelocity = Vector2.zero;
        if (myCol != null) myCol.enabled = false;

        StopAllCoroutines();
        if (spriteRend != null) spriteRend.color = originalColor;

        StartCoroutine(VanishRoutine());
    }

    // 🔥 Đã dẹp bỏ trò chớp chớp rẻ tiền, giờ chết nằm im 5 giây rồi tan biến
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
        // 1. Kiểm tra khoảng cách
        ColliderDistance2D collDist = Physics2D.Distance(myCol, playerCol);
        if (collDist.distance <= attackRange + 0.2f)
        {
            // 2. 🔥 MỚI: KIỂM TRA GÓC ĐÁNH (Chỉ chém trúng kẻ thù trước mặt)
            Vector2 myPos = myCol.bounds.center;
            Vector2 targetPos = playerCol.bounds.center;

            // Hướng từ Zombie tới Player
            Vector2 dirToPlayer = (targetPos - myPos).normalized;
            // Hướng mặt hiện tại của Zombie (đã được lưu ở lastMove)
            Vector2 zombieFacingDir = lastMove.normalized;

            // Tính góc lệch giữa mặt Zombie và vị trí Player
            float angle = Vector2.Angle(zombieFacingDir, dirToPlayer);

            // Nếu Player đứng trong vùng 120 độ trước mặt (mỗi bên 60 độ)
            if (angle <= 60f)
            {
                if (playerHealth != null)
                {
                    playerHealth.TakeDamage(zombieDamage);
                }
            }
            else
            {
                // Player đã luồn ra sau lưng kịp lúc!
                Debug.Log("Né đẹp! Zombie vồ hụt vào không khí!");
            }
        }
        else
        {
            // Player đã lùi ra khỏi tầm kịp lúc!
            Debug.Log("Lùi hay! Zombie cào hụt!");
        }
    }
}*/
{
    [Header("Movement")]
    public float speed = 2f;
    Collider2D playerCol;
    Collider2D myCol;

    [Header("--- Crowd Control (Separation) ---")]
    public float separationRadius = 1.0f;
    public float separationWeight = 0.8f;
    public LayerMask zombieLayer;

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

    // 🔥 BIẾN MỚI: Dùng để kiểm tra xem Zombie đã gây sát thương trong đòn đánh này chưa
    bool hasAppliedDamage = false;
    int attackIndex = 0;

    Transform player;
    Rigidbody2D rb;
    Animator anim;
    Vector2 movement;
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
                movement = Vector2.zero;
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

        // Xoay mặt nhìn thẳng Player (Mượt mà)
        if (isChasing || isAttacking)
        {
            Vector2 dirToPlayer = (targetPos - myPos).normalized;
            if (distance > 0.1f)
            {
                lastMove = Vector2.Lerp(lastMove, dirToPlayer, 15f * Time.deltaTime).normalized;
            }
        }

        // ===== 3. ATTACK =====
        if (distance <= attackRange && isChasing && !isAttacking && cooldownTimer <= 0)
        {
            attackIndex = Random.Range(1, 3);
            anim.SetInteger("AttackIndex", attackIndex);
            anim.SetTrigger("Attack");

            isAttacking = true;
            hasAppliedDamage = false; // 🔥 RESET: Bắt đầu đòn đánh mới, cho phép gây sát thương lại
            attackTimer = attackDuration;
            cooldownTimer = attackCooldown;
        }

        // ===== 4. MOVEMENT LOGIC =====
        if (isChasing && !isAttacking)
        {
            if (distance > attackRange)
            {
                Vector2 dirToPlayer = (targetPos - myPos).normalized;
                Vector2 separationMove = GetSeparationVector();

                Vector2 targetMovement = (dirToPlayer + separationMove * separationWeight).normalized;
                movement = Vector2.Lerp(movement, targetMovement, 10f * Time.deltaTime);
            }
            else
            {
                movement = Vector2.zero;
                rb.linearVelocity = Vector2.zero;
            }
        }
        else if (isInvestigating && !isAttacking && !isChasing)
        {
            float distToSound = Vector2.Distance(myPos, investigateTarget);
            if (distToSound > 0.5f)
            {
                Vector2 dirToSound = (investigateTarget - myPos).normalized;
                Vector2 separationMove = GetSeparationVector();

                Vector2 targetMovement = (dirToSound + separationMove * separationWeight).normalized;
                movement = Vector2.Lerp(movement, targetMovement, 10f * Time.deltaTime);
                lastMove = movement;
            }
            else
            {
                movement = Vector2.zero;
                rb.linearVelocity = Vector2.zero;

                investigateTimer -= Time.deltaTime;
                if (investigateTimer <= 0)
                {
                    isInvestigating = false;
                }
            }
        }
        else
        {
            movement = Vector2.zero;
            rb.linearVelocity = Vector2.zero;
        }

        // ===== 5. ANIMATOR =====
        anim.SetFloat("MoveX", lastMove.x);
        anim.SetFloat("MoveY", lastMove.y);
        anim.SetFloat("Speed", isAttacking ? 0f : movement.magnitude);
    }

    void FixedUpdate()
    {
        if (isDead || isStunned || isAttacking)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        float currentSpeed = isInvestigating ? (speed * 0.5f) : speed;
        rb.MovePosition(rb.position + movement * currentSpeed * Time.fixedDeltaTime);
    }

    private Vector2 GetSeparationVector()
    {
        Vector2 separation = Vector2.zero;
        int count = 0;

        Collider2D[] nearbyZombies = Physics2D.OverlapCircleAll(myCol.bounds.center, separationRadius, zombieLayer);

        foreach (Collider2D z in nearbyZombies)
        {
            if (z != myCol && z.gameObject != gameObject && !z.gameObject.CompareTag("Player"))
            {
                Vector2 diff = (Vector2)myCol.bounds.center - (Vector2)z.bounds.center;
                float dist = diff.magnitude;

                if (dist > 0f && dist < separationRadius)
                {
                    float pushForce = (separationRadius - dist) / separationRadius;
                    separation += (diff.normalized * pushForce);
                    count++;
                }
            }
        }

        if (count > 0)
        {
            separation /= count;
        }

        return separation;
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

        Gizmos.color = Color.blue;
        if (Application.isPlaying && myCol != null) Gizmos.DrawWireSphere(myCol.bounds.center, separationRadius);
        else Gizmos.DrawWireSphere(transform.position, separationRadius);
    }

    public void TriggerAttackDamage()
    {
        // 🔥 KIỂM TRA: Nếu đã trừ máu rồi (hoặc Zombie đã chết) thì bỏ qua, không trừ tiếp
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
                    hasAppliedDamage = true; // 🔥 KHÓA LẠI: Đánh trúng rồi, khóa không cho trừ máu thêm lần 2
                }
            }
            else
            {
                Debug.Log("Né đẹp! Zombie vồ hụt vào không khí!");
            }
        }
        else
        {
            Debug.Log("Lùi hay! Zombie cào hụt!");
        }
    }
}