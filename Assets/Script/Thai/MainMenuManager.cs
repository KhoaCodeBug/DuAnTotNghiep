using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using System.Threading.Tasks;

public class AutoMainMenuManager : MonoBehaviour
{
    public static AutoMainMenuManager Instance { get; private set; }

    [Header("Cài đặt chung")]
    public TMP_FontAsset gameFont;
    public Sprite backgroundImage;

    [Header("Cài đặt Fusion Network")]
    public NetworkRunner runnerPrefab;
    public int mainSceneIndex = 1;

    private Canvas mainCanvas;
    private GameObject mainPanel, newGamePanel, multiplayerPanel, optionsPanel, creditsPanel;
    private CanvasGroup currentActivePanel;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        GenerateEntireMenu();
    }

    private void GenerateEntireMenu()
    {
        if (FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        GameObject canvasGO = new GameObject("AutoMenuCanvas");
        mainCanvas = canvasGO.AddComponent<Canvas>();
        mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(canvasGO.transform, false);
        Image bgImg = bgObj.AddComponent<Image>();

        if (backgroundImage != null)
        {
            bgImg.sprite = backgroundImage;
            bgImg.color = Color.white;
        }
        else
        {
            bgImg.color = new Color(0.08f, 0.08f, 0.08f, 1f);
        }

        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero; bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero; bgRect.offsetMax = Vector2.zero;

        GenerateMainPanel(canvasGO);
        GenerateNewGamePanel(canvasGO);
        GenerateMultiplayerPanel(canvasGO);
        GenerateOptionsPanel(canvasGO);
        GenerateCreditsPanel(canvasGO);

        OpenPanel(mainPanel.GetComponent<CanvasGroup>());
    }

    #region TẠO MAIN PANEL
    private void GenerateMainPanel(GameObject canvasGO)
    {
        mainPanel = CreateBasePanel("MainPanel", canvasGO);
        CanvasGroup cg = mainPanel.AddComponent<CanvasGroup>();
        cg.alpha = 0f; cg.interactable = false; cg.blocksRaycasts = false;

        GameObject titleObj = new GameObject("TitleText");
        titleObj.transform.SetParent(mainPanel.transform, false);
        TextMeshProUGUI titleTxt = titleObj.AddComponent<TextMeshProUGUI>();
        if (gameFont != null) titleTxt.font = gameFont;
        titleTxt.text = "FRAGMENTS\nOF SURVIVAL";
        titleTxt.fontSize = 80;
        titleTxt.fontStyle = FontStyles.Bold;
        titleTxt.alignment = TextAlignmentOptions.TopLeft;
        titleTxt.color = new Color(0.9f, 0.9f, 0.9f, 0.8f);

        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.1f, 0.7f); titleRect.anchorMax = new Vector2(0.5f, 0.95f);
        titleRect.offsetMin = Vector2.zero; titleRect.offsetMax = Vector2.zero;

        GameObject btnContainer = new GameObject("ButtonContainer");
        btnContainer.transform.SetParent(mainPanel.transform, false);
        RectTransform btnRect = btnContainer.AddComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.1f, 0.1f); btnRect.anchorMax = new Vector2(0.3f, 0.6f);
        btnRect.offsetMin = Vector2.zero; btnRect.offsetMax = Vector2.zero;

        VerticalLayoutGroup vlg = btnContainer.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 15;
        vlg.childAlignment = TextAnchor.MiddleLeft;
        vlg.childControlHeight = false; vlg.childControlWidth = true;

        CreateMenuButton(btnContainer, "SOLO", () => OpenPanel(newGamePanel.GetComponent<CanvasGroup>()));
        CreateMenuButton(btnContainer, "HOST / JOIN", () => OpenPanel(multiplayerPanel.GetComponent<CanvasGroup>()));
        CreateMenuButton(btnContainer, "OPTIONS", () => OpenPanel(optionsPanel.GetComponent<CanvasGroup>()));
        CreateMenuButton(btnContainer, "CREDITS", () => OpenPanel(creditsPanel.GetComponent<CanvasGroup>()));
        CreateMenuButton(btnContainer, "QUIT", () => Application.Quit());
    }
    #endregion

    #region TẠO SUB PANELS
    private void GenerateNewGamePanel(GameObject canvasGO)
    {
        newGamePanel = CreateBasePanel("NewGamePanel", canvasGO);
        CanvasGroup cg = newGamePanel.AddComponent<CanvasGroup>();
        cg.alpha = 0f; cg.interactable = false; cg.blocksRaycasts = false;

        CreateTitleText(newGamePanel, "SELECT DIFFICULTY");

        GameObject btnContainer = new GameObject("DiffContainer");
        btnContainer.transform.SetParent(newGamePanel.transform, false);
        RectTransform btnRect = btnContainer.AddComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.4f, 0.3f); btnRect.anchorMax = new Vector2(0.6f, 0.7f);
        btnRect.offsetMin = Vector2.zero; btnRect.offsetMax = Vector2.zero;

        VerticalLayoutGroup vlg = btnContainer.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 20;

        CreateMenuButton(btnContainer, "BUILDER (EASY)", () => Debug.Log("Start Easy"));
        CreateMenuButton(btnContainer, "SURVIVOR (NORMAL)", () => Debug.Log("Start Normal"));
        CreateMenuButton(btnContainer, "APOCALYPSE (HARD)", () => Debug.Log("Start Hard"));

        CreateMenuButton(newGamePanel, "BACK", () => OpenPanel(mainPanel.GetComponent<CanvasGroup>()), new Vector2(0.1f, 0.1f));
    }

    // ==========================================
    // 🔥 BẢNG MULTIPLAYER (ĐÃ CẬP NHẬT JOIN AREA)
    // ==========================================
    private void GenerateMultiplayerPanel(GameObject canvasGO)
    {
        multiplayerPanel = CreateBasePanel("MultiplayerPanel", canvasGO);
        CanvasGroup cg = multiplayerPanel.AddComponent<CanvasGroup>();
        cg.alpha = 0f; cg.interactable = false; cg.blocksRaycasts = false;

        CreateTitleText(multiplayerPanel, "MULTIPLAYER");

        // --- KHU VỰC HOST (Bên Trái) ---
        GameObject hostArea = new GameObject("HostArea");
        hostArea.transform.SetParent(multiplayerPanel.transform, false);
        RectTransform hostRect = hostArea.AddComponent<RectTransform>();
        hostRect.anchorMin = new Vector2(0.15f, 0.3f); hostRect.anchorMax = new Vector2(0.45f, 0.7f);
        hostRect.offsetMin = Vector2.zero; hostRect.offsetMax = Vector2.zero;

        Image hostBg = hostArea.AddComponent<Image>();
        hostBg.color = new Color(0.12f, 0.12f, 0.12f, 0.8f);

        CreateSubTitleText(hostArea, "CREATE SERVER");

        GameObject roomInputObj = CreateInputField(hostArea, "RoomNameInput", "Enter Room Name...", new Vector2(0.1f, 0.5f), new Vector2(0.9f, 0.7f));
        TMP_InputField roomInput = roomInputObj.GetComponent<TMP_InputField>();

        CreateMenuButton(hostArea, "HOST GAME", () => {
            string roomName = roomInput.text;
            if (string.IsNullOrEmpty(roomName)) roomName = "ZombieServer_" + Random.Range(100, 999);
            StartHostGame(roomName);
        }, new Vector2(0.5f, 0.2f), true);


        // --- KHU VỰC JOIN (Bên Phải) ---
        GameObject joinArea = new GameObject("JoinArea");
        joinArea.transform.SetParent(multiplayerPanel.transform, false);
        RectTransform joinRect = joinArea.AddComponent<RectTransform>();
        joinRect.anchorMin = new Vector2(0.55f, 0.3f); joinRect.anchorMax = new Vector2(0.85f, 0.7f);
        joinRect.offsetMin = Vector2.zero; joinRect.offsetMax = Vector2.zero;

        Image joinBg = joinArea.AddComponent<Image>();
        joinBg.color = new Color(0.12f, 0.12f, 0.12f, 0.8f);

        CreateSubTitleText(joinArea, "JOIN SERVER");

        // 🔥 THÊM Ô NHẬP TÊN PHÒNG CHO KHÁCH
        GameObject joinInputObj = CreateInputField(joinArea, "JoinNameInput", "Enter Room to Join...", new Vector2(0.1f, 0.5f), new Vector2(0.9f, 0.7f));
        TMP_InputField joinInput = joinInputObj.GetComponent<TMP_InputField>();

        CreateMenuButton(joinArea, "JOIN GAME", () => {
            string roomName = joinInput.text;
            if (!string.IsNullOrEmpty(roomName))
            {
                StartClientGame(roomName);
            }
            else
            {
                Debug.LogWarning("Chưa nhập tên phòng để Join!");
            }
        }, new Vector2(0.5f, 0.2f), true);

        // --- NÚT TRỞ VỀ ---
        CreateMenuButton(multiplayerPanel, "BACK", () => OpenPanel(mainPanel.GetComponent<CanvasGroup>()), new Vector2(0.1f, 0.1f));
    }
    // ==========================================

    private void GenerateOptionsPanel(GameObject canvasGO)
    {
        optionsPanel = CreateBasePanel("OptionsPanel", canvasGO);
        CanvasGroup cg = optionsPanel.AddComponent<CanvasGroup>();
        cg.alpha = 0f; cg.interactable = false; cg.blocksRaycasts = false;

        CreateTitleText(optionsPanel, "OPTIONS");
        CreateMenuButton(optionsPanel, "BACK", () => OpenPanel(mainPanel.GetComponent<CanvasGroup>()), new Vector2(0.1f, 0.1f));
    }

    private void GenerateCreditsPanel(GameObject canvasGO)
    {
        creditsPanel = CreateBasePanel("CreditsPanel", canvasGO);
        CanvasGroup cg = creditsPanel.AddComponent<CanvasGroup>();
        cg.alpha = 0f; cg.interactable = false; cg.blocksRaycasts = false;

        CreateTitleText(creditsPanel, "CREDITS");
        CreateMenuButton(creditsPanel, "BACK", () => OpenPanel(mainPanel.GetComponent<CanvasGroup>()), new Vector2(0.1f, 0.1f));
    }
    #endregion

    #region HÀM HỖ TRỢ (HELPERS)
    private GameObject CreateBasePanel(string name, GameObject parent)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent.transform, false);
        RectTransform rect = panel.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        return panel;
    }

    private void CreateTitleText(GameObject parent, string text)
    {
        GameObject txtObj = new GameObject("Title");
        txtObj.transform.SetParent(parent.transform, false);
        TextMeshProUGUI txt = txtObj.AddComponent<TextMeshProUGUI>();
        if (gameFont != null) txt.font = gameFont;
        txt.text = text;
        txt.fontSize = 60;
        txt.fontStyle = FontStyles.Bold;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = new Color(0.8f, 0.8f, 0.8f, 1f);

        RectTransform rect = txtObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 0.8f); rect.anchorMax = new Vector2(1, 0.95f);
        rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
    }

    private void CreateSubTitleText(GameObject parent, string text)
    {
        GameObject txtObj = new GameObject("SubTitle");
        txtObj.transform.SetParent(parent.transform, false);
        TextMeshProUGUI txt = txtObj.AddComponent<TextMeshProUGUI>();
        if (gameFont != null) txt.font = gameFont;
        txt.text = text;
        txt.fontSize = 40;
        txt.fontStyle = FontStyles.Bold;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = new Color(0.7f, 0.7f, 0.7f, 1f);

        RectTransform rect = txtObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 0.8f); rect.anchorMax = new Vector2(1, 0.95f);
        rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
    }

    private GameObject CreateInputField(GameObject parent, string name, string placeholderTxt, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject inputObj = new GameObject(name);
        inputObj.transform.SetParent(parent.transform, false);
        RectTransform rect = inputObj.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin; rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;

        Image bg = inputObj.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.05f, 0.05f, 1f);

        TMP_InputField inputField = inputObj.AddComponent<TMP_InputField>();
        inputField.targetGraphic = bg;

        GameObject viewportObj = new GameObject("Viewport");
        viewportObj.transform.SetParent(inputObj.transform, false);
        RectTransform viewportRect = viewportObj.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero; viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(15, 0); viewportRect.offsetMax = new Vector2(-15, 0);
        viewportObj.AddComponent<RectMask2D>();

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(viewportObj.transform, false);
        TextMeshProUGUI txt = textObj.AddComponent<TextMeshProUGUI>();
        if (gameFont != null) txt.font = gameFont;
        txt.fontSize = 30; txt.color = Color.white; txt.alignment = TextAlignmentOptions.Left;
        RectTransform txtRect = textObj.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero; txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = Vector2.zero; txtRect.offsetMax = Vector2.zero;

        GameObject placeholderObj = new GameObject("Placeholder");
        placeholderObj.transform.SetParent(viewportObj.transform, false);
        TextMeshProUGUI pTxt = placeholderObj.AddComponent<TextMeshProUGUI>();
        if (gameFont != null) pTxt.font = gameFont;
        pTxt.fontSize = 30; pTxt.color = Color.gray; pTxt.alignment = TextAlignmentOptions.Left;
        pTxt.text = placeholderTxt;
        RectTransform pRect = placeholderObj.GetComponent<RectTransform>();
        pRect.anchorMin = Vector2.zero; pRect.anchorMax = Vector2.one;
        pRect.offsetMin = Vector2.zero; pRect.offsetMax = Vector2.zero;

        inputField.textViewport = viewportRect;
        inputField.textComponent = txt;
        inputField.placeholder = pTxt;

        return inputObj;
    }

    private void CreateMenuButton(GameObject parent, string text, UnityEngine.Events.UnityAction action, Vector2? customAnchor = null, bool isCenter = false)
    {
        GameObject btnObj = new GameObject("Btn_" + text);
        btnObj.transform.SetParent(parent.transform, false);

        RectTransform rect = btnObj.AddComponent<RectTransform>();
        if (customAnchor.HasValue)
        {
            rect.anchorMin = customAnchor.Value;
            rect.anchorMax = customAnchor.Value;
            rect.pivot = isCenter ? new Vector2(0.5f, 0.5f) : new Vector2(0, 0.5f);
            rect.sizeDelta = new Vector2(300, 50);
        }
        else
        {
            rect.sizeDelta = new Vector2(0, 50);
        }

        Image btnImg = btnObj.AddComponent<Image>();
        btnImg.color = new Color(1, 1, 1, 0);
        Button btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(action);

        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(btnObj.transform, false);
        TextMeshProUGUI tmpText = txtObj.AddComponent<TextMeshProUGUI>();
        if (gameFont != null) tmpText.font = gameFont;
        tmpText.text = text;
        tmpText.fontSize = 35;
        tmpText.alignment = isCenter ? TextAlignmentOptions.Center : TextAlignmentOptions.Left;
        tmpText.color = new Color(0.7f, 0.7f, 0.7f, 1f);

        RectTransform txtRect = txtObj.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero; txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = Vector2.zero; txtRect.offsetMax = Vector2.zero;

        AutoMenuButtonEffect effect = btnObj.AddComponent<AutoMenuButtonEffect>();
        effect.Setup(tmpText, isCenter);
    }
    #endregion

    #region QUẢN LÝ CHUYỂN TRANG
    private void OpenPanel(CanvasGroup targetPanel)
    {
        if (currentActivePanel == targetPanel) return;

        if (currentActivePanel != null)
        {
            StartCoroutine(FadePanel(currentActivePanel, 0f, false));
        }

        currentActivePanel = targetPanel;
        StartCoroutine(FadePanel(currentActivePanel, 1f, true));
    }

    private IEnumerator FadePanel(CanvasGroup panel, float targetAlpha, bool show)
    {
        if (show)
        {
            panel.gameObject.SetActive(true);
            panel.blocksRaycasts = true;
            panel.interactable = true;
        }
        else
        {
            panel.blocksRaycasts = false;
            panel.interactable = false;
        }

        float startAlpha = panel.alpha;
        float time = 0f;
        float duration = 0.25f;

        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            panel.alpha = Mathf.Lerp(startAlpha, targetAlpha, time / duration);
            yield return null;
        }

        panel.alpha = targetAlpha;

        if (!show)
        {
            panel.gameObject.SetActive(false);
        }
    }
    #endregion

    #region FUSION MULTIPLAYER LOGIC
    private async void StartHostGame(string roomName)
    {
        Debug.Log("Đang khởi động Server: " + roomName);

        NetworkRunner runner = Instantiate(runnerPrefab);
        runner.gameObject.AddComponent<NetworkSceneManagerDefault>();

        var result = await runner.StartGame(new StartGameArgs()
        {
            GameMode = GameMode.Host,
            SessionName = roomName,
            SceneManager = runner.GetComponent<NetworkSceneManagerDefault>()
        });

        if (result.Ok)
        {
            Debug.Log("Tạo phòng thành công! Đang chuyển map...");
            gameObject.SetActive(false);

            // 🔥 FIX CHO FUSION 2: Dùng LoadScene thay vì SetActiveScene
            await runner.LoadScene(SceneRef.FromIndex(mainSceneIndex));
        }
        else
        {
            Debug.LogError("Lỗi tạo phòng: " + result.ShutdownReason);
        }
    }

    private async void StartClientGame(string roomName)
    {
        Debug.Log("Đang tìm và vào phòng: " + roomName);

        NetworkRunner runner = Instantiate(runnerPrefab);
        runner.gameObject.AddComponent<NetworkSceneManagerDefault>();

        var result = await runner.StartGame(new StartGameArgs()
        {
            GameMode = GameMode.Client,
            SessionName = roomName,
            SceneManager = runner.GetComponent<NetworkSceneManagerDefault>()
        });

        if (result.Ok)
        {
            Debug.Log("Vào phòng thành công!");
            gameObject.SetActive(false);
        }
        else
        {
            Debug.LogError("Không tìm thấy phòng hoặc lỗi: " + result.ShutdownReason);
        }
    }
    #endregion
}

