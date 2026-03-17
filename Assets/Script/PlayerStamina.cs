using UnityEngine;

public class PlayerStamina : MonoBehaviour
{
    [Header("--- Stamina System ---")]
    public float maxStamina = 100f;
    public float currentStamina = 100f;
    public float staminaDrain = 20f;       // Mất bao nhiêu Stamina mỗi giây khi chạy

    [Tooltip("Tốc độ hồi Stamina khi ĐỨNG YÊN thở")]
    public float staminaRecoverIdle = 15f;

    [Tooltip("Tốc độ hồi Stamina khi ĐI BỘ/LẾT")]
    public float staminaRecoverWalk = 5f;

    // Biến này được đóng gói (Encapsulation), các script khác chỉ được ĐỌC chứ không được SỬA
    public bool IsExhausted { get; private set; } = false;

    // Hàm này sẽ được PlayerMovement gọi liên tục để cập nhật thể lực
    public void UpdateStamina(bool isRunning, bool isMovingNow)
    {
        // 1. Trừ thể lực khi đang chạy
        if (isRunning && isMovingNow)
        {
            currentStamina -= staminaDrain * Time.deltaTime;
            if (currentStamina <= 0)
            {
                currentStamina = 0;
                IsExhausted = true; // Cạn kiệt thể lực
            }
        }
        // 2. Hồi thể lực khi không chạy (Đi bộ hoặc Đứng yên)
        else
        {
            // Chọn tốc độ hồi phục tương ứng
            float currentRecoverRate = isMovingNow ? staminaRecoverWalk : staminaRecoverIdle;

            // Cộng thể lực
            currentStamina += currentRecoverRate * Time.deltaTime;

            if (currentStamina >= maxStamina)
            {
                currentStamina = maxStamina;
                IsExhausted = false; // Đầy thể lực, sẵn sàng chạy tiếp
            }
        }
    }
}