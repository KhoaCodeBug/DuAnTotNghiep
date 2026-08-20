using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Modal vehicle-condition screen for the broken arrival car. The hierarchy is
/// built at runtime so it is available for the dynamically spawned story car.
/// </summary>
[DisallowMultipleComponent]
public sealed class ArrivalCarInspectionUI : MonoBehaviour
{
    private static readonly Color Background = new Color(0.008f, 0.01f, 0.01f, 0.985f);
    private static readonly Color Panel = new Color(0.035f, 0.04f, 0.04f, 0.98f);
    private static readonly Color PanelLight = new Color(0.075f, 0.08f, 0.078f, 0.98f);
    private static readonly Color Border = new Color(0.44f, 0.46f, 0.44f, 0.82f);
    private static readonly Color Red = new Color(0.9f, 0.18f, 0.15f, 1f);
    private static readonly Color Amber = new Color(0.9f, 0.72f, 0.2f, 1f);
    private static readonly Color Green = new Color(0.35f, 0.86f, 0.42f, 1f);
    private static readonly Color Muted = new Color(0.67f, 0.69f, 0.67f, 1f);

    private readonly List<VehiclePartView> vehicleParts = new List<VehiclePartView>();
    private GameObject canvasObject;
    private GameObject overlayRoot;
    private TMP_FontAsset font;
    private BrokenArrivalCar owner;
    private bool built;
    private bool open;
    private bool waitForInteractionKeyRelease;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockMode;
    private QuestFlowUIPrototype suspendedQuestUI;
    private bool suspendedQuestUIWasEnabled;
    private RectTransform selectedPartHighlight;
    private VehiclePartPulseHighlight selectedPartPulse;
    private UIPolygonGraphic hoodDamageGraphic;
    private TextMeshProUGUI selectedPartTitle;
    private TextMeshProUGUI selectedPartDescription;
    private TextMeshProUGUI selectedPartRecommendation;
    private Image selectedPartActionBackground;
    private TextMeshProUGUI selectedPartActionText;
    private string selectedPartId;

    public static ArrivalCarInspectionUI ActiveInstance { get; private set; }
    public static bool IsAnyOpen => ActiveInstance != null && ActiveInstance.IsOpen;
    public bool IsOpen => open;

    private sealed class VehiclePartView
    {
        public string Id;
        public string Label;
        public string Category;
        public int Condition;
        public string Description;
        public string Recommendation;
        public string Action;
        public Vector2 DiagramPosition;
        public Vector2 DiagramSize;
        public Image RowBackground;
    }

    public string SelectedPartId => selectedPartId ?? string.Empty;
    public string SelectedPartActionText => selectedPartActionText != null
        ? selectedPartActionText.text
        : string.Empty;

    private void Awake()
    {
        EnsureBuilt();
    }

    private void Update()
    {
        if (!open) return;

        if (hoodDamageGraphic != null)
        {
            float pulse = (Mathf.Sin(Time.unscaledTime * 2.3f) + 1f) * 0.5f;
            hoodDamageGraphic.color = new Color(0.82f, 0.055f, 0.04f, Mathf.Lerp(0.3f, 0.5f, pulse));
        }

        if (waitForInteractionKeyRelease)
        {
            if (!Input.GetKey(KeyCode.E)) waitForInteractionKeyRelease = false;
        }
        else if (Input.GetKeyDown(KeyCode.E))
        {
            Close();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            AutoMainMenuManager.EscapeConsumedThisFrame = true;
            Close();
        }
    }

    private void OnDisable()
    {
        if (open) Close(false);
    }

    private void OnDestroy()
    {
        if (ActiveInstance == this) ActiveInstance = null;
        if (canvasObject != null) Destroy(canvasObject);
    }

