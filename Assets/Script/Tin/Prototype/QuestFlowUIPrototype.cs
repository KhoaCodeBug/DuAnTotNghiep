using System.Collections;
using System.Collections.Generic;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Interactive Canvas/TMP prototype for the pre-military quest flow.
/// It demonstrates presentation and navigation only; production quest data can replace it later.
/// </summary>
public sealed class QuestFlowUIPrototype : MonoBehaviour
{
    public static QuestFlowUIPrototype Instance { get; private set; }
    public event Action MapFragment1Acquired;
    private static readonly Color Ink = new Color(0.025f, 0.045f, 0.043f, 0.98f);
    private static readonly Color Panel = new Color(0.055f, 0.082f, 0.078f, 0.98f);
    private static readonly Color PanelLight = new Color(0.095f, 0.125f, 0.118f, 0.98f);
    private static readonly Color Amber = new Color(1f, 0.67f, 0.14f, 1f);
    private static readonly Color Purple = new Color(0.72f, 0.36f, 0.98f, 1f);
    private static readonly Color Mint = new Color(0.28f, 0.88f, 0.7f, 1f);
    private static readonly Color Muted = new Color(0.62f, 0.69f, 0.67f, 1f);
    private static readonly Color Border = new Color(0.28f, 0.36f, 0.34f, 0.85f);

    [Header("Prototype presentation")]
    [SerializeField] private bool buildDemoBackdrop = true;
    [SerializeField] private bool enablePreviewShortcuts = false;
    [SerializeField] private bool enableOfficeRevealPreview = true;

    private readonly RectTransform[] tabRects = new RectTransform[2];
    private readonly GameObject[] tabUnderlines = new GameObject[2];
    private readonly TextMeshProUGUI[] tabTexts = new TextMeshProUGUI[2];
    private readonly Image[] objectiveStates = new Image[3];
    private readonly TextMeshProUGUI[] objectiveNumbers = new TextMeshProUGUI[3];
    private readonly TextMeshProUGUI[] objectiveLabels = new TextMeshProUGUI[3];
    private readonly TextMeshProUGUI[] objectiveStatuses = new TextMeshProUGUI[3];
    private readonly Image[] sideClueSegmentImages = new Image[3];
    private readonly TextMeshProUGUI[] sideClueSegmentTexts = new TextMeshProUGUI[3];
    private readonly PreMilitaryQuestProgress mainQuestProgress = new PreMilitaryQuestProgress();
    private readonly List<CarRepairRequirementView> carRepairRequirementViews =
        new List<CarRepairRequirementView>();

    private Canvas canvas;
    private TMP_FontAsset font;
    private GameObject journalRoot;
    private GameObject activeContentRoot;
    private GameObject questListRoot;
    private GameObject questListDivider;
    private GameObject questDetailsRoot;
    private GameObject emptyStateRoot;
    private TextMeshProUGUI emptyStateTitle;
    private TextMeshProUGUI emptyStateBody;
    private GameObject completionRoot;
    private CanvasGroup completionGroup;
    private TextMeshProUGUI completionQuestName;
    private TextMeshProUGUI completionRewardText;
    private RectTransform completionRewardCard;
    private readonly RectTransform[] completionSparkles = new RectTransform[8];
    private Coroutine completionRoutine;
    private GameObject clueReadingRoot;
    private TextMeshProUGUI clueReadingEyebrow;
    private TextMeshProUGUI clueReadingTitle;
    private TextMeshProUGUI clueReadingBody;
    private TextMeshProUGUI clueReadingConclusion;
    private bool fragmentCompletionPending;

    private RectTransform mainQuestCard;
    private RectTransform sideQuestCard;
    private GameObject mainQuestHeader;
    private GameObject sideQuestHeader;
    private RectTransform mainQuestNameRect;
    private RectTransform mainQuestMetaRect;
    private RectTransform sideQuestNameRect;
    private RectTransform sideQuestMetaRect;
    private Image mainQuestCardImage;
    private Image sideQuestCardImage;
    private Image mainQuestAccent;
    private Image sideQuestAccent;
    private GameObject mapFragmentSlotsRoot;
    private GameObject sideQuestProgressRoot;
    private TextMeshProUGUI contextPanelTitle;
    private TextMeshProUGUI contextPanelCount;
    private TextMeshProUGUI mapFragment1SlotText;
    private TextMeshProUGUI mapFragment2SlotText;
    private TextMeshProUGUI mainQuestMetaText;
    private TextMeshProUGUI sideQuestMetaText;

    private TextMeshProUGUI detailEyebrow;
    private TextMeshProUGUI detailTitle;
    private TextMeshProUGUI storyText;
    private RectTransform statusBadge;
    private Image statusBadgeImage;
    private TextMeshProUGUI statusBadgeText;
    private TextMeshProUGUI rewardLabel;
    private TextMeshProUGUI rewardText;
    private Button trackingButton;
    private Image trackingButtonImage;
    private TextMeshProUGUI trackingButtonText;
    private Image objectiveProgressFill;
    private TextMeshProUGUI currentObjectiveText;
    private TextMeshProUGUI currentObjectiveProgressText;
    private GameObject mainQuestTrackedMarker;
    private GameObject sideQuestTrackedMarker;
    private GameObject carQuestTrackedMarker;
    private GameObject carQuestHeader;
    private RectTransform carQuestCard;
    private Image carQuestCardImage;
    private Image carQuestAccent;
    private TextMeshProUGUI carQuestMetaText;
    private GameObject carRepairRequirementsRoot;
    private TextMeshProUGUI mapLabel;
    private TextMeshProUGUI mapFooter;
    private GameObject miniMapApproximateArea;
    private GameObject miniMapOffice;
    private Image miniMapRouteImage;
    private RectTransform gameplayBackdrop;
    private GameObject backdropOfficeMarker;
    private GameObject cinematicRoot;
    private QuestMapUIPrototype mapPrototype;
    private Coroutine officeRevealRoutine;
    private float nextCarInventoryRefreshAt;
    private Func<string[], bool> carRepairInventoryQuery;

    private sealed class CarRepairRequirementView
    {
        public bool Required;
        public ArrivalCarItemKind ItemKind;
        public string[] InventoryNames;
        public Image CardBackground;
        public TextMeshProUGUI StateText;
    }

    private bool built;
    private bool journalOpen;
    private int selectedQuestIndex;
    private int selectedTabIndex;
    private int trackedQuestIndex = -1;
    private EscapeEndingRoute lockedEscapeRoute;
    private int demoHouseSequence;
    private int demoClueSequence;

    public bool IsJournalOpen => journalOpen;
    public int SelectedQuestIndex => selectedQuestIndex;
    public int SelectedTabIndex => selectedTabIndex;
    public int TrackedQuestIndex => trackedQuestIndex;
    public EscapeEndingRoute TrackedEscapeRoute => trackedQuestIndex == 2
        ? EscapeEndingRoute.CivilianCar
        : trackedQuestIndex == 0 ? EscapeEndingRoute.MilitaryEvacuation : EscapeEndingRoute.None;
    public EscapeEndingRoute LockedEscapeRoute => lockedEscapeRoute;
    public bool IsSelectedQuestTracked => trackedQuestIndex == selectedQuestIndex;
    public string TrackingButtonText => trackingButtonText == null ? string.Empty : trackingButtonText.text;
    public string CurrentDetailTitle => detailTitle == null ? string.Empty : detailTitle.text;
    public string CurrentObjectiveProgress => currentObjectiveProgressText == null
        ? string.Empty
        : currentObjectiveProgressText.text;
    public string CurrentContextPanelTitle => contextPanelTitle == null ? string.Empty : contextPanelTitle.text;
    public bool IsEmptyStateVisible => emptyStateRoot != null && emptyStateRoot.activeSelf;
    public bool IsQuestListDividerVisible => questListDivider != null && questListDivider.activeSelf;
    public bool IsMainQuestComplete => mainQuestProgress.MainQuestComplete;
    public bool IsHouseSearchComplete => mainQuestProgress.HouseSearchComplete;
    public bool HasMapFragment1 => mainQuestProgress.HasMapFragment1;
    public bool IsClueReadingOpen => clueReadingRoot != null && clueReadingRoot.activeSelf;
    public string CurrentClueReadingBody => clueReadingBody == null ? string.Empty : clueReadingBody.text;
    public string CurrentClueReadingConclusion => clueReadingConclusion == null ? string.Empty : clueReadingConclusion.text;
    public bool IsMapOpen => mapPrototype != null && mapPrototype.IsOpen;
    public string CurrentMapKnowledgeLabel => mapPrototype == null ? string.Empty : mapPrototype.CurrentKnowledgeLabel;
    public string CurrentMapClueSummary => mapPrototype == null ? string.Empty : mapPrototype.CurrentClueSummary;
    public int CurrentMapRotationQuarterTurns => mapPrototype == null ? 0 : mapPrototype.CurrentRasterRotationQuarterTurns;
    public int CurrentMapSearchZoneHouseCount => mapPrototype == null ? 0 : mapPrototype.SearchZoneHouseCount;
    public bool HasPendingMapUnlockReveal => mapPrototype != null && mapPrototype.HasPendingUnlockReveal;
    public int ActiveMapRestrictedFogCount => mapPrototype == null ? 0 : mapPrototype.ActiveRestrictedFogCount;
    public string CurrentRewardLabel => rewardLabel == null ? string.Empty : rewardLabel.text;
    public string CurrentRewardText => rewardText == null ? string.Empty : rewardText.text;
    public bool IsQuestOverlayOpen => IsJournalOpen || IsMapOpen || IsClueReadingOpen ||
                                      (completionRoot != null && completionRoot.activeSelf);

