using UnityEngine;

/// <summary>
/// Dịch vụ điều phối bố cục UI an toàn (Safe Area / HUD Layout).
/// Bảo đảm các transient prompt ở đáy màn hình (lục soát xác, sửa xe, điểm nhiệm vụ)
/// luôn nằm ở làn an toàn phía trên Hotbar ở mọi độ phân giải (720p, 768p, 900p, 1080p, v.v.)
/// và được ẩn đồng bộ khi đang Loading, Pause Menu, Chat Typing hoặc Modal UI.
/// </summary>
public static class GameplayHudLayout
{
    // Kích thước chuẩn của Hotbar trên Canvas 1920x1080
    // Hotbar Panel: Y=15, Height=96 -> Top=111. ItemNameText: Y=120, Height=35 -> Top=155.
    public const float CanonicalHotbarFootprint1080p = 165f;
    public const float SafeMargin1080p = 20f;

    /// <summary>
    /// Tính toán hệ số tỷ lệ giao diện màn hình so với chuẩn 1920x1080 (CanvasScaler MatchWidthOrHeight = 0.5)
    /// </summary>
    public static float GetUiScale()
    {
        float width = Screen.width > 0 ? Screen.width : 1920f;
        float height = Screen.height > 0 ? Screen.height : 1080f;
        float logWidth = Mathf.Log(width / 1920f, 2f);
        float logHeight = Mathf.Log(height / 1080f, 2f);
        float logWeighted = Mathf.Lerp(logWidth, logHeight, 0.5f);
        float scale = Mathf.Pow(2f, logWeighted);
        return Mathf.Clamp(scale, 0.5f, 2.5f);
    }

    /// <summary>
    /// Tính độ cao vùng Hotbar chiếm dụng từ đáy màn hình (tính bằng pixel thực tế).
    /// </summary>
    public static float GetHotbarHeightPixels()
    {
        return CanonicalHotbarFootprint1080p * GetUiScale();
    }

    /// <summary>
    /// Kiểm tra xem các transient gameplay prompt có đang bị chặn/ẩn bởi hệ thống hay không
    /// (Đang Loading, Mở Pause Menu, Đang gõ Chat, hoặc Mở Modal UI).
    /// </summary>
    public static bool AreGameplayPromptsSuppressed()
    {
        // 1. Kiểm tra Loading Screen
        if (GameplayReadinessCoordinator.IsLoadingActive) return true;

        // 2. Kiểm tra Pause Menu & Options
        if (AutoMainMenuManager.Instance != null && AutoMainMenuManager.Instance.IsPauseMenuOrOptionsOpen)
            return true;

        // 3. Kiểm tra Chat Typing
        if (AutoChatManager.Instance != null && AutoChatManager.Instance.IsTyping())
            return true;

        // 4. Kiểm tra UI Menu (Inventory, Trade, Loot Container, v.v.)
        if (AutoUIManager.Instance != null && AutoUIManager.Instance.IsAnyMenuOpen())
            return true;

        // 5. Kiểm tra Quest Overlay
        if (QuestFlowUIPrototype.Instance != null && QuestFlowUIPrototype.Instance.IsQuestOverlayOpen)
            return true;

        // 6. Kiểm tra các cinematic presentation
        if (CivilianRoutePresentationController.BlocksGameplayInput ||
            MilitaryRouteBEscapePresentation.BlocksGameplayInput ||
            VictorySummaryUI.IsShowing)
            return true;

        return false;
    }

    /// <summary>
    /// Tính toán Rect an toàn ở khu vực Bottom-Center không bao giờ đè lên Hotbar.
    /// </summary>
    /// <param name="promptWidth">Chiều rộng mong muốn (tại 1080p)</param>
    /// <param name="promptHeight">Chiều cao mong muốn (tại 1080p)</param>
    /// <param name="extraBottomMargin">Khoảng đệm phụ thêm nếu có progress bar phía dưới</param>
    public static Rect GetBottomCenterPromptRect(float promptWidth, float promptHeight, float extraBottomMargin = 0f)
    {
        float scale = GetUiScale();
        float scaledWidth = Mathf.Min(promptWidth * scale, Screen.width * 0.92f);
        float scaledHeight = promptHeight * scale;

        float hotbarTopPixels = GetHotbarHeightPixels();
        float marginPixels = (SafeMargin1080p + extraBottomMargin) * scale;

        float bottomYPixels = hotbarTopPixels + marginPixels + scaledHeight;
        float topYOnGui = Screen.height - bottomYPixels;

        float leftXOnGui = (Screen.width - scaledWidth) * 0.5f;

        return new Rect(leftXOnGui, topYOnGui, scaledWidth, scaledHeight);
    }

    /// <summary>
    /// Tính toán Rect cho thanh tiến trình (progress bar) hiển thị ngay phía trên prompt box.
    /// </summary>
    public static Rect GetProgressBarRectAbovePrompt(Rect promptRect, float barWidth, float barHeight, float gap = 8f)
    {
        float scale = GetUiScale();
        float scaledBarWidth = Mathf.Min(barWidth * scale, promptRect.width);
        float scaledBarHeight = barHeight * scale;
        float scaledGap = gap * scale;

        float leftX = (Screen.width - scaledBarWidth) * 0.5f;
        float topY = promptRect.y - scaledGap - scaledBarHeight;

        return new Rect(leftX, topY, scaledBarWidth, scaledBarHeight);
    }
}
