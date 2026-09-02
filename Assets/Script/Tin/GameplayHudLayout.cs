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

    // Layout constants for Top-Center HUD on canonical 1080p canvas
    public const float CanonicalObjectiveTop1080p = 14f;
    public const float CanonicalObjectiveHeight1080p = 36f;
    public const float CanonicalObjectiveBottom1080p = 50f;

    public const float CanonicalToastStartY1080p = 54f;
    public const float CanonicalToastTargetY1080p = 68f;
    public const float CanonicalToastWidth1080p = 440f;
    public const float CanonicalToastHeight1080p = 88f;

    /// <summary>
    /// Tính toán hệ số tỷ lệ giao diện màn hình so với chuẩn 1920x1080 (CanvasScaler MatchWidthOrHeight = 0.5)
    /// </summary>
    public static float GetUiScale(float screenWidth = 0f, float screenHeight = 0f)
    {
        float width = screenWidth > 0f ? screenWidth : (Screen.width > 0 ? Screen.width : 1920f);
        float height = screenHeight > 0f ? screenHeight : (Screen.height > 0 ? Screen.height : 1080f);
        float logWidth = Mathf.Log(width / 1920f, 2f);
        float logHeight = Mathf.Log(height / 1080f, 2f);
        float logWeighted = Mathf.Lerp(logWidth, logHeight, 0.5f);
        float scale = Mathf.Pow(2f, logWeighted);
        return Mathf.Clamp(scale, 0.5f, 2.5f);
    }

    /// <summary>
    /// Vùng an toàn Lane 0: Tracked Objective cố định ở đỉnh màn hình (không bao giờ đè lên toast).
    /// </summary>
    public static Rect GetTopCenterObjectiveRect() => GetTopCenterObjectiveRect(0f, 0f);

    public static Rect GetTopCenterObjectiveRect(float screenWidth, float screenHeight)
    {
        float widthVal = screenWidth > 0f ? screenWidth : (Screen.width > 0 ? Screen.width : 1920f);
        float scale = GetUiScale(screenWidth, screenHeight);
        float width = Mathf.Min(520f * scale, widthVal * 0.88f);
        float height = CanonicalObjectiveHeight1080p * scale;
        float topMargin = CanonicalObjectiveTop1080p * scale;
        float x = (widthVal - width) * 0.5f;
        return new Rect(x, topMargin, width, height);
    }

    /// <summary>
    /// Vùng bao phủ toàn bộ animation của Backpack Notification Toast từ start (y=54) đến target (y=68, height=88).
    /// </summary>
    public static Rect GetTopCenterBackpackNotificationAnimationEnvelope() =>
        GetTopCenterBackpackNotificationAnimationEnvelope(0f, 0f);

    public static Rect GetTopCenterBackpackNotificationAnimationEnvelope(float screenWidth, float screenHeight)
    {
        float widthVal = screenWidth > 0f ? screenWidth : (Screen.width > 0 ? Screen.width : 1920f);
        float scale = GetUiScale(screenWidth, screenHeight);
        float width = CanonicalToastWidth1080p * scale;
        float topY = CanonicalToastStartY1080p * scale;
        float bottomY = (CanonicalToastTargetY1080p + CanonicalToastHeight1080p) * scale;
        float height = bottomY - topY;
        float leftX = (widthVal - width) * 0.5f;
        return new Rect(leftX, topY, width, height);
    }

    /// <summary>
    /// Tọa độ màn hình thực tế (pixel) của Backpack Notification Toast HUD khi hiển thị tại vị trí dừng (y=68).
    /// </summary>
    public static Rect GetTopCenterBackpackNotificationBounds() => GetTopCenterBackpackNotificationBounds(0f, 0f);

    public static Rect GetTopCenterBackpackNotificationBounds(float screenWidth, float screenHeight)
    {
        float widthVal = screenWidth > 0f ? screenWidth : (Screen.width > 0 ? Screen.width : 1920f);
        float scale = GetUiScale(screenWidth, screenHeight);
        float width = CanonicalToastWidth1080p * scale;
        float height = CanonicalToastHeight1080p * scale;
        float topY = CanonicalToastTargetY1080p * scale;
        float leftX = (widthVal - width) * 0.5f;
        return new Rect(leftX, topY, width, height);
    }

    /// <summary>
    /// Tính độ cao chiếm dụng cơ bản của Top-Center (Objective + Toast Envelope + Active Quest Event).
    /// Không bao gồm School Clue để tránh đệ quy.
    /// </summary>
    public static float GetTopCenterBaseOccupiedBottomPixels(float screenWidth = 0f, float screenHeight = 0f, bool? toastVisibleOverride = null, float questEventNoticeBottomOverride = -1f)
    {
        float scale = GetUiScale(screenWidth, screenHeight);
        float bottom = CanonicalObjectiveBottom1080p * scale;

        bool isToastVisible = toastVisibleOverride ?? BackpackQuestRewardPresentation.IsNotificationVisible;
        if (isToastVisible)
        {
            float toastBottom = (CanonicalToastTargetY1080p + CanonicalToastHeight1080p) * scale;
            if (toastBottom > bottom) bottom = toastBottom;
        }

        float eventBottom = questEventNoticeBottomOverride;
        if (eventBottom < 0f && MainQuestManager.Instance != null && MainQuestManager.Instance.IsQuestEventNoticeActive)
        {
            eventBottom = MainQuestManager.Instance.CurrentQuestEventNoticeBottom;
        }
        if (eventBottom > bottom)
        {
            bottom = eventBottom;
        }

        return bottom;
    }

    /// <summary>
    /// Vùng an toàn cho tiến độ manh mối trường học (School Clue Progress).
    /// Nhường chỗ cho Toast (nếu có) và Quest Event Notice (nếu có).
    /// </summary>
    public static Rect GetTopCenterSchoolClueRect() =>
        GetTopCenterSchoolClueRect(0f, 0f, null, -1f, 430f, 38f);

    public static Rect GetTopCenterSchoolClueRect(float screenWidth, float screenHeight) =>
        GetTopCenterSchoolClueRect(screenWidth, screenHeight, null, -1f, 430f, 38f);

    public static Rect GetTopCenterSchoolClueRect(float screenWidth, float screenHeight, bool toastVisibleOverride) =>
        GetTopCenterSchoolClueRect(screenWidth, screenHeight, (bool?)toastVisibleOverride, -1f, 430f, 38f);

    public static Rect GetTopCenterSchoolClueRect(float screenWidth, float screenHeight, bool toastVisibleOverride, float questEventNoticeBottomOverride) =>
        GetTopCenterSchoolClueRect(screenWidth, screenHeight, (bool?)toastVisibleOverride, questEventNoticeBottomOverride, 430f, 38f);

    public static Rect GetTopCenterSchoolClueRect(float screenWidth, float screenHeight, bool? toastVisibleOverride, float questEventNoticeBottomOverride, float desiredWidth = 430f, float desiredHeight = 38f)
    {
        float widthVal = screenWidth > 0f ? screenWidth : (Screen.width > 0 ? Screen.width : 1920f);
        float scale = GetUiScale(screenWidth, screenHeight);
        float width = Mathf.Min(desiredWidth * scale, widthVal * 0.85f);
        float height = desiredHeight * scale;
        float gap = 8f * scale;

        float occupiedBottom = GetTopCenterBaseOccupiedBottomPixels(screenWidth, screenHeight, toastVisibleOverride, questEventNoticeBottomOverride);
        float topY = occupiedBottom + gap;
        float leftX = (widthVal - width) * 0.5f;
        return new Rect(leftX, topY, width, height);
    }

    /// <summary>
    /// Vùng an toàn cho Quest Event Notice khi hiển thị (Lane 1 dưới Objective).
    /// </summary>
    public static Rect GetTopCenterQuestEventNoticeRect(float desiredWidth, float desiredHeight, float screenWidth = 0f, float screenHeight = 0f)
    {
        float widthVal = screenWidth > 0f ? screenWidth : (Screen.width > 0 ? Screen.width : 1920f);
        float scale = GetUiScale(screenWidth, screenHeight);
        float width = Mathf.Min(desiredWidth * scale, widthVal - 48f * scale);
        float height = desiredHeight * scale;
        float topY = (CanonicalObjectiveBottom1080p + 10f) * scale;
        float leftX = (widthVal - width) * 0.5f;
        return new Rect(leftX, topY, width, height);
    }

    /// <summary>
    /// Tính toán độ cao đáy đã bị chiếm dụng bởi toàn bộ widget Top-Center đang hoạt động (gồm cả School Clue nếu visible).
    /// </summary>
    public static float GetTopCenterOccupiedBottomPixels(float screenWidth = 0f, float screenHeight = 0f, float occupiedBottomOverride = -1f)
    {
        if (occupiedBottomOverride >= 0f)
            return occupiedBottomOverride;

        float bottom = GetTopCenterBaseOccupiedBottomPixels(screenWidth, screenHeight);
        if (MilitaryBaseQuestManager.Instance != null && MilitaryBaseQuestManager.Instance.IsSchoolClueProgressVisible)
        {
            Rect schoolRect = GetTopCenterSchoolClueRect(screenWidth, screenHeight);
            if (schoolRect.yMax > bottom) bottom = schoolRect.yMax;
        }
        return bottom;
    }

    /// <summary>
    /// Giới hạn tọa độ cả nhóm waypoint (gồm mũi tên + nhãn) tránh che lấp khu vực Top-Center HUD.
    /// </summary>
    public static Rect ClampWaypointGroupAroundTopCenter(Rect groupRect, float occupiedBottomOverride = -1f, float screenWidth = 0f, float screenHeight = 0f)
    {
        float widthVal = screenWidth > 0f ? screenWidth : (Screen.width > 0 ? Screen.width : 1920f);
        float scale = GetUiScale(screenWidth, screenHeight);
        float occupiedBottom = GetTopCenterOccupiedBottomPixels(screenWidth, screenHeight, occupiedBottomOverride);

        float centerMargin = 280f * scale;
        float centerMinX = widthVal * 0.5f - centerMargin;
        float centerMaxX = widthVal * 0.5f + centerMargin;

        if (groupRect.xMax > centerMinX && groupRect.xMin < centerMaxX)
        {
            float minAllowedY = occupiedBottom + 8f * scale;
            if (groupRect.yMin < minAllowedY)
            {
                groupRect.y = minAllowedY;
            }
        }
        return groupRect;
    }

    public static Rect ClampWaypointAroundTopCenter(Rect waypointRect, float screenWidth = 0f, float screenHeight = 0f)
    {
        return ClampWaypointGroupAroundTopCenter(waypointRect, -1f, screenWidth, screenHeight);
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
        // 1. Kiểm tra Loading & Readiness Suppression Gate
        if (GameplayReadinessCoordinator.IsGameplaySuppressed) return true;

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
