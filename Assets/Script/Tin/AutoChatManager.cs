using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

/// <summary>
/// Hệ thống Box Chat Multiplayer - Hoạt động 100% cho mọi người chơi.
/// Dùng sự kiện onEndEdit của InputField (cách chuẩn Unity) thay vì tự bắt phím Enter.
/// Điều này tránh hoàn toàn lỗi "chat hiện rồi biến mất ngay".
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
                    DontDestroyOnLoad(go);
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

    // ========================= THAM CHIẾU UI =========================
    private CanvasGroup chatGroup;
    private Text chatHistory;
    private InputField chatInput;
    private GameObject inputContainer;
    private ScrollRect scrollRect;

    // ========================= CẤU HÌNH =========================
    private const float SHOW_DURATION = 6f;
    private const float FADE_SPEED    = 1.5f;

    // ========================= TRẠNG THÁI =========================
    private float fadeTimer = 0f;
    private bool  isTyping  = false;
    private bool  justClosed = false;

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
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        BuildChatUI();
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // ============================================================
    // XÂY DỰNG GIAO DIỆN (Chạy 1 lần duy nhất)
    // ============================================================
    void BuildChatUI()
    {
        // --- Đảm bảo có EventSystem ---
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<StandaloneInputModule>();
        }

        // --- Canvas ---
        var canvasGo = new GameObject("ChatCanvas");
        DontDestroyOnLoad(canvasGo);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        // --- Panel tổng (Góc dưới trái, nhích lên tránh đè Ammo UI) ---
        var panel = MakeRect("ChatPanel", canvasGo.transform,
            new Vector2(0, 0), new Vector2(0, 0),
            new Vector2(20, 95), new Vector2(400, 230));
        panel.pivot = new Vector2(0, 0);

        chatGroup = panel.gameObject.AddComponent<CanvasGroup>();
        chatGroup.alpha           = 0f;
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
        var vpBg     = viewport.gameObject.AddComponent<Image>();
        vpBg.color   = new Color(0f, 0f, 0f, 0.45f);
        var mask     = viewport.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = true;
        scrollRect.viewport  = viewport;

        var draggable = viewport.gameObject.AddComponent<UIDraggable>();
        draggable.targetToDrag = panel;

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
        phText.text         = "Nhấn Enter để chat...";
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

        // 🔥 Móc sự kiện onEndEdit: Unity tự động gọi khi kết thúc nhập (Enter, ESC hoặc click ra ngoài)
        chatInput.onEndEdit.AddListener(OnChatEndEdit);

        inputContainer.SetActive(false);
    }

    // ============================================================
    // VÒNG LẶP CHÍNH
    // ============================================================
    void Update()
    {
        // Bắt phím Enter để MỞ chat (chỉ khi chưa mở và không vừa mới đóng ở cùng frame)
        if (!isTyping && !justClosed && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
        {
            OpenChat();
        }

        // ESC để thoát chat
        if (isTyping && Input.GetKeyDown(KeyCode.Escape))
        {
            if (AutoMainMenuManager.Instance != null)
            {
                AutoMainMenuManager.EscapeConsumedThisFrame = true;
            }
            CloseChat();
        }

        // --- Logic làm mờ dần ---
        if (isTyping)
        {
            chatGroup.alpha = 1f;
        }
        else if (fadeTimer > 0f)
        {
            fadeTimer        -= Time.deltaTime;
            chatGroup.alpha   = 1f;
        }
        else
        {
            chatGroup.alpha = Mathf.MoveTowards(chatGroup.alpha, 0f, Time.deltaTime * FADE_SPEED);
        }

        // Reset cờ ở cuối frame Update
        justClosed = false;
    }

    // ============================================================
    // MỞ / ĐÓNG CHAT
    // ============================================================
    private void OpenChat()
    {
        isTyping = true;
        inputContainer.SetActive(true);
        chatGroup.blocksRaycasts = true;
        chatGroup.alpha          = 1f;

        // Delay 2 frame để phím Enter của frame hiện tại được xử lý xong
        // trước khi InputField bắt đầu lắng nghe bàn phím
        StartCoroutine(FocusAfterDelay());
    }

    private IEnumerator FocusAfterDelay()
    {
        yield return null;
        yield return null;

        if (chatInput == null || !isTyping) yield break;

        chatInput.text = "";
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(chatInput.gameObject);
        chatInput.ActivateInputField();
        chatInput.Select();
    }

    private void CloseChat()
    {
        isTyping = false;
        justClosed = true; // Đánh dấu là vừa đóng chat
        if (chatInput != null) chatInput.text = "";
        inputContainer.SetActive(false);
        chatGroup.blocksRaycasts = false;
        if (EventSystem.current != null && !EventSystem.current.alreadySelecting)
            EventSystem.current.SetSelectedGameObject(null);
        fadeTimer = SHOW_DURATION;
    }

    // ============================================================
    // XỬ LÝ KẾT THÚC CHAT (Gửi tin nhắn hoặc hủy bỏ)
    // ============================================================
    private void OnChatEndEdit(string message)
    {
        if (!isTyping) return;

        // Nếu không bấm ESC (tức là bấm Enter hoặc click ra ngoài)
        if (chatInput != null && !chatInput.wasCanceled)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                onSendMessage?.Invoke(message.Trim());
            }
        }
        else
        {
            // Bấm ESC
            if (AutoMainMenuManager.Instance != null)
            {
                AutoMainMenuManager.EscapeConsumedThisFrame = true;
            }
        }

        CloseChat();
    }

    // ============================================================
    // NHẬN TIN NHẮN VÀ HIỂN THỊ (RPC sẽ gọi hàm này)
    // ============================================================
    public void AddMessage(string sender, string message)
    {
        if (chatHistory == null) return;

        // Chống phình RAM: Cắt bớt lịch sử cũ khi quá dài
        if (chatHistory.text.Length > 3000)
            chatHistory.text = chatHistory.text.Substring(chatHistory.text.Length - 1500);

        chatHistory.text += $"\n<color=yellow><b>[{sender}]</b></color>: {message}";
        fadeTimer = SHOW_DURATION;

        Canvas.ForceUpdateCanvases();
        if (scrollRect != null)
            scrollRect.verticalNormalizedPosition = 0f;
    }

    // ============================================================
    // API CÔNG KHAI
    // ============================================================

    /// <summary>Được PlayerInputHandler2D dùng để chặn WASD khi đang chat</summary>
    public bool IsTyping() => isTyping;

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

