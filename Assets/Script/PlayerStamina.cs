using UnityEngine;
using System.Collections; // Bắt buộc phải có để dùng Coroutine

public class PlayerStamina : MonoBehaviour
{
    [Header("--- Stamina System ---")]
    public float maxStamina = 100f;
    public float currentStamina = 100f;
    public float staminaDrain = 20f;

    [Tooltip("Tốc độ hồi Stamina khi ĐỨNG YÊN thở")]
    public float staminaRecoverIdle = 15f;

    [Tooltip("Tốc độ hồi Stamina khi ĐI BỘ/LẾT")]
    public float staminaRecoverWalk = 5f;

    // 🔥 MỚI: Cài đặt thời gian "khựng" hồi thể lực sau khi đập súng
    [Header("--- Combat Penalty ---")]
    public float bashRegenDelay = 3f;
    private float currentRegenDelayTimer = 0f;

    public bool IsExhausted { get; private set; } = false;

    // --- BIẾN QUẢN LÝ BUFF ---
    public bool HasEnergyBuff { get; private set; } = false;
    public float CurrentSpeedMultiplier { get; private set; } = 1f;
    private Coroutine buffCoroutine;

    public void UpdateStamina(bool isRunning, bool isMovingNow)
    {
        // 🔥 MỚI: Luôn giảm đồng hồ đếm ngược (nếu có) theo thời gian thực
        if (currentRegenDelayTimer > 0)
        {
            currentRegenDelayTimer -= Time.deltaTime;
        }

        // 1. Trừ thể lực khi đang chạy
        if (isRunning && isMovingNow)
        {
            // CHỈ TRỪ KHI KHÔNG CÓ BUFF
            if (!HasEnergyBuff)
            {
                currentStamina -= staminaDrain * Time.deltaTime;
                if (currentStamina <= 0)
                {
                    currentStamina = 0;
                    IsExhausted = true;
                }
            }
        }
        // 2. Hồi thể lực khi không chạy
        else
        {
            // 🔥 MỚI: CHỐT CHẶN - Nếu vẫn đang trong 3 giây mệt do vung súng thì KHÔNG CHO HỒI!
            if (currentRegenDelayTimer > 0) return;

            float currentRecoverRate = isMovingNow ? staminaRecoverWalk : staminaRecoverIdle;

            // NẾU CÓ BUFF, HỒI THỂ LỰC NHANH GẤP 3 LẦN
            if (HasEnergyBuff) currentRecoverRate *= 3f;

            currentStamina += currentRecoverRate * Time.deltaTime;

            if (currentStamina >= maxStamina)
            {
                currentStamina = maxStamina;
                IsExhausted = false;
            }
        }
    }

    public void ConsumeStamina(float amount)
    {
        // Nếu đang uống nước tăng lực (có Buff) thì đánh không biết mệt
        if (!HasEnergyBuff)
        {
            currentStamina -= amount;

            // 🔥 MỚI: Vừa đập súng xong, kích hoạt đồng hồ 3 giây cấm hồi thể lực
            currentRegenDelayTimer = bashRegenDelay;

            if (currentStamina <= 0)
            {
                currentStamina = 0;
                IsExhausted = true; // Hết sạch thể lực thì chuyển sang thở dốc
            }
        }
    }

    public void RestoreStamina(float amount)
    {
        currentStamina += amount;
        if (currentStamina >= maxStamina) currentStamina = maxStamina;
        if (currentStamina > 0) IsExhausted = false;
    }

    // --- HÀM KÍCH HOẠT NƯỚC TĂNG LỰC ---
    public void ApplyEnergyBuff(float duration, float speedBoost, float staminaBoost)
    {
        if (buffCoroutine != null) StopCoroutine(buffCoroutine);
        buffCoroutine = StartCoroutine(EnergyBuffRoutine(duration, speedBoost, staminaBoost));
    }

    private IEnumerator EnergyBuffRoutine(float duration, float speedBoost, float staminaBoost)
    {
        HasEnergyBuff = true;
        IsExhausted = false;
        CurrentSpeedMultiplier = speedBoost;

        // 1. NỚI RỘNG MAX STAMINA (Ví dụ: 100 + 50 = 150)
        maxStamina += staminaBoost;
        // Bơm luôn 50 thể lực đó cho người chơi tràn trề năng lượng
        currentStamina += staminaBoost;

        Debug.Log($"⚡ BẮT ĐẦU BUFF: Tốc độ x{speedBoost}, Giới hạn Thể lực tăng lên {maxStamina} trong {duration}s!");

        // Chờ hết thời gian tác dụng
        yield return new WaitForSeconds(duration);

        // 2. HẾT GIỜ -> TRẢ MỌI THỨ VỀ CŨ
        HasEnergyBuff = false;
        CurrentSpeedMultiplier = 1f;
        maxStamina -= staminaBoost; // Trả về 100

        // Nếu lúc hết buff mà thể lực đang lố 100 thì ép nó về 100
        if (currentStamina > maxStamina) currentStamina = maxStamina;

        Debug.Log($"❌ HẾT BUFF: Giới hạn Thể lực tụt về lại {maxStamina}.");
    }
}