public class AutoMenuButtonEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    private TextMeshProUGUI btnText;
    private Color normalColor = new Color(0.7f, 0.7f, 0.7f, 1f);
    private Color hoverColor = new Color(0.7f, 0.15f, 0.15f, 1f);
    private Vector3 originalPos;
    private bool isCentered;

    private Coroutine colorRoutine;
    private Coroutine moveRoutine;

    public void Setup(TextMeshProUGUI textComponent, bool center)
    {
        btnText = textComponent;
        btnText.color = normalColor;
        originalPos = btnText.transform.localPosition;
        isCentered = center;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        AnimateColor(hoverColor);
        if (!isCentered) AnimateMove(originalPos + new Vector3(15f, 0, 0));
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        AnimateColor(normalColor);
        if (!isCentered) AnimateMove(originalPos);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (btnText != null) btnText.transform.localScale = Vector3.one * 0.9f;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (btnText != null) btnText.transform.localScale = Vector3.one;
    }

    private void AnimateColor(Color target)
    {
        if (btnText == null) return;
        if (colorRoutine != null) StopCoroutine(colorRoutine);
        colorRoutine = StartCoroutine(DoColor(target, 0.15f));
    }

    private void AnimateMove(Vector3 target)
    {
        if (btnText == null) return;
        if (moveRoutine != null) StopCoroutine(moveRoutine);
        moveRoutine = StartCoroutine(DoMove(target, 0.15f));
    }

    private IEnumerator DoColor(Color targetColor, float duration)
    {
        Color startColor = btnText.color;
        float t = 0;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            btnText.color = Color.Lerp(startColor, targetColor, t / duration);
            yield return null;
        }
        btnText.color = targetColor;
    }

    private IEnumerator DoMove(Vector3 targetPos, float duration)
    {
        Vector3 startPos = btnText.transform.localPosition;
        float t = 0;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float percent = t / duration;
            percent = percent * (2f - percent);
            btnText.transform.localPosition = Vector3.Lerp(startPos, targetPos, percent);
            yield return null;
        }
        btnText.transform.localPosition = targetPos;
    }
}