    private void Awake()
    {
        Instance = this;
        Application.targetFrameRate = 60;
        EnsureBuiltForTests();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        // A clue is a modal reading screen. Close it before accepting any
        // journal/map input so those layers can never overlap.
        if (IsClueReadingOpen)
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E) ||
                Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Return))
                CloseRouteClueReading();
            return;
        }

        // Completion/reward presentation owns the screen until its short
        // sequence ends. Ignoring journal/map input here prevents two modal
        // canvases from being opened on top of each other.
        if (completionRoot != null && completionRoot.activeSelf)
            return;

        if (Input.GetKeyDown(KeyCode.J))
        {
            if (mapPrototype != null) mapPrototype.SetOpen(false);
            SetJournalOpen(!journalOpen);
        }

        if (Input.GetKeyDown(KeyCode.M))
        {
            SetJournalOpen(false);
            if (mapPrototype != null)
            {
                bool openMap = !mapPrototype.IsOpen;
                mapPrototype.SetOpen(openMap);
            }
        }

        // Preview-only shortcuts. They are intentionally separate from production bindings.
        if (enablePreviewShortcuts && Input.GetKeyDown(KeyCode.F2))
        {
            if (mapPrototype != null) mapPrototype.SetOpen(false);
            SetJournalOpen(true);
        }

        if (enablePreviewShortcuts && Input.GetKeyDown(KeyCode.F3))
            SimulateNextHouseSearch();

        if (enablePreviewShortcuts && Input.GetKeyDown(KeyCode.F4))
            SimulateNextRouteClue();

        if (enablePreviewShortcuts && Input.GetKeyDown(KeyCode.F5))
            RegisterOfficeDiscoveredForPreview();

        if (enablePreviewShortcuts && Input.GetKeyDown(KeyCode.F6))
        {
            RegisterOfficeMapCabinetOpenedForPreview();
            RegisterMapFragment2AddedToInventoryForPreview();
        }

        if (!journalOpen)
            return;

        if (selectedQuestIndex == 2 && Time.unscaledTime >= nextCarInventoryRefreshAt)
        {
            nextCarInventoryRefreshAt = Time.unscaledTime + 0.25f;
            RefreshCarRepairRequirementStates();
        }

        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
            SelectQuest(selectedQuestIndex - 1);
        if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
            SelectQuest(selectedQuestIndex + 1);
        if (Input.GetKeyDown(KeyCode.Q))
            SelectTab(Wrap(selectedTabIndex - 1, 2));
        if (Input.GetKeyDown(KeyCode.E))
            SelectTab(Wrap(selectedTabIndex + 1, 2));
        if (Input.GetKeyDown(KeyCode.V))
            ToggleSelectedQuestTracking();
    }

    /// <summary>Builds the hierarchy when called from EditMode tests.</summary>
    public void EnsureBuiltForTests()
    {
        if (built)
            return;

        built = true;
        font = Resources.Load<TMP_FontAsset>("Fonts/VietnameseDynamic SDF") ?? TMP_Settings.defaultFontAsset;
        BuildCanvas();
        if (buildDemoBackdrop)
            BuildBackdrop();
        if (enableOfficeRevealPreview)
            BuildCinematicOverlay();
        BuildClueReading();
        BuildCompletionNotice();
        BuildJournal();
        mapPrototype = gameObject.GetComponent<QuestMapUIPrototype>();
        if (mapPrototype == null)
            mapPrototype = gameObject.AddComponent<QuestMapUIPrototype>();
        mapPrototype.Initialize(canvas.transform, font, mainQuestProgress);
    }

    public void SetJournalOpenForPreview(bool open)
    {
        EnsureBuiltForTests();
        SetJournalOpen(open);
    }

    public void SetTrackedEscapeRoute(EscapeEndingRoute route)
    {
        EnsureBuiltForTests();
        if (lockedEscapeRoute != EscapeEndingRoute.None && route != lockedEscapeRoute)
            return;
        trackedQuestIndex = route switch
        {
            EscapeEndingRoute.CivilianCar => 2,
            EscapeEndingRoute.MilitaryEvacuation => 0,
            _ => -1
        };
        UpdateTrackingPresentation();
    }

    public void SelectQuestForPreview(int index)
    {
        EnsureBuiltForTests();
        SelectQuest(index);
    }

    public void SelectTabForPreview(int index)
    {
        EnsureBuiltForTests();
        SelectTab(index);
    }

    public void ToggleSelectedQuestTrackingForPreview()
    {
        EnsureBuiltForTests();
        ToggleSelectedQuestTracking();
    }

    public string GetObjectiveStatusForPreview(int index)
    {
        EnsureBuiltForTests();
        return index >= 0 && index < objectiveStatuses.Length ? objectiveStatuses[index].text : string.Empty;
    }

    public string GetTabTextForPreview(int index)
    {
        EnsureBuiltForTests();
        return index >= 0 && index < tabTexts.Length ? tabTexts[index].text : string.Empty;
    }

    public string GetCarRepairRequirementStateForPreview(int index)
    {
        EnsureBuiltForTests();
        return index >= 0 && index < carRepairRequirementViews.Count
            ? carRepairRequirementViews[index].StateText.text
            : string.Empty;
    }

    public void SetCarRepairInventoryQuery(Func<string[], bool> query)
    {
        carRepairInventoryQuery = query;
        nextCarInventoryRefreshAt = 0f;
        RefreshCarRepairRequirementStates();
    }

    public void RegisterHouseLootContainerOpenedForPreview(string houseId)
    {
        EnsureBuiltForTests();
        mainQuestProgress.RegisterLootContainerOpenedInHouse(houseId);
        RefreshQuestPresentation();
    }

    /// <summary>
    /// Keeps the current prototype presentation, but replaces its local source
    /// of truth with the replicated team snapshot. Late joiners apply the first
    /// snapshot silently; subsequent transitions may play the existing feedback.
    /// </summary>
    public void ApplyAuthoritativeSnapshot(int searchedHouseMask, int routeClueMask,
        bool officeDiscovered, bool officeInvestigationComplete, bool hasMapFragment2,
        bool playTransitions, bool arrivalCarRepairUnlocked = false, bool arrivalCarRepaired = false,
        int arrivalCarRepairMask = 0, EscapeEndingRoute authoritativeLockedRoute = EscapeEndingRoute.None)
    {
        EnsureBuiltForTests();
        bool hadFragment1 = mainQuestProgress.HasMapFragment1;
        bool wasMainQuestComplete = mainQuestProgress.MainQuestComplete;

        mainQuestProgress.ApplyAuthoritativeSnapshot(searchedHouseMask, routeClueMask,
            officeDiscovered, officeInvestigationComplete, hasMapFragment2,
            arrivalCarRepairUnlocked, arrivalCarRepaired, arrivalCarRepairMask);
        lockedEscapeRoute = authoritativeLockedRoute;
        if (lockedEscapeRoute != EscapeEndingRoute.None)
            trackedQuestIndex = lockedEscapeRoute == EscapeEndingRoute.CivilianCar ? 2 : 0;
        RefreshQuestPresentation();

        if (!playTransitions || !Application.isPlaying) return;
        if (!hadFragment1 && mainQuestProgress.HasMapFragment1)
        {
            // Queue independently of the completion popup. If the RPC arrived
            // before this UI existed, the replicated snapshot still guarantees
            // that opening the map will play the reveal once.
            mapPrototype?.QueueUnlockReveal();
            if (IsClueReadingOpen)
                fragmentCompletionPending = true;
            else
                PlayMapFragmentOneCompletion();
        }
        if (!wasMainQuestComplete && mainQuestProgress.MainQuestComplete)
            PlayQuestCompletion("NHIỆM VỤ HOÀN THÀNH", "Tìm thêm thông tin về thành phố", "Mảnh bản đồ 2", null);
    }

    private void SimulateNextHouseSearch()
    {
        demoHouseSequence++;
        RegisterHouseLootContainerOpenedForPreview("DEMO_HOUSE_" + demoHouseSequence);
    }

    private void SimulateNextRouteClue()
    {
        demoClueSequence++;
        RegisterRouteClueForPreview("DEMO_CLUE_" + demoClueSequence);
    }

    public bool RegisterRouteClueForPreview(string clueId, bool deferCompletion = false)
    {
        EnsureBuiltForTests();
        bool hadFragment = mainQuestProgress.HasMapFragment1;
        bool added = mainQuestProgress.RegisterRouteClue(clueId);
        RefreshQuestPresentation();

        bool completedNow = !hadFragment && mainQuestProgress.HasMapFragment1;
        if (completedNow && Application.isPlaying)
        {
            if (deferCompletion)
                fragmentCompletionPending = true;
            else
                PlayMapFragmentOneCompletion();
        }

        return added && completedNow;
    }

    public void ShowRouteClueReading(string title, string body, string inference)
    {
        EnsureBuiltForTests();
        SetJournalOpen(false);
        if (mapPrototype != null) mapPrototype.SetOpen(false);

        clueReadingEyebrow.text = "MANH MỐI TUYẾN ĐƯỜNG  //  " +
                                  mainQuestProgress.RouteClueCount + " / " + PreMilitaryQuestProgress.RequiredRouteClues;
        clueReadingTitle.text = string.IsNullOrWhiteSpace(title) ? "MANH MỐI KHÔNG RÕ" : title.ToUpperInvariant();
        clueReadingBody.text = body ?? string.Empty;
        clueReadingConclusion.text = inference ?? string.Empty;
        clueReadingRoot.SetActive(true);
        clueReadingRoot.transform.SetAsLastSibling();
    }

    public void CloseRouteClueReading()
    {
        if (clueReadingRoot == null || !clueReadingRoot.activeSelf)
            return;

        clueReadingRoot.SetActive(false);
        if (!fragmentCompletionPending)
            return;

        fragmentCompletionPending = false;
        PlayMapFragmentOneCompletion();
    }

    private void PlayMapFragmentOneCompletion()
    {
        PlayQuestCompletion("ĐÃ PHÁT HIỆN ĐỦ MANH MỐI", "Dữ liệu tuyến đường đã hoàn chỉnh",
            "MỞ BẢN ĐỒ [M] ĐỂ KIỂM TRA",
            ContinueMapFragmentOneFlow);
    }

    public void RegisterOfficeDiscoveredForPreview()
    {
        EnsureBuiltForTests();
        mainQuestProgress.RegisterOfficeDiscovered();
        RefreshQuestPresentation();
    }

    public void RegisterOfficeMapCabinetOpenedForPreview()
    {
        EnsureBuiltForTests();
        mainQuestProgress.RegisterOfficeMapCabinetOpened();
        RefreshQuestPresentation();
    }

    public void RegisterMapFragment2AddedToInventoryForPreview()
    {
        EnsureBuiltForTests();
        bool wasComplete = mainQuestProgress.MainQuestComplete;
        mainQuestProgress.RegisterMapFragment2AddedToInventory();
        RefreshQuestPresentation();
        if (!wasComplete && mainQuestProgress.MainQuestComplete && Application.isPlaying)
            PlayQuestCompletion("NHIỆM VỤ HOÀN THÀNH", "Tìm thêm thông tin về thành phố", "Mảnh bản đồ 2", null);
    }

    private void ContinueMapFragmentOneFlow()
    {
        if (MapFragment1Acquired != null)
            MapFragment1Acquired.Invoke();
        else if (enableOfficeRevealPreview && gameplayBackdrop != null && cinematicRoot != null)
            PlayOfficeReveal();
        else if (mapPrototype != null)
            mapPrototype.SetOpen(true);
    }

    public void SetMapOpenForPreview(bool open)
    {
        EnsureBuiltForTests();
        SetJournalOpen(false);
        mapPrototype.SetOpen(open);
    }

    public void QueueMapUnlockReveal()
    {
        EnsureBuiltForTests();
        mapPrototype?.QueueUnlockReveal();
    }

    public void ConfigureWorldMap(Camera mapCameraTemplate, Transform officeTarget, Transform playerTarget = null)
    {
        EnsureBuiltForTests();
        mapPrototype.ConfigureWorldMap(mapCameraTemplate, officeTarget, playerTarget);
    }

    public void ConfigureSceneLayoutMap(Vector3[] housePositions, Transform officeTarget, Transform playerTarget = null)
    {
        EnsureBuiltForTests();
        mapPrototype.ConfigureSceneLayoutMap(housePositions, officeTarget, playerTarget);
    }

    public void ConfigureRasterMap(Texture2D mapTexture, Vector2 officeNormalized, Vector2 playerNormalized)
    {
        EnsureBuiltForTests();
        mapPrototype.ConfigureRasterMap(mapTexture, officeNormalized, playerNormalized);
    }

    public void ConfigureSearchZone(Vector2 minimumNormalized, Vector2 maximumNormalized, int houseCount)
    {
        EnsureBuiltForTests();
        mapPrototype.ConfigureSearchZone(minimumNormalized, maximumNormalized, houseCount);
    }

    public void ConfigureOfficeSearchArea(Vector2 minimumNormalized, Vector2 maximumNormalized)
    {
        EnsureBuiltForTests();
        mapPrototype.ConfigureOfficeSearchArea(minimumNormalized, maximumNormalized);
    }

    public void SetRasterMapPlayerPosition(Vector2 playerNormalized)
    {
        if (mapPrototype != null)
            mapPrototype.SetRasterMapPlayerPosition(playerNormalized);
    }

    public void RotateRasterMapForPreview(int quarterTurnDelta)
    {
        EnsureBuiltForTests();
        mapPrototype.RotateRasterMap(quarterTurnDelta);
    }

    public void CloseAllQuestOverlays()
    {
        EnsureBuiltForTests();
        SetJournalOpen(false);
        if (mapPrototype != null)
            mapPrototype.SetOpen(false);
        if (clueReadingRoot != null)
            clueReadingRoot.SetActive(false);
    }

    public bool HasBuiltElement(string elementName)
    {
        EnsureBuiltForTests();
        return FindChild(transform, elementName) != null;
    }

    /// <summary>
    /// Checks the invariants that caused the first prototype's visible bugs.
    /// Returning an empty array means the prototype passed all checks.
    /// </summary>
    public string[] ValidatePrototype()
    {
        EnsureBuiltForTests();
        var errors = new List<string>();

        RequireElement(errors, "Clue Reading Overlay");
        RequireElement(errors, "Clue Reading Body");
        RequireElement(errors, "Quest Journal");
        RequireElement(errors, "Main Quest Card");
        RequireElement(errors, "Side Quest Card");
        RequireElement(errors, "Car Repair Quest Card");
        RequireElement(errors, "Active Empty State");
        RequireElement(errors, "Current Objective");
        RequireElement(errors, "Current Objective Progress Bar");
        RequireElement(errors, "Tracking Button");
        RequireElement(errors, "Open Map Button");
        RequireElement(errors, "Quest Map");
        RequireElement(errors, "Approximate Office Area");
        RequireElement(errors, "Exact Office Marker");

        RectTransform shell = FindChild(transform, "Journal Shell") as RectTransform;
        if (shell == null || !Approximately(shell.sizeDelta, new Vector2(1400f, 760f)))
            errors.Add("Khung nhật ký phải có kích thước chuẩn 1400 x 760.");

        if (RectsOverlap(mainQuestNameRect, mainQuestMetaRect))
            errors.Add("Tên và trạng thái nhiệm vụ chính đang chồng nhau.");
        if (RectsOverlap(sideQuestNameRect, sideQuestMetaRect))
            errors.Add("Tên và trạng thái nhiệm vụ phụ đang chồng nhau.");
        if (RectsOverlap(mainQuestCard, sideQuestCard))
            errors.Add("Thẻ nhiệm vụ chính và nhiệm vụ phụ đang chồng nhau.");

        float tabWidth = tabRects[0].sizeDelta.x;
        for (int i = 1; i < tabRects.Length; i++)
        {
            if (!Mathf.Approximately(tabRects[i].sizeDelta.x, tabWidth))
                errors.Add("Hai tab nhật ký phải có cùng chiều rộng.");
        }

        if (selectedQuestIndex < 0 || selectedQuestIndex > 2)
            errors.Add("Chỉ số nhiệm vụ được chọn nằm ngoài phạm vi.");
        if (selectedTabIndex < 0 || selectedTabIndex > 2)
            errors.Add("Chỉ số tab được chọn nằm ngoài phạm vi.");

        if (FindChild(transform, "Fragment Progress") != null)
            errors.Add("Không được dùng thanh phần trăm cho hai mảnh bản đồ riêng biệt.");
        if (FindChild(transform, "Footer") != null)
            errors.Add("Không được lặp nút đóng ở chân trang nhật ký.");
        if (FindChild(transform, "Badge Dot") != null || FindChild(transform, "Empty State Icon") != null ||
            FindChild(transform, "Side Quest Icon") != null)
            errors.Add("Vẫn còn icon hình vuông placeholder không có ý nghĩa.");

        return errors.ToArray();
    }

    private void BuildCanvas()
    {
        GameObject canvasObject = new GameObject("Quest UI Canvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }

    private void BuildBackdrop()
    {
        gameplayBackdrop = StretchBox("Gameplay Backdrop", canvas.transform,
            new Color(0.035f, 0.065f, 0.063f, 1f));
        RectTransform root = gameplayBackdrop;
        root.SetAsFirstSibling();

        RectTransform road = Box("Road", root, new Vector2(0.5f, 0.5f), new Vector2(2300f, 190f),
            new Vector2(0f, -230f), new Color(0.13f, 0.16f, 0.155f, 1f));
        road.localRotation = Quaternion.Euler(0f, 0f, -13f);
        Box("Road Line", road, new Vector2(0.5f, 0.5f), new Vector2(2300f, 3f), Vector2.zero,
            new Color(0.66f, 0.57f, 0.28f, 0.45f));

        CreateBuilding(root, new Vector2(-650f, 270f), new Vector2(260f, 178f), new Color(0.16f, 0.25f, 0.22f));
        CreateBuilding(root, new Vector2(645f, 245f), new Vector2(270f, 190f), new Color(0.29f, 0.17f, 0.33f));
        CreateBuilding(root, new Vector2(-420f, -345f), new Vector2(220f, 142f), new Color(0.2f, 0.24f, 0.19f));
        CreateBuilding(root, new Vector2(520f, -350f), new Vector2(235f, 148f), new Color(0.17f, 0.23f, 0.22f));

        Box("Player", root, new Vector2(0.5f, 0.5f), new Vector2(18f, 18f), new Vector2(-50f, -185f), Mint);
        Box("Player Direction", root, new Vector2(0.5f, 0.5f), new Vector2(6f, 14f), new Vector2(-50f, -166f),
            new Color(Mint.r, Mint.g, Mint.b, 0.72f));

        backdropOfficeMarker = new GameObject("Office World Marker", typeof(RectTransform));
        backdropOfficeMarker.transform.SetParent(root, false);
        SetRect(backdropOfficeMarker.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f),
            new Vector2(220f, 100f), new Vector2(705f, 372f));
        Box("Office Pulse", backdropOfficeMarker.transform, new Vector2(0.5f, 0.5f), new Vector2(46f, 46f),
            Vector2.zero, new Color(Purple.r, Purple.g, Purple.b, 0.18f));
        Box("Office Marker", backdropOfficeMarker.transform, new Vector2(0.5f, 0.5f), new Vector2(16f, 16f),
            Vector2.zero, Purple);
        Text(backdropOfficeMarker.transform, "Office Label", "VĂN PHÒNG", 14f, Color.white, FontStyles.Bold,
            TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(190f, 28f), new Vector2(0f, -36f));
        backdropOfficeMarker.SetActive(false);

        StretchBox("Top Shade", root, new Color(0f, 0f, 0f, 0.28f),
            new Vector2(0f, 0.86f), Vector2.one, Vector2.zero, Vector2.zero);
        StretchBox("Bottom Shade", root, new Color(0f, 0f, 0f, 0.18f),
            Vector2.zero, new Vector2(1f, 0.12f), Vector2.zero, Vector2.zero);

        RectTransform controls = Box("Demo Controls", root, new Vector2(1f, 0f), new Vector2(390f, 122f),
            new Vector2(-38f, 34f), new Color(0.025f, 0.045f, 0.043f, 0.92f));
        AddBorder(controls, new Color(0.28f, 0.36f, 0.34f, 0.75f));
        Text(controls, "Demo Controls Title", "ĐIỀU KHIỂN DEMO", 11f, Amber, FontStyles.Bold,
            TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(340f, 22f), new Vector2(18f, -14f));
        Text(controls, "Demo Controls Text",
            "F3  LỤC SOÁT NHÀ     F4  NHẶT DẤU VẾT     F5  TÌM VĂN PHÒNG\nF6  NHẶT MẢNH 2        J  NHẬT KÝ              M  MỞ BẢN ĐỒ",
            11f, Color.white, FontStyles.Bold, TextAlignmentOptions.TopLeft,
            new Vector2(0f, 1f), new Vector2(350f, 64f), new Vector2(18f, -45f));
    }

    private void CreateBuilding(Transform parent, Vector2 position, Vector2 size, Color color)
    {
        Box("Building Shadow", parent, new Vector2(0.5f, 0.5f), size, position + new Vector2(12f, -13f),
            new Color(0f, 0f, 0f, 0.38f));
        RectTransform building = Box("Building", parent, new Vector2(0.5f, 0.5f), size, position, color);
        Box("Roof Edge", building, new Vector2(0.5f, 1f), new Vector2(size.x, 5f), new Vector2(0f, -2.5f),
            new Color(color.r + 0.08f, color.g + 0.08f, color.b + 0.08f));
        Box("Window L", building, new Vector2(0f, 1f), new Vector2(40f, 24f), new Vector2(46f, -48f),
            new Color(0.8f, 0.67f, 0.31f, 0.43f));
        Box("Window R", building, new Vector2(1f, 1f), new Vector2(40f, 24f), new Vector2(-46f, -48f),
            new Color(0.8f, 0.67f, 0.31f, 0.38f));
    }

    private void BuildCinematicOverlay()
    {
        cinematicRoot = new GameObject("Office Reveal Cinematic", typeof(RectTransform));
        cinematicRoot.transform.SetParent(canvas.transform, false);
        Stretch(cinematicRoot.GetComponent<RectTransform>());

        StretchBox("Cinematic Shade", cinematicRoot.transform, new Color(0f, 0f, 0f, 0.2f));
        StretchBox("Letterbox Top", cinematicRoot.transform, new Color(0f, 0f, 0f, 0.96f),
            new Vector2(0f, 0.88f), Vector2.one, Vector2.zero, Vector2.zero);
        StretchBox("Letterbox Bottom", cinematicRoot.transform, new Color(0f, 0f, 0f, 0.96f),
            Vector2.zero, new Vector2(1f, 0.12f), Vector2.zero, Vector2.zero);

        RectTransform location = Box("Office Reveal Label", cinematicRoot.transform, new Vector2(0.5f, 0f),
            new Vector2(620f, 78f), new Vector2(0f, 154f), Ink);
        AddBorder(location, new Color(Purple.r, Purple.g, Purple.b, 0.75f));
        Box("Reveal Label Accent", location, new Vector2(0f, 0.5f), new Vector2(5f, 78f),
            new Vector2(2.5f, 0f), Purple);
        Text(location, "Reveal Label Eyebrow", "MẢNH BẢN ĐỒ 1  //  ĐỊA ĐIỂM ĐÃ XÁC ĐỊNH", 11f,
            Purple, FontStyles.Bold, TextAlignmentOptions.Left, new Vector2(0f, 1f),
            new Vector2(560f, 22f), new Vector2(22f, -17f));
        Text(location, "Reveal Location Name", "VĂN PHÒNG BỊ BỎ HOANG", 22f, Color.white,
            FontStyles.Bold, TextAlignmentOptions.Left, new Vector2(0f, 0f),
            new Vector2(560f, 34f), new Vector2(22f, 20f));

        cinematicRoot.SetActive(false);
    }

    private void PlayOfficeReveal()
    {
        if (officeRevealRoutine != null)
            StopCoroutine(officeRevealRoutine);

        officeRevealRoutine = StartCoroutine(OfficeRevealRoutine());
    }

    private IEnumerator OfficeRevealRoutine()
    {
        SetJournalOpen(false);
        if (mapPrototype != null) mapPrototype.SetOpen(false);
        if (backdropOfficeMarker != null) backdropOfficeMarker.SetActive(true);
        cinematicRoot.SetActive(true);
        cinematicRoot.transform.SetAsLastSibling();

        Vector2 startPosition = Vector2.zero;
        Vector2 focusPosition = new Vector2(-560f, -245f);
        const float focusScale = 1.18f;

        float elapsed = 0f;
        const float travelDuration = 1.25f;
        while (elapsed < travelDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / travelDuration));
            gameplayBackdrop.anchoredPosition = Vector2.Lerp(startPosition, focusPosition, t);
            gameplayBackdrop.localScale = Vector3.one * Mathf.Lerp(1f, focusScale, t);
            yield return null;
        }

        yield return new WaitForSecondsRealtime(1.5f);

        elapsed = 0f;
        const float returnDuration = 0.75f;
        while (elapsed < returnDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / returnDuration));
            gameplayBackdrop.anchoredPosition = Vector2.Lerp(focusPosition, startPosition, t);
            gameplayBackdrop.localScale = Vector3.one * Mathf.Lerp(focusScale, 1f, t);
            yield return null;
        }

        gameplayBackdrop.anchoredPosition = startPosition;
        gameplayBackdrop.localScale = Vector3.one;
        cinematicRoot.SetActive(false);
        officeRevealRoutine = null;
        if (mapPrototype != null) mapPrototype.SetOpen(true);
    }

    private void BuildClueReading()
    {
        clueReadingRoot = new GameObject("Clue Reading Overlay", typeof(RectTransform));
        clueReadingRoot.transform.SetParent(canvas.transform, false);
        Stretch(clueReadingRoot.GetComponent<RectTransform>());

        StretchBox("Clue Reading Dimmer", clueReadingRoot.transform, new Color(0f, 0f, 0f, 0.74f));
        RectTransform panel = Box("Clue Reading Panel", clueReadingRoot.transform, new Vector2(0.5f, 0.5f),
            new Vector2(820f, 430f), new Vector2(0f, 20f), Ink);
        AddBorder(panel, new Color(Amber.r, Amber.g, Amber.b, 0.9f));
        Box("Clue Reading Top Accent", panel, new Vector2(0.5f, 1f), new Vector2(820f, 6f),
            new Vector2(0f, -3f), Amber);

        clueReadingEyebrow = Text(panel, "Clue Reading Eyebrow", string.Empty, 12f, Amber, FontStyles.Bold,
            TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(740f, 24f), new Vector2(38f, -30f));
        clueReadingTitle = Text(panel, "Clue Reading Title", string.Empty, 29f, Color.white, FontStyles.Bold,
            TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(740f, 46f), new Vector2(38f, -67f));

        RectTransform paper = Box("Clue Document", panel, new Vector2(0.5f, 0.5f), new Vector2(742f, 208f),
            new Vector2(0f, -15f), new Color(0.105f, 0.125f, 0.105f, 1f));
        AddBorder(paper, new Color(0.42f, 0.45f, 0.34f, 0.8f));
        clueReadingBody = Text(paper, "Clue Reading Body", string.Empty, 18f,
            new Color(0.93f, 0.91f, 0.78f, 1f), FontStyles.Normal, TextAlignmentOptions.TopLeft,
            new Vector2(0f, 1f), new Vector2(674f, 126f), new Vector2(30f, -27f));
        clueReadingConclusion = Text(paper, "Clue Reading Conclusion", string.Empty, 15f, Mint,
            FontStyles.Bold, TextAlignmentOptions.Left, new Vector2(0f, 0f),
            new Vector2(674f, 44f), new Vector2(30f, 29f));

        Text(panel, "Clue Reading Close Hint", "[SPACE / E]  CẤT MANH MỐI", 13f, Muted, FontStyles.Bold,
            TextAlignmentOptions.Right, new Vector2(1f, 0f), new Vector2(310f, 28f), new Vector2(-38f, 24f));
        clueReadingRoot.SetActive(false);
    }

    private void BuildCompletionNotice()
    {
        completionRoot = new GameObject("Quest Completion Notice", typeof(RectTransform), typeof(CanvasGroup));
        completionRoot.transform.SetParent(canvas.transform, false);
        Stretch(completionRoot.GetComponent<RectTransform>());
        completionGroup = completionRoot.GetComponent<CanvasGroup>();
        completionGroup.interactable = false;
        completionGroup.blocksRaycasts = true;

        StretchBox("Completion Dimmer", completionRoot.transform, new Color(0f, 0f, 0f, 0.62f));
        RectTransform panel = Box("Completion Panel", completionRoot.transform, new Vector2(0.5f, 0.5f),
            new Vector2(720f, 330f), new Vector2(0f, 25f), Ink);
        AddBorder(panel, new Color(Amber.r, Amber.g, Amber.b, 0.9f));
        Box("Completion Top Accent", panel, new Vector2(0.5f, 1f), new Vector2(720f, 6f),
            new Vector2(0f, -3f), Amber);
        Text(panel, "Completion Header", "HOÀN THÀNH NHIỆM VỤ", 15f, Mint, FontStyles.Bold,
            TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(620f, 28f), new Vector2(0f, -38f));
        completionQuestName = Text(panel, "Completed Quest Name", string.Empty, 30f, Color.white, FontStyles.Bold,
            TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(650f, 54f), new Vector2(0f, -78f));

        completionRewardCard = Box("Completion Reward Card", panel, new Vector2(0.5f, 0f),
            new Vector2(520f, 112f), new Vector2(0f, 45f), new Color(0.09f, 0.15f, 0.12f, 1f));
        AddBorder(completionRewardCard, new Color(Mint.r, Mint.g, Mint.b, 0.88f));
        Text(completionRewardCard, "Completion Reward Header", "PHẦN THƯỞNG ĐÃ NHẬN", 12f, Mint, FontStyles.Bold,
            TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(470f, 24f), new Vector2(0f, -18f));
        completionRewardText = Text(completionRewardCard, "Completion Reward Text", string.Empty, 23f,
            Color.white, FontStyles.Bold, TextAlignmentOptions.Center, new Vector2(0.5f, 0f),
            new Vector2(470f, 42f), new Vector2(0f, 18f));

        Vector2[] sparklePositions =
        {
            new Vector2(-300f, 112f), new Vector2(300f, 112f), new Vector2(-330f, 10f), new Vector2(330f, 10f),
            new Vector2(-250f, -112f), new Vector2(250f, -112f), new Vector2(-170f, 130f), new Vector2(170f, 130f)
        };
        for (int i = 0; i < completionSparkles.Length; i++)
        {
            RectTransform sparkle = Box("Reward Sparkle " + (i + 1), panel, new Vector2(0.5f, 0.5f),
                new Vector2(i % 2 == 0 ? 13f : 9f, i % 2 == 0 ? 34f : 24f), sparklePositions[i],
                i % 3 == 0 ? Amber : Color.white);
            sparkle.localRotation = Quaternion.Euler(0f, 0f, 45f);
            completionSparkles[i] = sparkle;
        }

        completionRoot.SetActive(false);
    }

    private void PlayQuestCompletion(string header, string questName, string reward, Action onFinished)
    {
        if (completionRoutine != null)
            StopCoroutine(completionRoutine);
        completionRoutine = StartCoroutine(QuestCompletionRoutine(header, questName, reward, onFinished));
    }

    private IEnumerator QuestCompletionRoutine(string header, string questName, string reward, Action onFinished)
    {
        SetJournalOpen(false);
        if (mapPrototype != null) mapPrototype.SetOpen(false);
        completionQuestName.text = questName;
        completionRewardText.text = reward;
        Transform headerTransform = FindChild(completionRoot.transform, "Completion Header");
        if (headerTransform != null)
            headerTransform.GetComponent<TextMeshProUGUI>().text = header;
        completionRewardCard.gameObject.SetActive(false);
        completionRoot.SetActive(true);
        completionRoot.transform.SetAsLastSibling();
        completionGroup.alpha = 0f;

        float elapsed = 0f;
        while (elapsed < 0.24f)
        {
            elapsed += Time.unscaledDeltaTime;
            completionGroup.alpha = Mathf.Clamp01(elapsed / 0.24f);
            yield return null;
        }
        yield return new WaitForSecondsRealtime(0.7f);

        completionRewardCard.gameObject.SetActive(true);
        elapsed = 0f;
        while (elapsed < 1.15f)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / 0.38f);
            float overshoot = t < 0.8f ? Mathf.Lerp(0.62f, 1.08f, t / 0.8f) : Mathf.Lerp(1.08f, 1f, (t - 0.8f) / 0.2f);
            completionRewardCard.localScale = Vector3.one * overshoot;
            for (int i = 0; i < completionSparkles.Length; i++)
            {
                float pulse = 0.45f + Mathf.Abs(Mathf.Sin(elapsed * 5.5f + i * 0.72f)) * 0.85f;
                completionSparkles[i].localScale = Vector3.one * pulse;
                completionSparkles[i].localRotation = Quaternion.Euler(0f, 0f, 45f + elapsed * (50f + i * 4f));
            }
            yield return null;
        }
        yield return new WaitForSecondsRealtime(0.75f);

        elapsed = 0f;
        while (elapsed < 0.32f)
        {
            elapsed += Time.unscaledDeltaTime;
            completionGroup.alpha = 1f - Mathf.Clamp01(elapsed / 0.32f);
            yield return null;
        }
        completionRoot.SetActive(false);
        completionRewardCard.localScale = Vector3.one;
        completionRoutine = null;
        onFinished?.Invoke();
    }

    private void BuildJournal()
    {
        journalRoot = new GameObject("Quest Journal", typeof(RectTransform));
        journalRoot.transform.SetParent(canvas.transform, false);
        RectTransform root = journalRoot.GetComponent<RectTransform>();
        Stretch(root);

        StretchBox("Dimmer", root, new Color(0f, 0f, 0f, 0.72f));
        RectTransform shellShadow = Box("Journal Shadow", root, new Vector2(0.5f, 0.5f),
            new Vector2(1400f, 760f), new Vector2(12f, -14f), new Color(0f, 0f, 0f, 0.56f));
        shellShadow.SetAsFirstSibling();
        RectTransform shell = Box("Journal Shell", root, new Vector2(0.5f, 0.5f),
            new Vector2(1400f, 760f), Vector2.zero, new Color(0.035f, 0.04f, 0.04f, 0.985f));
        AddBorder(shell, new Color(0.34f, 0.36f, 0.35f, 0.9f));

        Text(shell, "Journal Title", "NHẬT KÝ NHIỆM VỤ", 27f, Color.white, FontStyles.Bold,
            TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(520f, 42f), new Vector2(44f, -38f));
        Text(shell, "Journal Subtitle", "Ngày 01", 13f, Muted, FontStyles.Normal,
            TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(200f, 24f), new Vector2(44f, -72f));

        RectTransform closeHint = Box("Close Hint", shell, new Vector2(1f, 1f), new Vector2(112f, 40f),
            new Vector2(-42f, -38f), Color.clear);
        Text(closeHint, "Close Text", "[J]  ĐÓNG", 14f, Color.white, FontStyles.Bold,
            TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(108f, 36f), Vector2.zero);
        MakeClickable(closeHint, () => SetJournalOpen(false));

        RectTransform content = Box("Content", shell, new Vector2(0.5f, 0.5f), new Vector2(1328f, 590f),
            new Vector2(0f, -64f), Color.clear);
        activeContentRoot = new GameObject("Active Quest Content", typeof(RectTransform));
        activeContentRoot.transform.SetParent(content, false);
        Stretch(activeContentRoot.GetComponent<RectTransform>());

        BuildTabs(activeContentRoot.transform);
        BuildQuestList(activeContentRoot.transform);
        BuildQuestDetails(activeContentRoot.transform);
        BuildEmptyState(content);

        SelectQuest(0);
        SelectTab(0);
        SetJournalOpen(false);
    }

    private void BuildTabs(Transform shell)
    {
        const float tabWidth = 190f;
        string[] labels = { "ĐANG LÀM  02", "HOÀN THÀNH  00" };
        RectTransform tabs = Box("Tabs", shell, new Vector2(0f, 1f), new Vector2(410f, 48f),
            Vector2.zero, Color.clear);

        for (int i = 0; i < tabRects.Length; i++)
        {
            int capturedIndex = i;
            RectTransform tab = Box("Tab " + i, tabs, new Vector2(0f, 0.5f), new Vector2(tabWidth, 44f),
                new Vector2((tabWidth + 18f) * i, 0f), Color.clear);
            tabRects[i] = tab;
            RectTransform underline = Box("Tab Underline " + i, tab, new Vector2(0f, 0f),
                new Vector2(112f, 2f), Vector2.zero, Amber);
            tabUnderlines[i] = underline.gameObject;
            tabTexts[i] = Text(tab, "Tab Text " + i, labels[i], 13f, Muted, FontStyles.Bold,
                TextAlignmentOptions.Left, new Vector2(0f, 0.5f), new Vector2(tabWidth - 4f, 40f), Vector2.zero);
            MakeClickable(tab, () => SelectTab(capturedIndex));
        }
    }

    private void BuildQuestList(Transform content)
    {
        RectTransform left = Box("Quest List", content, new Vector2(0f, 0.5f), new Vector2(410f, 520f),
            new Vector2(0f, -35f), Color.clear);
        questListRoot = left.gameObject;
        questListDivider = Box("List Right Rule", content, new Vector2(0f, 0.5f), new Vector2(1f, 540f),
            new Vector2(438f, -25f), new Color(0.32f, 0.34f, 0.33f, 0.55f)).gameObject;

        mainQuestHeader = Text(left, "Main Quest Header", "TUYẾN THOÁT HIỂM B  •  CỐT TRUYỆN", 11f, Amber, FontStyles.Bold,
            TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(180f, 20f), new Vector2(18f, -22f)).gameObject;

        mainQuestCard = Box("Main Quest Card", left, new Vector2(0f, 1f), new Vector2(410f, 96f),
            new Vector2(0f, -50f), new Color(0.105f, 0.11f, 0.105f, 1f));
        mainQuestCardImage = mainQuestCard.GetComponent<Image>();
        mainQuestAccent = Box("Main Quest Accent", mainQuestCard, new Vector2(0f, 0.5f),
            new Vector2(4f, 96f), new Vector2(2f, 0f), Amber).GetComponent<Image>();
        RectTransform questIcon = Box("Main Quest Icon", mainQuestCard, new Vector2(0f, 0.5f),
            new Vector2(22f, 22f), new Vector2(28f, 0f), new Color(0.35f, 0.18f, 0.42f, 1f));
        questIcon.localRotation = Quaternion.Euler(0f, 0f, 45f);
        Box("Main Quest Icon Core", questIcon, new Vector2(0.5f, 0.5f), new Vector2(11f, 11f),
            Vector2.zero, new Color(0.95f, 0.82f, 0.24f));
        TextMeshProUGUI mainName = Text(mainQuestCard, "Main Quest Name", "Lần theo tín hiệu quân sự", 16f,
            Color.white, FontStyles.Bold, TextAlignmentOptions.Left, new Vector2(0f, 1f),
            new Vector2(260f, 30f), new Vector2(54f, -20f));
        mainQuestNameRect = mainName.rectTransform;
        mainQuestMetaText = Text(mainQuestCard, "Main Quest Meta", "0 / 3", 12f,
            Muted, FontStyles.Bold, TextAlignmentOptions.Right, new Vector2(1f, 1f),
            new Vector2(64f, 22f), new Vector2(-18f, -20f));
        mainQuestMetaRect = mainQuestMetaText.rectTransform;
        mainQuestTrackedMarker = Text(mainQuestCard, "Main Quest Tracked Marker", "●  ĐANG THEO DÕI", 10f,
            Amber, FontStyles.Bold, TextAlignmentOptions.Left, new Vector2(0f, 0f),
            new Vector2(180f, 20f), new Vector2(54f, 15f)).gameObject;
        MakeClickable(mainQuestCard, () => SelectQuest(0));

        sideQuestHeader = Text(left, "Side Quest Header", "TIẾN ĐỘ TUYẾN B", 13f, Mint, FontStyles.Bold,
            TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(180f, 20f), new Vector2(18f, -186f)).gameObject;
        sideQuestCard = Box("Side Quest Card", left, new Vector2(0f, 1f), new Vector2(410f, 84f),
            new Vector2(0f, -214f), new Color(0.065f, 0.075f, 0.072f, 1f));
        sideQuestCardImage = sideQuestCard.GetComponent<Image>();
        sideQuestAccent = Box("Side Quest Accent", sideQuestCard, new Vector2(0f, 0.5f),
            new Vector2(4f, 84f), new Vector2(2f, 0f), new Color(Mint.r, Mint.g, Mint.b, 0.35f)).GetComponent<Image>();
        TextMeshProUGUI sideName = Text(sideQuestCard, "Side Quest Name", "Thu thập hồ sơ sơ tán", 15f,
            Color.white, FontStyles.Bold, TextAlignmentOptions.Left, new Vector2(0f, 1f),
            new Vector2(290f, 26f), new Vector2(24f, -17f));
        sideQuestNameRect = sideName.rectTransform;
        sideQuestMetaText = Text(sideQuestCard, "Side Quest Meta", "0 / 3", 12f,
            Muted, FontStyles.Bold, TextAlignmentOptions.Right, new Vector2(1f, 1f),
            new Vector2(64f, 22f), new Vector2(-18f, -17f));
        sideQuestMetaRect = sideQuestMetaText.rectTransform;
        sideQuestTrackedMarker = Text(sideQuestCard, "Side Quest Tracked Marker", "●  ĐANG THEO DÕI", 10f,
            Mint, FontStyles.Bold, TextAlignmentOptions.Left, new Vector2(0f, 0f),
            new Vector2(180f, 20f), new Vector2(24f, 13f)).gameObject;
        MakeClickable(sideQuestCard, () => SelectQuest(1));

        carQuestHeader = Text(left, "Car Quest Header", "TUYẾN THOÁT HIỂM A  •  CHIẾC XE", 11f, Mint,
            FontStyles.Bold, TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(240f, 20f),
            new Vector2(18f, -186f)).gameObject;
        carQuestCard = Box("Car Repair Quest Card", left, new Vector2(0f, 1f), new Vector2(410f, 84f),
            new Vector2(0f, -214f), new Color(0.065f, 0.075f, 0.072f, 1f));
        carQuestCardImage = carQuestCard.GetComponent<Image>();
        carQuestAccent = Box("Car Repair Quest Accent", carQuestCard, new Vector2(0f, 0.5f),
            new Vector2(4f, 84f), new Vector2(2f, 0f), new Color(Mint.r, Mint.g, Mint.b, 0.35f)).GetComponent<Image>();
        Text(carQuestCard, "Car Repair Quest Name", "Khôi phục chiếc xe", 15f, Color.white,
            FontStyles.Bold, TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(290f, 26f),
            new Vector2(24f, -17f));
        carQuestMetaText = Text(carQuestCard, "Car Repair Quest Meta", "ĐANG CHUẨN BỊ", 11f, Muted,
            FontStyles.Bold, TextAlignmentOptions.Right, new Vector2(1f, 1f), new Vector2(90f, 22f),
            new Vector2(-18f, -17f));
        carQuestTrackedMarker = Text(carQuestCard, "Car Repair Quest Tracked Marker", "●  ĐANG THEO DÕI", 10f,
            Mint, FontStyles.Bold, TextAlignmentOptions.Left, new Vector2(0f, 0f),
            new Vector2(180f, 20f), new Vector2(24f, 13f)).gameObject;
        MakeClickable(carQuestCard, () => SelectQuest(2));

        // These hidden values preserve the preview/debug API without putting the old
        // inventory-heavy context panel back into the simplified layout.
        RectTransform stateCache = Box("Quest Context State", left, new Vector2(0f, 0f),
            new Vector2(1f, 1f), Vector2.zero, Color.clear);
        contextPanelTitle = Text(stateCache, "Context Panel Title", string.Empty, 1f, Color.clear,
            FontStyles.Normal, TextAlignmentOptions.Left, Vector2.zero, Vector2.one, Vector2.zero);
        contextPanelCount = Text(stateCache, "Context Panel Count", string.Empty, 1f, Color.clear,
            FontStyles.Normal, TextAlignmentOptions.Left, Vector2.zero, Vector2.one, Vector2.zero);
    }

    private void BuildQuestDetails(Transform content)
    {
        RectTransform right = Box("Quest Details", content, new Vector2(1f, 0.5f), new Vector2(855f, 540f),
            new Vector2(0f, -25f), Color.clear);
        questDetailsRoot = right.gameObject;

        detailEyebrow = Text(right, "Detail Eyebrow", string.Empty, 11f, Amber, FontStyles.Bold,
            TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(620f, 22f), new Vector2(0f, -4f));
        detailTitle = Text(right, "Detail Title", string.Empty, 31f, Color.white, FontStyles.Bold,
            TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(820f, 58f), new Vector2(0f, -38f));

        storyText = Text(right, "Story", string.Empty, 14f, new Color(0.75f, 0.78f, 0.77f),
            FontStyles.Normal, TextAlignmentOptions.TopLeft, new Vector2(0f, 1f),
            new Vector2(820f, 52f), new Vector2(0f, -108f));

        Text(right, "Objectives Header", "MỤC TIÊU HIỆN TẠI", 11f, Muted, FontStyles.Bold,
            TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(260f, 22f), new Vector2(0f, -184f));
        currentObjectiveText = Text(right, "Current Objective", string.Empty, 18f, Color.white, FontStyles.Bold,
            TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(680f, 34f), new Vector2(0f, -216f));
        currentObjectiveProgressText = Text(right, "Current Objective Progress", string.Empty, 13f, Amber,
            FontStyles.Bold, TextAlignmentOptions.Right, new Vector2(1f, 1f),
            new Vector2(125f, 30f), new Vector2(0f, -216f));

        RectTransform progressBar = Box("Current Objective Progress Bar", right, new Vector2(0f, 1f),
            new Vector2(855f, 5f), new Vector2(0f, -264f), new Color(0.16f, 0.17f, 0.17f, 1f));
        objectiveProgressFill = Box("Current Objective Progress Fill", progressBar, new Vector2(0f, 0.5f),
            new Vector2(0f, 5f), Vector2.zero, Amber).GetComponent<Image>();
        objectiveProgressFill.rectTransform.pivot = new Vector2(0f, 0.5f);

        RectTransform reward = Box("Progress Reward", right, new Vector2(0f, 1f), new Vector2(855f, 68f),
            new Vector2(0f, -306f), new Color(0.065f, 0.07f, 0.068f, 1f));
        AddBorder(reward, new Color(0.23f, 0.25f, 0.24f, 0.65f));
        rewardLabel = Text(reward, "Reward Label", string.Empty, 10f, Muted, FontStyles.Bold,
            TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(180f, 18f), new Vector2(18f, -10f));
        rewardText = Text(reward, "Reward Text", string.Empty, 15f, Color.white, FontStyles.Bold,
            TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(520f, 28f), new Vector2(18f, -34f));

        BuildCarRepairRequirements(right);

        const float actionWidth = 188f;
        const float actionHeight = 48f;
        RectTransform tracking = Box("Tracking Button", right, new Vector2(1f, 0f),
            new Vector2(actionWidth, actionHeight), new Vector2(-206f, 0f), new Color(0.93f, 0.93f, 0.9f, 1f));
        trackingButtonImage = tracking.GetComponent<Image>();
        trackingButtonText = Text(tracking, "Tracking Button Text", "[V]  THEO DÕI", 13f,
            new Color(0.05f, 0.055f, 0.053f, 1f), FontStyles.Bold, TextAlignmentOptions.Center,
            new Vector2(0.5f, 0.5f), new Vector2(actionWidth - 12f, actionHeight - 8f), Vector2.zero);
        trackingButton = MakeClickable(tracking, ToggleSelectedQuestTracking);

        RectTransform openMap = Box("Open Map Button", right, new Vector2(1f, 0f),
            new Vector2(actionWidth, actionHeight), Vector2.zero, new Color(0.045f, 0.05f, 0.048f, 1f));
        AddBorder(openMap, new Color(0.54f, 0.56f, 0.55f, 0.9f));
        Text(openMap, "Open Map Button Text", "[M]  MỞ BẢN ĐỒ", 13f, Color.white, FontStyles.Bold,
            TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f),
            new Vector2(actionWidth - 12f, actionHeight - 8f), Vector2.zero);
        MakeClickable(openMap, OpenMapFromJournal);

        // Objective state cache keeps quest-flow tests and data inspection useful,
        // while only the current objective is rendered in the player-facing screen.
        RectTransform objectiveCache = Box("Objective State Cache", right, Vector2.zero,
            Vector2.one, Vector2.zero, Color.clear);
        for (int i = 0; i < 3; i++)
            BuildObjectiveRow(objectiveCache, i, new Vector2(0f, -60f * i));
        objectiveCache.gameObject.SetActive(false);
    }

    private void BuildCarRepairRequirements(Transform parent)
    {
        carRepairRequirementsRoot = new GameObject("Car Repair Requirements", typeof(RectTransform));
        carRepairRequirementsRoot.transform.SetParent(parent, false);
        RectTransform root = carRepairRequirementsRoot.GetComponent<RectTransform>();
        SetRect(root, new Vector2(0f, 1f), new Vector2(855f, 104f), new Vector2(0f, -384f));

        Text(root, "Car Requirements Header", "VẬT PHẨM CẦN TÌM  •  CẬP NHẬT THEO TÚI ĐỒ", 10f,
            Muted, FontStyles.Bold, TextAlignmentOptions.Left, new Vector2(0f, 1f),
            new Vector2(500f, 20f), Vector2.zero);

        BuildCarRequirementCard(root, 0, ArrivalCarItemKind.Toolbox,
            "Story/CarUI/Toolbox", "BỘ DỤNG CỤ", true,
            new[] { "ArrivalCarToolbox", "Bộ dụng cụ sửa xe", "Toolbox", "Bộ dụng cụ" });
        BuildCarRequirementCard(root, 1, ArrivalCarItemKind.Hammer,
            "Story/CarUI/Hammer", "BÚA SỬA", true,
            new[] { "ArrivalCarHammer", "Búa sửa chữa", "Hammer", "Búa" });
        BuildCarRequirementCard(root, 2, ArrivalCarItemKind.FuelCan,
            "Story/CarUI/GasCan", "NHIÊN LIỆU", true,
            new[] { "ArrivalCarFuelCan", "Can nhiên liệu" });
        BuildCarRequirementCard(root, 3, ArrivalCarItemKind.Battery,
            "Story/CarUI/CarBattery", "ẮC QUY", false,
            new[] { "ArrivalCarBattery", "Ắc quy xe", "CarBattery", "Ắc quy" });
        BuildCarRequirementCard(root, 4, ArrivalCarItemKind.Tire,
            "Story/CarUI/CarTire", "LỐP XE", false,
            new[] { "ArrivalCarTire", "Lốp xe", "CarTire", "Lốp" });
        carRepairRequirementsRoot.SetActive(false);
    }

    private void BuildCarRequirementCard(Transform parent, int index, ArrivalCarItemKind itemKind,
        string texturePath, string label, bool required, string[] inventoryNames)
    {
        const float width = 161f;
        RectTransform card = Box("Car Requirement " + label, parent, new Vector2(0f, 1f),
            new Vector2(width, 70f), new Vector2(index * 173.5f, -28f),
            new Color(0.055f, 0.06f, 0.058f, 1f));
        AddBorder(card, new Color(0.25f, 0.28f, 0.27f, 0.85f));

        Texture2D texture = Resources.Load<Texture2D>(texturePath);
        if (texture != null)
        {
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
        }
        GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(RawImage));
        iconObject.transform.SetParent(card, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        SetRect(iconRect, new Vector2(0f, 0.5f), new Vector2(38f, 38f), new Vector2(12f, 0f));
        RawImage icon = iconObject.GetComponent<RawImage>();
        icon.texture = texture;
        icon.color = texture != null ? Color.white : Muted;
        icon.raycastTarget = false;

        Text(card, "Item Name", label, 10f, Color.white, FontStyles.Bold,
            TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(101f, 22f), new Vector2(54f, -9f));
        Text(card, "Requirement Type", required ? "BẮT BUỘC" : "KHÔNG BẮT BUỘC", 8f,
            required ? Amber : Muted, FontStyles.Bold, TextAlignmentOptions.Left,
            new Vector2(0f, 0.5f), new Vector2(101f, 18f), new Vector2(54f, 2f));
        TextMeshProUGUI state = Text(card, "Inventory State", "THIẾU", 9f,
            new Color(0.95f, 0.25f, 0.2f), FontStyles.Bold, TextAlignmentOptions.Left,
            new Vector2(0f, 0f), new Vector2(101f, 20f), new Vector2(54f, 7f));

        carRepairRequirementViews.Add(new CarRepairRequirementView
        {
            Required = required,
            ItemKind = itemKind,
            InventoryNames = inventoryNames,
            CardBackground = card.GetComponent<Image>(),
            StateText = state
        });
    }

    private void RefreshCarRepairRequirementStates()
    {
        if (carRepairRequirementViews.Count == 0) return;
        int requiredOwned = 0;
        int requiredTotal = 0;

        for (int i = 0; i < carRepairRequirementViews.Count; i++)
        {
            CarRepairRequirementView view = carRepairRequirementViews[i];
            bool applied = IsArrivalCarItemApplied(view.ItemKind, mainQuestProgress.ArrivalCarRepairMask);
            bool available = applied ||
                             (carRepairInventoryQuery != null && carRepairInventoryQuery(view.InventoryNames));
            if (view.Required)
            {
                requiredTotal++;
                if (available) requiredOwned++;
            }

            bool retainedTool = applied &&
                                (view.ItemKind == ArrivalCarItemKind.Toolbox ||
                                 view.ItemKind == ArrivalCarItemKind.Hammer);
            view.StateText.text = retainedTool
                ? "GIỮ LẠI"
                : applied
                    ? view.ItemKind == ArrivalCarItemKind.FuelCan ? "ĐÃ DÙNG" : "ĐÃ LẮP"
                    : available ? "ĐÃ CÓ" : view.Required ? "THIẾU" : "CHƯA LẮP";
            view.StateText.color = available
                ? Mint
                : view.Required ? new Color(0.95f, 0.25f, 0.2f) : Muted;
            view.CardBackground.color = available
                ? new Color(Mint.r, Mint.g, Mint.b, 0.11f)
                : new Color(0.055f, 0.06f, 0.058f, 1f);
        }

        if (selectedQuestIndex == 2 && !mainQuestProgress.ArrivalCarRepaired && requiredTotal > 0)
        {
            SetCurrentObjective("Thu thập vật phẩm bắt buộc để sửa xe",
                requiredOwned + " / " + requiredTotal + " BẮT BUỘC",
                requiredOwned / (float)requiredTotal, requiredOwned == requiredTotal ? Mint : Amber);
        }
    }

    private static bool IsArrivalCarItemApplied(ArrivalCarItemKind kind, int repairMask)
    {
        ArrivalCarRepairAction action = kind switch
        {
            ArrivalCarItemKind.Toolbox => ArrivalCarRepairAction.RepairCore,
            ArrivalCarItemKind.Hammer => ArrivalCarRepairAction.RepairCore,
            ArrivalCarItemKind.FuelCan => ArrivalCarRepairAction.AddFuel,
            ArrivalCarItemKind.Battery => ArrivalCarRepairAction.ReplaceBattery,
            _ => ArrivalCarRepairAction.ReplaceTire
        };
        return ArrivalCarRepairRules.IsApplied(repairMask, action);
    }

    private void BuildObjectiveRow(Transform parent, int index, Vector2 topLeft)
    {
        string number = (index + 1).ToString("00");
        RectTransform row = Box("Objective " + number, parent, new Vector2(0f, 1f), new Vector2(630f, 48f),
            topLeft, Panel);
        objectiveStates[index] = Box("State", row, new Vector2(0f, 0.5f), new Vector2(24f, 24f),
            new Vector2(24f, 0f), new Color(0.18f, 0.23f, 0.22f)).GetComponent<Image>();
        objectiveNumbers[index] = Text(row, "Number", number, 11f, Muted, FontStyles.Bold,
            TextAlignmentOptions.Center, new Vector2(0f, 0.5f), new Vector2(24f, 24f), new Vector2(24f, 0f));
        objectiveLabels[index] = Text(row, "Label", string.Empty, 15f, new Color(0.7f, 0.75f, 0.73f),
            FontStyles.Normal, TextAlignmentOptions.Left, new Vector2(0f, 0.5f),
            new Vector2(400f, 38f), new Vector2(67f, 0f));
        objectiveStatuses[index] = Text(row, "Status", string.Empty, 11f, Muted, FontStyles.Bold,
            TextAlignmentOptions.Right, new Vector2(1f, 0.5f), new Vector2(145f, 36f),
            new Vector2(-16f, 0f));
    }

    private void BuildMapCard(Transform parent)
    {
        RectTransform card = Box("Map Preview", parent, new Vector2(1f, 0f), new Vector2(340f, 276f),
            new Vector2(-32f, 32f), new Color(0.028f, 0.05f, 0.048f, 1f));
        AddBorder(card, new Color(0.3f, 0.39f, 0.36f, 0.9f));
        mapLabel = Text(card, "Map Label", string.Empty, 11f, Muted, FontStyles.Bold,
            TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(290f, 22f), new Vector2(18f, -18f));

        RectTransform route = Box("Route", card, new Vector2(0.5f, 0.5f), new Vector2(290f, 20f),
            new Vector2(2f, -15f), new Color(0.18f, 0.22f, 0.21f));
        route.localRotation = Quaternion.Euler(0f, 0f, 17f);
        miniMapRouteImage = route.GetComponent<Image>();
        RectTransform home = Box("Home", card, new Vector2(0f, 0f), new Vector2(70f, 42f), new Vector2(55f, 64f),
            new Color(0.18f, 0.3f, 0.26f));
        Text(home, "Home Label", "NHÀ", 10f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center,
            new Vector2(0.5f, 0.5f), new Vector2(64f, 30f), Vector2.zero);
        miniMapApproximateArea = new GameObject("Approximate Office Area", typeof(RectTransform), typeof(Image));
        miniMapApproximateArea.transform.SetParent(card, false);
        SetRect(miniMapApproximateArea.GetComponent<RectTransform>(), new Vector2(1f, 1f),
            new Vector2(118f, 86f), new Vector2(-76f, -88f));
        miniMapApproximateArea.GetComponent<Image>().color = new Color(Amber.r, Amber.g, Amber.b, 0.16f);
        AddBorder(miniMapApproximateArea.GetComponent<RectTransform>(), new Color(Amber.r, Amber.g, Amber.b, 0.75f));
        Text(miniMapApproximateArea.transform, "Approximate Office Label", "VÙNG ?", 10f, Amber, FontStyles.Bold,
            TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(100f, 50f), Vector2.zero);

        RectTransform office = Box("Office", card, new Vector2(1f, 1f), new Vector2(88f, 52f),
            new Vector2(-62f, -72f), new Color(0.32f, 0.17f, 0.39f));
        miniMapOffice = office.gameObject;
        Text(office, "Office Label", "VĂN PHÒNG", 9f, Color.white, FontStyles.Bold,
            TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(82f, 38f), Vector2.zero);
        Text(card, "Player Label", "BẠN", 10f, Mint, FontStyles.Bold, TextAlignmentOptions.Center,
            new Vector2(0.5f, 0.5f), new Vector2(54f, 26f), new Vector2(-38f, -6f));
        mapFooter = Text(card, "Map Footer", string.Empty, 10f, new Color(0.38f, 0.43f, 0.42f),
            FontStyles.Bold, TextAlignmentOptions.Left, new Vector2(0f, 0f),
            new Vector2(185f, 22f), new Vector2(14f, 20f));
        Text(card, "Open Map Hint", "[M] MỞ BẢN ĐỒ", 10f, Color.white, FontStyles.Bold,
            TextAlignmentOptions.Right, new Vector2(1f, 0f), new Vector2(120f, 22f), new Vector2(-14f, 20f));
    }

    private void BuildEmptyState(Transform content)
    {
        emptyStateRoot = new GameObject("Active Empty State", typeof(RectTransform));
        emptyStateRoot.transform.SetParent(content, false);
        Stretch(emptyStateRoot.GetComponent<RectTransform>());

        RectTransform card = Box("Empty State Card", emptyStateRoot.transform, new Vector2(0.5f, 0.5f),
            new Vector2(620f, 210f), Vector2.zero, Panel);
        AddBorder(card, Border);
        Box("Empty State Accent", card, new Vector2(0.5f, 1f), new Vector2(90f, 3f),
            new Vector2(0f, -39f), new Color(Mint.r, Mint.g, Mint.b, 0.55f));
        emptyStateTitle = Text(card, "Empty State Title", string.Empty, 24f, Color.white, FontStyles.Bold,
            TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(560f, 38f), new Vector2(0f, -72f));
        emptyStateBody = Text(card, "Empty State Body", string.Empty, 15f, Muted, FontStyles.Normal,
            TextAlignmentOptions.Top, new Vector2(0.5f, 1f), new Vector2(540f, 58f), new Vector2(0f, -119f));
    }

    private void SelectQuest(int index)
    {
        int candidate = Wrap(index, 3);
        if (activeContentRoot != null && !QuestBelongsToTab(candidate, selectedTabIndex))
        {
            for (int questIndex = 0; questIndex < 3; questIndex++)
            {
                if (!QuestBelongsToTab(questIndex, selectedTabIndex)) continue;
                candidate = questIndex;
                break;
            }
        }

        selectedQuestIndex = candidate;
        if (detailTitle == null)
            return;

        bool main = selectedQuestIndex == 0;
        bool route = selectedQuestIndex == 1;
        bool car = selectedQuestIndex == 2;
        mainQuestCardImage.color = main
            ? new Color(0.105f, 0.11f, 0.105f, 1f)
            : new Color(0.055f, 0.06f, 0.058f, 1f);
        sideQuestCardImage.color = route
            ? new Color(0.085f, 0.105f, 0.098f, 1f)
            : new Color(0.065f, 0.075f, 0.072f, 1f);
        carQuestCardImage.color = car
            ? new Color(0.085f, 0.105f, 0.098f, 1f)
            : new Color(0.065f, 0.075f, 0.072f, 1f);
        mainQuestAccent.color = main ? Amber : new Color(Amber.r, Amber.g, Amber.b, 0.28f);
        sideQuestAccent.color = route ? Mint : new Color(Mint.r, Mint.g, Mint.b, 0.35f);
        carQuestAccent.color = car ? Mint : new Color(Mint.r, Mint.g, Mint.b, 0.35f);

        if (main) ShowMainQuestDetails();
        else if (route) ShowSideQuestDetails();
        else ShowCarRepairQuestDetails();
    }

    private void ShowMainQuestDetails()
    {
        if (carRepairRequirementsRoot != null) carRepairRequirementsRoot.SetActive(false);
        detailEyebrow.text = lockedEscapeRoute == EscapeEndingRoute.MilitaryEvacuation
            ? "TUYẾN THOÁT HIỂM B  /  ĐÃ KHÓA FINALE"
            : lockedEscapeRoute == EscapeEndingRoute.CivilianCar
                ? "TUYẾN THOÁT HIỂM B  /  KHÔNG CÒN KHẢ DỤNG"
                : "TUYẾN THOÁT HIỂM B  /  SƠ TÁN QUÂN SỰ";
        detailEyebrow.color = Amber;
        detailTitle.text = "LẦN THEO TUYẾN SƠ TÁN CUỐI CÙNG";
        storyText.text = "Chiếc xe đã chết máy. Hãy tìm tài liệu về nguồn vật tư và tuyến sơ tán trong các ngôi nhà xung quanh.";

        bool searchingClues = !mainQuestProgress.HasMapFragment1;
        SetObjective(0, "Tìm 3 tài liệu về tuyến tiếp tế và sơ tán",
            mainQuestProgress.HasMapFragment1
                ? "HOÀN THÀNH"
                : "ĐÃ TÌM THẤY  " + mainQuestProgress.RouteClueCount + " / " +
                  PreMilitaryQuestProgress.RequiredRouteClues + " MANH MỐI",
            searchingClues, mainQuestProgress.HasMapFragment1 ? Mint : Amber);

        string officeLocationStatus;
        if (mainQuestProgress.OfficeDiscovered) officeLocationStatus = "ĐÃ TÌM THẤY";
        else if (mainQuestProgress.HasMapFragment1) officeLocationStatus = "VỊ TRÍ CHÍNH XÁC";
        else officeLocationStatus = "ĐANG KHÓA";
        bool locatingOffice = mainQuestProgress.HasMapFragment1 && !mainQuestProgress.OfficeDiscovered;
        SetObjective(1, "Đối chiếu tài liệu và tìm Văn phòng Điều phối", officeLocationStatus, locatingOffice,
            mainQuestProgress.OfficeDiscovered ? Mint : mainQuestProgress.HasMapFragment1 ? Purple : Muted);

        string investigationStatus = mainQuestProgress.HasMapFragment2
            ? "ĐÃ CÓ MẢNH 2"
            : mainQuestProgress.OfficeInvestigationComplete ? "ĐÃ KIỂM TRA" :
            mainQuestProgress.OfficeDiscovered ? "TÌM MẢNH 2" : "CHƯA MỞ";
        bool investigatingOffice = mainQuestProgress.OfficeDiscovered && !mainQuestProgress.HasMapFragment2;
        SetObjective(2, "Kiểm tra bàn điều phối, radio và tủ hồ sơ", investigationStatus, investigatingOffice,
            mainQuestProgress.HasMapFragment2 ? Mint : mainQuestProgress.OfficeDiscovered ? Purple : Muted);

        rewardLabel.text = mainQuestProgress.MainQuestComplete ? "PHẦN THƯỞNG ĐÃ NHẬN" : "PHẦN THƯỞNG";
        rewardText.text = mainQuestProgress.MainQuestComplete ? "Mảnh bản đồ 2" : "Chưa xác định";

        contextPanelTitle.text = "MANH MỐI NHIỆM VỤ";
        contextPanelCount.text = mainQuestProgress.RouteClueCount + " / " +
                                 PreMilitaryQuestProgress.RequiredRouteClues;

        if (!mainQuestProgress.HasMapFragment1)
            SetCurrentObjective("Tìm 3 tài liệu về tuyến tiếp tế và sơ tán",
                mainQuestProgress.RouteClueCount + " / " + PreMilitaryQuestProgress.RequiredRouteClues,
                mainQuestProgress.RouteClueCount / (float)PreMilitaryQuestProgress.RequiredRouteClues, Amber);
        else if (!mainQuestProgress.OfficeDiscovered)
            SetCurrentObjective("Tìm Văn phòng Điều phối trong khu vực đã xác định",
                mainQuestProgress.HasMapFragment1 ? "ĐÃ XÁC ĐỊNH" : "ĐANG TÌM", 0f, Purple);
        else if (!mainQuestProgress.HasMapFragment2)
            SetCurrentObjective("Lần theo bàn điều phối → radio → tủ hồ sơ", "ĐANG ĐIỀU TRA", 0f, Purple);
        else
            SetCurrentObjective("Nhiệm vụ đã hoàn thành", "HOÀN THÀNH", 1f, Mint);

        UpdateTrackingPresentation();
    }

    private void ShowSideQuestDetails()
    {
        if (carRepairRequirementsRoot != null) carRepairRequirementsRoot.SetActive(false);
        detailEyebrow.text = "TUYẾN THOÁT HIỂM B  /  HỒ SƠ SƠ TÁN";
        detailEyebrow.color = Mint;
        detailTitle.text = "GHÉP LẠI TUYẾN ĐƯỜNG";
        storyText.text = "Thu thập ba dấu vết trong các căn nhà để ghép thành Mảnh bản đồ 1.";

        SetObjective(0, "Thu thập 3 dấu vết tuyến đường",
            mainQuestProgress.RouteClueCount + " / " + PreMilitaryQuestProgress.RequiredRouteClues,
            !mainQuestProgress.SideQuestResolved, mainQuestProgress.HasMapFragment1 ? Mint : Amber);
        SetObjective(1, "Ghép thành Mảnh bản đồ 1",
            mainQuestProgress.HasMapFragment1 ? "ĐÃ GHÉP" :
            mainQuestProgress.SideQuestSkipped ? "ĐÃ BỎ QUA" : "ĐANG KHÓA",
            false, mainQuestProgress.HasMapFragment1 ? Mint : Muted);
        SetObjective(2, "Đánh dấu chính xác vị trí văn phòng",
            mainQuestProgress.HasMapFragment1 ? "ĐÃ ĐÁNH DẤU" :
            mainQuestProgress.SideQuestSkipped ? "ĐÃ TỰ TÌM" : "ĐANG KHÓA",
            false, mainQuestProgress.HasMapFragment1 ? Purple : Muted);
        rewardLabel.text = mainQuestProgress.HasMapFragment1 ? "PHẦN THƯỞNG ĐÃ NHẬN" : "PHẦN THƯỞNG";
        rewardText.text = mainQuestProgress.HasMapFragment1 ? "Mảnh bản đồ 1" : "Chưa xác định";

        contextPanelTitle.text = "DẤU VẾT ĐÃ THU THẬP";
        contextPanelCount.text = mainQuestProgress.RouteClueCount + " / 3";

        if (mainQuestProgress.HasMapFragment1)
            SetCurrentObjective("Mảnh bản đồ 1 đã được ghép", "HOÀN THÀNH", 1f, Mint);
        else if (mainQuestProgress.SideQuestSkipped)
            SetCurrentObjective("Tuyến đường đã được tìm theo cách khác", "ĐÃ BỎ QUA", 1f, Muted);
        else
            SetCurrentObjective("Thu thập 3 dấu vết tuyến đường",
                mainQuestProgress.RouteClueCount + " / " + PreMilitaryQuestProgress.RequiredRouteClues,
                mainQuestProgress.RouteClueCount / (float)PreMilitaryQuestProgress.RequiredRouteClues, Mint);

        UpdateTrackingPresentation();
    }

    private void ShowCarRepairQuestDetails()
    {
        if (carRepairRequirementsRoot != null) carRepairRequirementsRoot.SetActive(true);
        bool coreRepaired = ArrivalCarRepairRules.IsApplied(mainQuestProgress.ArrivalCarRepairMask,
            ArrivalCarRepairAction.RepairCore);
        bool fuelAdded = ArrivalCarRepairRules.IsApplied(mainQuestProgress.ArrivalCarRepairMask,
            ArrivalCarRepairAction.AddFuel);
        int completedRequiredActions = (coreRepaired ? 1 : 0) + (fuelAdded ? 1 : 0);
        detailEyebrow.text = lockedEscapeRoute == EscapeEndingRoute.CivilianCar
            ? "TUYẾN THOÁT HIỂM A  /  ĐÃ KHÓA FINALE"
            : lockedEscapeRoute == EscapeEndingRoute.MilitaryEvacuation
                ? "TUYẾN THOÁT HIỂM A  /  KHÔNG CÒN KHẢ DỤNG"
                : "TUYẾN THOÁT HIỂM A  /  CHIẾC XE DÂN SỰ";
        detailEyebrow.color = Mint;
        detailTitle.text = "KHÔI PHỤC CHIẾC XE";
        storyText.text = "Chiếc xe vẫn có thể hoạt động nếu sửa bộ đề và bổ sung nhiên liệu. " +
                         "Sửa xe để khám phá lối thoát dân sự; ending chỉ bị khóa khi xác nhận vượt vòng phong tỏa.";

        SetObjective(0, "Tìm bộ dụng cụ và búa sửa chữa",
            coreRepaired ? "ĐÃ SỬA BỘ ĐỀ" : "ĐANG THIẾU",
            !coreRepaired, coreRepaired ? Mint : Amber);
        SetObjective(1, "Tìm một can nhiên liệu",
            fuelAdded ? "ĐÃ ĐỔ NHIÊN LIỆU" : "ĐANG THIẾU",
            coreRepaired && !fuelAdded, fuelAdded ? Mint : Amber);
        SetObjective(2, "Quay lại mũi xe để kiểm tra và sửa chữa",
            mainQuestProgress.ArrivalCarRepaired ? "ĐÃ SỬA XONG" : "TÙY CHỌN",
            false, mainQuestProgress.ArrivalCarRepaired ? Mint : Muted);

        rewardLabel.text = mainQuestProgress.ArrivalCarRepaired ? "PHẦN THƯỞNG ĐÃ MỞ" : "PHẦN THƯỞNG";
        rewardText.text = mainQuestProgress.ArrivalCarRepaired
            ? "Phương tiện khám phá đã sẵn sàng"
            : "Mở phương tiện khám phá các lối thoát dân sự";
        contextPanelTitle.text = "TÌNH TRẠNG PHƯƠNG TIỆN";
        contextPanelCount.text = mainQuestProgress.ArrivalCarRepaired ? "HOẠT ĐỘNG" : "HƯ HỎNG";

        SetCurrentObjective(mainQuestProgress.ArrivalCarRepaired
                ? "Chiếc xe đã sẵn sàng"
                : coreRepaired ? "Tìm và đổ can nhiên liệu" : "Tìm dụng cụ sửa chữa và can nhiên liệu",
            mainQuestProgress.ArrivalCarRepaired ? "HOÀN THÀNH" : completedRequiredActions + " / 2",
            completedRequiredActions / 2f,
            mainQuestProgress.ArrivalCarRepaired ? Mint : Amber);
        RefreshCarRepairRequirementStates();
        UpdateTrackingPresentation();
    }

    private void RefreshQuestPresentation()
    {
        if (mainQuestMetaText != null)
        {
            mainQuestMetaText.text = lockedEscapeRoute == EscapeEndingRoute.MilitaryEvacuation
                ? "ĐÃ KHÓA"
                : lockedEscapeRoute == EscapeEndingRoute.CivilianCar
                    ? "ĐÃ ĐÓNG"
                    : mainQuestProgress.MainQuestComplete ? "XONG" : mainQuestProgress.RouteClueCount + " / 3";
        }

        if (sideQuestMetaText != null)
        {
            sideQuestMetaText.text = mainQuestProgress.SideQuestSkipped
                ? "BỎ QUA"
                : mainQuestProgress.HasMapFragment1
                    ? "XONG"
                    : mainQuestProgress.RouteClueCount + " / 3";
        }

        if (carQuestMetaText != null)
            carQuestMetaText.text = lockedEscapeRoute == EscapeEndingRoute.CivilianCar
                ? "ĐÃ KHÓA"
                : lockedEscapeRoute == EscapeEndingRoute.MilitaryEvacuation
                    ? "ĐÃ ĐÓNG"
                    : mainQuestProgress.ArrivalCarRepaired ? "SẴN SÀNG" : "ĐANG CHUẨN BỊ";

        if (trackedQuestIndex >= 0 && !QuestBelongsToTab(trackedQuestIndex, 0))
            trackedQuestIndex = -1;

        UpdateTabCounts();
        SelectTab(selectedTabIndex);

        if (mapPrototype != null)
            mapPrototype.Refresh();

        bool exactOrDiscovered = mainQuestProgress.HasMapFragment1 || mainQuestProgress.OfficeDiscovered;
        if (backdropOfficeMarker != null && officeRevealRoutine == null)
            backdropOfficeMarker.SetActive(exactOrDiscovered);
    }

    private void UpdateMiniMapPreview()
    {
        if (miniMapApproximateArea == null || miniMapOffice == null || miniMapRouteImage == null)
            return;

        OfficeKnowledgeLevel knowledge = mainQuestProgress.OfficeKnowledge;
        bool approximate = knowledge == OfficeKnowledgeLevel.ApproximateArea;
        bool exact = knowledge == OfficeKnowledgeLevel.ExactLocation || knowledge == OfficeKnowledgeLevel.Discovered;
        miniMapApproximateArea.SetActive(approximate);
        miniMapOffice.SetActive(exact);
        miniMapRouteImage.color = exact
            ? new Color(Purple.r, Purple.g, Purple.b, 0.72f)
            : new Color(0.18f, 0.22f, 0.21f);

        if (knowledge == OfficeKnowledgeLevel.Discovered)
        {
            mapLabel.text = "VĂN PHÒNG ĐÃ KHÁM PHÁ";
            mapFooter.text = "ĐỊA ĐIỂM ĐÃ XÁC NHẬN";
        }
        else if (knowledge == OfficeKnowledgeLevel.ExactLocation)
        {
            mapLabel.text = "MẢNH 1  •  VỊ TRÍ CHÍNH XÁC";
            mapFooter.text = "ĐÃ MỞ MARKER VĂN PHÒNG";
        }
        else if (knowledge == OfficeKnowledgeLevel.ApproximateArea)
        {
            mapLabel.text = "MANH MỐI CHÍNH";
            mapFooter.text = "CHỈ BIẾT VÙNG TÌM KIẾM";
        }
        else
        {
            mapLabel.text = "SƠ ĐỒ MANH MỐI";
            mapFooter.text = "VĂN PHÒNG CHƯA XÁC ĐỊNH";
        }
    }

    private void SetBadge(string value, Color color)
    {
        if (statusBadgeImage == null || statusBadgeText == null)
            return;

        statusBadgeImage.color = new Color(color.r, color.g, color.b, 0.16f);
        statusBadgeText.text = value;
        statusBadgeText.color = color;
    }

    private void SetCurrentObjective(string value, string progress, float normalizedProgress, Color accent)
    {
        currentObjectiveText.text = value;
        currentObjectiveProgressText.text = progress;
        currentObjectiveProgressText.color = accent;
        objectiveProgressFill.color = accent;
        objectiveProgressFill.rectTransform.sizeDelta = new Vector2(855f * Mathf.Clamp01(normalizedProgress), 5f);
    }

    private void ToggleSelectedQuestTracking()
    {
        if (!QuestBelongsToTab(selectedQuestIndex, 0))
            return;

        trackedQuestIndex = trackedQuestIndex == selectedQuestIndex ? -1 : selectedQuestIndex;
        UpdateTrackingPresentation();
    }

    private void UpdateTrackingPresentation()
    {
        if (trackingButtonImage == null || trackingButtonText == null)
            return;

        bool selectedIsTracked = trackedQuestIndex == selectedQuestIndex;
        trackingButtonText.text = selectedIsTracked ? "[V]  HỦY THEO DÕI" : "[V]  THEO DÕI";
        trackingButtonImage.color = selectedIsTracked
            ? new Color(1f, 0.67f, 0.14f, 1f)
            : new Color(0.93f, 0.93f, 0.9f, 1f);
        trackingButtonText.color = new Color(0.05f, 0.055f, 0.053f, 1f);

        if (mainQuestTrackedMarker != null)
            mainQuestTrackedMarker.SetActive(trackedQuestIndex == 0);
        if (sideQuestTrackedMarker != null)
            sideQuestTrackedMarker.SetActive(trackedQuestIndex == 1);
        if (carQuestTrackedMarker != null)
            carQuestTrackedMarker.SetActive(trackedQuestIndex == 2);
    }

    public bool TryGetTrackedObjectiveText(out string objective)
    {
        objective = string.Empty;
        if (trackedQuestIndex < 0)
            return false;

        if (trackedQuestIndex == 0)
        {
            if (!mainQuestProgress.HasMapFragment1)
                objective = "Tìm tài liệu về tuyến tiếp tế và sơ tán  •  " +
                            mainQuestProgress.RouteClueCount + "/" + PreMilitaryQuestProgress.RequiredRouteClues;
            else if (!mainQuestProgress.OfficeDiscovered)
                objective = "Tìm Văn phòng Điều phối trong khu vực đã xác định";
            else if (!mainQuestProgress.HasMapFragment2)
                objective = "Lần theo bàn điều phối → radio → tủ hồ sơ";
            else
                return false;
        }
        else if (trackedQuestIndex == 1)
        {
            if (mainQuestProgress.SideQuestResolved)
                return false;
            objective = "Thu thập dấu vết tuyến đường  •  " +
                        mainQuestProgress.RouteClueCount + "/" + PreMilitaryQuestProgress.RequiredRouteClues;
        }
        else
        {
            if (!mainQuestProgress.ArrivalCarRepairUnlocked || mainQuestProgress.ArrivalCarRepaired)
                return false;
            objective = "TUYẾN A: Tìm dụng cụ sửa chữa và can nhiên liệu";
        }

        return true;
    }

    private void OpenMapFromJournal()
    {
        SetJournalOpen(false);
        if (mapPrototype != null)
            mapPrototype.SetOpen(true);
    }

    private void SetObjective(int index, string value, string status, bool highlighted, Color accent)
    {
        objectiveStates[index].color = highlighted ? accent : new Color(accent.r, accent.g, accent.b, 0.28f);
        objectiveNumbers[index].color = highlighted ? Ink : accent;
        objectiveLabels[index].text = value;
        objectiveLabels[index].color = Color.white;
        objectiveLabels[index].fontStyle = highlighted ? FontStyles.Bold : FontStyles.Normal;
        objectiveStatuses[index].text = status;
        objectiveStatuses[index].color = accent;
        objectiveStates[index].transform.parent.GetComponent<Image>().color =
            highlighted ? new Color(0.13f, 0.16f, 0.13f, 1f) : Panel;
    }

    private void SelectTab(int index)
    {
        // Index 2 remains an internal compatibility state for legacy saves and
        // tests, but no Failed tab is rendered or reachable through Q/E.
        selectedTabIndex = Wrap(index, 3);
        if (activeContentRoot == null)
            return;

        UpdateTabCounts();

        for (int i = 0; i < tabRects.Length; i++)
        {
            bool active = i == selectedTabIndex;
            tabRects[i].GetComponent<Image>().color = Color.clear;
            tabUnderlines[i].SetActive(active);
            tabTexts[i].color = active ? Color.white : Muted;
        }

        bool mainVisible = QuestBelongsToTab(0, selectedTabIndex);
        bool sideVisible = QuestBelongsToTab(1, selectedTabIndex);
        bool carVisible = QuestBelongsToTab(2, selectedTabIndex);
        bool showQuestContent = mainVisible || sideVisible || carVisible;
        activeContentRoot.SetActive(true);
        emptyStateRoot.SetActive(!showQuestContent);
        questListRoot.SetActive(showQuestContent);
        if (questListDivider != null) questListDivider.SetActive(showQuestContent);
        questDetailsRoot.SetActive(showQuestContent);
        mainQuestHeader.SetActive(mainVisible);
        mainQuestCard.gameObject.SetActive(mainVisible);
        sideQuestHeader.SetActive(sideVisible);
        sideQuestCard.gameObject.SetActive(sideVisible);
        carQuestHeader.SetActive(carVisible);
        carQuestCard.gameObject.SetActive(carVisible);

        if (showQuestContent)
        {
            if (!QuestBelongsToTab(selectedQuestIndex, selectedTabIndex))
                selectedQuestIndex = mainVisible ? 0 : sideVisible ? 1 : 2;
            SelectQuest(selectedQuestIndex);
        }
        else
        {
            bool completed = selectedTabIndex == 1;
            emptyStateTitle.text = completed ? "CHƯA CÓ NHIỆM VỤ HOÀN THÀNH" : "CHƯA CÓ NHIỆM VỤ THẤT BẠI";
            emptyStateBody.text = completed
                ? "Nhiệm vụ hoàn thành sẽ được lưu tại đây để người chơi xem lại."
                : "Nhiệm vụ thất bại sẽ xuất hiện tại đây cùng nguyên nhân và điều kiện thử lại.";
        }
    }

    private void UpdateTabCounts()
    {
        if (tabTexts[0] == null)
            return;

        string[] labels = { "ĐANG LÀM", "HOÀN THÀNH" };
        for (int i = 0; i < tabTexts.Length; i++)
            tabTexts[i].text = labels[i] + "  " + GetQuestCountForTab(i).ToString("00");
    }

    private int GetQuestCountForTab(int tabIndex)
    {
        int count = 0;
        if (QuestBelongsToTab(0, tabIndex)) count++;
        if (QuestBelongsToTab(1, tabIndex)) count++;
        if (QuestBelongsToTab(2, tabIndex)) count++;
        return count;
    }

    private bool QuestBelongsToTab(int questIndex, int tabIndex)
    {
        if (questIndex == 0)
        {
            if (tabIndex == 0) return !mainQuestProgress.MainQuestComplete;
            if (tabIndex == 1) return mainQuestProgress.MainQuestComplete;
            return false;
        }

        if (questIndex == 1)
            return false; // Legacy duplicate route card is no longer player-facing.

        if (!mainQuestProgress.ArrivalCarRepairUnlocked) return false;
        if (tabIndex == 0) return !mainQuestProgress.ArrivalCarRepaired;
        if (tabIndex == 1) return mainQuestProgress.ArrivalCarRepaired;
        return false;
    }

    private void SetJournalOpen(bool open)
    {
        journalOpen = open;
        if (journalRoot != null)
            journalRoot.SetActive(open);
        if (!open)
            return;

        nextCarInventoryRefreshAt = 0f;
        if (selectedQuestIndex == 2)
            RefreshCarRepairRequirementStates();
    }

    private void RequireElement(List<string> errors, string elementName)
    {
        if (FindChild(transform, elementName) == null)
            errors.Add("Thiếu thành phần UI: " + elementName + ".");
    }

    private static Transform FindChild(Transform parent, string childName)
    {
        if (parent.name == childName)
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform result = FindChild(parent.GetChild(i), childName);
            if (result != null)
                return result;
        }

        return null;
    }

    private static bool RectsOverlap(RectTransform first, RectTransform second)
    {
        if (first == null || second == null)
            return false;

        Vector3[] firstCorners = new Vector3[4];
        Vector3[] secondCorners = new Vector3[4];
        first.GetWorldCorners(firstCorners);
        second.GetWorldCorners(secondCorners);
        Rect firstRect = Rect.MinMaxRect(firstCorners[0].x, firstCorners[0].y, firstCorners[2].x, firstCorners[2].y);
        Rect secondRect = Rect.MinMaxRect(secondCorners[0].x, secondCorners[0].y, secondCorners[2].x, secondCorners[2].y);
        return firstRect.Overlaps(secondRect);
    }

    private static bool Approximately(Vector2 first, Vector2 second)
    {
        return Mathf.Abs(first.x - second.x) < 0.1f && Mathf.Abs(first.y - second.y) < 0.1f;
    }

    private static int Wrap(int value, int count)
    {
        return (value % count + count) % count;
    }

    private RectTransform StretchBox(string name, Transform parent, Color color)
    {
        return StretchBox(name, parent, color, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    }

    private RectTransform StretchBox(string name, Transform parent, Color color, Vector2 anchorMin,
        Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        gameObject.transform.SetParent(parent, false);
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        Image image = gameObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    private RectTransform Box(string name, Transform parent, Vector2 anchor, Vector2 size,
        Vector2 position, Color color)
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

    private static Button MakeClickable(RectTransform target, Action action)
    {
        Image image = target.GetComponent<Image>();
        image.raycastTarget = true;

        Button button = target.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.88f);
        colors.pressedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.55f);
        colors.colorMultiplier = 1f;
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

    private TextMeshProUGUI Text(Transform parent, string name, string value, float size, Color color,
        FontStyles style, TextAlignmentOptions alignment, Vector2 anchor, Vector2 dimensions, Vector2 position)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        gameObject.transform.SetParent(parent, false);
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        SetRect(rect, anchor, dimensions, position);

        TextMeshProUGUI text = gameObject.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.font = font;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
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
