using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [Header("Chỉ số Máu")]
    public float maxHealth = 100f;
    public float currentHealth;

    [Header("Hiệu ứng khi bị đánh")]
    [Tooltip("Thời gian bị khóa di chuyển (giây) khi trúng đòn")]
    public float stunDuration = 0.4f; // 🔥 MỚI: Biến này sẽ hiện ra ở Inspector

    // Lấy component di chuyển
    private PlayerMovement movementScript;

    private void Start()
    {
        currentHealth = maxHealth;
        movementScript = GetComponent<PlayerMovement>(); // Tìm script movement
    }

    // Hàm gọi khi bị zombie cào
    public void TakeDamage(float damage)
    {
        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        Debug.Log("Bị thương! Máu còn: " + currentHealth);

        // 🔥 MỚI: Đã thay 0.4f cứng nhắc bằng biến stunDuration
        if (movementScript != null)
        {
            movementScript.LockMovement(stunDuration);
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void Heal(float amount)
    {
        currentHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        Debug.Log("Đã hồi máu! Máu hiện tại: " + currentHealth);
    }

    private void Die()
    {
        Debug.Log("Player đã CHẾT!");
        // Chèn logic game over, animation gục ngã vào đây
    }
}