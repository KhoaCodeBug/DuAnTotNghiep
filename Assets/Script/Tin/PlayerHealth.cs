using UnityEngine;
using System.Collections;

public class PlayerHealth : MonoBehaviour
{
    [Header("Chỉ số Máu")]
    public float maxHealth = 100f;
    public float currentHealth;

    [Header("Hiệu ứng khi bị đánh")]
    [Tooltip("Thời gian bị khóa di chuyển (giây) khi trúng đòn")]
    public float stunDuration = 0.4f;

    [Tooltip("Màu sẽ chớp lên khi bị dính đòn")]
    public Color hurtColor = Color.red;
    [Tooltip("Thời gian chớp màu (0.1 là một phần mười giây)")]
    public float flashDuration = 0.1f;

    private PlayerMovement movementScript;
    private Animator anim;
    private SpriteRenderer spriteRend;
    private Color originalColor;
    private bool isFlashing = false;

    // 🔥 MỚI: Biến kiểm tra xem đã chết chưa để khóa mọi thứ
    public bool isDead { get; private set; } = false;

    private void Start()
    {
        currentHealth = maxHealth;
        movementScript = GetComponent<PlayerMovement>();
        anim = GetComponent<Animator>();
        spriteRend = GetComponentInChildren<SpriteRenderer>();

        if (spriteRend != null)
        {
            originalColor = spriteRend.color;
        }
    }

    public void TakeDamage(float damage)
    {
        // 🔥 Nếu đã chết rồi thì bỏ qua luôn, không nhận thêm sát thương hay giật mình nữa
        if (isDead) return;

        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        Debug.Log("Bị thương! Máu còn: " + currentHealth);

        if (currentHealth <= 0)
        {
            Die();
            return; // Thoát hàm luôn, không chạy các hiệu ứng TakeDamage bên dưới nữa
        }

        // Chỉ chạy hiệu ứng giật mình & chớp đỏ khi CHƯA chết
        if (anim != null) anim.SetTrigger("TakeDamage");

        if (spriteRend != null && !isFlashing)
        {
            StartCoroutine(FlashHurtRoutine());
        }

        if (movementScript != null)
        {
            movementScript.LockMovement(stunDuration);
        }
    }

    private IEnumerator FlashHurtRoutine()
    {
        isFlashing = true;
        spriteRend.color = hurtColor;
        yield return new WaitForSeconds(flashDuration);
        spriteRend.color = originalColor;
        isFlashing = false;
    }

    public void Heal(float amount)
    {
        if (isDead) return; // Chết rồi thì không bơm máu được nữa
        currentHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        Debug.Log("Đã hồi máu! Máu hiện tại: " + currentHealth);
    }

    // --- 🔥 ĐÃ VIẾT LẠI HÀM DIE ---
    private void Die()
    {
        if (isDead) return;
        isDead = true;

        Debug.Log("Player đã CHẾT!");

        if (anim != null) anim.SetBool("IsDead", true);

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.linearVelocity = Vector2.zero;

        if (movementScript != null) movementScript.enabled = false;

        Collider2D coll = GetComponent<Collider2D>();
        if (coll != null) coll.enabled = false;

        StopAllCoroutines();
        if (spriteRend != null) spriteRend.color = originalColor;

        // 🔥 THÊM DÒNG NÀY: Gọi hiệu ứng chớp rồi biến mất
        StartCoroutine(BlinkAndVanishRoutine());
    }

    // --- 🔥 COROUTINE MỚI: Xử lý chớp xác ---
    private IEnumerator BlinkAndVanishRoutine()
    {
        // 1. Nằm yên n giây cho người chơi "thấm" nỗi đau
        yield return new WaitForSeconds(1f);

        // 2. Chớp nháy 5 lần (tắt/bật SpriteRenderer)
        for (int i = 0; i < 5; i++)
        {
            if (spriteRend != null) spriteRend.enabled = false;
            yield return new WaitForSeconds(0.15f);

            if (spriteRend != null) spriteRend.enabled = true;
            yield return new WaitForSeconds(0.15f);
        }

        // 3. Biến mất hoàn toàn (Có thể dùng Destroy(gameObject) nhưng SetActive an toàn hơn)
        gameObject.SetActive(false);
    }
}