using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

/// <summary>
/// Hệ thống Box Chat Multiplayer - Hoạt động 100% cho mọi người chơi.
/// Enter được xử lý rõ ràng trong Update; onEndEdit chỉ xử lý mất focus.
/// Lịch sử, nền và ô nhập có vòng đời độc lập theo kiểu chat trong game.
/// </summary>
public class AutoChatManager : MonoBehaviour
{
    private static AutoChatManager instance;
    public static AutoChatManager Instance
    {
        get
        {
            if (instance == null || instance.gameObject == null)
            {
                instance = FindFirstObjectByType<AutoChatManager>();
                if (instance == null)
                {
                    var go = new GameObject("--- AUTO CHAT MANAGER ---");
                    if (Application.isPlaying) DontDestroyOnLoad(go);
                    instance = go.AddComponent<AutoChatManager>();
                }
            }
            return instance;
        }
        private set
        {
            instance = value;
        }
    }

    public static AutoChatManager ExistingInstance
    {
        get
        {
            if (instance == null || instance.gameObject == null)
                instance = FindFirstObjectByType<AutoChatManager>();
            return instance;
        }
    }

    private CanvasGroup chatGroup;
    private CanvasGroup historyGroup;
    private Text chatHistory;
    private Text chatPrompt;
    private InputField chatInput;
    private GameObject inputContainer;
    private ScrollRect scrollRect;
    private RectTransform chatPanelRt;
    private RectTransform dragHandleRt;
    private Image dragHandleImage;
    private Image vpBg;
    public const string PrefsKeyPosX = "Chat_PosX";
    public const string PrefsKeyPosY = "Chat_PosY";
    private const int MaxHistoryLines = 30;
    private const int MaxHistoryCharacters = 1200;
    private readonly System.Collections.Generic.List<string> messageHistory = new System.Collections.Generic.List<string>();
    private static readonly System.Collections.Generic.List<string> PendingPreloadMessages = new System.Collections.Generic.List<string>();

    // ========================= CẤU HÌNH =========================
    private const float BACKGROUND_MAX_ALPHA = 0.82f;
    private const float BACKGROUND_FADE_SPEED = 4f;
    private const float DEFAULT_PANEL_WIDTH = 360f;
    private const float DEFAULT_PANEL_HEIGHT = 200f;
    private const float DRAG_HANDLE_HEIGHT = 24f;

    // ========================= TRẠNG THÁI =========================
    private enum ChatDisplayState
    {
        Hidden,
        TextOnly,
        Hovered,
        Editing,
        Dragging
    }

    private ChatDisplayState displayState = ChatDisplayState.Hidden;
    private float backgroundAlpha = 0f;
    private bool  isTyping  = false;
    private bool  justClosed = false;
    private bool  isDragging = false;
    private bool pointerOverChat = false;
    private bool inputTransitionInProgress = false;
    private bool dragPointerDown = false;
    private int dragFocusGuardUntilFrame = -1;
    private bool pendingVisibilityAfterSuppression = false;
    private bool wasSuppressed = false;

    // ========================= SỰ KIỆN (Cho PlayerInputHandler2D) =========================
    public delegate void OnSendMessage(string message);
    public OnSendMessage onSendMessage;

    // ============================================================
    // KHỞI TẠO TỰ ĐỘNG
    // ============================================================
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoSpawn()
    {
        // Kích hoạt qua cơ chế Lazy initialization
        var trigger = Instance;
    }

