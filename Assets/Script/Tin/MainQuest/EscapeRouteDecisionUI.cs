using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Introduces both escape routes without locking either one, then provides the
/// explicit point-of-no-return confirmation used by both finales.
/// </summary>
public sealed class EscapeRouteDecisionUI : MonoBehaviour
{
    private static EscapeRouteDecisionUI instance;

    private Canvas canvas;
    private GameObject introductionRoot;
    private GameObject confirmationRoot;
    private TMP_Text confirmationEyebrow;
    private TMP_Text confirmationTitle;
    private TMP_Text confirmationBody;
    private TMP_Text confirmationButtonText;
    private Action pendingConfirmation;

    public static bool IsVisible => instance != null && instance.canvas != null &&
                                    instance.canvas.enabled && instance.gameObject.activeSelf;

    public static void ShowInitialChoice()
    {
        EscapeRouteDecisionUI ui = EnsureInstance();
        ui.ShowIntroduction();
    }

    public static void ShowFinaleConfirmation(EscapeEndingRoute route, Action onConfirmed)
    {
        if (!EscapeEndingRules.IsValidPlayableRoute(route)) return;
        EscapeRouteDecisionUI ui = EnsureInstance();
        ui.ShowConfirmation(route, onConfirmed);
    }

    public static void CloseIfOpen()
    {
        if (instance != null) instance.Hide();
    }

    private static EscapeRouteDecisionUI EnsureInstance()
    {
        if (instance != null) return instance;
        GameObject host = new GameObject("Escape Route Decision UI");
        instance = host.AddComponent<EscapeRouteDecisionUI>();
        instance.Build();
        return instance;
    }

