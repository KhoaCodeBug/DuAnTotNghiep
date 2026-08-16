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
    [SerializeField] private bool showNoticeOnAwake = true;
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

    private Canvas canvas;
    private TMP_FontAsset font;
    private CanvasGroup noticeGroup;
    private GameObject noticeRoot;
    private GameObject journalRoot;
    private GameObject activeContentRoot;
    private GameObject emptyStateRoot;
    private TextMeshProUGUI emptyStateTitle;
    private TextMeshProUGUI emptyStateBody;
    private Coroutine noticeRoutine;
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

    private bool built;
    private bool journalOpen;
    private int selectedQuestIndex;
    private int selectedTabIndex;
    private int demoHouseSequence;
    private int demoClueSequence;

    public bool IsJournalOpen => journalOpen;
    public int SelectedQuestIndex => selectedQuestIndex;
    public int SelectedTabIndex => selectedTabIndex;
    public string CurrentDetailTitle => detailTitle == null ? string.Empty : detailTitle.text;
    public string CurrentContextPanelTitle => contextPanelTitle == null ? string.Empty : contextPanelTitle.text;
    public bool IsEmptyStateVisible => emptyStateRoot != null && emptyStateRoot.activeSelf;
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
    public string CurrentRewardLabel => rewardLabel == null ? string.Empty : rewardLabel.text;
    public string CurrentRewardText => rewardText == null ? string.Empty : rewardText.text;
    public bool IsQuestOverlayOpen => IsJournalOpen || IsMapOpen || IsClueReadingOpen ||
                                      (completionRoot != null && completionRoot.activeSelf);

    private void Awake()
    {
        Application.targetFrameRate = 60;
        EnsureBuiltForTests();

        if (Application.isPlaying && showNoticeOnAwake)
            ReplayNotice();
        else if (!Application.isPlaying && showNoticeOnAwake)
            ShowNoticeImmediately();
        else
            HideNoticeImmediately();
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
                if (openMap) DismissQuestNotice();
                mapPrototype.SetOpen(openMap);
            }
        }

        // Preview-only shortcuts. They are intentionally separate from production bindings.
        if (enablePreviewShortcuts && Input.GetKeyDown(KeyCode.F1))
        {
            SetJournalOpen(false);
            ReplayNotice();
        }

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

        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
            SelectQuest(selectedQuestIndex - 1);
        if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
            SelectQuest(selectedQuestIndex + 1);
        if (Input.GetKeyDown(KeyCode.Q))
            SelectTab(Wrap(selectedTabIndex - 1, 2));
        if (Input.GetKeyDown(KeyCode.E))
            SelectTab(Wrap(selectedTabIndex + 1, 2));
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
        BuildNotice();
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

    public void RegisterHouseLootContainerOpenedForPreview(string houseId)
    {
        EnsureBuiltForTests();
        mainQuestProgress.RegisterLootContainerOpenedInHouse(houseId);
        RefreshQuestPresentation();
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
        DismissQuestNotice();

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
        PlayQuestCompletion("NHIỆM VỤ PHỤ HOÀN THÀNH", "Ghép lại tuyến đường", "Mảnh bản đồ 1",
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
        if (open) DismissQuestNotice();
        mapPrototype.SetOpen(open);
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

        RequireElement(errors, "Journal Hint Notice");
        RequireElement(errors, "Clue Reading Overlay");
        RequireElement(errors, "Clue Reading Body");
        RequireElement(errors, "Quest Journal");
        RequireElement(errors, "Main Quest Card");
        RequireElement(errors, "Side Quest Card");
        RequireElement(errors, "Active Empty State");
        RequireElement(errors, "Map Fragment Slot 1");
        RequireElement(errors, "Map Fragment Slot 2");
        RequireElement(errors, "Side Objective Segment 1");
        RequireElement(errors, "Quest Map");
        RequireElement(errors, "Approximate Office Area");
        RequireElement(errors, "Exact Office Marker");

        RectTransform noticeRect = noticeRoot.GetComponent<RectTransform>();
        if (!Approximately(noticeRect.sizeDelta, new Vector2(430f, 66f)))
            errors.Add("Gợi ý mở nhật ký phải có kích thước chuẩn 430 x 66.");

        RectTransform shell = FindChild(transform, "Journal Shell") as RectTransform;
        if (shell == null || !Approximately(shell.sizeDelta, new Vector2(1640f, 884f)))
            errors.Add("Khung nhật ký phải có kích thước chuẩn 1640 x 884.");

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
                errors.Add("Ba tab nhật ký phải có cùng chiều rộng.");
        }

        if (selectedQuestIndex < 0 || selectedQuestIndex > 1)
            errors.Add("Chỉ số nhiệm vụ được chọn nằm ngoài phạm vi.");
        if (selectedTabIndex < 0 || selectedTabIndex > 2)
            errors.Add("Chỉ số tab được chọn nằm ngoài phạm vi.");

        if (FindChild(transform, "Fragment Progress") != null)
            errors.Add("Không được dùng thanh phần trăm cho hai mảnh bản đồ riêng biệt.");
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

    private void BuildNotice()
    {
        // Startup guidance only. Quest details stay inside the journal and are
        // never pushed onto the player as an automatic acceptance popup.
        noticeRoot = new GameObject("Journal Hint Notice", typeof(RectTransform), typeof(CanvasGroup));
        noticeRoot.transform.SetParent(canvas.transform, false);
        RectTransform root = noticeRoot.GetComponent<RectTransform>();
        SetRect(root, new Vector2(0.5f, 1f), new Vector2(430f, 66f), new Vector2(0f, -62f));
        noticeGroup = noticeRoot.GetComponent<CanvasGroup>();
        noticeGroup.interactable = false;
        noticeGroup.blocksRaycasts = false;

        Box("Notice Shadow", root, new Vector2(0.5f, 0.5f), new Vector2(430f, 66f), new Vector2(6f, -7f),
            new Color(0f, 0f, 0f, 0.55f));
        RectTransform panel = Box("Notice Panel", root, new Vector2(0.5f, 0.5f), new Vector2(430f, 66f),
            Vector2.zero, Ink);
        AddBorder(panel, new Color(0.38f, 0.47f, 0.44f, 0.82f));
        Box("Notice Left Accent", root, new Vector2(0f, 0.5f), new Vector2(5f, 66f),
            new Vector2(2.5f, 0f), Amber);
        Text(root, "Notice Journal Hint", "[J]  MỞ NHẬT KÝ NHIỆM VỤ", 16f, Color.white,
            FontStyles.Bold, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f),
            new Vector2(390f, 38f), Vector2.zero);
    }

    private void BuildJournal()
    {
        journalRoot = new GameObject("Quest Journal", typeof(RectTransform));
        journalRoot.transform.SetParent(canvas.transform, false);
        RectTransform root = journalRoot.GetComponent<RectTransform>();
        Stretch(root);

        StretchBox("Dimmer", root, new Color(0f, 0f, 0f, 0.78f));
        RectTransform shellShadow = Box("Journal Shadow", root, new Vector2(0.5f, 0.5f),
            new Vector2(1640f, 884f), new Vector2(14f, -16f), new Color(0f, 0f, 0f, 0.58f));
        shellShadow.SetAsFirstSibling();
        RectTransform shell = Box("Journal Shell", root, new Vector2(0.5f, 0.5f),
            new Vector2(1640f, 884f), Vector2.zero, Ink);
        AddBorder(shell, new Color(0.4f, 0.49f, 0.46f, 0.8f));

        Box("Top Accent", shell, new Vector2(0.5f, 1f), new Vector2(1640f, 5f), new Vector2(0f, -2.5f), Amber);
        Box("Header", shell, new Vector2(0.5f, 1f), new Vector2(1640f, 92f), new Vector2(0f, -48f), Panel);
        Text(shell, "Journal Title", "NHẬT KÝ SINH TỒN", 29f, Color.white, FontStyles.Bold,
            TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(600f, 44f), new Vector2(38f, -35f));
        Text(shell, "Journal Subtitle", "DỮ LIỆU NHIỆM VỤ  //  NGÀY 01", 12f, Muted, FontStyles.Bold,
            TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(520f, 24f), new Vector2(40f, -68f));

        RectTransform closeHint = Box("Close Hint", shell, new Vector2(1f, 1f), new Vector2(154f, 44f),
            new Vector2(-98f, -44f), PanelLight);
        AddBorder(closeHint, new Color(0.28f, 0.36f, 0.34f, 0.7f));
        Text(closeHint, "Close Text", "[J]  ĐÓNG", 14f, Color.white, FontStyles.Bold,
            TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(150f, 40f), Vector2.zero);

        BuildTabs(shell);

        RectTransform content = Box("Content", shell, new Vector2(0.5f, 0.5f), new Vector2(1580f, 686f),
            new Vector2(0f, -103f), new Color(0.032f, 0.057f, 0.054f, 1f));
        activeContentRoot = new GameObject("Active Quest Content", typeof(RectTransform));
        activeContentRoot.transform.SetParent(content, false);
        Stretch(activeContentRoot.GetComponent<RectTransform>());

        BuildQuestList(activeContentRoot.transform);
        BuildQuestDetails(activeContentRoot.transform);
        BuildEmptyState(content);

        Text(shell, "Footer", "[W/S] CHỌN NHIỆM VỤ  •  [Q/E] ĐỔI TAB  •  [M] BẢN ĐỒ  •  [J] ĐÓNG",
            12f, Muted, FontStyles.Bold, TextAlignmentOptions.Center, new Vector2(0.5f, 0f),
            new Vector2(1000f, 26f), new Vector2(0f, 17f));

        SelectQuest(0);
        SelectTab(0);
        SetJournalOpen(false);
    }

    private void BuildTabs(Transform shell)
    {
        const float tabWidth = 790f;
        string[] labels = { "ĐANG HOẠT ĐỘNG     02", "HOÀN THÀNH     00", "THẤT BẠI     00" };
        RectTransform tabs = Box("Tabs", shell, new Vector2(0.5f, 1f), new Vector2(1580f, 72f),
            new Vector2(0f, -130f), new Color(0.036f, 0.062f, 0.059f, 1f));

        for (int i = 0; i < tabRects.Length; i++)
        {
            RectTransform tab = Box("Tab " + i, tabs, new Vector2(0f, 0.5f), new Vector2(tabWidth, 52f),
                new Vector2(tabWidth * i, 0f), Color.clear);
            tabRects[i] = tab;
            RectTransform underline = Box("Tab Underline " + i, tab, new Vector2(0.5f, 0f),
                new Vector2(tabWidth, 4f), new Vector2(0f, 2f), Amber);
            tabUnderlines[i] = underline.gameObject;
            tabTexts[i] = Text(tab, "Tab Text " + i, labels[i], 16f, Muted, FontStyles.Bold,
                TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(tabWidth - 10f, 48f), Vector2.zero);
        }

        Box("Tab Divider A", tabs, new Vector2(0f, 0.5f), new Vector2(1f, 36f),
            new Vector2(tabWidth, 0f), new Color(0.32f, 0.39f, 0.37f, 0.7f));
    }

    private void BuildQuestList(Transform content)
    {
        RectTransform left = Box("Quest List", content, new Vector2(0f, 0.5f), new Vector2(485f, 650f),
            Vector2.zero, Panel);
        AddBorder(left, Border);
        Box("List Right Rule", left, new Vector2(1f, 0.5f), new Vector2(2f, 650f), new Vector2(-1f, 0f),
            new Color(0.35f, 0.43f, 0.4f, 0.65f));

        Text(left, "Chapter", "CHƯƠNG I", 12f, Amber, FontStyles.Bold,
            TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(420f, 22f), new Vector2(26f, -28f));
        Text(left, "Chapter Name", "TÍN HIỆU CUỐI CÙNG", 23f, Color.white, FontStyles.Bold,
            TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(420f, 36f), new Vector2(26f, -60f));
        Text(left, "Chapter Meta", "KHU DÂN CƯ  •  2 NHIỆM VỤ", 12f, Muted, FontStyles.Bold,
            TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(420f, 22f), new Vector2(26f, -94f));

        mainQuestHeader = Text(left, "Main Quest Header", "NHIỆM VỤ CHÍNH", 13f, Muted, FontStyles.Bold,
            TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(420f, 24f), new Vector2(26f, -148f)).gameObject;

        mainQuestCard = Box("Main Quest Card", left, new Vector2(0.5f, 1f), new Vector2(433f, 92f),
            new Vector2(0f, -184f), new Color(0.15f, 0.17f, 0.14f, 1f));
        mainQuestCardImage = mainQuestCard.GetComponent<Image>();
        mainQuestAccent = Box("Main Quest Accent", mainQuestCard, new Vector2(0f, 0.5f),
            new Vector2(6f, 92f), new Vector2(3f, 0f), Amber).GetComponent<Image>();
        RectTransform questIcon = Box("Main Quest Icon", mainQuestCard, new Vector2(0f, 0.5f),
            new Vector2(30f, 30f), new Vector2(36f, 0f), new Color(0.35f, 0.18f, 0.42f, 1f));
        questIcon.localRotation = Quaternion.Euler(0f, 0f, 45f);
        Box("Main Quest Icon Core", questIcon, new Vector2(0.5f, 0.5f), new Vector2(11f, 11f),
            Vector2.zero, new Color(0.95f, 0.82f, 0.24f));
        TextMeshProUGUI mainName = Text(mainQuestCard, "Main Quest Name", "Tìm thêm thông tin về thành phố", 16f,
            Color.white, FontStyles.Bold, TextAlignmentOptions.Left, new Vector2(0f, 1f),
            new Vector2(350f, 26f), new Vector2(66f, -19f));
        mainQuestNameRect = mainName.rectTransform;
        mainQuestMetaText = Text(mainQuestCard, "Main Quest Meta", "Khu dân cư  •  0 / 3 nhà", 12f,
            Muted, FontStyles.Normal, TextAlignmentOptions.Left, new Vector2(0f, 1f),
            new Vector2(350f, 20f), new Vector2(66f, -55f));
        mainQuestMetaRect = mainQuestMetaText.rectTransform;

        sideQuestHeader = Text(left, "Side Quest Header", "NHIỆM VỤ PHỤ", 13f, Mint, FontStyles.Bold,
            TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(420f, 24f), new Vector2(26f, -302f)).gameObject;
        sideQuestCard = Box("Side Quest Card", left, new Vector2(0.5f, 1f), new Vector2(433f, 78f),
            new Vector2(0f, -338f), new Color(0.075f, 0.1f, 0.094f, 1f));
        sideQuestCardImage = sideQuestCard.GetComponent<Image>();
        sideQuestAccent = Box("Side Quest Accent", sideQuestCard, new Vector2(0f, 0.5f),
            new Vector2(5f, 78f), new Vector2(2.5f, 0f), new Color(Mint.r, Mint.g, Mint.b, 0.35f)).GetComponent<Image>();
        TextMeshProUGUI sideName = Text(sideQuestCard, "Side Quest Name", "Ghép lại tuyến đường", 15f,
            Color.white, FontStyles.Bold, TextAlignmentOptions.Left, new Vector2(0f, 1f),
            new Vector2(380f, 24f), new Vector2(26f, -16f));
        sideQuestNameRect = sideName.rectTransform;
        sideQuestMetaText = Text(sideQuestCard, "Side Quest Meta", "Tùy chọn  •  0 / 3 dấu vết", 12f,
            Muted, FontStyles.Normal, TextAlignmentOptions.Left, new Vector2(0f, 1f),
            new Vector2(380f, 20f), new Vector2(26f, -48f));
        sideQuestMetaRect = sideQuestMetaText.rectTransform;

        RectTransform context = Box("Quest Context Panel", left, new Vector2(0.5f, 0f), new Vector2(433f, 118f),
            new Vector2(0f, 82f), new Color(0.045f, 0.07f, 0.065f, 1f));
        AddBorder(context, new Color(0.2f, 0.31f, 0.28f, 0.8f));
        contextPanelTitle = Text(context, "Context Panel Title", string.Empty, 12f, Muted, FontStyles.Bold,
            TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(380f, 22f), new Vector2(20f, -20f));
        contextPanelCount = Text(context, "Context Panel Count", string.Empty, 18f, Color.white, FontStyles.Bold,
            TextAlignmentOptions.Right, new Vector2(1f, 1f), new Vector2(100f, 28f), new Vector2(-66f, -20f));

        mapFragmentSlotsRoot = new GameObject("Map Fragment Slots", typeof(RectTransform));
        mapFragmentSlotsRoot.transform.SetParent(context, false);
        Stretch(mapFragmentSlotsRoot.GetComponent<RectTransform>());
        RectTransform slot1 = Box("Map Fragment Slot 1", mapFragmentSlotsRoot.transform, new Vector2(0f, 0f),
            new Vector2(188f, 42f), new Vector2(20f, 16f), new Color(Amber.r, Amber.g, Amber.b, 0.16f));
        AddBorder(slot1, new Color(Amber.r, Amber.g, Amber.b, 0.72f));
        mapFragment1SlotText = Text(slot1, "Map Fragment Slot 1 Text", "MẢNH 1  •  CHƯA CÓ", 12f, Muted, FontStyles.Bold,
            TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(180f, 34f), Vector2.zero);
        RectTransform slot2 = Box("Map Fragment Slot 2", mapFragmentSlotsRoot.transform, new Vector2(1f, 0f),
            new Vector2(188f, 42f), new Vector2(-20f, 16f), new Color(0.08f, 0.105f, 0.1f, 1f));
        AddBorder(slot2, new Color(0.25f, 0.32f, 0.3f, 0.85f));
        mapFragment2SlotText = Text(slot2, "Map Fragment Slot 2 Text", "MẢNH 2  •  CHƯA CÓ", 12f, Muted,
            FontStyles.Bold, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f),
            new Vector2(180f, 34f), Vector2.zero);

        sideQuestProgressRoot = new GameObject("Side Quest Objective Progress", typeof(RectTransform));
        sideQuestProgressRoot.transform.SetParent(context, false);
        Stretch(sideQuestProgressRoot.GetComponent<RectTransform>());
        for (int i = 0; i < 3; i++)
        {
            RectTransform segment = Box("Side Objective Segment " + (i + 1), sideQuestProgressRoot.transform,
                new Vector2(0f, 0f), new Vector2(123f, 42f), new Vector2(20f + i * 135f, 16f),
                new Color(0.08f, 0.105f, 0.1f, 1f));
            sideClueSegmentImages[i] = segment.GetComponent<Image>();
            AddBorder(segment, new Color(Mint.r, Mint.g, Mint.b, 0.3f));
            sideClueSegmentTexts[i] = Text(segment, "Side Segment Text " + (i + 1),
                (i + 1).ToString("00") + "  CHƯA CÓ", 10f,
                Muted, FontStyles.Bold, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f),
                new Vector2(116f, 34f), Vector2.zero);
        }
    }

    private void BuildQuestDetails(Transform content)
    {
        RectTransform right = Box("Quest Details", content, new Vector2(1f, 0.5f), new Vector2(1065f, 650f),
            Vector2.zero, new Color(0.042f, 0.067f, 0.063f, 1f));
        AddBorder(right, Border);

        detailEyebrow = Text(right, "Detail Eyebrow", string.Empty, 12f, Amber, FontStyles.Bold,
            TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(650f, 24f), new Vector2(32f, -30f));
        detailTitle = Text(right, "Detail Title", string.Empty, 32f, Color.white, FontStyles.Bold,
            TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(690f, 86f), new Vector2(32f, -67f));

        statusBadge = Box("Status Badge", right, new Vector2(1f, 1f), new Vector2(186f, 38f),
            new Vector2(-120f, -44f), new Color(Amber.r, Amber.g, Amber.b, 0.16f));
        statusBadgeImage = statusBadge.GetComponent<Image>();
        statusBadgeText = Text(statusBadge, "Badge Text", string.Empty, 12f, Amber, FontStyles.Bold,
            TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(176f, 34f), Vector2.zero);

        Box("Title Rule", right, new Vector2(0.5f, 1f), new Vector2(1000f, 1f), new Vector2(0f, -164f),
            new Color(0.34f, 0.42f, 0.39f, 0.68f));
        storyText = Text(right, "Story", string.Empty, 16f, new Color(0.8f, 0.85f, 0.83f),
            FontStyles.Normal, TextAlignmentOptions.TopLeft, new Vector2(0f, 1f),
            new Vector2(995f, 72f), new Vector2(32f, -208f));

        Text(right, "Objectives Header", "MỤC TIÊU VÀ ĐIỀU KIỆN", 14f, Color.white, FontStyles.Bold,
            TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(570f, 26f), new Vector2(32f, -302f));

        BuildObjectiveRow(right, 0, new Vector2(32f, -351f));
        BuildObjectiveRow(right, 1, new Vector2(32f, -409f));
        BuildObjectiveRow(right, 2, new Vector2(32f, -467f));
        BuildMapCard(right);

        RectTransform reward = Box("Progress Reward", right, new Vector2(0f, 0f), new Vector2(630f, 92f),
            new Vector2(32f, 32f), PanelLight);
        AddBorder(reward, new Color(0.2f, 0.31f, 0.28f, 0.75f));
        Box("Reward Accent", reward, new Vector2(0f, 0.5f), new Vector2(5f, 92f),
            new Vector2(2.5f, 0f), Mint);
        rewardLabel = Text(reward, "Reward Label", string.Empty, 11f, Muted, FontStyles.Bold,
            TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(570f, 18f), new Vector2(20f, -12f));
        rewardText = Text(reward, "Reward Text", string.Empty, 17f, Color.white, FontStyles.Bold,
            TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(570f, 34f), new Vector2(20f, -43f));
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
        int candidate = Wrap(index, 2);
        if (activeContentRoot != null && !QuestBelongsToTab(candidate, selectedTabIndex))
        {
            int other = candidate == 0 ? 1 : 0;
            if (QuestBelongsToTab(other, selectedTabIndex))
                candidate = other;
        }

        selectedQuestIndex = candidate;
        if (detailTitle == null)
            return;

        bool main = selectedQuestIndex == 0;
        mainQuestCardImage.color = main ? new Color(0.15f, 0.17f, 0.14f, 1f) : new Color(0.075f, 0.1f, 0.094f, 1f);
        sideQuestCardImage.color = main ? new Color(0.075f, 0.1f, 0.094f, 1f) : new Color(0.11f, 0.16f, 0.14f, 1f);
        mainQuestAccent.color = main ? Amber : new Color(Amber.r, Amber.g, Amber.b, 0.28f);
        sideQuestAccent.color = main ? new Color(Mint.r, Mint.g, Mint.b, 0.35f) : Mint;

        if (main)
            ShowMainQuestDetails();
        else
            ShowSideQuestDetails();
    }

    private void ShowMainQuestDetails()
    {
        detailEyebrow.text = "NHIỆM VỤ CHÍNH  //  GIAI ĐOẠN 01";
        detailEyebrow.color = Amber;
        detailTitle.text = "TÌM THÊM THÔNG TIN\nVỀ THÀNH PHỐ";
        storyText.text = "Phần bản đồ chưa an toàn bị phủ đen và tạm thời không thể đi vào. Hãy lục soát ba trong sáu căn nhà thuộc khu mở để nhận manh mối chính; ba vật phẩm nhiệm vụ trong tủ loot có thể ghép thành Mảnh bản đồ 1.";
        SetBadge(mainQuestProgress.MainQuestComplete ? "HOÀN THÀNH" : "ĐANG HOẠT ĐỘNG",
            mainQuestProgress.MainQuestComplete ? Mint : Amber);

        bool searchingHouses = !mainQuestProgress.HouseSearchComplete;
        SetObjective(0, "Lục soát 3 căn nhà trong khu tìm kiếm",
            mainQuestProgress.HouseSearchComplete
                ? "HOÀN THÀNH"
                : mainQuestProgress.SearchedHouseCount + " / " + PreMilitaryQuestProgress.RequiredDistinctHouses + " NHÀ",
            searchingHouses, mainQuestProgress.HouseSearchComplete ? Mint : Amber);

        string officeLocationStatus;
        if (mainQuestProgress.OfficeDiscovered) officeLocationStatus = "ĐÃ TÌM THẤY";
        else if (mainQuestProgress.HasMapFragment1) officeLocationStatus = "VỊ TRÍ CHÍNH XÁC";
        else if (mainQuestProgress.ApproximateOfficeAreaRevealed) officeLocationStatus = "VÙNG TƯƠNG ĐỐI";
        else officeLocationStatus = "ĐANG KHÓA";
        bool locatingOffice = mainQuestProgress.HouseSearchComplete && !mainQuestProgress.OfficeDiscovered;
        SetObjective(1, "Xác định và tìm đến văn phòng màu tím", officeLocationStatus, locatingOffice,
            mainQuestProgress.OfficeDiscovered ? Mint : mainQuestProgress.HouseSearchComplete ? Purple : Muted);

        string investigationStatus = mainQuestProgress.HasMapFragment2
            ? "ĐÃ CÓ MẢNH 2"
            : mainQuestProgress.OfficeInvestigationComplete ? "ĐÃ KIỂM TRA" :
            mainQuestProgress.OfficeDiscovered ? "TÌM MẢNH 2" : "CHƯA MỞ";
        bool investigatingOffice = mainQuestProgress.OfficeDiscovered && !mainQuestProgress.HasMapFragment2;
        SetObjective(2, "Điều tra các điểm khả nghi trong văn phòng", investigationStatus, investigatingOffice,
            mainQuestProgress.HasMapFragment2 ? Mint : mainQuestProgress.OfficeDiscovered ? Purple : Muted);

        rewardLabel.text = mainQuestProgress.MainQuestComplete ? "PHẦN THƯỞNG ĐÃ NHẬN" : "PHẦN THƯỞNG";
        rewardText.text = mainQuestProgress.MainQuestComplete ? "Mảnh bản đồ 2" : "Chưa xác định";

        contextPanelTitle.text = "VẬT PHẨM NHIỆM VỤ";
        int fragmentCount = (mainQuestProgress.HasMapFragment1 ? 1 : 0) +
                            (mainQuestProgress.HasMapFragment2 ? 1 : 0);
        contextPanelCount.text = fragmentCount.ToString("00") + " / 02";
        mapFragmentSlotsRoot.SetActive(true);
        sideQuestProgressRoot.SetActive(false);
        Image firstSlot = mapFragment1SlotText.transform.parent.GetComponent<Image>();
        firstSlot.color = mainQuestProgress.HasMapFragment1
            ? new Color(Purple.r, Purple.g, Purple.b, 0.16f)
            : new Color(0.08f, 0.105f, 0.1f, 1f);
        mapFragment1SlotText.text = mainQuestProgress.HasMapFragment1 ? "MẢNH 1  •  ĐÃ CÓ" : "MẢNH 1  •  CHƯA CÓ";
        mapFragment1SlotText.color = mainQuestProgress.HasMapFragment1 ? Purple : Muted;
        Image secondSlot = mapFragment2SlotText.transform.parent.GetComponent<Image>();
        secondSlot.color = mainQuestProgress.HasMapFragment2
            ? new Color(Mint.r, Mint.g, Mint.b, 0.16f)
            : new Color(0.08f, 0.105f, 0.1f, 1f);
        mapFragment2SlotText.text = mainQuestProgress.HasMapFragment2 ? "MẢNH 2  •  ĐÃ CÓ" : "MẢNH 2  •  CHƯA CÓ";
        mapFragment2SlotText.color = mainQuestProgress.HasMapFragment2 ? Mint : Muted;
        UpdateMiniMapPreview();
    }

    private void ShowSideQuestDetails()
    {
        detailEyebrow.text = "NHIỆM VỤ PHỤ  //  TÙY CHỌN";
        detailEyebrow.color = Mint;
        detailTitle.text = "GHÉP LẠI\nTUYẾN ĐƯỜNG";
        storyText.text = "Ba dấu vết phụ nằm trong các căn nhà đang lục soát: hóa đơn giao hàng, sơ đồ tuyến xe và ghi chú địa chỉ. Ghép đủ chúng sẽ tạo Mảnh bản đồ 1.";

        string sideBadge = mainQuestProgress.SideQuestSkipped ? "ĐÃ BỎ QUA" :
            mainQuestProgress.HasMapFragment1 ? "HOÀN THÀNH" : "TÙY CHỌN";
        SetBadge(sideBadge, mainQuestProgress.SideQuestSkipped ? Muted : Mint);
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
        mapFragmentSlotsRoot.SetActive(false);
        sideQuestProgressRoot.SetActive(true);
        string[] clueNames = { "HÓA ĐƠN", "TUYẾN XE", "GHI CHÚ" };
        for (int i = 0; i < sideClueSegmentImages.Length; i++)
        {
            bool acquired = i < mainQuestProgress.RouteClueCount;
            sideClueSegmentImages[i].color = acquired
                ? new Color(Mint.r, Mint.g, Mint.b, 0.16f)
                : new Color(0.08f, 0.105f, 0.1f, 1f);
            sideClueSegmentTexts[i].text = acquired ? clueNames[i] : (i + 1).ToString("00") + "  CHƯA CÓ";
            sideClueSegmentTexts[i].color = acquired ? Mint : Muted;
        }
        UpdateMiniMapPreview();
    }

    private void RefreshQuestPresentation()
    {
        if (mainQuestMetaText != null)
        {
            mainQuestMetaText.text = mainQuestProgress.MainQuestComplete
                ? "Khu dân cư  •  Hoàn thành"
                : "Khu dân cư  •  " + mainQuestProgress.SearchedHouseCount + " / 3 nhà";
        }

        if (sideQuestMetaText != null)
        {
            sideQuestMetaText.text = mainQuestProgress.SideQuestSkipped
                ? "Tùy chọn  •  Đã tự tìm văn phòng"
                : mainQuestProgress.HasMapFragment1
                    ? "Tùy chọn  •  Đã nhận Mảnh 1"
                    : "Tùy chọn  •  " + mainQuestProgress.RouteClueCount + " / 3 dấu vết";
        }

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
        statusBadgeImage.color = new Color(color.r, color.g, color.b, 0.16f);
        statusBadgeText.text = value;
        statusBadgeText.color = color;
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
            tabRects[i].GetComponent<Image>().color = active ? new Color(0.12f, 0.16f, 0.15f, 1f) : Color.clear;
            tabUnderlines[i].SetActive(active);
            tabTexts[i].color = active ? Color.white : Muted;
        }

        bool mainVisible = QuestBelongsToTab(0, selectedTabIndex);
        bool sideVisible = QuestBelongsToTab(1, selectedTabIndex);
        bool showQuestContent = mainVisible || sideVisible;
        activeContentRoot.SetActive(showQuestContent);
        emptyStateRoot.SetActive(!showQuestContent);
        mainQuestHeader.SetActive(mainVisible);
        mainQuestCard.gameObject.SetActive(mainVisible);
        sideQuestHeader.SetActive(sideVisible);
        sideQuestCard.gameObject.SetActive(sideVisible);

        RectTransform sideHeaderRect = sideQuestHeader.GetComponent<RectTransform>();
        sideHeaderRect.anchoredPosition = sideVisible && !mainVisible
            ? new Vector2(26f, -148f)
            : new Vector2(26f, -302f);
        sideQuestCard.anchoredPosition = sideVisible && !mainVisible
            ? new Vector2(0f, -184f)
            : new Vector2(0f, -338f);

        if (showQuestContent)
        {
            if (!QuestBelongsToTab(selectedQuestIndex, selectedTabIndex))
                selectedQuestIndex = mainVisible ? 0 : 1;
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

        string[] labels = { "ĐANG HOẠT ĐỘNG", "HOÀN THÀNH", "THẤT BẠI" };
        for (int i = 0; i < tabTexts.Length; i++)
            tabTexts[i].text = labels[i] + "     " + GetQuestCountForTab(i).ToString("00");
    }

    private int GetQuestCountForTab(int tabIndex)
    {
        int count = 0;
        if (QuestBelongsToTab(0, tabIndex)) count++;
        if (QuestBelongsToTab(1, tabIndex)) count++;
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

        if (tabIndex == 0) return !mainQuestProgress.SideQuestResolved;
        if (tabIndex == 1) return mainQuestProgress.HasMapFragment1;
        return mainQuestProgress.SideQuestSkipped;
    }

    private void ReplayNotice()
    {
        EnsureBuiltForTests();
        if (!Application.isPlaying)
        {
            ShowNoticeImmediately();
            return;
        }

        if (noticeRoutine != null)
            StopCoroutine(noticeRoutine);

        ShowNoticeImmediately();
        noticeRoutine = StartCoroutine(ShowNoticeRoutine());
    }

    private void ShowNoticeImmediately()
    {
        noticeRoot.SetActive(true);
        noticeGroup.alpha = 1f;
    }

    private void HideNoticeImmediately()
    {
        if (noticeRoot == null)
            return;

        noticeRoot.SetActive(false);
        noticeGroup.alpha = 0f;
    }

    private void DismissQuestNotice()
    {
        if (noticeRoutine != null)
        {
            StopCoroutine(noticeRoutine);
            noticeRoutine = null;
        }
        HideNoticeImmediately();
    }

    private IEnumerator ShowNoticeRoutine()
    {
        yield return new WaitForSecondsRealtime(5.8f);
        float elapsed = 0f;
        const float duration = 0.4f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            noticeGroup.alpha = 1f - Mathf.Clamp01(elapsed / duration);
            yield return null;
        }

        noticeGroup.alpha = 0f;
        noticeRoot.SetActive(false);
        noticeRoutine = null;
    }

    private void SetJournalOpen(bool open)
    {
        journalOpen = open;
        if (journalRoot != null)
            journalRoot.SetActive(open);
        if (!open)
            return;

        if (noticeRoutine != null)
        {
            StopCoroutine(noticeRoutine);
            noticeRoutine = null;
        }

        if (noticeRoot != null)
            noticeRoot.SetActive(false);
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