    public void Open(BrokenArrivalCar target)
    {
        EnsureBuilt();
        if (open || overlayRoot == null) return;

        owner = target;
        open = true;
        ActiveInstance = this;
        waitForInteractionKeyRelease = true;
        previousCursorVisible = Cursor.visible;
        previousCursorLockMode = Cursor.lockState;

        overlayRoot.SetActive(true);
        overlayRoot.transform.SetAsLastSibling();
        suspendedQuestUI = QuestFlowUIPrototype.Instance;
        if (suspendedQuestUI != null)
        {
            suspendedQuestUIWasEnabled = suspendedQuestUI.enabled;
            suspendedQuestUI.enabled = false;
        }
        AutoUIManager.Instance?.SetQuestOverlayOpen(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SelectVehiclePart(string.IsNullOrEmpty(selectedPartId) ? "engine" : selectedPartId);
    }

    public void Close()
    {
        Close(true);
    }

    private void Close(bool notifyOwner)
    {
        if (!open) return;

        open = false;
        if (ActiveInstance == this) ActiveInstance = null;
        if (overlayRoot != null) overlayRoot.SetActive(false);
        AutoUIManager.Instance?.SetQuestOverlayOpen(false);
        if (suspendedQuestUI != null)
        {
            suspendedQuestUI.enabled = suspendedQuestUIWasEnabled;
            suspendedQuestUI = null;
        }
        Cursor.lockState = previousCursorLockMode;
        Cursor.visible = previousCursorVisible;

        BrokenArrivalCar closedOwner = owner;
        owner = null;
        if (notifyOwner) closedOwner?.NotifyInspectionUIClosed();
    }

    private void EnsureBuilt()
    {
        if (built) return;
        built = true;

        font = Resources.Load<TMP_FontAsset>("Fonts/VietnameseDynamic SDF") ?? TMP_Settings.defaultFontAsset;
        canvasObject = new GameObject("Arrival Car Inspection Canvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 760;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        overlayRoot = new GameObject("Vehicle Condition Overlay", typeof(RectTransform), typeof(Image));
        overlayRoot.transform.SetParent(canvasObject.transform, false);
        Stretch(overlayRoot.GetComponent<RectTransform>());
        Image shade = overlayRoot.GetComponent<Image>();
        shade.color = new Color(0f, 0f, 0f, 0.82f);
        shade.raycastTarget = true;

        RectTransform shell = Box("Vehicle Condition Window", overlayRoot.transform, new Vector2(0.5f, 0.5f),
            new Vector2(1420f, 820f), Vector2.zero, Background);
        AddBorder(shell, new Color(0.7f, 0.72f, 0.7f, 0.9f), 2f);
        Box("Header Rule", shell, new Vector2(0.5f, 1f), new Vector2(1420f, 3f), new Vector2(0f, -86f), Border);
        Text(shell, "Header Eyebrow", "KIỂM TRA PHƯƠNG TIỆN  //  XE DÂN DỤNG", 13f, Muted,
            FontStyles.Bold, TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(700f, 26f),
            new Vector2(36f, -26f));
        Text(shell, "Header Title", "TÌNH TRẠNG XE", 30f, Color.white, FontStyles.Bold,
            TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(760f, 44f), new Vector2(36f, -52f));

        RectTransform close = Box("Close Button", shell, new Vector2(1f, 1f), new Vector2(52f, 52f),
            new Vector2(-26f, -26f), new Color(0.12f, 0.125f, 0.12f, 1f));
        AddBorder(close, Border);
        Text(close, "Close Text", "×", 31f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center,
            new Vector2(0.5f, 0.5f), new Vector2(44f, 44f), Vector2.zero);
        MakeClickable(close, Close);

        EnsureVehiclePartDefinitions();
        BuildVehicleDiagram(shell);
        BuildConditionPanel(shell);
        SelectVehiclePart("engine");

        Text(shell, "Footer Hint", "[E] ĐÓNG     •     [ESC] ĐÓNG     •     HOẶC BẤM  ×", 13f, Muted,
            FontStyles.Bold, TextAlignmentOptions.Center, new Vector2(0.5f, 0f), new Vector2(760f, 28f),
            new Vector2(0f, 22f));
        overlayRoot.SetActive(false);
    }

    private void BuildVehicleDiagram(Transform shell)
    {
        RectTransform panel = Box("Vehicle Diagram Panel", shell, new Vector2(0f, 0.5f),
            new Vector2(520f, 650f), new Vector2(34f, -18f), new Color(0.012f, 0.014f, 0.014f, 1f));
        AddBorder(panel, Border);
        Text(panel, "Diagram Label", "CHỌN BỘ PHẬN ĐỂ KIỂM TRA", 12f, Muted, FontStyles.Bold,
            TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(330f, 24f), new Vector2(20f, -18f));

        Texture2D blueprint = LoadTexture("Story/CarUI/VehicleBlueprint");
        GameObject imageObject = new GameObject("4 Door Vehicle Blueprint", typeof(RectTransform), typeof(RawImage));
        imageObject.transform.SetParent(panel, false);
        RectTransform imageRect = imageObject.GetComponent<RectTransform>();
        SetRect(imageRect, new Vector2(0.5f, 0.5f), new Vector2(263f, 600f), new Vector2(0f, -9f));
        RawImage image = imageObject.GetComponent<RawImage>();
        image.texture = blueprint;
        image.color = blueprint != null ? Color.white : new Color(0.3f, 0.33f, 0.31f, 1f);
        image.raycastTarget = false;

        GameObject hoodDamage = new GameObject("Damaged Hood Polygon", typeof(RectTransform),
            typeof(UIPolygonGraphic));
        hoodDamage.transform.SetParent(panel, false);
        RectTransform hoodDamageRect = hoodDamage.GetComponent<RectTransform>();
        SetRect(hoodDamageRect, new Vector2(0.5f, 0.5f), new Vector2(263f, 600f), new Vector2(0f, -9f));
        hoodDamageGraphic = hoodDamage.GetComponent<UIPolygonGraphic>();
        hoodDamageGraphic.raycastTarget = false;
        hoodDamageGraphic.color = new Color(0.82f, 0.055f, 0.04f, 0.4f);
        hoodDamageGraphic.SetNormalizedPoints(
            new Vector2(0.266f, 0.78f), new Vector2(0.726f, 0.777f),
            new Vector2(0.768f, 0.632f), new Vector2(0.658f, 0.545f),
            new Vector2(0.312f, 0.545f), new Vector2(0.232f, 0.635f));

        GameObject selection = new GameObject("Selected Vehicle Part", typeof(RectTransform),
            typeof(VehiclePartPulseHighlight));
        selection.transform.SetParent(imageRect, false);
        selectedPartHighlight = selection.GetComponent<RectTransform>();
        SetRect(selectedPartHighlight, new Vector2(0.5f, 0.5f), new Vector2(91f, 61f),
            new Vector2(46f, 221.5f));
        selectedPartPulse = selection.GetComponent<VehiclePartPulseHighlight>();

        for (int i = 0; i < vehicleParts.Count; i++)
        {
            VehiclePartView capturedPart = vehicleParts[i];
            RectTransform hotspot = Box("Vehicle Part Hotspot " + capturedPart.Id, imageRect,
                new Vector2(0.5f, 0.5f), capturedPart.DiagramSize, capturedPart.DiagramPosition, Color.clear);
            MakeClickable(hotspot, () => SelectVehiclePart(capturedPart.Id));
        }

        Text(panel, "Diagram Hint", "BẤM VÀO BIỂU TƯỢNG HOẶC BỘ PHẬN TRÊN XE", 10f, Muted,
            FontStyles.Bold, TextAlignmentOptions.Center, new Vector2(0.5f, 0f),
            new Vector2(430f, 22f), new Vector2(0f, 10f));
    }

    private void BuildConditionPanel(Transform shell)
    {
        RectTransform panel = Box("Vehicle Status Panel", shell, new Vector2(1f, 1f),
            new Vector2(800f, 650f), new Vector2(-34f, -112f), Panel);
        AddBorder(panel, Border);
        Text(panel, "Vehicle Name", "CHEVALIER NYALA", 21f, Color.white, FontStyles.Bold,
            TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(360f, 32f), new Vector2(22f, -18f));
        Text(panel, "Overall Condition", "TÌNH TRẠNG TỔNG THỂ: 52%", 13f, Amber, FontStyles.Bold,
            TextAlignmentOptions.Right, new Vector2(1f, 1f), new Vector2(300f, 26f), new Vector2(-22f, -23f));
        Box("Status Rule", panel, new Vector2(0.5f, 1f), new Vector2(756f, 1f),
            new Vector2(0f, -62f), Border);

        BuildPartGroup(panel, "KHOANG ĐỘNG CƠ", new[] { "engine", "battery", "exhaust" }, 22f, -82f);
        BuildPartGroup(panel, "NHIÊN LIỆU", new[] { "fuel" }, 22f, -218f);
        BuildPartGroup(panel, "BÁNH XE", new[] { "front_left", "rear_left", "front_right", "rear_right" },
            412f, -82f);
        BuildPartGroup(panel, "THÂN XE", new[] { "hood", "windshield", "front_door" }, 412f, -250f);

        RectTransform detail = Box("Selected Part Detail", panel, new Vector2(0f, 0f),
            new Vector2(756f, 150f), new Vector2(22f, 18f), new Color(0.055f, 0.06f, 0.058f, 1f));
        AddBorder(detail, new Color(0.38f, 0.4f, 0.39f, 0.9f));
        selectedPartTitle = Text(detail, "Selected Part Title", string.Empty, 15f, Color.white,
            FontStyles.Bold, TextAlignmentOptions.Left, new Vector2(0f, 1f),
            new Vector2(700f, 28f), new Vector2(18f, -14f));
        Box("Detail Rule", detail, new Vector2(0.5f, 1f), new Vector2(720f, 1f),
            new Vector2(0f, -45f), new Color(0.25f, 0.27f, 0.26f, 1f));
        selectedPartDescription = Text(detail, "Selected Part Description", string.Empty, 13f, Color.white,
            FontStyles.Normal, TextAlignmentOptions.TopLeft, new Vector2(0f, 1f),
            new Vector2(710f, 42f), new Vector2(18f, -57f));
        selectedPartRecommendation = Text(detail, "Selected Part Recommendation", string.Empty, 11f, Muted,
            FontStyles.Normal, TextAlignmentOptions.Left, new Vector2(0f, 0f),
            new Vector2(525f, 28f), new Vector2(18f, 15f));

        RectTransform action = Box("Selected Part Action Button", detail, new Vector2(1f, 0f),
            new Vector2(165f, 42f), new Vector2(-16f, 16f), new Color(0.18f, 0.19f, 0.185f, 1f));
        AddBorder(action, new Color(0.55f, 0.57f, 0.56f, 0.95f));
        selectedPartActionBackground = action.GetComponent<Image>();
        selectedPartActionText = Text(action, "Selected Part Action Text", string.Empty, 12f, Color.white,
            FontStyles.Bold, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f),
            new Vector2(153f, 32f), Vector2.zero);
        MakeClickable(action, InvokeSelectedPartAction);
    }

    private void BuildPartGroup(Transform parent, string title, string[] partIds, float x, float y)
    {
        Text(parent, "Group " + title, title, 13f, Color.white, FontStyles.Bold,
            TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(320f, 22f), new Vector2(x, y));
        for (int i = 0; i < partIds.Length; i++)
        {
            VehiclePartView part = FindPart(partIds[i]);
            if (part == null) continue;
            RectTransform row = Box("Part Row " + part.Id, parent, new Vector2(0f, 1f),
                new Vector2(366f, 30f), new Vector2(x, y - 28f - i * 32f), Color.clear);
            part.RowBackground = row.GetComponent<Image>();
            Text(row, "Part Name", part.Label, 13f, Color.white, FontStyles.Normal,
                TextAlignmentOptions.Left, new Vector2(0f, 0.5f), new Vector2(270f, 24f), new Vector2(10f, 0f));
            Color stateColor = GetConditionColor(part.Condition);
            Text(row, "Part Condition", part.Condition + "%", 13f, stateColor, FontStyles.Bold,
                TextAlignmentOptions.Right, new Vector2(1f, 0.5f), new Vector2(70f, 24f), new Vector2(-10f, 0f));
            VehiclePartView capturedPart = part;
            MakeClickable(row, () => SelectVehiclePart(capturedPart.Id));
        }
    }

    private void SelectVehiclePart(string partId)
    {
        VehiclePartView part = FindPart(partId);
        if (part == null) return;
        selectedPartId = part.Id;

        for (int i = 0; i < vehicleParts.Count; i++)
        {
            if (vehicleParts[i].RowBackground != null)
                vehicleParts[i].RowBackground.color = vehicleParts[i] == part
                    ? new Color(0.26f, 0.27f, 0.26f, 0.95f)
                    : Color.clear;
        }

        if (selectedPartHighlight != null)
        {
            SetRect(selectedPartHighlight, new Vector2(0.5f, 0.5f), part.DiagramSize, part.DiagramPosition);
            selectedPartHighlight.SetAsLastSibling();
            selectedPartPulse?.Configure(GetConditionColor(part.Condition), 2f);
        }

        Color conditionColor = GetConditionColor(part.Condition);
        if (selectedPartTitle != null)
        {
            selectedPartTitle.text = part.Label.ToUpperInvariant() + "  (" + part.Condition + "%)";
            selectedPartTitle.color = conditionColor;
            selectedPartDescription.text = part.Description;
            selectedPartRecommendation.text = "CHẨN ĐOÁN: " + part.Recommendation + "  •  VẬT PHẨM THEO DÕI TRONG [J]";
            selectedPartActionText.text = part.Action.ToUpperInvariant();
            selectedPartActionBackground.color = part.Action == "Kiểm tra"
                ? new Color(0.18f, 0.19f, 0.185f, 1f)
                : new Color(0.28f, 0.16f, 0.075f, 1f);
        }
    }

    private void InvokeSelectedPartAction()
    {
        VehiclePartView part = FindPart(selectedPartId);
        if (part == null || selectedPartRecommendation == null) return;

        if (part.Action == "Kiểm tra")
        {
            selectedPartRecommendation.text = "KẾT QUẢ KIỂM TRA: " + part.Recommendation;
            AutoChatManager.Instance?.AddMessage("KIỂM TRA XE", part.Label + ": " + part.Recommendation);
            return;
        }

        string message = part.Action + " " + part.Label.ToLowerInvariant() +
                         " cần vật phẩm phù hợp. Mở nhật ký [J] để xem trạng thái đã có/đang thiếu.";
        selectedPartRecommendation.text = "CHƯA THỂ " + part.Action.ToUpperInvariant() +
                                          "  •  KIỂM TRA VẬT PHẨM TRONG [J]";
        AutoChatManager.Instance?.AddMessage("CƠ KHÍ PHƯƠNG TIỆN", message);
    }

    private void EnsureVehiclePartDefinitions()
    {
        if (vehicleParts.Count > 0) return;
        AddPart("engine", "Động cơ", "KHOANG ĐỘNG CƠ", 18,
            "Động cơ bị quá nhiệt và bộ đề đang kẹt.",
            "Làm nguội động cơ, kiểm tra bộ đề và hệ thống đánh lửa.", "Sửa chữa",
            new Vector2(46f, 221.5f), new Vector2(91f, 61f));
        AddPart("battery", "Ắc quy", "KHOANG ĐỘNG CƠ", 82,
            "Ắc quy vẫn còn điện và các đầu cực chưa bị ăn mòn nặng.",
            "Có thể tiếp tục sử dụng; thay thế chỉ là nâng cấp tùy chọn.", "Thay linh kiện",
            new Vector2(-45f, 218f), new Vector2(46f, 36f));
        AddPart("exhaust", "Ống xả", "KHOANG ĐỘNG CƠ", 76,
            "Ống xả còn nguyên, chưa phát hiện rò khí nghiêm trọng.",
            "Chưa cần can thiệp ngay.", "Kiểm tra", new Vector2(66f, -223f), new Vector2(46f, 72f));
        AddPart("fuel", "Bình xăng", "NHIÊN LIỆU", 4,
            "Bình gần như cạn và không còn nhiên liệu dự phòng trên xe.",
            "Bổ sung nhiên liệu trước khi thử khởi động.", "Đổ nhiên liệu",
            new Vector2(-48f, -225f), new Vector2(92f, 62f));
        AddPart("front_left", "Lốp trước trái", "BÁNH XE", 46,
            "Lốp trước trái đã mòn rõ và áp suất thấp.",
            "Vẫn có thể di chuyển chậm; nên thay nếu tìm được lốp tốt.", "Thay linh kiện",
            new Vector2(-100f, 87f), new Vector2(54f, 76f));
        AddPart("rear_left", "Lốp sau trái", "BÁNH XE", 73,
            "Lốp sau trái còn sử dụng được.",
            "Theo dõi áp suất sau khi xe hoạt động.", "Kiểm tra",
            new Vector2(-100f, -106f), new Vector2(54f, 76f));
        AddPart("front_right", "Lốp trước phải", "BÁNH XE", 61,
            "Lốp trước phải có dấu hiệu chai bề mặt.",
            "Có thể sử dụng tạm thời, tránh tăng tốc gấp.", "Thay linh kiện",
            new Vector2(100f, 87f), new Vector2(54f, 76f));
        AddPart("rear_right", "Lốp sau phải", "BÁNH XE", 69,
            "Lốp sau phải mòn không đều nhưng chưa thủng.",
            "Có thể tiếp tục sử dụng trong quãng đường ngắn.", "Thay linh kiện",
            new Vector2(100f, -106f), new Vector2(54f, 76f));
        AddPart("hood", "Nắp capo", "THÂN XE", 23,
            "Nắp capo biến dạng do nhiệt và đang che khuất điểm kẹt của bộ đề.",
            "Mở nắp và xử lý cơ cấu khóa trước khi sửa động cơ.", "Sửa chữa",
            new Vector2(0f, 119f), new Vector2(150f, 112f));
        AddPart("windshield", "Kính chắn gió", "THÂN XE", 57,
            "Kính chắn gió có nhiều vết xước nhưng chưa vỡ.",
            "Tầm nhìn vẫn chấp nhận được trong điều kiện sáng.", "Kiểm tra",
            new Vector2(0f, 48f), new Vector2(120f, 58f));
        AddPart("front_door", "Cửa trước", "THÂN XE", 89,
            "Cửa trước, bản lề và khóa vẫn hoạt động bình thường.",
            "Không cần sửa chữa.", "Kiểm tra", new Vector2(0f, -30f), new Vector2(122f, 118f));
    }

    private void AddPart(string id, string label, string category, int condition, string description,
        string recommendation, string action, Vector2 diagramPosition, Vector2 diagramSize)
    {
        vehicleParts.Add(new VehiclePartView
        {
            Id = id,
            Label = label,
            Category = category,
            Condition = condition,
            Description = description,
            Recommendation = recommendation,
            Action = action,
            DiagramPosition = diagramPosition,
            DiagramSize = diagramSize
        });
    }

    private VehiclePartView FindPart(string partId)
    {
        for (int i = 0; i < vehicleParts.Count; i++)
            if (vehicleParts[i].Id == partId) return vehicleParts[i];
        return null;
    }

    private static Color GetConditionColor(int condition)
    {
        if (condition <= 30) return Red;
        if (condition < 70) return Amber;
        return Green;
    }

    private static Texture2D LoadTexture(string path)
    {
        Texture2D texture = Resources.Load<Texture2D>(path);
        if (texture == null)
        {
            Debug.LogWarning("[ARRIVAL CAR UI] Missing texture at Resources/" + path + ".");
            return null;
        }

        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        return texture;
    }

    private RectTransform Box(string name, Transform parent, Vector2 anchor, Vector2 size, Vector2 position,
        Color color)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        gameObject.transform.SetParent(parent, false);
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        SetRect(rect, anchor, size, position);
        Image image = gameObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    private TextMeshProUGUI Text(Transform parent, string name, string value, float size, Color color,
        FontStyles style, TextAlignmentOptions alignment, Vector2 anchor, Vector2 dimensions, Vector2 position)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        gameObject.transform.SetParent(parent, false);
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        SetRect(rect, anchor, dimensions, position);
        TextMeshProUGUI text = gameObject.GetComponent<TextMeshProUGUI>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }

    private static Button MakeClickable(RectTransform target, Action action)
    {
        Image image = target.GetComponent<Image>();
        image.raycastTarget = true;
        Button button = target.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.82f);
        colors.pressedColor = new Color(0.65f, 0.65f, 0.65f, 1f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        button.onClick.AddListener(() => action?.Invoke());
        return button;
    }

    private static void AddBorder(RectTransform target, Color color, float distance = 1f)
    {
        Outline outline = target.gameObject.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = new Vector2(distance, -distance);
        outline.useGraphicAlpha = true;
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
