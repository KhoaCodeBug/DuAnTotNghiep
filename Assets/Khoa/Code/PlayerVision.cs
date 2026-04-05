using UnityEngine;
using UnityEngine.Rendering.Universal;
using Fusion;

public class PlayerVision : NetworkBehaviour
{
    [Header("=== Ánh sáng của Player ===")]
    [Tooltip("Kéo cái Light 2D nằm trong người Player vào đây")]
    public Light2D playerLight;

    [Header("=== Cài đặt Tầm Nhìn ===")]
    [Tooltip("Đồ thị Bán Kính: Trưa = 15m, Đêm = 4m")]
    public AnimationCurve radiusCurve;

    [Tooltip("Đồ thị Độ Sáng: Trưa = 0 (tắt đèn), Đêm = 1 (sáng rõ)")]
    public AnimationCurve intensityCurve;

    private void Update()
    {
        // 1. Chỉ chạy code này trên máy của người đang điều khiển nhân vật đó (Local Player)
        // Không chỉnh đèn của mấy thằng người chơi khác (nếu có)
        if (!HasInputAuthority) return;

        // 2. Chắc chắn DayNightManager đã tồn tại
        if (DayNightManager.Instance == null || playerLight == null) return;

        // 3. Lấy % thời gian từ DayNightManager (0.0 đến 1.0)
        float timePercent = DayNightManager.Instance.GetTimePercent();

        // 4. Áp dụng vào Đèn của Player
        playerLight.pointLightOuterRadius = radiusCurve.Evaluate(timePercent);
        playerLight.intensity = intensityCurve.Evaluate(timePercent);
    }
}