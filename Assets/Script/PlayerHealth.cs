using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [Header("Chỉ số Máu")]
    public float maxHealth = 100f;
    public float currentHealth;

    private void Start()
    {
        currentHealth = maxHealth; // Mới vào game thì đầy máu
    }

    // Hàm gọi khi bị zombie cào
    public void TakeDamage(float damage)
    {
        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        Debug.Log("Bị thương! Máu còn: " + currentHealth);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    // Hàm gọi khi xài Băng gạc / Medkit
    public void Heal(float amount)
    {
        currentHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth); // Không cho hồi lố 100

        Debug.Log("Đã hồi máu! Máu hiện tại: " + currentHealth);
    }

    private void Die()
    {
        Debug.Log("Player đã CHẾT!");
        // Chèn logic game over, animation gục ngã vào đây
    }
}