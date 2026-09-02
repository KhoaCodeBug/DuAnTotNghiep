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
    private RoadsideVehicleRepairStation policeOwner;
    private bool policeMode;
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
    private TextMeshProUGUI overallConditionText;
    private TextMeshProUGUI headerEyebrowText;
    private TextMeshProUGUI headerTitleText;
    private TextMeshProUGUI vehicleNameText;
    private TextMeshProUGUI footerHintText;
    private TextMeshProUGUI diagramLabelText;
    private TextMeshProUGUI diagramHintText;
    private TextMeshProUGUI repairClockLabelText;
    private TextMeshProUGUI repairClockCancelText;
    private sealed class GroupHeaderView
    {
        public TextMeshProUGUI TextComponent;
        public string Key;
    }
    private readonly List<GroupHeaderView> groupHeaderViews = new List<GroupHeaderView>();
    private Image selectedPartActionBackground;
    private TextMeshProUGUI selectedPartActionText;
    private Button selectedPartActionButton;
    private Image startEngineBackground;
    private TextMeshProUGUI startEngineText;
    private Button startEngineButton;
    private GameObject repairClockRoot;
    private RectTransform repairClockHand;
    private Texture2D repairClockRingTexture;
    private string selectedPartId;
    private int lastDisplayedRepairMask = int.MinValue;
    private bool lastDisplayedVehicleStarted;
    private AudioSource hoodAudioSource;
    private AudioClip hoodOpenClip;
    private AudioClip hoodCloseClip;
    [SerializeField, Range(0f, 1.5f)] private float hoodOpenVolumeMultiplier = 1.25f;
    [SerializeField, Range(0f, 1.5f)] private float hoodCloseVolumeMultiplier = 0.5f;
    private AudioSource repairAudioSource;
    private AudioClip fuelRepairClip;
    private AudioClip tireRepairClip;
    private AudioClip hammerRepairClip;
    private bool localRepairPending;
    private bool localRepairActive;
    private string localRepairPartId;
    private float localRepairStartedAt;
    private float localRepairDuration;

    public static ArrivalCarInspectionUI ActiveInstance { get; private set; }
    public static bool IsAnyOpen => ActiveInstance != null && ActiveInstance.IsOpen;
    public bool IsOpen => open;

    private sealed class VehiclePartView
    {
        public string Id;
        public string NameKey;
        public string CategoryKey;
        public int Condition;
        public string DescKey;
        public string RecKey;
        public string ActionKey;
        public string Label => GameLocalization.Get(NameKey);
        public string Category => GameLocalization.Get(CategoryKey);
        public string Description => GameLocalization.Get(DescKey);
        public string Recommendation => GameLocalization.Get(RecKey);
        public string Action => GameLocalization.Get(ActionKey);
        public Vector2 DiagramPosition;
        public Vector2 DiagramSize;
        public Image RowBackground;
        public TextMeshProUGUI NameText;
        public TextMeshProUGUI ConditionText;
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

        MainQuestManager manager = MainQuestManager.Instance;
        MilitaryBaseQuestManager policeManager = MilitaryBaseQuestManager.Instance;
        int repairMask = policeMode
            ? policeManager != null && policeManager.IsNetworkReady ? policeManager.PoliceCarRepairMask : 0
            : manager != null && manager.IsNetworkReady ? manager.ArrivalCarRepairMask : 0;
        bool vehicleStarted = policeMode
            ? policeManager != null && policeManager.ArePoliceCarRepairsComplete
            : manager != null && manager.IsNetworkReady && manager.IsArrivalCarRepaired;
        if (repairMask != lastDisplayedRepairMask || vehicleStarted != lastDisplayedVehicleStarted)
        {
            lastDisplayedRepairMask = repairMask;
            lastDisplayedVehicleStarted = vehicleStarted;
            SelectVehiclePart(string.IsNullOrEmpty(selectedPartId) ? "engine" : selectedPartId);
        }

        if (hoodDamageGraphic != null)
        {
            float pulse = (Mathf.Sin(Time.unscaledTime * 2.3f) + 1f) * 0.5f;
            hoodDamageGraphic.color = new Color(0.82f, 0.055f, 0.04f, Mathf.Lerp(0.3f, 0.5f, pulse));
        }

        if (localRepairActive) UpdateRepairClock();

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

    private void OnEnable()
    {
        GameLocalization.LanguageChanged += OnLanguageChanged;
    }

    private void OnDisable()
    {
        GameLocalization.LanguageChanged -= OnLanguageChanged;
        if (open) Close(false);
    }

    private void OnLanguageChanged()
    {
        if (!built) return;
        RefreshStaticLabels();
        RefreshGroupAndRowLabels();
        if (open)
        {
            SelectVehiclePart(string.IsNullOrEmpty(selectedPartId) ? "engine" : selectedPartId);
        }
    }

    private void RefreshStaticLabels()
    {
        if (headerEyebrowText != null)
            headerEyebrowText.text = GameLocalization.Get(policeMode ? "arrival_ui.header_eyebrow_police" : "arrival_ui.header_eyebrow_civilian");
        if (headerTitleText != null)
            headerTitleText.text = GameLocalization.Get("arrival_ui.header_title");
        if (vehicleNameText != null)
            vehicleNameText.text = GameLocalization.Get(policeMode ? "arrival_ui.vehicle_police" : "arrival_ui.vehicle_civilian");
        if (footerHintText != null)
            footerHintText.text = GameLocalization.Get("arrival_ui.footer_hint");
        if (diagramLabelText != null)
            diagramLabelText.text = GameLocalization.Get("arrival_ui.diagram_label");
        if (diagramHintText != null)
            diagramHintText.text = GameLocalization.Get("arrival_ui.diagram_hint");
        if (repairClockLabelText != null)
            repairClockLabelText.text = GameLocalization.Get("arrival_ui.repair_clock_label");
        if (repairClockCancelText != null)
            repairClockCancelText.text = GameLocalization.Get("arrival_ui.repair_clock_cancel");
    }

    private void RefreshGroupAndRowLabels()
    {
        for (int i = 0; i < groupHeaderViews.Count; i++)
        {
            if (groupHeaderViews[i].TextComponent != null)
                groupHeaderViews[i].TextComponent.text = GameLocalization.Get(groupHeaderViews[i].Key);
        }
        for (int i = 0; i < vehicleParts.Count; i++)
        {
            if (vehicleParts[i].NameText != null)
                vehicleParts[i].NameText.text = vehicleParts[i].Label;
        }
    }

    private void OnDestroy()
    {
        EndLocalRepairPresentation();
        if (ActiveInstance == this) ActiveInstance = null;
        if (canvasObject != null) Destroy(canvasObject);
        if (repairClockRingTexture != null) Destroy(repairClockRingTexture);
    }

    public void Open(BrokenArrivalCar target)
    {
        policeMode = false;
        owner = target;
        policeOwner = null;
        OpenShared();
    }

    public void Open(RoadsideVehicleRepairStation target)
    {
        policeMode = true;
        policeOwner = target;
        owner = null;
        OpenShared();
    }

    private void OpenShared()
    {
        EnsureBuilt();
        if (open || overlayRoot == null) return;
        open = true;
        lastDisplayedRepairMask = int.MinValue;
        lastDisplayedVehicleStarted = false;
        ActiveInstance = this;
        waitForInteractionKeyRelease = true;
        if (!policeMode) PlayHoodAudio(true);
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
        RefreshStaticLabels();
        RefreshGroupAndRowLabels();
        SelectVehiclePart(string.IsNullOrEmpty(selectedPartId) ? "engine" : selectedPartId);
    }

    public void Close()
    {
        Close(true);
    }

    private void Close(bool notifyOwner)
    {
        if (!open) return;

        bool wasCivilianInspection = !policeMode && owner != null;
        if (localRepairActive)
        {
            if (policeMode) MilitaryBaseQuestManager.Instance?.RequestCancelRepairSkillCheck();
            else MainQuestManager.Instance?.RequestCancelArrivalCarRepair();
            EndLocalRepairPresentation();
        }
        else
        {
            localRepairPending = false;
        }
        open = false;
        if (wasCivilianInspection) PlayHoodAudio(false);
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
        RoadsideVehicleRepairStation closedPoliceOwner = policeOwner;
        owner = null;
        policeOwner = null;
        if (notifyOwner)
        {
            closedOwner?.NotifyInspectionUIClosed();
            closedPoliceOwner?.NotifyInspectionUIClosed();
        }
    }

    private void PlayHoodAudio(bool opening)
    {
        if (!isActiveAndEnabled) return;
        EnsureHoodAudio();
        AudioClip clip = opening ? hoodOpenClip : hoodCloseClip;
        if (hoodAudioSource == null || clip == null) return;
        hoodAudioSource.Stop();
        float clipMultiplier = opening ? hoodOpenVolumeMultiplier : hoodCloseVolumeMultiplier;
        float volumeScale = Mathf.Clamp(
            PlayerPrefs.GetFloat("GameSFXVolume", 0.8f) * clipMultiplier, 0f, 1.5f);
        hoodAudioSource.PlayOneShot(clip, volumeScale);
    }

    private void EnsureHoodAudio()
    {
        if (hoodAudioSource != null) return;
        hoodOpenClip = Resources.Load<AudioClip>("Intro/VehicleAudio/CarHoodOpen");
        hoodCloseClip = Resources.Load<AudioClip>("Intro/VehicleAudio/CarHoodClose");
        GameObject audioObject = new GameObject("Arrival Car Hood Audio");
        audioObject.transform.SetParent(transform, false);
        hoodAudioSource = audioObject.AddComponent<AudioSource>();
        GameplayAudioSpatializer.Configure(hoodAudioSource, GameplayAudioSpatializer.Profile.Body);
        hoodAudioSource.playOnAwake = false;
        hoodAudioSource.loop = false;
        hoodAudioSource.volume = 1f;
    }

    private void EnsureBuilt()
    {
        if (built) return;
        built = true;

        font = GameLocalization.GetRuntimeFont();
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
        Box("Header Rule", shell, new Vector2(0.5f, 1f), new Vector2(1420f, 3f), new Vector2(0f, -96f), Border);
        headerEyebrowText = Text(shell, "Header Eyebrow", GameLocalization.Get("arrival_ui.header_eyebrow_civilian"), 13f, Muted,
            FontStyles.Bold, TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(700f, 22f),
            new Vector2(36f, -18f));
        headerTitleText = Text(shell, "Header Title", GameLocalization.Get("arrival_ui.header_title"), 28f, Color.white, FontStyles.Bold,
            TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(760f, 38f), new Vector2(36f, -45f));

        RectTransform close = Box("Close Button", shell, new Vector2(1f, 1f), new Vector2(52f, 52f),
            new Vector2(-26f, -26f), new Color(0.12f, 0.125f, 0.12f, 1f));
        AddBorder(close, Border);
        Text(close, "Close Text", "×", 31f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center,
            new Vector2(0.5f, 0.5f), new Vector2(44f, 44f), Vector2.zero);
        MakeClickable(close, Close);

        RectTransform start = Box("Start Engine Button", shell, new Vector2(1f, 1f),
            new Vector2(220f, 44f), new Vector2(-92f, -50f), new Color(0.12f, 0.13f, 0.125f, 1f));
        AddBorder(start, new Color(0.42f, 0.45f, 0.43f, 0.95f));
        startEngineBackground = start.GetComponent<Image>();
        startEngineText = Text(start, "Start Engine Text", GameLocalization.Get("arrival_ui.cannot_start_btn"), 11f, Muted,
            FontStyles.Bold, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f),
            new Vector2(204f, 32f), Vector2.zero);
        startEngineButton = MakeClickable(start, InvokeStartEngine);

        EnsureVehiclePartDefinitions();
        BuildVehicleDiagram(shell);
        BuildConditionPanel(shell);
        BuildRepairClock(shell);
        SelectVehiclePart("engine");

        footerHintText = Text(shell, "Footer Hint", GameLocalization.Get("arrival_ui.footer_hint"), 13f, Muted,
            FontStyles.Bold, TextAlignmentOptions.Center, new Vector2(0.5f, 0f), new Vector2(760f, 28f),
            new Vector2(0f, 22f));
        overlayRoot.SetActive(false);
    }

    private void BuildVehicleDiagram(Transform shell)
    {
        RectTransform panel = Box("Vehicle Diagram Panel", shell, new Vector2(0f, 0.5f),
            new Vector2(520f, 650f), new Vector2(34f, -18f), new Color(0.012f, 0.014f, 0.014f, 1f));
        AddBorder(panel, Border);
        diagramLabelText = Text(panel, "Diagram Label", GameLocalization.Get("arrival_ui.diagram_label"), 12f, Muted, FontStyles.Bold,
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
        SetRect(selectedPartHighlight, new Vector2(0.5f, 0.5f), new Vector2(103f, 63f),
            new Vector2(47.09247f, 222.6873f));
        selectedPartPulse = selection.GetComponent<VehiclePartPulseHighlight>();

        for (int i = 0; i < vehicleParts.Count; i++)
        {
            VehiclePartView capturedPart = vehicleParts[i];
            RectTransform hotspot = Box("Vehicle Part Hotspot " + capturedPart.Id, imageRect,
                new Vector2(0.5f, 0.5f), capturedPart.DiagramSize, capturedPart.DiagramPosition, Color.clear);
            MakeClickable(hotspot, () => SelectVehiclePart(capturedPart.Id));
        }

        diagramHintText = Text(panel, "Diagram Hint", GameLocalization.Get("arrival_ui.diagram_hint"), 10f, Muted,
            FontStyles.Bold, TextAlignmentOptions.Center, new Vector2(0.5f, 0f),
            new Vector2(430f, 22f), new Vector2(0f, 10f));
    }

    private void BuildConditionPanel(Transform shell)
    {
        RectTransform panel = Box("Vehicle Status Panel", shell, new Vector2(1f, 1f),
            new Vector2(800f, 650f), new Vector2(-34f, -112f), Panel);
        AddBorder(panel, Border);
        vehicleNameText = Text(panel, "Vehicle Name", GameLocalization.Get("arrival_ui.vehicle_civilian"), 21f, Color.white, FontStyles.Bold,
            TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(360f, 32f), new Vector2(22f, -18f));
        overallConditionText = Text(panel, "Overall Condition", string.Format(GameLocalization.Get("arrival_ui.overall_condition"), 39), 13f, Amber, FontStyles.Bold,
            TextAlignmentOptions.Right, new Vector2(1f, 1f), new Vector2(300f, 26f), new Vector2(-22f, -23f));
        Box("Status Rule", panel, new Vector2(0.5f, 1f), new Vector2(756f, 1f),
            new Vector2(0f, -62f), Border);

        groupHeaderViews.Clear();
        BuildPartGroup(panel, "arrival_ui.group_engine", new[] { "engine", "battery", "exhaust" }, 22f, -82f);
        BuildPartGroup(panel, "arrival_ui.group_fuel", new[] { "fuel" }, 22f, -218f);
        BuildPartGroup(panel, "arrival_ui.group_wheels", new[] { "front_left", "rear_left", "front_right", "rear_right" },
            412f, -82f);
        BuildPartGroup(panel, "arrival_ui.group_body", new[] { "hood", "windshield", "front_door" }, 412f, -250f);

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
        selectedPartActionButton = MakeClickable(action, InvokeSelectedPartAction);
    }

    private void BuildRepairClock(Transform shell)
    {
        repairClockRoot = new GameObject("Repair Clock Overlay", typeof(RectTransform), typeof(Image));
        repairClockRoot.transform.SetParent(shell, false);
        RectTransform overlay = repairClockRoot.GetComponent<RectTransform>();
        Stretch(overlay);
        Image blocker = repairClockRoot.GetComponent<Image>();
        blocker.color = new Color(0f, 0f, 0f, 0.68f);
        blocker.raycastTarget = true;

        RectTransform panel = Box("Repair Clock Panel", repairClockRoot.transform,
            new Vector2(0.5f, 0.5f), new Vector2(250f, 250f), Vector2.zero,
            new Color(0.025f, 0.03f, 0.028f, 0.98f));
        AddBorder(panel, new Color(0.32f, 0.9f, 0.47f, 0.9f), 2f);
        repairClockLabelText = Text(panel, "Repair Clock Label", GameLocalization.Get("arrival_ui.repair_clock_label"), 15f, Color.white, FontStyles.Bold,
            TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(200f, 28f),
            new Vector2(0f, -20f));

        repairClockRingTexture = CreateRepairClockRingTexture(128);
        GameObject ringObject = new GameObject("Repair Clock Ring", typeof(RectTransform), typeof(RawImage));
        ringObject.transform.SetParent(panel, false);
        RectTransform ring = ringObject.GetComponent<RectTransform>();
        SetRect(ring, new Vector2(0.5f, 0.5f), new Vector2(142f, 142f), new Vector2(0f, -8f));
        RawImage ringImage = ringObject.GetComponent<RawImage>();
        ringImage.texture = repairClockRingTexture;
        ringImage.color = Color.white;
        ringImage.raycastTarget = false;

        RectTransform hand = Box("Repair Clock Hand", ring, new Vector2(0.5f, 0.5f),
            new Vector2(5f, 57f), Vector2.zero, new Color(0.26f, 1f, 0.42f, 1f));
        hand.pivot = new Vector2(0.5f, 0f);
        hand.anchoredPosition = Vector2.zero;
        repairClockHand = hand;

        Box("Repair Clock Center", ring, new Vector2(0.5f, 0.5f),
            new Vector2(12f, 12f), Vector2.zero, new Color(0.9f, 1f, 0.92f, 1f));
        repairClockCancelText = Text(panel, "Repair Clock Cancel Hint", GameLocalization.Get("arrival_ui.repair_clock_cancel"), 11f, Muted, FontStyles.Bold,
            TextAlignmentOptions.Center, new Vector2(0.5f, 0f), new Vector2(200f, 24f),
            new Vector2(0f, 15f));

        repairClockRoot.SetActive(false);
    }

    private static Texture2D CreateRepairClockRingTexture(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "ARRIVAL_CAR_REPAIR_CLOCK_RING",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };
        Color32[] pixels = new Color32[size * size];
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float outerRadius = size * 0.47f;
        float innerRadius = size * 0.41f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            Vector2 delta = new Vector2(x, y) - center;
            float radius = delta.magnitude;
            if (radius < innerRadius || radius > outerRadius)
            {
                pixels[y * size + x] = new Color32(0, 0, 0, 0);
                continue;
            }

            float angle = Mathf.Repeat(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, 360f);
            float nearestQuarter = Mathf.Abs(Mathf.DeltaAngle(angle, Mathf.Round(angle / 90f) * 90f));
            bool cardinalTick = nearestQuarter <= 2.5f && radius >= size * 0.38f;
            pixels[y * size + x] = cardinalTick
                ? new Color32(88, 255, 128, 255)
                : new Color32(178, 190, 181, 230);
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private void BuildPartGroup(Transform parent, string titleKey, string[] partIds, float x, float y)
    {
        TextMeshProUGUI header = Text(parent, "Group " + titleKey, GameLocalization.Get(titleKey), 13f, Color.white, FontStyles.Bold,
            TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(320f, 22f), new Vector2(x, y));
        groupHeaderViews.Add(new GroupHeaderView { TextComponent = header, Key = titleKey });
        for (int i = 0; i < partIds.Length; i++)
        {
            VehiclePartView part = FindPart(partIds[i]);
            if (part == null) continue;
            RectTransform row = Box("Part Row " + part.Id, parent, new Vector2(0f, 1f),
                new Vector2(366f, 30f), new Vector2(x, y - 28f - i * 32f), Color.clear);
            part.RowBackground = row.GetComponent<Image>();
            part.NameText = Text(row, "Part Name", part.Label, 13f, Color.white, FontStyles.Normal,
                TextAlignmentOptions.Left, new Vector2(0f, 0.5f), new Vector2(270f, 24f), new Vector2(10f, 0f));
            Color stateColor = GetConditionColor(part.Condition);
            part.ConditionText = Text(row, "Part Condition", part.Condition + "%", 13f, stateColor, FontStyles.Bold,
                TextAlignmentOptions.Right, new Vector2(1f, 0.5f), new Vector2(70f, 24f), new Vector2(-10f, 0f));
            VehiclePartView capturedPart = part;
            MakeClickable(row, () => SelectVehiclePart(capturedPart.Id));
        }
    }

    private void SelectVehiclePart(string partId)
    {
        if (localRepairPending && partId != selectedPartId) return;
        if (localRepairActive && partId != localRepairPartId) return;
        VehiclePartView part = FindPart(partId);
        if (part == null) return;
        selectedPartId = part.Id;

        MainQuestManager manager = MainQuestManager.Instance;
        int totalCondition = 0;
        for (int i = 0; i < vehicleParts.Count; i++)
        {
            VehiclePartView current = vehicleParts[i];
            bool currentApplied = IsPartApplied(current.Id);
            int displayedCondition = currentApplied ? 100 : current.Condition;
            totalCondition += displayedCondition;
            if (current.ConditionText != null)
            {
                current.ConditionText.text = displayedCondition + "%";
                current.ConditionText.color = GetConditionColor(displayedCondition);
            }
            if (current.RowBackground != null)
                current.RowBackground.color = current == part
                    ? new Color(0.26f, 0.27f, 0.26f, 0.95f)
                    : Color.clear;
        }

        if (overallConditionText != null && vehicleParts.Count > 0)
        {
            int overall = Mathf.RoundToInt(totalCondition / (float)vehicleParts.Count);
            overallConditionText.text = string.Format(GameLocalization.Get("arrival_ui.overall_condition"), overall);
            overallConditionText.color = GetConditionColor(overall);
        }

        if (selectedPartHighlight != null)
        {
            SetRect(selectedPartHighlight, new Vector2(0.5f, 0.5f),
                part.DiagramSize, part.DiagramPosition);
            selectedPartHighlight.SetAsLastSibling();
            selectedPartPulse?.Configure(GetConditionColor(part.Condition), 2f);
        }

        Color conditionColor = GetConditionColor(part.Condition);
        bool actionApplied = IsPartApplied(part.Id);
        bool repairingSelectedPart = localRepairActive && part.Id == localRepairPartId;
        if (selectedPartTitle != null)
        {
            int displayedCondition = actionApplied ? 100 : part.Condition;
            selectedPartTitle.text = part.Label.ToUpperInvariant() + "  (" + displayedCondition + "%)";
            selectedPartTitle.color = actionApplied ? Green : conditionColor;
            selectedPartDescription.text = part.Description;
            selectedPartRecommendation.text = repairingSelectedPart
                ? GameLocalization.Get("arrival_ui.repairing_progress")
                : actionApplied
                ? GameLocalization.Get("arrival_ui.status_completed_server")
                : BuildRecommendation(part);
            selectedPartActionText.text = repairingSelectedPart
                ? GameLocalization.Get("arrival_ui.status_repairing")
                : actionApplied ? GameLocalization.Get("arrival_ui.status_completed") : part.Action.ToUpperInvariant();
            selectedPartActionBackground.color = repairingSelectedPart
                ? new Color(0.08f, 0.32f, 0.17f, 1f)
                : actionApplied
                ? new Color(0.08f, 0.28f, 0.16f, 1f)
                : part.ActionKey == "arrival_ui.action_inspect"
                ? new Color(0.18f, 0.19f, 0.185f, 1f)
                : new Color(0.28f, 0.16f, 0.075f, 1f);
            if (selectedPartActionButton != null)
                selectedPartActionButton.interactable = !actionApplied && !localRepairActive &&
                                                        !localRepairPending;
        }

        RefreshStartEngineButton(manager);
    }

    private void InvokeSelectedPartAction()
    {
        if (localRepairActive || localRepairPending) return;
        VehiclePartView part = FindPart(selectedPartId);
        if (part == null || selectedPartRecommendation == null) return;

        if (part.ActionKey == "arrival_ui.action_inspect")
        {
            selectedPartRecommendation.text = GameLocalization.Get("arrival_ui.inspect_result_prefix") + part.Recommendation;
            AutoChatManager.Instance?.AddMessage(GameLocalization.Get("arrival_ui.chat_sender_inspect"), part.Label + ": " + part.Recommendation);
            return;
        }

        if (policeMode)
        {
            MilitaryBaseQuestManager policeManager = MilitaryBaseQuestManager.Instance;
            if (policeManager == null || !policeManager.IsNetworkReady || policeOwner == null)
            {
                selectedPartRecommendation.text = GameLocalization.Get("arrival_ui.police_not_connected");
                return;
            }
            if (!PoliceCarRepairRules.TryGetAction(part.Id, out PoliceCarRepairAction policeAction)) return;
            selectedPartRecommendation.text = GameLocalization.Get("arrival_ui.verifying_server");
            localRepairPending = true;
            if (!PoliceCarRepairRules.UsesTimedArrivalCarInteraction(policeAction))
                VehicleRepairSkillCheckUI.PrepareFromInspection(policeManager, policeOwner);
            policeManager.RequestStartPoliceCarRepair(part.Id);
            return;
        }

        MainQuestManager manager = MainQuestManager.Instance;
        if (manager == null || !manager.IsNetworkReady)
        {
            selectedPartRecommendation.text = GameLocalization.Get("arrival_ui.quest_not_connected");
            return;
        }

        selectedPartRecommendation.text = GameLocalization.Get("arrival_ui.verifying_server");
        localRepairPending = true;
        if (selectedPartActionButton != null) selectedPartActionButton.interactable = false;
        if (selectedPartActionText != null) selectedPartActionText.text = GameLocalization.Get("arrival_ui.verifying_button");
        manager.RequestRepairArrivalCarPart(part.Id);
    }

    private void InvokeStartEngine()
    {
        if (policeMode || localRepairActive || localRepairPending) return;
        MainQuestManager manager = MainQuestManager.Instance;
        if (manager == null || !manager.IsNetworkReady)
        {
            if (selectedPartRecommendation != null)
                selectedPartRecommendation.text = GameLocalization.Get("arrival_ui.start_server_not_connected");
            return;
        }

        if (!manager.AreArrivalCarRequiredRepairsComplete)
        {
            if (selectedPartRecommendation != null)
                selectedPartRecommendation.text = GameLocalization.Get("arrival_ui.start_missing_parts_warn");
            return;
        }

        if (startEngineButton != null) startEngineButton.interactable = false;
        if (startEngineText != null) startEngineText.text = GameLocalization.Get("arrival_ui.starting_button");
        manager.RequestStartArrivalCar();
    }

    private void RefreshStartEngineButton(MainQuestManager manager)
    {
        if (startEngineButton == null || startEngineText == null || startEngineBackground == null) return;
        if (policeMode)
        {
            MilitaryBaseQuestManager policeManager = MilitaryBaseQuestManager.Instance;
            int repaired = policeManager != null && policeManager.IsNetworkReady
                ? PoliceCarRepairRules.CountApplied(policeManager.PoliceCarRepairMask)
                : 0;
            bool complete = repaired >= PoliceCarRepairRules.RequiredActionCount;
            startEngineButton.interactable = false;
            startEngineText.text = complete ? GameLocalization.Get("arrival_ui.police_repair_done") : string.Format(GameLocalization.Get("arrival_ui.police_repaired_count"), repaired);
            startEngineText.color = complete ? Color.white : Muted;
            startEngineBackground.color = complete
                ? new Color(0.07f, 0.32f, 0.15f, 1f)
                : new Color(0.12f, 0.13f, 0.125f, 1f);
            return;
        }
        bool networkReady = manager != null && manager.IsNetworkReady;
        bool started = networkReady && manager.IsArrivalCarRepaired;
        bool ready = networkReady && manager.AreArrivalCarRequiredRepairsComplete;
        startEngineButton.interactable = ready && !started && !localRepairActive && !localRepairPending;
        startEngineText.text = started
            ? GameLocalization.Get("arrival_ui.vehicle_started_btn")
            : ready ? GameLocalization.Get("arrival_ui.start_vehicle_btn") : GameLocalization.Get("arrival_ui.cannot_start_btn");
        startEngineText.color = started || ready ? Color.white : Muted;
        startEngineBackground.color = started
            ? new Color(0.07f, 0.32f, 0.15f, 1f)
            : ready ? new Color(0.2f, 0.36f, 0.12f, 1f) : new Color(0.12f, 0.13f, 0.125f, 1f);
    }

    public void NotifyRepairStartResult(ArrivalCarRepairAction action, bool accepted,
        float durationSeconds, string message)
    {
        localRepairPending = false;
        if (!open || policeMode)
        {
            if (accepted) MainQuestManager.Instance?.RequestCancelArrivalCarRepair();
            return;
        }

        if (!accepted)
        {
            SelectVehiclePart(selectedPartId);
            if (selectedPartRecommendation != null)
                selectedPartRecommendation.text = GameLocalization.Get("arrival_ui.failed_prefix") + message;
            return;
        }

        localRepairActive = true;
        localRepairPartId = selectedPartId;
        localRepairDuration = Mathf.Max(0.1f, durationSeconds);
        localRepairStartedAt = Time.unscaledTime;
        if (repairClockHand != null) repairClockHand.localEulerAngles = Vector3.zero;
        if (repairClockRoot != null)
        {
            repairClockRoot.SetActive(true);
            repairClockRoot.transform.SetAsLastSibling();
        }
        SelectVehiclePart(localRepairPartId);
    }

    public void NotifyPoliceTimedRepairStart(PoliceCarRepairAction action, bool accepted,
        float durationSeconds, string message)
    {
        localRepairPending = false;
        if (!open || !policeMode)
        {
            if (accepted) MilitaryBaseQuestManager.Instance?.RequestCancelRepairSkillCheck();
            return;
        }
        if (!accepted)
        {
            SelectVehiclePart(selectedPartId);
            if (selectedPartRecommendation != null)
                selectedPartRecommendation.text = GameLocalization.Get("arrival_ui.failed_prefix") + message;
            return;
        }

        localRepairActive = true;
        localRepairPartId = selectedPartId;
        localRepairDuration = Mathf.Max(0.1f, durationSeconds);
        localRepairStartedAt = Time.unscaledTime;
        if (repairClockHand != null) repairClockHand.localEulerAngles = Vector3.zero;
        if (repairClockRoot != null)
        {
            repairClockRoot.SetActive(true);
            repairClockRoot.transform.SetAsLastSibling();
        }
        SelectVehiclePart(localRepairPartId);
    }

    public void NotifyPoliceTimedRepairInterrupted(string message)
    {
        string partId = localRepairPartId;
        EndLocalRepairPresentation();
        if (!open || !policeMode || selectedPartRecommendation == null) return;
        SelectVehiclePart(string.IsNullOrEmpty(partId) ? selectedPartId : partId);
        selectedPartRecommendation.text = GameLocalization.Get("arrival_ui.stopped_prefix") + message;
    }

    public void NotifyPoliceTimedRepairCompleted(bool allComplete)
    {
        string partId = localRepairPartId;
        EndLocalRepairPresentation();
        if (!open || !policeMode || selectedPartRecommendation == null) return;
        SelectVehiclePart(string.IsNullOrEmpty(partId) ? selectedPartId : partId);
        selectedPartRecommendation.text = allComplete
            ? GameLocalization.Get("arrival_ui.police_all_repaired_diag")
            : GameLocalization.Get("arrival_ui.police_part_repaired_diag");
    }

    public void NotifyRepairInterrupted(string message)
    {
        string partId = localRepairPartId;
        EndLocalRepairPresentation();
        if (!open || selectedPartRecommendation == null) return;
        SelectVehiclePart(string.IsNullOrEmpty(partId) ? selectedPartId : partId);
        selectedPartRecommendation.text = GameLocalization.Get("arrival_ui.stopped_prefix") + message;
    }

    public void NotifyRepairResult(ArrivalCarRepairAction action, bool success, string message)
    {
        string partId = localRepairPartId;
        EndLocalRepairPresentation();
        if (!open || selectedPartRecommendation == null) return;
        SelectVehiclePart(string.IsNullOrEmpty(partId) ? selectedPartId : partId);
        selectedPartRecommendation.text = (success ? GameLocalization.Get("arrival_ui.completed_prefix") : GameLocalization.Get("arrival_ui.failed_prefix")) + message;
    }

    private void UpdateRepairClock()
    {
        float progress = Mathf.Clamp01(
            (Time.unscaledTime - localRepairStartedAt) / Mathf.Max(0.1f, localRepairDuration));
        if (repairClockHand != null)
            repairClockHand.localEulerAngles = new Vector3(0f, 0f, -360f * Mathf.Clamp01(progress));
    }

    public void PlayRepairAudioForNetwork(ArrivalCarRepairAction action, float durationSeconds)
    {
        PlayRepairAudio(action, durationSeconds);
    }

    public void StopRepairAudioForNetwork()
    {
        if (repairAudioSource == null) return;
        repairAudioSource.Stop();
        repairAudioSource.clip = null;
        repairAudioSource.pitch = 1f;
    }

    private void PlayRepairAudio(ArrivalCarRepairAction action, float durationSeconds)
    {
        EnsureRepairAudio();
        if (repairAudioSource == null) return;
        AudioClip clip = action switch
        {
            ArrivalCarRepairAction.AddFuel => fuelRepairClip,
            ArrivalCarRepairAction.ReplaceTire => tireRepairClip,
            _ => hammerRepairClip
        };
        if (clip == null) return;

        repairAudioSource.Stop();
        repairAudioSource.clip = clip;
        repairAudioSource.pitch = Mathf.Clamp(clip.length / Mathf.Max(0.1f, durationSeconds), 0.75f, 1.25f);
        repairAudioSource.volume = Mathf.Clamp(PlayerPrefs.GetFloat("GameSFXVolume", 0.8f), 0f, 1f);
        repairAudioSource.time = 0f;
        repairAudioSource.Play();
    }

    private void EnsureRepairAudio()
    {
        if (repairAudioSource != null) return;
        fuelRepairClip = Resources.Load<AudioClip>("Sound/Vehicles/Repair/GarPump");
        tireRepairClip = Resources.Load<AudioClip>("Sound/Vehicles/Repair/Drill");
        hammerRepairClip = Resources.Load<AudioClip>("Sound/Vehicles/Repair/hammer");
        GameObject audioObject = new GameObject("Arrival Car Repair Audio");
        audioObject.transform.SetParent(transform, false);
        repairAudioSource = audioObject.AddComponent<AudioSource>();
        GameplayAudioSpatializer.Configure(repairAudioSource, GameplayAudioSpatializer.Profile.Body);
        repairAudioSource.playOnAwake = false;
        repairAudioSource.loop = false;
    }

    private void EndLocalRepairPresentation()
    {
        localRepairPending = false;
        localRepairActive = false;
        localRepairPartId = null;
        localRepairDuration = 0f;
        localRepairStartedAt = 0f;
        if (repairAudioSource != null)
            StopRepairAudioForNetwork();
        if (repairClockRoot != null) repairClockRoot.SetActive(false);
    }

    public void NotifyStartResult(bool success, string message)
    {
        if (!open) return;
        RefreshStartEngineButton(MainQuestManager.Instance);
        if (selectedPartRecommendation != null)
            selectedPartRecommendation.text = (success ? GameLocalization.Get("arrival_ui.start_success_prefix") : GameLocalization.Get("arrival_ui.failed_prefix")) +
                                              message;
    }

    public void CloseForPoliceMinigame()
    {
        Close(false);
    }

    public void NotifyPoliceRepairRequestFailed(string message)
    {
        if (!open && policeOwner != null) Open(policeOwner);
        if (selectedPartRecommendation != null)
            selectedPartRecommendation.text = GameLocalization.Get("arrival_ui.failed_prefix") + message;
    }

    public void ReopenPoliceInspection(RoadsideVehicleRepairStation target, string message)
    {
        if (!open) Open(target);
        SelectVehiclePart(string.IsNullOrEmpty(selectedPartId) ? "engine" : selectedPartId);
        if (selectedPartRecommendation != null && !string.IsNullOrEmpty(message))
            selectedPartRecommendation.text = message;
    }

    private bool IsPartApplied(string partId)
    {
        if (policeMode)
        {
            MilitaryBaseQuestManager manager = MilitaryBaseQuestManager.Instance;
            return manager != null && manager.IsNetworkReady &&
                   PoliceCarRepairRules.TryGetAction(partId, out PoliceCarRepairAction action) &&
                   PoliceCarRepairRules.IsApplied(manager.PoliceCarRepairMask, action);
        }

        MainQuestManager arrivalManager = MainQuestManager.Instance;
        return arrivalManager != null && arrivalManager.IsNetworkReady &&
               ArrivalCarRepairRules.TryGetAction(partId, out ArrivalCarRepairAction arrivalAction) &&
               ArrivalCarRepairRules.IsApplied(arrivalManager.ArrivalCarRepairMask, arrivalAction);
    }

    private string BuildRecommendation(VehiclePartView part)
    {
        if (!policeMode || !PoliceCarRepairRules.TryGetAction(part.Id, out PoliceCarRepairAction action))
            return GameLocalization.Get("arrival_ui.diagnosis_prefix") + part.Recommendation +
                   GameLocalization.Get("arrival_ui.track_items_hint");

        MilitaryBaseQuestManager manager = MilitaryBaseQuestManager.Instance;
        float progress = manager != null && manager.IsNetworkReady ? manager.GetPoliceRepairProgress(action) : 0f;
        ArrivalCarItemKind item = PoliceCarRepairRules.GetRequiredItem(action);
        string itemName = GameLocalization.TranslateLiteral(PoliceCarItemCatalog.GetDisplayName(item)).ToUpperInvariant();
        return $"{GameLocalization.Get("arrival_ui.diagnosis_prefix")}{part.Recommendation}{GameLocalization.Get("arrival_ui.progress_prefix")}{progress:0.0}%{GameLocalization.Get("arrival_ui.needs_prefix")}{itemName}";
    }

    private void EnsureVehiclePartDefinitions()
    {
        if (vehicleParts.Count > 0) return;
        AddPart("engine", "arrival_ui.part.engine.name", "arrival_ui.group_engine", 0,
            "arrival_ui.part.engine.desc",
            "arrival_ui.part.engine.rec", "arrival_ui.action_repair",
            new Vector2(47.09247f, 222.6873f), new Vector2(103f, 63f));
        AddPart("battery", "arrival_ui.part.battery.name", "arrival_ui.group_engine", 0,
            "arrival_ui.part.battery.desc",
            "arrival_ui.part.battery.rec", "arrival_ui.action_replace",
            new Vector2(-47.5f, 218f), new Vector2(46f, 35f));
        AddPart("exhaust", "arrival_ui.part.exhaust.name", "arrival_ui.group_engine", 76,
            "arrival_ui.part.exhaust.desc",
            "arrival_ui.part.exhaust.rec", "arrival_ui.action_inspect", new Vector2(55f, -222f), new Vector2(38f, 69f));
        AddPart("fuel", "arrival_ui.part.fuel.name", "arrival_ui.group_fuel", 0,
            "arrival_ui.part.fuel.desc",
            "arrival_ui.part.fuel.rec", "arrival_ui.action_refuel",
            new Vector2(-48f, -224.5f), new Vector2(89f, 59f));
        AddPart("front_left", "arrival_ui.part.front_left.name", "arrival_ui.group_wheels", 0,
            "arrival_ui.part.front_left.desc",
            "arrival_ui.part.front_left.rec", "arrival_ui.action_replace",
            new Vector2(-103f, 86.5f), new Vector2(41f, 73f));
        AddPart("rear_left", "arrival_ui.part.rear_left.name", "arrival_ui.group_wheels", 73,
            "arrival_ui.part.rear_left.desc",
            "arrival_ui.part.rear_left.rec", "arrival_ui.action_inspect",
            new Vector2(-103f, -102f), new Vector2(41f, 73f));
        AddPart("front_right", "arrival_ui.part.front_right.name", "arrival_ui.group_wheels", 61,
            "arrival_ui.part.front_right.desc",
            "arrival_ui.part.front_right.rec", "arrival_ui.action_inspect",
            new Vector2(107f, 86.5f), new Vector2(41f, 73f));
        AddPart("rear_right", "arrival_ui.part.rear_right.name", "arrival_ui.group_wheels", 69,
            "arrival_ui.part.rear_right.desc",
            "arrival_ui.part.rear_right.rec", "arrival_ui.action_inspect",
            new Vector2(107f, -102f), new Vector2(41f, 73f));
        AddPart("hood", "arrival_ui.part.hood.name", "arrival_ui.group_body", 0,
            "arrival_ui.part.hood.desc",
            "arrival_ui.part.hood.rec", "arrival_ui.action_repair",
            new Vector2(0f, 119f), new Vector2(110f, 85f));
        AddPart("windshield", "arrival_ui.part.windshield.name", "arrival_ui.group_body", 65,
            "arrival_ui.part.windshield.desc",
            "arrival_ui.part.windshield.rec", "arrival_ui.action_inspect",
            new Vector2(0f, 48f), new Vector2(107f, 52f));
        AddPart("front_door", "arrival_ui.part.front_door.name", "arrival_ui.group_body", 89,
            "arrival_ui.part.front_door.desc",
            "arrival_ui.part.front_door.rec", "arrival_ui.action_inspect", new Vector2(0f, -30f), new Vector2(122f, 118f));
    }

    private void AddPart(string id, string nameKey, string categoryKey, int condition, string descKey,
        string recKey, string actionKey, Vector2 diagramPosition, Vector2 diagramSize)
    {
        vehicleParts.Add(new VehiclePartView
        {
            Id = id,
            NameKey = nameKey,
            CategoryKey = categoryKey,
            Condition = condition,
            DescKey = descKey,
            RecKey = recKey,
            ActionKey = actionKey,
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
        if (condition < 60) return Amber;
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