    void Awake()
    {
        if (Application.isPlaying)
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            if (instance != null && instance != this)
            {
                if (instance.gameObject != null && instance.gameObject.name.Contains("AUTO CHAT MANAGER"))
                {
                    DestroyImmediate(instance.gameObject);
                }
            }
            instance = this;
        }
        BuildChatUI();
    }

    void OnDestroy()
    {
        if (instance != this) return;

        instance = null;
        messageHistory.Clear();
        PendingPreloadMessages.Clear();
    }

    void OnDisable()
    {
        isTyping = false;
        justClosed = false;
        isDragging = false;
        pointerOverChat = false;
        inputTransitionInProgress = false;
        dragPointerDown = false;
        dragFocusGuardUntilFrame = -1;
        displayState = ChatDisplayState.Hidden;
    }

    public static void ResetForTests()
    {
        PendingPreloadMessages.Clear();
        if (instance != null)
        {
            instance.messageHistory.Clear();
            if (instance.gameObject != null)
            {
                if (Application.isPlaying) Destroy(instance.gameObject);
                else DestroyImmediate(instance.gameObject);
            }
            instance = null;
        }
    }

    private bool isSuppressedByReward;

    public bool IsRewardSuppressed
    {
        get
        {
            if (BackpackQuestRewardPresentation.IsVisible)
            {
                return true;
            }
            if (isSuppressedByReward)
            {
                isSuppressedByReward = false;
            }
            return false;
        }
    }

    public bool IsChatVisible => chatPanelRt != null && chatPanelRt.gameObject.activeSelf &&
        ((historyGroup != null && historyGroup.alpha > 0.01f) ||
         (chatPrompt != null && chatPrompt.gameObject.activeSelf) || isTyping);

    public void SetSuppressedByReward(bool suppressed)
    {
        isSuppressedByReward = suppressed;
        if (chatPanelRt != null)
        {
            chatPanelRt.gameObject.SetActive(!suppressed);
        }
        if (chatGroup != null && suppressed)
        {
            chatGroup.alpha = 0f;
            chatGroup.blocksRaycasts = false;
        }
        if (historyGroup != null && suppressed)
            historyGroup.alpha = 0f;
        if (suppressed)
        {
            backgroundAlpha = 0f;
            ApplyBackgroundAlpha();
        }
        else if (pendingVisibilityAfterSuppression)
        {
            ShowHistoryNow();
        }
        RefreshDragHandleVisibility(suppressed);
    }

    private static Type ResolveInputSystemUIInputModuleType()
    {
        Type type = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (type != null) return type;
        type = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem.ForUI");
        if (type != null) return type;
        type = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule");
        if (type != null) return type;

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            type = assembly.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule");
            if (type != null) return type;
        }
        return null;
    }

    public static void EnsureEventSystem()
    {
        EventSystem[] activeSystems = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        if (activeSystems == null || activeSystems.Length == 0)
        {
            var esGo = new GameObject("EventSystem");
            var newEs = esGo.AddComponent<EventSystem>();
            newEs.sendNavigationEvents = false;

            Type inputType = ResolveInputSystemUIInputModuleType();
            if (inputType != null)
            {
                esGo.AddComponent(inputType);
            }
            return;
        }

        Type inputModuleType = ResolveInputSystemUIInputModuleType();

        EventSystem canonical = null;
        if (activeSystems.Length == 1)
        {
            canonical = activeSystems[0];
        }
        else
        {
            int bestScore = int.MinValue;
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

            for (int i = 0; i < activeSystems.Length; i++)
            {
                EventSystem candidate = activeSystems[i];
                if (candidate == null) continue;

                int score = 0;
                // Prefer system with InputSystemUIInputModule
                if (inputModuleType != null && candidate.GetComponent(inputModuleType) != null)
                    score += 20;
                else if (candidate.GetComponent("InputSystemUIInputModule") != null)
                    score += 20;

                // Prefer system authored in the active scene
                if (candidate.gameObject.scene.IsValid() && candidate.gameObject.scene == activeScene)
                    score += 10;

                // Deprecate previous menu systems during transition
                if (candidate.gameObject.name.IndexOf("MainMenu", StringComparison.OrdinalIgnoreCase) >= 0)
                    score -= 5;

                if (candidate.gameObject.name == "EventSystem")
                    score += 2;

                if (candidate == EventSystem.current)
                    score += 1;

                if (score > bestScore)
                {
                    bestScore = score;
                    canonical = candidate;
                }
            }

            if (canonical == null) canonical = activeSystems[0];

            // Deactivate and remove duplicate EventSystems immediately
            for (int i = 0; i < activeSystems.Length; i++)
            {
                EventSystem duplicate = activeSystems[i];
                if (duplicate != null && duplicate != canonical && duplicate.gameObject != null)
                {
                    duplicate.gameObject.SetActive(false);
                    if (Application.isPlaying)
                    {
                        UnityEngine.Object.Destroy(duplicate.gameObject);
                    }
                    else
                    {
                        UnityEngine.Object.DestroyImmediate(duplicate.gameObject);
                    }
                }
            }
        }

        if (canonical != null)
        {
            var standaloneModules = canonical.GetComponents<StandaloneInputModule>();
            for (int i = 0; i < standaloneModules.Length; i++)
            {
                var sm = standaloneModules[i];
                if (sm != null)
                {
                    if (Application.isPlaying) UnityEngine.Object.Destroy(sm);
                    else UnityEngine.Object.DestroyImmediate(sm);
                }
            }

            Component inputModule = inputModuleType != null
                ? canonical.GetComponent(inputModuleType)
                : canonical.GetComponent("InputSystemUIInputModule");

            if (inputModule == null && inputModuleType != null)
            {
                inputModule = canonical.gameObject.AddComponent(inputModuleType);
            }

            var allModules = canonical.GetComponents<BaseInputModule>();
            for (int i = 0; i < allModules.Length; i++)
            {
                var mod = allModules[i];
                if (mod != null && mod != inputModule)
                {
                    if (Application.isPlaying) UnityEngine.Object.Destroy(mod);
                    else UnityEngine.Object.DestroyImmediate(mod);
                }
            }
        }
    }

    // ============================================================
    // XÂY DỰNG GIAO DIỆN (Chạy 1 lần duy nhất)
    // ============================================================
    public void BuildChatUI()
    {
        if (chatHistory != null && chatGroup != null && historyGroup != null) return;

        // --- Đảm bảo có EventSystem ---
        EnsureEventSystem();

        // --- Canvas ---
        var canvasGo = new GameObject("ChatCanvas");
        if (Application.isPlaying) DontDestroyOnLoad(canvasGo);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        GameplayReadinessCoordinator.RegisterGameplayCanvas(canvas);

        // --- Panel tổng (Góc dưới trái, nhích lên tránh đè Ammo UI) ---
        var panel = MakeRect("ChatPanel", canvasGo.transform,
            new Vector2(0, 0), new Vector2(0, 0),
        new Vector2(20, 95), new Vector2(DEFAULT_PANEL_WIDTH, DEFAULT_PANEL_HEIGHT));
        panel.pivot = new Vector2(0, 0);
        chatPanelRt = panel;

        // Khôi phục vị trí người chơi đã lưu từ PlayerPrefs nếu có
        if (PlayerPrefs.HasKey(PrefsKeyPosX) && PlayerPrefs.HasKey(PrefsKeyPosY))
        {
            panel.anchoredPosition = new Vector2(
                PlayerPrefs.GetFloat(PrefsKeyPosX),
                PlayerPrefs.GetFloat(PrefsKeyPosY));
        }
        ClampChatPanelPosition();

        Debug.Log($"[CHAT-DIAG] BuildChatUI: canvas active={canvasGo.activeInHierarchy}, sortingOrder={canvas.sortingOrder}, panelPos={panel.anchoredPosition}");

        chatGroup = panel.gameObject.AddComponent<CanvasGroup>();
        chatGroup.alpha           = 1f;
        chatGroup.blocksRaycasts  = false;
        chatGroup.interactable    = true;

        // --- Khu vực hiển thị lịch sử chat (Scroll View) ---
        var scrollGo = new GameObject("ScrollView");
        scrollGo.transform.SetParent(panel, false);
        var scrollRt = scrollGo.AddComponent<RectTransform>();
        scrollRt.anchorMin = new Vector2(0f, 0.18f);
        scrollRt.anchorMax = new Vector2(1f, 1f);
        scrollRt.offsetMin = Vector2.zero;
        scrollRt.offsetMax = Vector2.zero;

        scrollRect = scrollGo.AddComponent<ScrollRect>();
        scrollRect.horizontal        = false;
        scrollRect.scrollSensitivity = 20f;
        scrollRect.movementType      = ScrollRect.MovementType.Clamped;

        // Viewport
        var viewport = MakeRectStretch("Viewport", scrollGo.transform);
        vpBg         = viewport.gameObject.AddComponent<Image>();
        vpBg.color   = new Color(0f, 0f, 0f, 0f);
        // Clip history by rectangle only. A stencil Mask tied to the
        // background graphic can cull the text when the black background
        // fades to transparent.
        viewport.gameObject.AddComponent<RectMask2D>();
        scrollRect.viewport  = viewport;

        // Content (kéo dài vô hạn theo nội dung)
        var content    = new GameObject("Content");
        content.transform.SetParent(viewport, false);
        var contentRt  = content.AddComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 0f);
        contentRt.anchorMax = new Vector2(1f, 0f);
        contentRt.pivot     = new Vector2(0.5f, 0f);
        contentRt.offsetMin = new Vector2(5f, 0f);
        contentRt.offsetMax = new Vector2(-5f, 0f);
        scrollRect.content  = contentRt;

        // Only the history content gets this fade. The viewport background is
        // controlled independently by vpBg/backgroundAlpha.
        historyGroup = content.AddComponent<CanvasGroup>();
        historyGroup.alpha = 0f;
        historyGroup.blocksRaycasts = false;
        historyGroup.interactable = false;

        var csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        chatHistory = content.AddComponent<Text>();
        chatHistory.font              = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        chatHistory.fontSize          = 14;
        chatHistory.color             = Color.white;
        chatHistory.alignment         = TextAnchor.LowerLeft;
        chatHistory.horizontalOverflow = HorizontalWrapMode.Wrap;
        chatHistory.verticalOverflow   = VerticalWrapMode.Overflow;
        chatHistory.raycastTarget      = false;

        var outline = content.AddComponent<Outline>();
        outline.effectColor    = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(1f, -1f);

        // --- Ô nhập liệu (Bottom 18%) ---
        inputContainer = new GameObject("InputContainer");
        inputContainer.transform.SetParent(panel, false);
        var inputRt       = inputContainer.AddComponent<RectTransform>();
        inputRt.anchorMin = new Vector2(0f, 0f);
        inputRt.anchorMax = new Vector2(1f, 0.17f);
        inputRt.offsetMin = new Vector2(0f, 0f);
        inputRt.offsetMax = new Vector2(0f, 0f);

        var inputBg   = inputContainer.AddComponent<Image>();
        inputBg.color = new Color(0.05f, 0.05f, 0.05f, 0.92f);

        chatInput                  = inputContainer.AddComponent<InputField>();
        chatInput.targetGraphic    = inputBg;
        chatInput.customCaretColor = true;
        chatInput.caretColor       = Color.white;
        chatInput.caretWidth       = 2;
        chatInput.lineType         = InputField.LineType.SingleLine;

        // Placeholder
        var phGo = new GameObject("Placeholder");
        phGo.transform.SetParent(inputContainer.transform, false);
        var phRt       = phGo.AddComponent<RectTransform>();
        phRt.anchorMin = Vector2.zero;
        phRt.anchorMax = Vector2.one;
        phRt.offsetMin = new Vector2(8f, 2f);
        phRt.offsetMax = new Vector2(-8f, -2f);
        var phText          = phGo.AddComponent<Text>();
        phText.font         = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        phText.fontSize     = 13;
        phText.color        = new Color(0.75f, 0.75f, 0.75f, 0.6f);
        phText.text         = GameLocalization.Get("chat.input_placeholder");
        phText.fontStyle    = FontStyle.Italic;
        phText.raycastTarget = false;
        chatInput.placeholder = phText;

        // Text hiển thị người dùng gõ
        var textGo = new GameObject("Text");
        textGo.transform.SetParent(inputContainer.transform, false);
        var textRt       = textGo.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(8f, 2f);
        textRt.offsetMax = new Vector2(-8f, -2f);
        var inputText             = textGo.AddComponent<Text>();
        inputText.font            = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        inputText.fontSize        = 14;
        inputText.color           = Color.white;
        inputText.alignment       = TextAnchor.MiddleLeft;
        inputText.supportRichText = false;
        chatInput.textComponent   = inputText;
        chatInput.characterLimit  = 120;
        chatInput.lineType        = InputField.LineType.SingleLine;

        // Enter/Escape được xử lý trong Update. onEndEdit chỉ đóng khi mất focus.
        chatInput.onEndEdit.AddListener(OnChatEndEdit);

        inputContainer.SetActive(false);

        // Vùng kéo là sibling của ChatPanel nên vẫn nhận chuột khi chatGroup
        // chuyển sang text-only (blocksRaycasts=false). Chỉ phủ dải mỏng ở
        // mép trên, không phủ lịch sử hay ô nhập.
        var dragHandleGo = new GameObject("DragHandle");
        dragHandleGo.transform.SetParent(canvasGo.transform, false);
        dragHandleRt = dragHandleGo.AddComponent<RectTransform>();
        dragHandleRt.anchorMin = Vector2.zero;
        dragHandleRt.anchorMax = Vector2.zero;
        dragHandleRt.pivot = new Vector2(0f, 0f);
        dragHandleImage = dragHandleGo.AddComponent<Image>();
        dragHandleImage.color = Color.clear;
        dragHandleImage.raycastTarget = true;
        var draggable = dragHandleGo.AddComponent<UIDraggable>();
        draggable.targetToDrag = panel;
        UpdateDragHandleGeometry();
        dragHandleGo.SetActive(false);

        if (PendingPreloadMessages.Count > 0)
        {
            foreach (string msg in PendingPreloadMessages)
            {
                AppendFormattedLine(msg);
            }
            PendingPreloadMessages.Clear();
        }
    }

    public bool CanOpenChat()
    {
        if (isTyping || justClosed) return false;
        if (GameplayReadinessCoordinator.IsGameplaySuppressed) return false;
        if (AutoMainMenuManager.Instance != null && AutoMainMenuManager.Instance.IsPauseMenuOrOptionsOpen) return false;
        if (AutoUIManager.Instance != null && AutoUIManager.Instance.IsAnyMenuOpen()) return false;
        if (QuestFlowUIPrototype.Instance != null && QuestFlowUIPrototype.Instance.IsQuestOverlayOpen) return false;
        if (CivilianRoutePresentationController.BlocksGameplayInput ||
            MilitaryRouteBEscapePresentation.BlocksGameplayInput ||
            VictorySummaryUI.IsShowing) return false;
        return true;
    }

    // ============================================================
    // VÒNG LẶP CHÍNH
    // ============================================================
    void Update()
    {
        bool enterPressed = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
        bool escapePressed = Input.GetKeyDown(KeyCode.Escape);

        if (isTyping)
        {
            if (escapePressed)
            {
                if (AutoMainMenuManager.Instance != null)
                    AutoMainMenuManager.EscapeConsumedThisFrame = true;
                CloseChat();
            }
            else if (enterPressed)
            {
                SubmitCurrentMessageOrClose();
            }
        }
        else if (CanOpenChat() && enterPressed)
        {
            OpenChat();
        }

        bool suppressed = IsRewardSuppressed;
        if (suppressed)
        {
            wasSuppressed = true;
            if (chatGroup != null)
            {
                chatGroup.alpha = 0f;
                chatGroup.blocksRaycasts = false;
            }
            if (historyGroup != null)
                historyGroup.alpha = 0f;
            backgroundAlpha = 0f;
            ApplyBackgroundAlpha();
            if (chatPanelRt != null && chatPanelRt.gameObject.activeSelf)
                chatPanelRt.gameObject.SetActive(false);
            RefreshDragHandleVisibility(true);
            return;
        }

        if (chatPanelRt != null && !chatPanelRt.gameObject.activeSelf)
            chatPanelRt.gameObject.SetActive(true);

        if (wasSuppressed)
        {
            wasSuppressed = false;
            if (pendingVisibilityAfterSuppression)
                ShowHistoryNow();
        }

        UpdatePointerHover();
        UpdateDisplayVisuals();
        justClosed = false;
    }

    private void SubmitCurrentMessageOrClose()
    {
        if (!isTyping) return;

        string rawMessage = chatInput != null ? chatInput.text : string.Empty;
        if (string.IsNullOrWhiteSpace(rawMessage))
        {
            CloseChat();
            return;
        }

        string clean = rawMessage.Trim();
        Debug.Log($"[CHAT-DIAG] Submit chat: '{clean}' (listeners={GetSendMessageListenerCount()})");
        onSendMessage?.Invoke(clean);

        // Keep the input session open after a sent message. A second Enter
        // only closes the chat when the next input is empty.
        inputTransitionInProgress = true;
        dragFocusGuardUntilFrame = Time.frameCount + 1;
        if (chatInput != null)
        {
            chatInput.text = string.Empty;
            chatInput.ActivateInputField();
            chatInput.Select();
        }
        inputTransitionInProgress = false;
        ApplyEditingVisuals();
        if (Application.isPlaying) StartCoroutine(FocusInputFieldRoutine());
    }

    private void UpdatePointerHover()
    {
        if (isDragging)
        {
            pointerOverChat = true;
            return;
        }

        pointerOverChat = false;
        if (dragHandleRt == null || chatPanelRt == null || !chatPanelRt.gameObject.activeInHierarchy)
            return;
        if (!Cursor.visible || Cursor.lockState == CursorLockMode.Locked)
            return;

        Canvas canvas = chatPanelRt.GetComponentInParent<Canvas>();
        Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;
        pointerOverChat = RectTransformUtility.RectangleContainsScreenPoint(
            chatPanelRt, Input.mousePosition, eventCamera);
    }

    private void UpdateDisplayVisuals()
    {
        if (chatGroup == null || historyGroup == null) return;

        // The parent group is only for the global reward suppression path.
        // History opacity is deliberately independent from the black panel.
        chatGroup.alpha = 1f;

        // Chat history is persistent, like an in-game chat log. Only the
        // black background is allowed to fade; history is removed only by the
        // explicit line/character limits in AppendFormattedLine().
        historyGroup.alpha = messageHistory.Count > 0 ? 1f : 0f;

        // Hovering the passive history must never add a black panel. The
        // background belongs only to active chat input or panel dragging.
        bool showBackground = isTyping || isDragging;
        float targetBackgroundAlpha = showBackground ? BACKGROUND_MAX_ALPHA : 0f;
        backgroundAlpha = Mathf.MoveTowards(
            backgroundAlpha,
            targetBackgroundAlpha,
            Time.deltaTime * BACKGROUND_FADE_SPEED);
        ApplyBackgroundAlpha();
        UpdateDragHandleGeometry();

        if (inputContainer != null && inputContainer.activeSelf != isTyping)
            inputContainer.SetActive(isTyping);

        chatGroup.blocksRaycasts = isTyping;
        chatGroup.interactable = isTyping;

        displayState = isDragging ? ChatDisplayState.Dragging :
            isTyping ? ChatDisplayState.Editing :
            pointerOverChat ? ChatDisplayState.Hovered :
            historyGroup.alpha > 0.01f ? ChatDisplayState.TextOnly :
            ChatDisplayState.Hidden;

        RefreshDragHandleVisibility(false);
        UpdateChatPromptVisibility();
    }

    // ============================================================
    // MỞ / ĐÓNG CHAT
    // ============================================================
    public void OpenChat()
    {
        if (isTyping) return;
        isTyping = true;
        isDragging = false;
        dragPointerDown = false;
        displayState = ChatDisplayState.Editing;

        ClampChatPanelPosition();

        ApplyEditingVisuals();

        if (chatInput != null)
        {
            chatInput.text = string.Empty;
        }

        Debug.Log($"[CHAT-DIAG] OpenChat called: isTyping={isTyping}, chatPanel active={chatPanelRt?.gameObject.activeInHierarchy}, pos={chatPanelRt?.anchoredPosition}");
        FocusInputFieldNow();
        if (Application.isPlaying) StartCoroutine(FocusInputFieldRoutine());
    }

    private IEnumerator FocusInputFieldRoutine()
    {
        yield return null;

        if (chatInput == null || !isTyping) yield break;

        FocusInputFieldNow();

        Debug.Log($"[CHAT-DIAG] FocusInputFieldRoutine: input active={chatInput.gameObject.activeInHierarchy}, interactable={chatInput.interactable}, isFocused={chatInput.isFocused}, currentSelected={(EventSystem.current != null ? EventSystem.current.currentSelectedGameObject?.name : "null")}");
    }

    private void FocusInputFieldNow()
    {
        if (chatInput == null || !isTyping || !chatInput.gameObject.activeInHierarchy) return;

        EnsureEventSystem();
        EventSystem eventSystem = GetUsableEventSystem();
        if (eventSystem != null && !eventSystem.alreadySelecting)
            eventSystem.SetSelectedGameObject(chatInput.gameObject);
        chatInput.ActivateInputField();
        chatInput.Select();
    }

    private static EventSystem GetUsableEventSystem()
    {
        if (EventSystem.current != null) return EventSystem.current;
        return UnityEngine.Object.FindFirstObjectByType<EventSystem>();
    }

    internal void NotifyChatDragPointerDown()
    {
        pointerOverChat = true;
        dragPointerDown = true;
        if (isTyping)
        {
            // InputField can emit onEndEdit as soon as the drag overlay is
            // selected. Keep that transient focus loss from closing chat.
            dragFocusGuardUntilFrame = Mathf.Max(dragFocusGuardUntilFrame, Time.frameCount + 1);
        }
    }

    internal void NotifyChatDragStarted()
    {
        isDragging = true;
        pointerOverChat = true;
        dragPointerDown = true;
        dragFocusGuardUntilFrame = Mathf.Max(dragFocusGuardUntilFrame, Time.frameCount + 1);
        displayState = ChatDisplayState.Dragging;
        if (chatGroup != null) chatGroup.alpha = 1f;
        backgroundAlpha = BACKGROUND_MAX_ALPHA;
        ApplyBackgroundAlpha();
        UpdateChatPromptVisibility();
    }

    internal void NotifyChatDragEnded()
    {
        isDragging = false;
        dragFocusGuardUntilFrame = Time.frameCount + 2;
        UpdateDragHandleGeometry();
        displayState = isTyping ? ChatDisplayState.Editing :
            pointerOverChat ? ChatDisplayState.Hovered : ChatDisplayState.TextOnly;
        UpdateChatPromptVisibility();

        if (isTyping)
        {
            ApplyEditingVisuals();
            FocusInputFieldNow();
            if (Application.isPlaying) StartCoroutine(FocusInputFieldRoutine());
        }
    }

    internal void NotifyChatDragPointerUp()
    {
        pointerOverChat = true;
        if (isTyping)
        {
            dragFocusGuardUntilFrame = Time.frameCount + 2;
            FocusInputFieldNow();
            dragPointerDown = false;
        }
        else
        {
            dragPointerDown = false;
        }
    }

    internal void NotifyChatPanelMoved()
    {
        UpdateDragHandleGeometry();
    }

    private bool IsChatDragFocusGuardActive => Time.frameCount <= dragFocusGuardUntilFrame;

    private bool IsPointerOverChatDragHandle()
    {
        if (dragHandleRt == null || chatPanelRt == null ||
            !chatPanelRt.gameObject.activeInHierarchy)
            return false;

        Canvas canvas = dragHandleRt.GetComponentInParent<Canvas>();
        Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;
        return RectTransformUtility.RectangleContainsScreenPoint(
            dragHandleRt, Input.mousePosition, eventCamera);
    }

    private void ApplyEditingVisuals()
    {
        if (inputContainer != null) inputContainer.SetActive(true);
        if (chatGroup != null)
        {
            chatGroup.alpha = 1f;
            chatGroup.blocksRaycasts = true;
            chatGroup.interactable = true;
        }
        if (historyGroup != null)
            historyGroup.alpha = 1f;
        backgroundAlpha = BACKGROUND_MAX_ALPHA;
        ApplyBackgroundAlpha();
        SetDragHandleVisible(true);
        UpdateChatPromptVisibility();
    }

    private void ApplyTextOnlyVisuals()
    {
        if (inputContainer != null) inputContainer.SetActive(false);
        if (chatGroup != null)
        {
            chatGroup.blocksRaycasts = false;
            chatGroup.interactable = false;
        }
        displayState = ChatDisplayState.TextOnly;
        UpdateChatPromptVisibility();
    }

    private void ApplyBackgroundAlpha()
    {
        if (vpBg == null) return;

        vpBg.color = new Color(0.045f, 0.045f, 0.06f, backgroundAlpha);
    }

    public void CloseChat()
    {
        isTyping = false;
        justClosed = true;
        dragPointerDown = false;
        dragFocusGuardUntilFrame = -1;
        inputTransitionInProgress = true;
        if (chatInput != null)
        {
            chatInput.text = string.Empty;
            chatInput.DeactivateInputField();
        }
        inputTransitionInProgress = false;
        if (inputContainer != null) inputContainer.SetActive(false);
        ApplyTextOnlyVisuals();
        EventSystem eventSystem = GetUsableEventSystem();
        if (eventSystem != null && !eventSystem.alreadySelecting)
            eventSystem.SetSelectedGameObject(null);
        RefreshDragHandleVisibility(false);
        UpdateChatPromptVisibility();
        Debug.Log("[CHAT-DIAG] CloseChat completed.");
    }

    // ============================================================
    // XỬ LÝ KẾT THÚC CHAT (Gửi tin nhắn hoặc hủy bỏ)
    // ============================================================
    private void OnChatEndEdit(string message)
    {
        if (!isTyping || inputTransitionInProgress) return;

        // Focus can be lost before BeginDrag, or the legacy InputField can
        // report it after the pointer has already moved. Keep the chat session
        // alive for that drag path and restore focus on release.
        if (IsChatDragFocusGuardActive ||
            (IsPointerOverChatDragHandle() &&
             (dragPointerDown || Input.GetMouseButton(0) ||
              Mathf.Abs(Input.GetAxis("Mouse X")) > 0.01f ||
              Mathf.Abs(Input.GetAxis("Mouse Y")) > 0.01f)))
        {
            dragPointerDown = true;
            Debug.Log("[CHAT-DIAG] OnChatEndEdit ignored while preserving focus during panel drag.");
            return;
        }

        bool keyboardClose = Input.GetKeyDown(KeyCode.Return) ||
            Input.GetKeyDown(KeyCode.KeypadEnter) ||
            Input.GetKeyDown(KeyCode.Escape);
        if (keyboardClose) return;

        Debug.Log("[CHAT-DIAG] Chat input lost focus; closing without sending.");
        CloseChat();
    }

    // ============================================================
    // NHẬN TIN NHẮN VÀ HIỂN THỊ (RPC sẽ gọi hàm này)
    // ============================================================
    public void AddPlayerMessage(string sender, string message)
    {
        string safeSender = PlayerDeathContext.SanitizeRichText(sender);
        if (string.IsNullOrWhiteSpace(safeSender)) safeSender = "Survivor";
        if (safeSender.Length > 32) safeSender = safeSender.Substring(0, 32);

        string safeMsg = PlayerDeathContext.SanitizeRichText(message);
        if (string.IsNullOrWhiteSpace(safeMsg)) return;
        if (safeMsg.Length > 128) safeMsg = safeMsg.Substring(0, 128);

        Debug.Log($"[CHAT-DIAG] AddPlayerMessage: sender='{safeSender}', msg='{safeMsg}'");

        string formatted = $"<color=yellow><b>[{safeSender}]</b></color>: {safeMsg}";
        if (chatHistory == null)
        {
            PendingPreloadMessages.Add(formatted);
            if (PendingPreloadMessages.Count > MaxHistoryLines)
                PendingPreloadMessages.RemoveAt(0);
            return;
        }

        AppendFormattedLine(formatted);
    }

    public void AddSystemMessage(string message)
    {
        string prefix = GameLocalization.Get("chat.system_prefix", "SYSTEM");
        string safeMsg = PlayerDeathContext.SanitizeRichText(message);
        if (string.IsNullOrWhiteSpace(safeMsg)) return;
        if (safeMsg.Length > 180) safeMsg = safeMsg.Substring(0, 180);

        string formatted = $"<color=#FFD54A>[{prefix}] {safeMsg}</color>";
        if (chatHistory == null)
        {
            PendingPreloadMessages.Add(formatted);
            if (PendingPreloadMessages.Count > MaxHistoryLines)
                PendingPreloadMessages.RemoveAt(0);
            return;
        }

        AppendFormattedLine(formatted);
    }

    public void AddMessage(string sender, string message)
    {
        if (string.Equals(sender, "SYSTEM", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(sender, "HỆ THỐNG", System.StringComparison.OrdinalIgnoreCase))
        {
            AddSystemMessage(message);
        }
        else
        {
            AddPlayerMessage(sender, message);
        }
    }

    private void AppendFormattedLine(string formattedLine)
    {
        if (chatHistory == null) return;

        if (messageHistory.Count == 0 && !string.IsNullOrEmpty(chatHistory.text))
        {
            string[] existing = chatHistory.text.Split('\n');
            for (int i = 0; i < existing.Length; i++)
            {
                if (!string.IsNullOrEmpty(existing[i]))
                    messageHistory.Add(existing[i]);
            }
        }

        messageHistory.Add(formattedLine);
        while (messageHistory.Count > MaxHistoryLines)
        {
            messageHistory.RemoveAt(0);
        }

        int totalChars = 0;
        for (int i = 0; i < messageHistory.Count; i++)
            totalChars += messageHistory[i].Length + 1;

        while (totalChars > MaxHistoryCharacters && messageHistory.Count > 1)
        {
            totalChars -= (messageHistory[0].Length + 1);
            messageHistory.RemoveAt(0);
        }

        chatHistory.text = string.Join("\n", messageHistory);

        if (IsRewardSuppressed)
        {
            pendingVisibilityAfterSuppression = true;
            if (chatGroup != null)
            {
                chatGroup.alpha = 0f;
                chatGroup.blocksRaycasts = false;
            }
            if (historyGroup != null)
                historyGroup.alpha = 0f;
            if (chatPanelRt != null)
            {
                chatPanelRt.gameObject.SetActive(false);
            }
        }
        else
        {
            ShowHistoryNow();
        }

        if (isActiveAndEnabled && gameObject.activeInHierarchy && chatPanelRt != null)
        {
            try
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(chatPanelRt);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[AutoChatManager] Layout rebuild warning: {ex.Message}");
            }
        }

        if (scrollRect != null)
            scrollRect.verticalNormalizedPosition = 0f;
    }

    // ============================================================
    // API CÔNG KHAI
    // ============================================================

    /// <summary>Được PlayerInputHandler2D dùng để chặn WASD khi đang chat</summary>
    public bool IsTyping() => isTyping;

    public int GetSendMessageListenerCount()
    {
        return onSendMessage != null ? onSendMessage.GetInvocationList().Length : 0;
    }

    public void ClampChatPanelPosition()
    {
        if (chatPanelRt == null) return;
        RectTransform parentRect = chatPanelRt.parent as RectTransform;
        float canvasWidth = parentRect != null && parentRect.rect.width > 100f ? parentRect.rect.width : 1920f;
        float canvasHeight = parentRect != null && parentRect.rect.height > 100f ? parentRect.rect.height : 1080f;

        Vector2 pos = chatPanelRt.anchoredPosition;
        float minX = 0f;
        float maxX = Mathf.Max(minX, canvasWidth - chatPanelRt.rect.width);
        float minY = 0f;
        float maxY = Mathf.Max(minY, canvasHeight - chatPanelRt.rect.height);

        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.y = Mathf.Clamp(pos.y, minY, maxY);
        chatPanelRt.anchoredPosition = pos;
    }

    private void ShowHistoryNow()
    {
        pendingVisibilityAfterSuppression = false;
        if (chatPanelRt != null && !chatPanelRt.gameObject.activeSelf)
            chatPanelRt.gameObject.SetActive(true);
        if (chatGroup != null)
            chatGroup.alpha = 1f;
        if (historyGroup != null)
            historyGroup.alpha = 1f;
        if (!isTyping && !isDragging && !pointerOverChat)
            displayState = ChatDisplayState.TextOnly;
        RefreshDragHandleVisibility(false);
    }

    private void UpdateDragHandleGeometry()
    {
        if (dragHandleRt == null || chatPanelRt == null) return;

        Vector2 panelSize = chatPanelRt.rect.size;
        if (panelSize.x <= 0f) panelSize.x = chatPanelRt.sizeDelta.x;
        if (panelSize.y <= 0f) panelSize.y = chatPanelRt.sizeDelta.y;

        float dragHeight = Mathf.Min(DRAG_HANDLE_HEIGHT, panelSize.y);
        dragHandleRt.anchoredPosition = chatPanelRt.anchoredPosition +
            new Vector2(0f, panelSize.y - dragHeight);
        dragHandleRt.sizeDelta = new Vector2(panelSize.x, dragHeight);
    }

    private void SetDragHandleVisible(bool visible)
    {
        if (dragHandleRt == null) return;
        UpdateDragHandleGeometry();
        if (dragHandleRt.gameObject.activeSelf != visible)
            dragHandleRt.gameObject.SetActive(visible);
    }

    private void RefreshDragHandleVisibility(bool suppressed)
    {
        bool visible = !suppressed &&
            chatPanelRt != null && chatPanelRt.gameObject.activeSelf &&
            (isTyping || isDragging || backgroundAlpha > 0.01f);
        SetDragHandleVisible(visible);
    }

    private void UpdateChatPromptVisibility()
    {
        if (chatPrompt == null) return;

        bool visible = chatPanelRt != null && chatPanelRt.gameObject.activeSelf &&
            !isTyping && !isDragging;
        if (chatPrompt.gameObject.activeSelf != visible)
            chatPrompt.gameObject.SetActive(visible);
    }

    // ============================================================
    // HELPER METHODS
    // ============================================================
    private RectTransform MakeRect(string name, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt             = go.AddComponent<RectTransform>();
        rt.anchorMin       = anchorMin;
        rt.anchorMax       = anchorMax;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta       = size;
        return rt;
    }

    private RectTransform MakeRectStretch(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt       = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }
}

