using Fusion;
using UnityEngine;

// 🔥 1. ĐỔI THÀNH NetworkBehaviour ĐỂ CHẠY ĐƯỢC MẠNG
public class PlayerStamina : NetworkBehaviour
{
    [Header("--- Stamina System ---")]
    // 🔥 2. ĐỔI CÁC CHỈ SỐ QUAN TRỌNG THÀNH BIẾN MẠNG
    [Networked] public float maxStamina { get; set; }
    [Networked] public float currentStamina { get; set; }

    public float staminaDrain = 20f;

    [Tooltip("Tốc độ hồi Stamina khi ĐỨNG YÊN thở")]
    public float staminaRecoverIdle = 15f;

    [Tooltip("Tốc độ hồi Stamina khi ĐI BỘ/LẾT")]
    public float staminaRecoverWalk = 5f;

    [Header("--- Combat Penalty ---")]
    public float bashRegenDelay = 3f;
    [Networked] private float currentRegenDelayTimer { get; set; }

    // 🔥 Dùng NetworkBool thay cho bool thường
    [Networked] public NetworkBool IsExhausted { get; private set; }

    // --- BIẾN QUẢN LÝ BUFF CHUẨN MẠNG ---
    [Networked] public NetworkBool HasEnergyBuff { get; private set; }
    [Networked] public float CurrentSpeedMultiplier { get; private set; }

    // Thay thế Coroutine bằng Đồng hồ mạng (TickTimer)
    [Networked] private TickTimer buffTimer { get; set; }
    [Networked] private float activeStaminaBoost { get; set; }

    public override void Spawned()
    {
        // Máy chủ thiết lập các chỉ số gốc khi mới đẻ ra
        if (HasStateAuthority)
        {
            maxStamina = 100f;
            currentStamina = 100f;
            CurrentSpeedMultiplier = 1f;
            IsExhausted = false;
            HasEnergyBuff = false;
        }
    }

    // 🔥 HÀM MẠNG: Dùng để check xem cái Buff hết giờ chưa
    public override void FixedUpdateNetwork()
    {
        // Nếu đang có Buff và cái đồng hồ mạng báo Hết Giờ
        if (HasEnergyBuff && buffTimer.Expired(Runner))
        {
            // HẾT GIỜ -> TRẢ MỌI THỨ VỀ CŨ
            HasEnergyBuff = false;
            CurrentSpeedMultiplier = 1f;
            maxStamina -= activeStaminaBoost;

            if (currentStamina > maxStamina) currentStamina = maxStamina;
            activeStaminaBoost = 0f;

            Debug.Log($"❌ HẾT BUFF: Giới hạn Thể lực tụt về lại {maxStamina}.");
        }
    }

    public void UpdateStamina(bool isRunning, bool isMovingNow)
    {
        // 🔥 3. THAY TOÀN BỘ Time.deltaTime THÀNH Runner.DeltaTime
        if (currentRegenDelayTimer > 0)
        {
            currentRegenDelayTimer -= Runner.DeltaTime;
        }

        // 1. Trừ thể lực khi đang chạy
        if (isRunning && isMovingNow)
        {
            if (!HasEnergyBuff)
            {
                currentStamina -= staminaDrain * Runner.DeltaTime;
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
            if (currentRegenDelayTimer > 0) return;

            float currentRecoverRate = isMovingNow ? staminaRecoverWalk : staminaRecoverIdle;

            if (HasEnergyBuff) currentRecoverRate *= 3f;

            currentStamina += currentRecoverRate * Runner.DeltaTime;

            if (currentStamina >= maxStamina)
            {
                currentStamina = maxStamina;
                IsExhausted = false;
            }
        }
    }

    public void ConsumeStamina(float amount)
    {
        if (!HasEnergyBuff)
        {
            currentStamina -= amount;
            currentRegenDelayTimer = bashRegenDelay;

            if (currentStamina <= 0)
            {
                currentStamina = 0;
                IsExhausted = true;
            }
        }
    }

    public void RestoreStamina(float amount)
    {
        currentStamina += amount;
        if (currentStamina >= maxStamina) currentStamina = maxStamina;
        if (currentStamina > 0) IsExhausted = false;
    }

    // --- HÀM KÍCH HOẠT NƯỚC TĂNG LỰC (Không dùng Coroutine nữa) ---
    public void ApplyEnergyBuff(float duration, float speedBoost, float staminaBoost)
    {
        HasEnergyBuff = true;
        IsExhausted = false;
        CurrentSpeedMultiplier = speedBoost;

        activeStaminaBoost = staminaBoost;
        maxStamina += staminaBoost;
        currentStamina += staminaBoost;

        // Vặn đồng hồ báo thức trên mạng
        buffTimer = TickTimer.CreateFromSeconds(Runner, duration);

        Debug.Log($"⚡ BẮT ĐẦU BUFF: Tốc độ x{speedBoost}, Giới hạn Thể lực lên {maxStamina} trong {duration}s!");
    }
}