using System;
using UnityEngine;

/// <summary>
/// Trình điều phối trạng thái sẵn sàng (Readiness State Machine) và tiến trình Loading thực tế.
/// Bảo đảm một nguồn sự thật duy nhất (Single Source of Truth) cho quá trình chuẩn bị trận đấu
/// của cả Host, Client kết nối ban đầu và Client Late-Join.
/// </summary>
public static class GameplayReadinessCoordinator
{
    public enum ReadinessStage
    {
        None = 0,
        Connecting = 1,             // 0% - 15%
        SceneLoading = 2,           // 15% - 55%
        FusionSceneReady = 3,       // 55% - 70%
        PlayerSpawnWaiting = 4,     // 70% - 80%
        LocalAvatarBinding = 5,     // 80% - 88%
        HUDAndSystemsReady = 6,     // 88% - 95%
        AwaitingHostRelease = 7,    // 95% - 99%
        ReleasedToGameplay = 8,     // 100%
        Failed = 9                  // Lỗi
    }

    public static ReadinessStage CurrentStage { get; private set; } = ReadinessStage.None;
    public static float CurrentProgress { get; private set; } = 0f;
    public static string CurrentStatusText { get; private set; } = string.Empty;
    public static bool IsLoadingActive { get; private set; } = false;
    public static bool IsReleasedToGameplay => CurrentStage == ReadinessStage.ReleasedToGameplay;

    public static event Action<float, string> OnProgressUpdated;
    public static event Action OnReleased;
    public static event Action<string> OnFailed;

    public static void ResetCoordinator()
    {
        CurrentStage = ReadinessStage.None;
        CurrentProgress = 0f;
        CurrentStatusText = string.Empty;
        IsLoadingActive = false;
    }

    public static void StartLoading(string initialStatus = "Đang kết nối máy chủ...")
    {
        ResetCoordinator();
        IsLoadingActive = true;
        SetStage(ReadinessStage.Connecting, 0f, initialStatus);
    }

    public static void SetStage(ReadinessStage stage, float subProgress01 = 0f, string customMessage = null)
    {
        if (stage < CurrentStage && stage != ReadinessStage.None && CurrentStage != ReadinessStage.Failed)
        {
            // Bảo đảm tính đơn điệu (Monotonic): không được tụt stage trừ khi reset
            return;
        }

        CurrentStage = stage;
        float baseProgress = GetBaseProgressForStage(stage);
        float stageSpan = GetStageSpan(stage);
        float calculatedProgress = Mathf.Clamp01(baseProgress + Mathf.Clamp01(subProgress01) * stageSpan);

        // Bảo đảm tiến độ không bao giờ giảm và không đạt 100% trước khi Release
        if (calculatedProgress > CurrentProgress)
        {
            CurrentProgress = calculatedProgress;
        }

        if (CurrentStage != ReadinessStage.ReleasedToGameplay && CurrentProgress >= 0.98f)
        {
            CurrentProgress = 0.98f;
        }

        CurrentStatusText = !string.IsNullOrEmpty(customMessage)
            ? customMessage
            : GetDefaultMessageForStage(stage);

        OnProgressUpdated?.Invoke(CurrentProgress, CurrentStatusText);

        if (stage == ReadinessStage.ReleasedToGameplay)
        {
            CurrentProgress = 1.0f;
            IsLoadingActive = false;
            OnReleased?.Invoke();
        }
        else if (stage == ReadinessStage.Failed)
        {
            IsLoadingActive = false;
            OnFailed?.Invoke(CurrentStatusText);
        }
    }

    public static void UpdateSceneLoadProgress(float asyncProgress)
    {
        if (CurrentStage == ReadinessStage.SceneLoading || CurrentStage == ReadinessStage.Connecting)
        {
            SetStage(ReadinessStage.SceneLoading, asyncProgress, "Đang tải bản đồ...");
        }
    }

    public static void Release()
    {
        SetStage(ReadinessStage.ReleasedToGameplay, 1.0f, "Hoàn tất!");
    }

    public static void Fail(string errorMessage)
    {
        SetStage(ReadinessStage.Failed, 0f, errorMessage);
    }

    private static float GetBaseProgressForStage(ReadinessStage stage)
    {
        return stage switch
        {
            ReadinessStage.Connecting          => 0.05f,
            ReadinessStage.SceneLoading        => 0.15f,
            ReadinessStage.FusionSceneReady    => 0.55f,
            ReadinessStage.PlayerSpawnWaiting  => 0.70f,
            ReadinessStage.LocalAvatarBinding  => 0.80f,
            ReadinessStage.HUDAndSystemsReady  => 0.88f,
            ReadinessStage.AwaitingHostRelease => 0.95f,
            ReadinessStage.ReleasedToGameplay  => 1.00f,
            ReadinessStage.Failed              => CurrentProgress,
            _                                  => 0f
        };
    }

    private static float GetStageSpan(ReadinessStage stage)
    {
        return stage switch
        {
            ReadinessStage.Connecting          => 0.10f,
            ReadinessStage.SceneLoading        => 0.40f,
            ReadinessStage.FusionSceneReady    => 0.15f,
            ReadinessStage.PlayerSpawnWaiting  => 0.10f,
            ReadinessStage.LocalAvatarBinding  => 0.08f,
            ReadinessStage.HUDAndSystemsReady  => 0.07f,
            ReadinessStage.AwaitingHostRelease => 0.03f,
            ReadinessStage.ReleasedToGameplay  => 0.00f,
            _                                  => 0f
        };
    }

    private static string GetDefaultMessageForStage(ReadinessStage stage)
    {
        return stage switch
        {
            ReadinessStage.Connecting          => "Đang kết nối phiên chơi...",
            ReadinessStage.SceneLoading        => "Đang nạp tài nguyên bản đồ...",
            ReadinessStage.FusionSceneReady    => "Đang khởi tạo môi trường mạng...",
            ReadinessStage.PlayerSpawnWaiting  => "Đang tạo nhân vật người chơi...",
            ReadinessStage.LocalAvatarBinding  => "Đang liên kết điều khiển và góc nhìn...",
            ReadinessStage.HUDAndSystemsReady  => "Đang chuẩn bị giao diện và nhiệm vụ...",
            ReadinessStage.AwaitingHostRelease => "Đang chờ máy chủ xác nhận và giải phóng...",
            ReadinessStage.ReleasedToGameplay  => "Sẵn sàng!",
            ReadinessStage.Failed              => "Tải trận đấu thất bại.",
            _                                  => string.Empty
        };
    }
}
