using UnityEngine;

public class PlayerSurvival : MonoBehaviour
{
    [Header("--- Chỉ số Đói (Hunger) ---")]
    public float maxHunger = 100f;
    public float currentHunger;
    public float hungerDrainRate = 0.5f; // Mất 0.5 điểm mỗi giây

    [Header("--- Chỉ số Khát (Thirst) ---")]
    public float maxThirst = 100f;
    public float currentThirst;
    public float thirstDrainRate = 0.8f; // Khát thường tụt nhanh hơn đói

    [Header("--- Sức khỏe ---")]
    public float damageOverTime = 2f; // Trừ 2 máu mỗi giây khi cạn kiệt
    private PlayerHealth healthScript;

    void Start()
    {
        currentHunger = maxHunger;
        currentThirst = maxThirst;
        healthScript = GetComponent<PlayerHealth>();
    }

    void Update()
    {
        // 1. Tụt chỉ số theo thời gian
        currentHunger -= hungerDrainRate * Time.deltaTime;
        currentThirst -= thirstDrainRate * Time.deltaTime;

        // Giới hạn không cho xuống dưới 0
        currentHunger = Mathf.Max(currentHunger, 0);
        currentThirst = Mathf.Max(currentThirst, 0);

        // 2. Logic trừ máu khi cạn kiệt (Starvation/Dehydration)
        if (currentHunger <= 0 || currentThirst <= 0)
        {
            if (healthScript != null)
            {
                // Trừ máu mỗi giây
                healthScript.TakeDamage(damageOverTime * Time.deltaTime);
                Debug.LogWarning("⚠️ Bạn đang chết dần vì đói hoặc khát!");
            }
        }
    }

    // Hàm để gọi khi Ăn hoặc Uống từ Inventory
    public void RestoreHunger(float amount)
    {
        currentHunger += amount;
        currentHunger = Mathf.Min(currentHunger, maxHunger);
        Debug.Log("Đã ăn! Đói hiện tại: " + (int)currentHunger);
    }

    public void RestoreThirst(float amount)
    {
        currentThirst += amount;
        currentThirst = Mathf.Min(currentThirst, maxThirst);
        Debug.Log("Đã uống! Khát hiện tại: " + (int)currentThirst);
    }
}