    private void Awake()
    {
        if (instance == null) instance = this;
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }
        if (canvas == null) Build();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Update()
    {
        if (!IsVisible) return;
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.X))
        {
            Hide();
            return;
        }

        if (!introductionRoot.activeSelf) return;
        if (Input.GetKeyDown(KeyCode.Alpha1)) TrackRoute(EscapeEndingRoute.CivilianCar);
        else if (Input.GetKeyDown(KeyCode.Alpha2)) TrackRoute(EscapeEndingRoute.MilitaryEvacuation);
    }

    private void Build()
    {
        if (canvas != null) return;
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 4400;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        BuildIntroduction();
        BuildConfirmation();
        canvas.enabled = false;
    }

    private void BuildIntroduction()
    {
        introductionRoot = CreateRoot("Escape Route Introduction");
        RectTransform panel = CreatePanel(introductionRoot.transform, new Vector2(1260f, 700f));
        CreateText(panel, "Introduction Eyebrow", "SAU TÍN HIỆU RADIO  //  HAI HƯỚNG THOÁT",
            14f, new Color(1f, 0.67f, 0.14f), FontStyles.Bold,
            new Vector2(0f, 1f), new Vector2(1120f, 28f), new Vector2(54f, -42f), TextAlignmentOptions.Left);
        CreateText(panel, "Introduction Title", "BẠN SẼ CHUẨN BỊ THEO HƯỚNG NÀO?",
            31f, Color.white, FontStyles.Bold, new Vector2(0f, 1f),
            new Vector2(1120f, 48f), new Vector2(54f, -78f), TextAlignmentOptions.Left);
        CreateText(panel, "Introduction Body",
            "Chiếc xe vẫn có thể sửa. Đồng thời radio vừa bắt được dấu vết về tuyến sơ tán quân sự. " +
            "Chọn tuyến muốn ưu tiên theo dõi. Cả hai tuyến vẫn tiến triển song song cho tới điểm không thể quay lại.",
            16f, new Color(0.72f, 0.78f, 0.75f), FontStyles.Normal,
            new Vector2(0f, 1f), new Vector2(1120f, 62f), new Vector2(54f, -137f),
            TextAlignmentOptions.TopLeft);

        RectTransform civilian = CreateRouteCard(panel, "Civilian Route Card", new Vector2(-292f, -35f),
            new Color(0.12f, 0.19f, 0.16f, 1f), "TUYẾN A  //  TỰ TÌM ĐƯỜNG THOÁT",
            "KHÔI PHỤC CHIẾC XE",
            "Tìm dụng cụ, nhiên liệu và linh kiện. Sửa chiếc xe, khám phá lối ra dân sự và vượt vòng phong tỏa.",
            "TỰ DO KHÁM PHÁ  •  PHỤ THUỘC PHƯƠNG TIỆN",
            "[1]  THEO DÕI TUYẾN CHIẾC XE");
        civilian.GetComponent<Button>().onClick.AddListener(() => TrackRoute(EscapeEndingRoute.CivilianCar));

        RectTransform military = CreateRouteCard(panel, "Military Route Card", new Vector2(292f, -35f),
            new Color(0.16f, 0.13f, 0.19f, 1f), "TUYẾN B  //  TUYẾN CỐT TRUYỆN",
            "LẦN THEO TÍN HIỆU QUÂN SỰ",
            "Thu thập tài liệu sơ tán, tìm Văn phòng Điều phối và lần theo bản đồ tới căn cứ quân sự.",
            "CỐT TRUYỆN DÀI  •  NGUY HIỂM CAO  •  MỞ CĂN CỨ QUÂN SỰ",
            "[2]  THEO DÕI TUYẾN QUÂN SỰ");
        military.GetComponent<Button>().onClick.AddListener(() => TrackRoute(EscapeEndingRoute.MilitaryEvacuation));

        CreateText(panel, "Tracking Does Not Lock Ending",
            "CHƯA KHÓA ENDING  •  Có thể đổi tuyến theo dõi trong Nhật ký trước điểm không thể quay lại.",
            12f, new Color(0.72f, 0.78f, 0.75f), FontStyles.Bold,
            new Vector2(0.5f, 0f), new Vector2(760f, 24f), new Vector2(0f, 78f), TextAlignmentOptions.Center);

        Button later = CreateButton(panel, "Choose Later", "[X]  CHỌN SAU",
            new Vector2(0.5f, 0f), new Vector2(360f, 46f), new Vector2(0f, 28f),
            new Color(0.08f, 0.09f, 0.085f, 1f));
        later.onClick.AddListener(Hide);
    }

    private void BuildConfirmation()
    {
        confirmationRoot = CreateRoot("Escape Route Finale Confirmation");
        RectTransform panel = CreatePanel(confirmationRoot.transform, new Vector2(760f, 470f));
        confirmationEyebrow = CreateText(panel, "Finale Eyebrow", string.Empty, 13f,
            new Color(1f, 0.67f, 0.14f), FontStyles.Bold, new Vector2(0f, 1f),
            new Vector2(650f, 26f), new Vector2(52f, -42f), TextAlignmentOptions.Left);
        confirmationTitle = CreateText(panel, "Finale Title", string.Empty, 30f, Color.white,
            FontStyles.Bold, new Vector2(0f, 1f), new Vector2(650f, 72f),
            new Vector2(52f, -80f), TextAlignmentOptions.TopLeft);
        confirmationBody = CreateText(panel, "Finale Body", string.Empty, 16f,
            new Color(0.76f, 0.81f, 0.79f), FontStyles.Normal, new Vector2(0f, 1f),
            new Vector2(650f, 130f), new Vector2(52f, -170f), TextAlignmentOptions.TopLeft);

        Button confirm = CreateButton(panel, "Confirm Finale", string.Empty, new Vector2(1f, 0f),
            new Vector2(300f, 58f), new Vector2(-52f, 42f), new Color(0.7f, 0.22f, 0.12f, 1f));
        confirmationButtonText = confirm.GetComponentInChildren<TextMeshProUGUI>();
        confirm.onClick.AddListener(ConfirmFinale);
        Button cancel = CreateButton(panel, "Cancel Finale", "QUAY LẠI", new Vector2(0f, 0f),
            new Vector2(220f, 58f), new Vector2(52f, 42f), new Color(0.11f, 0.13f, 0.12f, 1f));
        cancel.onClick.AddListener(Hide);
        confirmationRoot.SetActive(false);
    }

    private void ShowIntroduction()
    {
        PrepareForModal();
        pendingConfirmation = null;
        confirmationRoot.SetActive(false);
        introductionRoot.SetActive(true);
        canvas.enabled = true;
    }

    private void ShowConfirmation(EscapeEndingRoute route, Action onConfirmed)
    {
        PrepareForModal();
        pendingConfirmation = onConfirmed;
        introductionRoot.SetActive(false);
        confirmationRoot.SetActive(true);
        confirmationEyebrow.text = "ĐIỂM KHÔNG THỂ QUAY LẠI  //  " + EscapeEndingRules.GetDisplayName(route);
        confirmationTitle.text = route == EscapeEndingRoute.CivilianCar
            ? "BẮT ĐẦU VƯỢT VÒNG PHONG TỎA?"
            : "KÍCH HOẠT KẾ HOẠCH SƠ TÁN?";
        confirmationBody.text = route == EscapeEndingRoute.CivilianCar
            ? "Bạn sắp dùng chiếc xe dân sự để bắt đầu cuộc thoát hiểm cuối cùng. " +
              "Xác nhận sẽ khóa tuyến căn cứ quân sự cho toàn đội."
            : "Bạn sắp kích hoạt báo động và cuộc phòng thủ tại căn cứ. " +
              "Xác nhận sẽ khóa tuyến thoát bằng chiếc xe dân sự cho toàn đội.";
        confirmationButtonText.text = route == EscapeEndingRoute.CivilianCar
            ? "XÁC NHẬN TUYẾN A"
            : "XÁC NHẬN TUYẾN B";
        canvas.enabled = true;
    }

    private void TrackRoute(EscapeEndingRoute route)
    {
        QuestFlowUIPrototype.Instance?.SetTrackedEscapeRoute(route);
        AutoChatManager.Instance?.AddMessage("THEO DÕI",
            EscapeEndingRules.GetDisplayName(route) + " — chưa khóa ending; có thể đổi trong Nhật ký.");
        Hide();
    }

    private void ConfirmFinale()
    {
        Action callback = pendingConfirmation;
        Hide();
        callback?.Invoke();
    }

    private void PrepareForModal()
    {
        QuestFlowUIPrototype.Instance?.CloseAllQuestOverlays();
        AutoUIManager.Instance?.SetQuestOverlayOpen(true);
    }

    private void Hide()
    {
        pendingConfirmation = null;
        if (canvas != null) canvas.enabled = false;
        if (introductionRoot != null) introductionRoot.SetActive(false);
        if (confirmationRoot != null) confirmationRoot.SetActive(false);
        AutoUIManager.Instance?.SetQuestOverlayOpen(false);
    }

    private GameObject CreateRoot(string name)
    {
        GameObject root = new GameObject(name, typeof(RectTransform), typeof(Image));
        root.transform.SetParent(transform, false);
        RectTransform rect = root.GetComponent<RectTransform>();
        Stretch(rect);
        root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.84f);
        return root;
    }

    private static RectTransform CreatePanel(Transform parent, Vector2 size)
    {
        GameObject panel = new GameObject("Decision Panel", typeof(RectTransform), typeof(Image), typeof(Outline));
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        SetRect(rect, new Vector2(0.5f, 0.5f), size, Vector2.zero);
        panel.GetComponent<Image>().color = new Color(0.025f, 0.04f, 0.037f, 0.99f);
        panel.GetComponent<Outline>().effectColor = new Color(0.35f, 0.42f, 0.39f, 0.95f);
        return rect;
    }

    private static RectTransform CreateRouteCard(Transform parent, string name, Vector2 position, Color color,
        string eyebrow, string title, string body, string profile, string action)
    {
        GameObject card = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
        card.transform.SetParent(parent, false);
        RectTransform rect = card.GetComponent<RectTransform>();
        SetRect(rect, new Vector2(0.5f, 0.5f), new Vector2(535f, 330f), position);
        card.GetComponent<Image>().color = color;
        card.GetComponent<Outline>().effectColor = new Color(0.35f, 0.48f, 0.42f, 0.9f);
        CreateText(rect, "Route Eyebrow", eyebrow, 12f, new Color(1f, 0.7f, 0.22f), FontStyles.Bold,
            new Vector2(0f, 1f), new Vector2(465f, 24f), new Vector2(30f, -28f), TextAlignmentOptions.Left);
        CreateText(rect, "Route Title", title, 23f, Color.white, FontStyles.Bold,
            new Vector2(0f, 1f), new Vector2(465f, 58f), new Vector2(30f, -62f), TextAlignmentOptions.TopLeft);
        CreateText(rect, "Route Body", body, 15f, new Color(0.76f, 0.82f, 0.79f), FontStyles.Normal,
            new Vector2(0f, 1f), new Vector2(465f, 82f), new Vector2(30f, -132f), TextAlignmentOptions.TopLeft);
        CreateText(rect, "Route Profile", profile, 11f, new Color(1f, 0.7f, 0.22f), FontStyles.Bold,
            new Vector2(0f, 1f), new Vector2(465f, 44f), new Vector2(30f, -226f), TextAlignmentOptions.TopLeft);
        CreateText(rect, "Route Action", action, 14f, Color.white, FontStyles.Bold,
            new Vector2(0.5f, 0f), new Vector2(455f, 44f), new Vector2(0f, 30f), TextAlignmentOptions.Center);
        return rect;
    }

    private static Button CreateButton(Transform parent, string name, string label, Vector2 anchor,
        Vector2 size, Vector2 position, Color color)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        SetRect(rect, anchor, size, position);
        buttonObject.GetComponent<Image>().color = color;
        buttonObject.GetComponent<Outline>().effectColor = new Color(0.45f, 0.52f, 0.49f, 0.85f);
        CreateText(rect, "Button Label", label, 14f, Color.white, FontStyles.Bold,
            new Vector2(0.5f, 0.5f), size - new Vector2(14f, 10f), Vector2.zero, TextAlignmentOptions.Center);
        return buttonObject.GetComponent<Button>();
    }

    private static TextMeshProUGUI CreateText(Transform parent, string name, string value, float size,
        Color color, FontStyles style, Vector2 anchor, Vector2 dimensions, Vector2 position,
        TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        SetRect(rect, anchor, dimensions, position);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = GameLocalization.GetRuntimeFont(TMP_Settings.defaultFontAsset);
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.fontStyle = style;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    private static void SetRect(RectTransform rect, Vector2 anchor, Vector2 size, Vector2 position)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
