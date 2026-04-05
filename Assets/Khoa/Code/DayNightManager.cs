using Fusion;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using TMPro;

public class DayNightManager : NetworkBehaviour
{
    public static DayNightManager Instance { get; private set; }

    #region Cài đặt
    [Header("=== Cài đặt Thời gian ===")]
    [Tooltip("Bao nhiêu phút ngoài đời = 1 ngày trong game?")]
    public float realMinutesPerDay = 24f;

    [Networked] public float CurrentTime { get; set; }

    [Header("=== Môi trường (Global Light) ===")]
    public Light2D globalLight;

    [Tooltip("Đồ thị độ sáng: Trưa = 1 (Sáng bừng), Đêm = 0.1 (Tối thui)")]
    public AnimationCurve globalIntensityCurve;

    [Tooltip("Dải màu bầu trời: Trưa = Trắng/Vàng, Đêm = Xanh nhạt/Tím sẫm")]
    public Gradient skyColorCurve;

    [Header("=== Giao diện UI ===")]
    public TextMeshProUGUI clockText;

    private bool _isSpawned = false;   // 🔥 Flag kiểm tra Fusion đã spawn chưa
    #endregion

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public override void Spawned()
    {
        base.Spawned();                    // Quan trọng phải có

        _isSpawned = true;                 // ✅ Đánh dấu đã spawn thành công

        if (HasStateAuthority)
        {
            CurrentTime = 12f;             // Host bắt đầu lúc 12h trưa
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (HasStateAuthority)
        {
            float timeSpeed = 24f / (realMinutesPerDay * 60f);
            CurrentTime += Runner.DeltaTime * timeSpeed;

            if (CurrentTime >= 24f) CurrentTime = 0f;
        }
    }

    private void Update()
    {
        // 🔥 CHỈ update khi Fusion đã spawn xong
        if (!_isSpawned) return;

        UpdateLighting();
        UpdateUI();
    }

    private void UpdateLighting()
    {
        if (globalLight != null)
        {
            float timePercent = CurrentTime / 24f;

            globalLight.intensity = globalIntensityCurve.Evaluate(timePercent);
            globalLight.color = skyColorCurve.Evaluate(timePercent);
        }
    }

    private void UpdateUI()
    {
        if (AutoUIManager.Instance != null && AutoUIManager.Instance.clockText != null)
        {
            int hours = Mathf.FloorToInt(CurrentTime);
            int minutes = Mathf.FloorToInt((CurrentTime - hours) * 60f);
            AutoUIManager.Instance.clockText.text = string.Format("{0:00}:{1:00}", hours, minutes);
        }
    }

    public float GetTimePercent()
    {
        return CurrentTime / 24f;
    }
}