public class UIDraggable : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    public RectTransform targetToDrag;
    private Vector2 dragOffset;

    void Start()
    {
        if (targetToDrag == null)
            targetToDrag = GetComponent<RectTransform>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (targetToDrag == null) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            targetToDrag.parent as RectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localMousePos);
        dragOffset = targetToDrag.anchoredPosition - localMousePos;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (targetToDrag == null) return;
        
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            targetToDrag.parent as RectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localMousePos);
            
        targetToDrag.anchoredPosition = localMousePos + dragOffset;
        ClampToParent();
    }

    private void ClampToParent()
    {
        if (targetToDrag == null || targetToDrag.parent == null) return;
        RectTransform parentRect = targetToDrag.parent as RectTransform;
        
        Vector2 pos = targetToDrag.anchoredPosition;
        
        float minX = -parentRect.rect.width * targetToDrag.anchorMin.x + targetToDrag.rect.width * targetToDrag.pivot.x;
        float maxX = parentRect.rect.width * (1f - targetToDrag.anchorMax.x) - targetToDrag.rect.width * (1f - targetToDrag.pivot.x);
        float minY = -parentRect.rect.height * targetToDrag.anchorMin.y + targetToDrag.rect.height * targetToDrag.pivot.y;
        float maxY = parentRect.rect.height * (1f - targetToDrag.anchorMax.y) - targetToDrag.rect.height * (1f - targetToDrag.pivot.y);
        
        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.y = Mathf.Clamp(pos.y, minY, maxY);
        targetToDrag.anchoredPosition = pos;
    }
}