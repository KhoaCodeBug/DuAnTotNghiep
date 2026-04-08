using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class AutoMainMenuManager : MonoBehaviour
{
    public static AutoMainMenuManager Instance { get; private set; }

    [Header("Cài đặt chung")]
    public TMP_FontAsset gameFont;

    private Canvas mainCanvas;
    private GameObject mainPanel, newGamePanel, optionsPanel, creditsPanel;
    private CanvasGroup currentActivePanel;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Tự động xây dựng toàn bộ UI khi game bắt đầu
        GenerateEntireMenu();
    }

    private void GenerateEntireMenu()
    {
        // 1. Tạo Event System nếu chưa có
        if (FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        // 2. Tạo Canvas
        GameObject canvasGO = new GameObject("AutoMenuCanvas");
        mainCanvas = canvasGO.AddComponent<Canvas>();
        mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // 3. Tạo Background màu đen tối
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(canvasGO.transform, false);
        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0.08f, 0.08f, 0.08f, 1f); // Đen xám cực tối
        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero; bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero; bgRect.offsetMax = Vector2.zero;

        // 4. Tạo các Panel
        GenerateMainPanel(canvasGO);
        GenerateNewGamePanel(canvasGO);
        GenerateOptionsPanel(canvasGO);
        GenerateCreditsPanel(canvasGO);

        // Hiển thị Main Panel mặc định
        OpenPanel(mainPanel.GetComponent<CanvasGroup>());
    }

    #region TẠO MAIN PANEL
    private void GenerateMainPanel(GameObject canvasGO)
    {
        mainPanel = CreateBasePanel("MainPanel", canvasGO);
        CanvasGroup cg = mainPanel.AddComponent<CanvasGroup>();
        cg.alpha = 0f; cg.interactable = false; cg.blocksRaycasts = false;

        // Tiêu đề game (Góc trên bên phải mang phong cách PZ)
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

        // Container chứa nút bấm
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
        CreateMenuButton(btnContainer, "HOST / JOIN", () => Debug.Log("Mở Multiplayer..."));
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

    private void CreateMenuButton(GameObject parent, string text, UnityEngine.Events.UnityAction action, Vector2? customAnchor = null)
    {
        GameObject btnObj = new GameObject("Btn_" + text);
        btnObj.transform.SetParent(parent.transform, false);

        RectTransform rect = btnObj.AddComponent<RectTransform>();
        if (customAnchor.HasValue)
        {
            rect.anchorMin = customAnchor.Value;
            rect.anchorMax = customAnchor.Value;
            rect.pivot = new Vector2(0, 0.5f); // Canh lề trái
            rect.sizeDelta = new Vector2(300, 50);
        }
        else
        {
            rect.sizeDelta = new Vector2(0, 50); // Auto width theo VerticalLayoutGroup
        }

        // Add Button
        Image btnImg = btnObj.AddComponent<Image>();
        btnImg.color = new Color(1, 1, 1, 0); // Tàng hình background
        Button btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(action);

        // Add Text
        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(btnObj.transform, false);
        TextMeshProUGUI tmpText = txtObj.AddComponent<TextMeshProUGUI>();
        if (gameFont != null) tmpText.font = gameFont;
        tmpText.text = text;
        tmpText.fontSize = 35;
        tmpText.alignment = customAnchor.HasValue ? TextAlignmentOptions.Left : TextAlignmentOptions.Left;
        tmpText.color = new Color(0.7f, 0.7f, 0.7f, 1f); // Xám nhạt

        RectTransform txtRect = txtObj.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero; txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = Vector2.zero; txtRect.offsetMax = Vector2.zero;

        // Gắn hiệu ứng Auto Zomboid Button
        AutoMenuButtonEffect effect = btnObj.AddComponent<AutoMenuButtonEffect>();
        effect.Setup(tmpText);
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
}

// =========================================================
// HIỆU ỨNG NÚT BẤM (HOVER, CLICK) BẰNG COROUTINE THUẦN
// =========================================================
public class AutoMenuButtonEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    private TextMeshProUGUI btnText;
    private Color normalColor = new Color(0.7f, 0.7f, 0.7f, 1f); // Xám
    private Color hoverColor = new Color(0.7f, 0.15f, 0.15f, 1f); // Đỏ thẫm máu
    private Vector3 originalPos;

    private Coroutine colorRoutine;
    private Coroutine moveRoutine;

    public void Setup(TextMeshProUGUI textComponent)
    {
        btnText = textComponent;
        btnText.color = normalColor;
        originalPos = btnText.transform.localPosition;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        AnimateColor(hoverColor);
        AnimateMove(originalPos + new Vector3(15f, 0, 0)); // Dịch text sang phải 15 pixel
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        AnimateColor(normalColor);
        AnimateMove(originalPos);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (btnText != null) btnText.transform.localScale = Vector3.one * 0.9f; // Thụt nhỏ lại
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
            // Easing out curve mượt mà
            percent = percent * (2f - percent);
            btnText.transform.localPosition = Vector3.Lerp(startPos, targetPos, percent);
            yield return null;
        }
        btnText.transform.localPosition = targetPos;
    }
}