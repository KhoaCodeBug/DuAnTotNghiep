using System;
using System.Collections.Generic;
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
    public static string CurrentStatusKeyOrText { get; private set; } = string.Empty;
    public static bool IsLoadingActive { get; private set; } = false;
    public static bool IsReleasedToGameplay => CurrentStage == ReadinessStage.ReleasedToGameplay;
    public static bool IsGameplaySuppressed => IsLoadingActive || (CurrentStage != ReadinessStage.None && CurrentStage != ReadinessStage.ReleasedToGameplay);

    public static event Action<float, string> OnProgressUpdated;
    public static event Action OnReleased;
    public static event Action<string> OnFailed;
    public static event Action<bool> OnSuppressionChanged;

    private static readonly HashSet<Canvas> RegisteredGameplayCanvases = new HashSet<Canvas>();

    public static string CurrentStatusText
    {
        get
        {
            if (string.IsNullOrEmpty(CurrentStatusKeyOrText))
                return GetDefaultMessageForStage(CurrentStage);

            return GameLocalization.Get(CurrentStatusKeyOrText, CurrentStatusKeyOrText);
        }
    }

    public static void ResetCoordinator()
    {
        CurrentStage = ReadinessStage.None;
        CurrentProgress = 0f;
        CurrentStatusKeyOrText = string.Empty;
        IsLoadingActive = false;
        OnSuppressionChanged?.Invoke(IsGameplaySuppressed);
    }

    public static void StartLoading(string initialStatusKey = "loading.connecting")
    {
        ResetCoordinator();
        IsLoadingActive = true;
        SetStage(ReadinessStage.Connecting, 0f, initialStatusKey);
        OnSuppressionChanged?.Invoke(IsGameplaySuppressed);
        ApplyCanvasSuppression();
    }

    public static void SetStage(ReadinessStage stage, float subProgress01 = 0f, string customMessageOrKey = null)
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

        CurrentStatusKeyOrText = !string.IsNullOrEmpty(customMessageOrKey)
            ? customMessageOrKey
            : GetDefaultKeyForStage(stage);

        OnProgressUpdated?.Invoke(CurrentProgress, CurrentStatusText);

        if (stage == ReadinessStage.ReleasedToGameplay)
        {
            CurrentProgress = 1.0f;
            IsLoadingActive = false;
            OnReleased?.Invoke();
            OnSuppressionChanged?.Invoke(false);
            ApplyCanvasSuppression();
        }
        else if (stage == ReadinessStage.Failed)
        {
            IsLoadingActive = false;
            OnFailed?.Invoke(CurrentStatusText);
            OnSuppressionChanged?.Invoke(true);
        }
    }

    public static void UpdateSceneLoadProgress(float asyncProgress)
    {
        if (CurrentStage == ReadinessStage.SceneLoading || CurrentStage == ReadinessStage.Connecting)
        {
            SetStage(ReadinessStage.SceneLoading, asyncProgress, "loading.scene_loading");
        }
    }

    public static void Release()
    {
        SetStage(ReadinessStage.ReleasedToGameplay, 1.0f, "loading.ready_complete");
    }

    public static void Fail(string errorMessage)
    {
        SetStage(ReadinessStage.Failed, 0f, errorMessage);
    }

    public static void RegisterGameplayCanvas(Canvas canvas)
    {
        if (canvas == null) return;
        RegisteredGameplayCanvases.Add(canvas);
        UpdateSingleCanvasSuppression(canvas, IsGameplaySuppressed);
    }

    public static void UnregisterGameplayCanvas(Canvas canvas)
    {
        if (canvas == null) return;
        RegisteredGameplayCanvases.Remove(canvas);
    }

    public static void ApplyCanvasSuppression()
    {
        bool suppress = IsGameplaySuppressed;
        RegisteredGameplayCanvases.RemoveWhere(c => c == null);
        foreach (Canvas canvas in RegisteredGameplayCanvases)
        {
            UpdateSingleCanvasSuppression(canvas, suppress);
        }
    }

    private static void UpdateSingleCanvasSuppression(Canvas canvas, bool suppress)
    {
        if (canvas == null) return;
        if (canvas.TryGetComponent<CanvasGroup>(out var cg))
        {
            cg.alpha = suppress ? 0f : 1f;
            cg.blocksRaycasts = !suppress;
            cg.interactable = !suppress;
        }
        else
        {
            canvas.enabled = !suppress;
        }
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

    public static string GetDefaultKeyForStage(ReadinessStage stage)
    {
        return stage switch
        {
            ReadinessStage.Connecting          => "loading.connecting",
            ReadinessStage.SceneLoading        => "loading.scene_loading",
            ReadinessStage.FusionSceneReady    => "loading.fusion_ready",
            ReadinessStage.PlayerSpawnWaiting  => "loading.player_spawn_waiting",
            ReadinessStage.LocalAvatarBinding  => "loading.avatar_binding",
            ReadinessStage.HUDAndSystemsReady  => "loading.hud_ready",
            ReadinessStage.AwaitingHostRelease => "loading.awaiting_host",
            ReadinessStage.ReleasedToGameplay  => "loading.ready_complete",
            ReadinessStage.Failed              => "loading.failed",
            _                                  => string.Empty
        };
    }

    private static string GetDefaultMessageForStage(ReadinessStage stage)
    {
        string key = GetDefaultKeyForStage(stage);
        if (string.IsNullOrEmpty(key)) return string.Empty;
        return GameLocalization.Get(key);
    }
}
