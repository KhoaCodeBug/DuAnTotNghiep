using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class AutoChatManager : MonoBehaviour
{
    public static AutoChatManager Instance;

    private CanvasGroup chatGroup;
    private Text chatHistory;
    private InputField chatInput;
    private GameObject inputObj;
    private ScrollRect chatScrollRect;

    private float fadeTimer = 0f;
    private float showDuration = 5f;
    private bool isTyping = false;

    public delegate void OnSendMessage(string msg);
    public OnSendMessage onSendMessage;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoSpawn()
    {
        if (Instance == null)
        {
            GameObject go = new GameObject("--- AUTO CHAT MANAGER ---");
            go.AddComponent<AutoChatManager>();
            DontDestroyOnLoad(go);
        }
    }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        SetupChatUI();
    }

    void SetupChatUI()
    {
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<EventSystem>();
            esObj.AddComponent<StandaloneInputModule>();
        }

        GameObject canvasObj = new GameObject("AutoChatCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObj.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(canvasObj);

        // --- KHUNG TỔNG ---
        GameObject panelObj = new GameObject("ChatPanel");
        panelObj.transform.SetParent(canvasObj.transform, false);
        RectTransform panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0, 0); panelRect.anchorMax = new Vector2(0, 0);
        panelRect.pivot = new Vector2(0, 0);

        // 👉 ĐỔI SỐ NÀY: 20 -> 150 để khung chat nhích lên cao khỏi mép dưới
        panelRect.anchoredPosition = new Vector2(20, 60);

        // 🔥 Đã thu nhỏ gọn gàng (Trước đó là 450x250)
        panelRect.sizeDelta = new Vector2(350, 200);

        chatGroup = panelObj.AddComponent<CanvasGroup>();
        chatGroup.alpha = 0f;
        // Bắt đầu game: Xuyên thấu để không cản trở chuột khi bắn súng
        chatGroup.blocksRaycasts = false;

        // ===============================================
        // 🔥 HỆ THỐNG LĂN CHUỘT (SCROLL VIEW) HOÀN TOÀN MỚI
        // ===============================================
        GameObject scrollViewObj = new GameObject("ScrollView");
        scrollViewObj.transform.SetParent(panelObj.transform, false);
        RectTransform scrollRectTrans = scrollViewObj.AddComponent<RectTransform>();
        scrollRectTrans.anchorMin = new Vector2(0, 0.18f); // Chừa chỗ dưới cùng cho Input gõ chữ
        scrollRectTrans.anchorMax = new Vector2(1, 1);
        scrollRectTrans.offsetMin = Vector2.zero; scrollRectTrans.offsetMax = Vector2.zero;

        chatScrollRect = scrollViewObj.AddComponent<ScrollRect>();
        chatScrollRect.horizontal = false; // Không cuộn ngang
        chatScrollRect.vertical = true;    // Cho phép cuộn dọc
        chatScrollRect.scrollSensitivity = 20f; // Độ nhạy lăn chuột

        // Viewport (Mặt nạ cắt chữ bị tràn ra ngoài)
        GameObject viewportObj = new GameObject("Viewport");
        viewportObj.transform.SetParent(scrollViewObj.transform, false);
        RectTransform viewportRect = viewportObj.AddComponent<RectTransform>();
        viewportRect.anchorMin = new Vector2(0, 0); viewportRect.anchorMax = new Vector2(1, 1);
        viewportRect.offsetMin = Vector2.zero; viewportRect.offsetMax = Vector2.zero;

        Image viewportBg = viewportObj.AddComponent<Image>();
        viewportBg.color = new Color(0, 0, 0, 0.4f); // Nền hơi đen trong suốt làm nổi bật chữ
        Mask mask = viewportObj.AddComponent<Mask>();
        mask.showMaskGraphic = true;

        // Content (Cục co giãn chứa text có thể dài ra vô hạn)
        GameObject contentObj = new GameObject("Content");
        contentObj.transform.SetParent(viewportObj.transform, false);
        RectTransform contentRect = contentObj.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 0); contentRect.anchorMax = new Vector2(1, 0);
        contentRect.pivot = new Vector2(0, 0); // Neo dưới đáy đẩy lên
        contentRect.offsetMin = Vector2.zero; contentRect.offsetMax = Vector2.zero;

        chatHistory = contentObj.AddComponent<Text>();
        chatHistory.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        chatHistory.fontSize = 15; // 🔥 Chữ thu nhỏ lại 
        chatHistory.color = Color.white;
        chatHistory.alignment = TextAnchor.LowerLeft;

        // Thêm Outline đen cho chữ để dễ đọc trên mọi nền cảnh vật
        Outline outline = contentObj.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(1, -1);

        // Cục Fitter phép thuật: Tự kéo dài khung khi chữ nhiều lên
        ContentSizeFitter fitter = contentObj.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        chatScrollRect.viewport = viewportRect;
        chatScrollRect.content = contentRect;

        // --- Ô GÕ CHỮ ---
        inputObj = new GameObject("ChatInput");
        inputObj.transform.SetParent(panelObj.transform, false);
        RectTransform inputRect = inputObj.AddComponent<RectTransform>();
        inputRect.anchorMin = new Vector2(0, 0); inputRect.anchorMax = new Vector2(1, 0.16f);
        inputRect.offsetMin = Vector2.zero; inputRect.offsetMax = Vector2.zero;

        Image inputBg = inputObj.AddComponent<Image>();
        inputBg.color = new Color(0, 0, 0, 0.8f);
        chatInput = inputObj.AddComponent<InputField>();

        GameObject inputTextObj = new GameObject("Text");
        inputTextObj.transform.SetParent(inputObj.transform, false);
        RectTransform textRect = inputTextObj.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0, 0); textRect.anchorMax = new Vector2(1, 1);
        textRect.offsetMin = new Vector2(10, 0); textRect.offsetMax = new Vector2(-10, 0);

        Text inputText = inputTextObj.AddComponent<Text>();
        inputText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        inputText.fontSize = 15;
        inputText.color = Color.white;
        inputText.alignment = TextAnchor.MiddleLeft;

        chatInput.textComponent = inputText;

        inputObj.SetActive(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            if (!isTyping)
            {
                // 🔥 LẦN 1: MỞ KHUNG CHAT LÊN
                isTyping = true;
                inputObj.SetActive(true);
                chatInput.Select();
                chatInput.ActivateInputField();
                chatGroup.blocksRaycasts = true;
                fadeTimer = showDuration;
            }
            else
            {
                // 🔥 ĐANG MỞ CHAT VÀ BẤM ENTER LẦN NỮA
                if (!string.IsNullOrWhiteSpace(chatInput.text))
                {
                    // 1. Nếu CÓ CHỮ: Gửi tin nhắn đi
                    onSendMessage?.Invoke(chatInput.text);

                    // 2. Xóa trắng ô gõ chữ
                    chatInput.text = "";

                    // 3. Quan trọng: Ép con trỏ chuột quay lại ô gõ để nhập tiếp luôn!
                    chatInput.Select();
                    chatInput.ActivateInputField();

                    // Reset thời gian để khung chat sáng rực rỡ
                    fadeTimer = showDuration;
                }
                else
                {
                    // 4. Nếu KHÔNG CÓ CHỮ (Trống rỗng): Đóng khung chat lại, cho phép nhân vật đi lại
                    CloseChat();
                }
            }
        }

        // Bấm ESC để hủy ngang
        if (isTyping && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseChat();
        }

        // Tự động mờ dần
        if (isTyping)
        {
            chatGroup.alpha = 1f;
            fadeTimer = showDuration; // Đang gõ thì luôn sáng
        }
        else
        {
            if (fadeTimer > 0)
            {
                fadeTimer -= Time.deltaTime;
                chatGroup.alpha = 1f;
            }
            else
            {
                // Mờ tàng hình dần
                chatGroup.alpha = Mathf.MoveTowards(chatGroup.alpha, 0f, Time.deltaTime * 2f);
            }
        }
    }

    void CloseChat()
    {
        isTyping = false;
        chatInput.text = "";
        inputObj.SetActive(false);
        chatGroup.blocksRaycasts = false; // Tắt chặn chuột để chơi game không bị vướng
        EventSystem.current.SetSelectedGameObject(null);
        fadeTimer = showDuration;
    }

    public void AddMessage(string sender, string msg)
    {
        // Chống lag nếu chat quá nhiều (Tự động xóa bớt lịch sử cũ nếu > 2000 ký tự)
        if (chatHistory.text.Length > 2000)
        {
            chatHistory.text = chatHistory.text.Substring(chatHistory.text.Length - 1000);
        }

        chatHistory.text += $"\n<color=yellow><b>[{sender}]</b></color>: {msg}";
        fadeTimer = showDuration;

        // 🔥 Tự động cuộn xuống dưới cùng mỗi khi có tin nhắn mới
        Canvas.ForceUpdateCanvases();
        if (chatScrollRect != null)
        {
            chatScrollRect.verticalNormalizedPosition = 0f;
        }
    }

    public bool IsTyping()
    {
        return isTyping;
    }
}