public class UIDraggable : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public RectTransform targetToDrag;
    private Vector2 dragOffset;
    public bool IsDragging { get; private set; }

    void Start()
    {
        if (targetToDrag == null)
            targetToDrag = GetComponent<RectTransform>();
        ClampToParent();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        AutoChatManager.ExistingInstance?.NotifyChatDragPointerDown();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (targetToDrag == null) return;
        AutoChatManager chat = AutoChatManager.ExistingInstance;

        IsDragging = true;
        chat?.NotifyChatDragStarted();
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            targetToDrag.parent as RectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localMousePos);
        dragOffset = targetToDrag.anchoredPosition - localMousePos;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (targetToDrag == null || !IsDragging) return;
        
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            targetToDrag.parent as RectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localMousePos);
            
        targetToDrag.anchoredPosition = localMousePos + dragOffset;
        ClampToParent();
        AutoChatManager.ExistingInstance?.NotifyChatPanelMoved();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!IsDragging) return;
        IsDragging = false;
        if (targetToDrag != null)
        {
            PlayerPrefs.SetFloat(AutoChatManager.PrefsKeyPosX, targetToDrag.anchoredPosition.x);
            PlayerPrefs.SetFloat(AutoChatManager.PrefsKeyPosY, targetToDrag.anchoredPosition.y);
            PlayerPrefs.Save();
        }
        AutoChatManager.ExistingInstance?.NotifyChatDragEnded();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        AutoChatManager.ExistingInstance?.NotifyChatDragPointerUp();
    }

    public void ClampToParent()
    {
        if (targetToDrag == null || targetToDrag.parent == null) return;
        RectTransform parentRect = targetToDrag.parent as RectTransform;
        if (parentRect == null) return;

        float pWidth = parentRect.rect.width > 100f ? parentRect.rect.width : 1920f;
        float pHeight = parentRect.rect.height > 100f ? parentRect.rect.height : 1080f;

        Vector2 pos = targetToDrag.anchoredPosition;
        float minX = 0f;
        float maxX = Mathf.Max(0f, pWidth - targetToDrag.rect.width);
        float minY = 0f;
        float maxY = Mathf.Max(0f, pHeight - targetToDrag.rect.height);

        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.y = Mathf.Clamp(pos.y, minY, maxY);
        targetToDrag.anchoredPosition = pos;
    }
}
