using System.Collections;
using System.Collections.Generic;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Interactive Canvas/TMP journal for both halves of Route B and the parallel
/// Route A preparation. Runtime state is supplied by authoritative snapshots.
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
    private PreMilitaryQuestStage authoritativeQuestStage = PreMilitaryQuestStage.NotStarted;
    private bool hasAuthoritativeQuestStage;
    private int authoritativeHospitalStage;
    private float authoritativeHospitalRadioProgress;
    private int authoritativeHospitalRadioCheckpointCount;
    private RouteBMilitaryPresentationPhase militaryPhase = RouteBMilitaryPresentationPhase.NotReached;
    private bool hasMilitarySnapshot;
    private bool militaryGeneratorActive;
    private bool militaryHasAllParts;
    private float militaryVehicleRepairProgress;
    private float militaryGateCurrentHealth;
    private float militaryGateMaxHealth = 1f;
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
    private RawImage completionRewardMapImage;
    private RectTransform completionRewardMapRect;
    private Texture completionRewardDefaultTexture;
    private RectTransform completionRewardCard;
    private readonly RectTransform[] completionSparkles = new RectTransform[8];
    private Coroutine completionRoutine;
    private GameObject clueReadingRoot;
    private TextMeshProUGUI clueReadingEyebrow;
    private TextMeshProUGUI clueReadingTitle;
    private TextMeshProUGUI clueReadingBody;
    private TextMeshProUGUI clueReadingConclusion;
    private bool fragmentCompletionPending;
    private bool fragmentCompletionPresented;
    private bool fragmentRewardRequestedAfterDialogue;

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
        public TextMeshProUGUI ItemNameText;
        public TextMeshProUGUI RequirementText;
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
    public bool IsMilitaryRouteTracked => trackedQuestIndex == 0;
    public EscapeEndingRoute LockedEscapeRoute => lockedEscapeRoute;
    public bool IsSelectedQuestTracked => trackedQuestIndex == selectedQuestIndex;
    public string TrackingButtonText => trackingButtonText == null ? string.Empty : trackingButtonText.text;
    public string CurrentDetailTitle => detailTitle == null ? string.Empty : detailTitle.text;
    public string CurrentObjectiveProgress => currentObjectiveProgressText == null
        ? string.Empty
        : currentObjectiveProgressText.text;
    public Texture CurrentCompletionRewardTexture => completionRewardMapImage != null
        ? completionRewardMapImage.texture
        : null;
    public string CurrentContextPanelTitle => contextPanelTitle == null ? string.Empty : contextPanelTitle.text;
    public bool IsEmptyStateVisible => emptyStateRoot != null && emptyStateRoot.activeSelf;
    public bool IsQuestListDividerVisible => questListDivider != null && questListDivider.activeSelf;
    public bool IsMainQuestComplete => hasMilitarySnapshot &&
                                       militaryPhase == RouteBMilitaryPresentationPhase.Escaped;
    public bool IsMainQuestFailed => hasMilitarySnapshot &&
                                     militaryPhase == RouteBMilitaryPresentationPhase.Failed;
    public RouteBMilitaryPresentationPhase MilitaryPresentationPhase => militaryPhase;
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
    public bool IsMapMilitaryDestinationVisible => mapPrototype != null &&
                                                    mapPrototype.IsMilitaryDestinationVisible;
    public bool IsMapOfficeDestinationVisible => mapPrototype != null &&
                                                  mapPrototype.IsOfficeDestinationVisible;
    public bool HasPendingMapUnlockReveal => mapPrototype != null && mapPrototype.HasPendingUnlockReveal;
    public int ActiveMapRestrictedFogCount => mapPrototype == null ? 0 : mapPrototype.ActiveRestrictedFogCount;
    public string CurrentRewardLabel => rewardLabel == null ? string.Empty : rewardLabel.text;
    public string CurrentRewardText => rewardText == null ? string.Empty : rewardText.text;
    public string CurrentHospitalRadioTranscript => QuestUILocalization.IsVietnamese
        ? RouteBAudioContent.HospitalTranscriptVietnamese
        : RouteBAudioContent.HospitalTranscriptEnglish;
    public bool IsQuestOverlayOpen => IsJournalOpen || IsMapOpen || IsClueReadingOpen ||
                                      (completionRoot != null && completionRoot.activeSelf);

    private void Awake()
    {
        Instance = this;
        Application.targetFrameRate = 60;
        QuestUILocalization.LanguageChanged -= ApplyLocalization;
        QuestUILocalization.LanguageChanged += ApplyLocalization;
        EnsureBuiltForTests();
        ApplyLocalization();
    }

    private void OnDestroy()
    {
        QuestUILocalization.LanguageChanged -= ApplyLocalization;
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (QuestUIDialogueState.IsActive) return;

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
        QuestUILocalization.LanguageChanged -= ApplyLocalization;
        QuestUILocalization.LanguageChanged += ApplyLocalization;
        if (built)
            return;

        built = true;
        font = Resources.Load<TMP_FontAsset>("Fonts/Vietnamese Static SDF") ?? TMP_Settings.defaultFontAsset;
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

    private static string L(string english, string vietnamese) =>
        QuestUILocalization.IsVietnamese ? vietnamese : english;

    private void ApplyLocalization()
    {
        if (!built || journalRoot == null) return;
        SetNamedText("Journal Title", L("MISSION JOURNAL", "NHẬT KÝ NHIỆM VỤ"));
        SetNamedText("Journal Subtitle", L("Day 01", "Ngày 01"));
        SetNamedText("Close Text", L("[J]  CLOSE", "[J]  ĐÓNG"));
        SetNamedText("Main Quest Header", L("ESCAPE ROUTE B  •  STORY", "TUYẾN THOÁT HIỂM B  •  CỐT TRUYỆN"));
        SetNamedText("Main Quest Name", GetMainQuestCardName());
        SetNamedText("Side Quest Header", L("ROUTE B PROGRESS", "TIẾN ĐỘ TUYẾN B"));
        SetNamedText("Side Quest Name", L("Collect evacuation records", "Thu thập hồ sơ sơ tán"));
        SetNamedText("Car Quest Header", L("ESCAPE ROUTE A  •  THE CAR", "TUYẾN THOÁT HIỂM A  •  CHIẾC XE"));
        SetNamedText("Car Repair Quest Name", L("Restore the car", "Khôi phục chiếc xe"));
        SetNamedText("Main Quest Tracked Marker", L("●  TRACKED", "●  ĐANG THEO DÕI"));
        SetNamedText("Side Quest Tracked Marker", L("●  TRACKED", "●  ĐANG THEO DÕI"));
        SetNamedText("Car Repair Quest Tracked Marker", L("●  TRACKED", "●  ĐANG THEO DÕI"));
        SetNamedText("Objectives Header", L("CURRENT OBJECTIVE", "MỤC TIÊU HIỆN TẠI"));
        SetNamedText("Open Map Button Text", L("[M]  OPEN MAP", "[M]  MỞ BẢN ĐỒ"));
        SetNamedText("Car Requirements Header", L("ITEMS TO FIND  •  LIVE INVENTORY", "VẬT PHẨM CẦN TÌM  •  CẬP NHẬT THEO TÚI ĐỒ"));
        SetNamedText("Open Map Hint", L("[M] OPEN MAP", "[M] MỞ BẢN ĐỒ"));
        mapPrototype?.Refresh();
        RefreshQuestPresentation();
    }

    private void SetNamedText(string objectName, string value)
    {
        Transform target = FindChild(transform, objectName);
        TMP_Text text = target != null ? target.GetComponent<TMP_Text>() : null;
        if (text != null) text.text = value;
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
        int arrivalCarRepairMask = 0, EscapeEndingRoute authoritativeLockedRoute = EscapeEndingRoute.None,
        int authoritativeStage = -1, int hospitalInvestigationStage = 0,
        float hospitalRadioProgress = 0f, int hospitalRadioCheckpointCount = 0)
    {
        EnsureBuiltForTests();
        bool hadFragment1 = mainQuestProgress.HasMapFragment1;

        mainQuestProgress.ApplyAuthoritativeSnapshot(searchedHouseMask, routeClueMask,
            officeDiscovered, officeInvestigationComplete, hasMapFragment2,
            arrivalCarRepairUnlocked, arrivalCarRepaired, arrivalCarRepairMask);
        if (authoritativeStage >= (int)PreMilitaryQuestStage.NotStarted &&
            authoritativeStage <= (int)PreMilitaryQuestStage.CityMapFound)
        {
            authoritativeQuestStage = (PreMilitaryQuestStage)authoritativeStage;
            hasAuthoritativeQuestStage = true;
        }
        authoritativeHospitalStage = Mathf.Clamp(hospitalInvestigationStage, 0, 5);
        authoritativeHospitalRadioProgress = Mathf.Clamp01(hospitalRadioProgress);
        authoritativeHospitalRadioCheckpointCount = Mathf.Clamp(hospitalRadioCheckpointCount, 0, 3);
        lockedEscapeRoute = authoritativeLockedRoute;
        if (lockedEscapeRoute != EscapeEndingRoute.None)
            trackedQuestIndex = lockedEscapeRoute == EscapeEndingRoute.CivilianCar ? 2 : 0;
        RefreshQuestPresentation();

        // The dialogue RPC and replicated snapshot can arrive in either order.
        // If the dialogue already finished, continue the reward as soon as the
        // 3/3 snapshot reaches this client instead of dropping the callback.
        TryPlayMapFragmentOneRewardAfterDialogue();

        if (!playTransitions || !Application.isPlaying) return;
        if (!hadFragment1 && mainQuestProgress.HasMapFragment1)
        {
            // The authoritative RPC owns the ordered dialogue -> reward -> map
            // sequence. The snapshot only preserves the reveal in case the RPC
            // arrived before this UI was ready; it must not start the reward in
            // parallel with the dialogue.
            mapPrototype?.QueueUnlockReveal();
        }
        // Recovering the military map only opens Route B's second half.  The
        // route completes later when the military extraction reaches Escaped.
    }

    /// <summary>
    /// Extends the journal beyond CityMapFound. VictorySummaryUI owns the final
    /// cinematic/result presentation; this method only keeps the journal live.
    /// </summary>
    public void ApplyMilitarySnapshot(int phase, bool generatorActive, bool hasAllParts,
        float vehicleRepairProgress, float gateCurrentHealth, float gateMaxHealth,
        bool playTransitions)
    {
        if (phase < (int)RouteBMilitaryPresentationPhase.NotReached ||
            phase > (int)RouteBMilitaryPresentationPhase.Failed)
            return;

        EnsureBuiltForTests();
        _ = playTransitions;
        militaryPhase = (RouteBMilitaryPresentationPhase)phase;
        hasMilitarySnapshot = true;
        militaryGeneratorActive = generatorActive;
        militaryHasAllParts = hasAllParts;
        militaryVehicleRepairProgress = Mathf.Clamp(vehicleRepairProgress, 0f, 100f);
        militaryGateCurrentHealth = Mathf.Max(0f, gateCurrentHealth);
        militaryGateMaxHealth = Mathf.Max(1f, gateMaxHealth);
        RefreshQuestPresentation();

    }

    /// <summary>
    /// Applies the stage carried by a quest-update RPC immediately. The regular
    /// replicated snapshot remains authoritative and confirms it on the next
    /// network update, while the notice and journal change in the same frame.
    /// </summary>
    public void NotifyAuthoritativeQuestStage(int stage)
    {
        if (stage < (int)PreMilitaryQuestStage.NotStarted ||
            stage > (int)PreMilitaryQuestStage.CityMapFound)
            return;
        EnsureBuiltForTests();
        authoritativeQuestStage = (PreMilitaryQuestStage)stage;
        hasAuthoritativeQuestStage = true;
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

    public void ShowHospitalRadioTranscript()
    {
        EnsureBuiltForTests();
        if (!mainQuestProgress.HasMapFragment2 && authoritativeQuestStage != PreMilitaryQuestStage.CityMapFound)
            return;
        SetJournalOpen(false);
        if (mapPrototype != null) mapPrototype.SetOpen(false);
        clueReadingEyebrow.text = L("HOSPITAL ARCHIVE  //  SAVED TRANSCRIPT",
            "LƯU TRỮ BỆNH VIỆN  //  TRANSCRIPT ĐÃ LƯU");
        clueReadingTitle.text = L("RECOVERED RADIO RECORDING", "BẢN GHI RADIO ĐÃ KHÔI PHỤC");
        clueReadingBody.text = CurrentHospitalRadioTranscript;
        clueReadingConclusion.text = L(
            "Conclusion: the convoy withdrew to North Base; the recording does not confirm anyone is alive there.",
            "Kết luận: đoàn xe đã rút về Căn cứ phía Bắc; bản ghi không xác nhận ở đó còn người sống.");
        clueReadingRoot.SetActive(true);
        clueReadingRoot.transform.SetAsLastSibling();
    }

    private void PlayMapFragmentOneCompletion()
    {
        SetCompletionRewardTexture(completionRewardDefaultTexture, new Vector2(90f, 82f));
        PlayQuestCompletion(L("ALL RECORDS RECOVERED", "ĐÃ PHÁT HIỆN ĐỦ MANH MỐI"),
            L("Route data reconstructed", "Dữ liệu tuyến đường đã hoàn chỉnh"),
            L("MAP FRAGMENT 1 — HOSPITAL LOCATION", "MẢNH BẢN ĐỒ 1 — VỊ TRÍ BỆNH VIỆN"),
            ContinueMapFragmentOneFlow, true);
    }

    public void PlayMilitaryMapRewardAfterDialogue(Action onFinished = null)
    {
        EnsureBuiltForTests();
        // Fragment 2 is a physical torn paper reward, just like Fragment 1.
        // The full raster belongs to the map screen and must never be presented
        // as though the player received the whole town map as an inventory item.
        SetCompletionRewardTexture(completionRewardDefaultTexture, new Vector2(90f, 82f));

        PlayQuestCompletion(
            L("COORDINATION INVESTIGATION COMPLETE", "HOÀN THÀNH ĐIỀU TRA KHU ĐIỀU PHỐI"),
            L("Military route recovered", "Đã tìm thấy bản đồ tuyến quân sự"),
            L("MAP FRAGMENT 2 — MILITARY ROUTE", "MẢNH BẢN ĐỒ 2 — TUYẾN QUÂN SỰ"),
            onFinished, true);
    }

    public void PlayMilitaryMapReveal(Action onFinished = null)
    {
        EnsureBuiltForTests();
        SetJournalOpen(false);
        mapPrototype.PlayMilitaryDestinationReveal(() =>
        {
            mapPrototype.SetOpen(false);
            onFinished?.Invoke();
        });
    }

    private void SetCompletionRewardTexture(Texture texture, Vector2 size)
    {
        if (completionRewardMapImage != null)
            completionRewardMapImage.texture = texture;
        if (completionRewardMapRect != null)
            completionRewardMapRect.sizeDelta = size;
    }

    public void PrepareForMapFragmentDialogue()
    {
        EnsureBuiltForTests();
        fragmentCompletionPending = false;
        if (clueReadingRoot != null) clueReadingRoot.SetActive(false);
        SetJournalOpen(false);
        if (mapPrototype != null) mapPrototype.SetOpen(false);
    }

    public void PlayMapFragmentOneRewardAfterDialogue()
    {
        EnsureBuiltForTests();
        fragmentRewardRequestedAfterDialogue = true;
        TryPlayMapFragmentOneRewardAfterDialogue();
    }

    private void TryPlayMapFragmentOneRewardAfterDialogue()
    {
        if (!Application.isPlaying || !fragmentRewardRequestedAfterDialogue ||
            !mainQuestProgress.HasMapFragment1 || fragmentCompletionPresented)
            return;

        fragmentRewardRequestedAfterDialogue = false;
        fragmentCompletionPresented = true;
        PlayMapFragmentOneCompletion();
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
        mainQuestProgress.RegisterMapFragment2AddedToInventory();
        RefreshQuestPresentation();
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

    public void QueueMapUnlockReveal(Action onFinished = null)
    {
        EnsureBuiltForTests();
        mapPrototype?.QueueUnlockReveal(onFinished);
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

    public void ConfigureMilitaryDestination(Vector2 militaryNormalized)
    {
        EnsureBuiltForTests();
        mapPrototype.ConfigureMilitaryDestination(militaryNormalized);
    }

    public void SetCivilianCityMapUnlocked(bool unlocked)
    {
        EnsureBuiltForTests();
        mapPrototype?.SetCivilianCityMapUnlocked(unlocked);
    }

    public void ConfigureCivilianEscapeRoute(Vector2 checkpointNormalized, Vector2 cityExitNormalized,
        CivilianEscapePresentationStage stage)
    {
        EnsureBuiltForTests();
        mapPrototype?.ConfigureCivilianEscapeRoute(checkpointNormalized, cityExitNormalized, stage);
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
            new Vector2(900f, 600f), new Vector2(0f, 20f), Ink);
        AddBorder(panel, new Color(Amber.r, Amber.g, Amber.b, 0.9f));
        Box("Clue Reading Top Accent", panel, new Vector2(0.5f, 1f), new Vector2(900f, 6f),
            new Vector2(0f, -3f), Amber);

        clueReadingEyebrow = Text(panel, "Clue Reading Eyebrow", string.Empty, 12f, Amber, FontStyles.Bold,
            TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(820f, 24f), new Vector2(38f, -30f));
        clueReadingTitle = Text(panel, "Clue Reading Title", string.Empty, 29f, Color.white, FontStyles.Bold,
            TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(820f, 46f), new Vector2(38f, -67f));

        RectTransform paper = Box("Clue Document", panel, new Vector2(0.5f, 0.5f), new Vector2(822f, 378f),
            new Vector2(0f, -25f), new Color(0.105f, 0.125f, 0.105f, 1f));
        AddBorder(paper, new Color(0.42f, 0.45f, 0.34f, 0.8f));
        clueReadingBody = Text(paper, "Clue Reading Body", string.Empty, 16f,
            new Color(0.93f, 0.91f, 0.78f, 1f), FontStyles.Normal, TextAlignmentOptions.TopLeft,
            new Vector2(0f, 1f), new Vector2(754f, 282f), new Vector2(30f, -27f));
        clueReadingConclusion = Text(paper, "Clue Reading Conclusion", string.Empty, 15f, Mint,
            FontStyles.Bold, TextAlignmentOptions.Left, new Vector2(0f, 0f),
            new Vector2(754f, 56f), new Vector2(30f, 29f));

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
            new Vector2(520f, 150f), new Vector2(0f, 20f), new Color(0.09f, 0.15f, 0.12f, 1f));
        AddBorder(completionRewardCard, new Color(Mint.r, Mint.g, Mint.b, 0.88f));
        Text(completionRewardCard, "Completion Reward Header", "PHẦN THƯỞNG ĐÃ NHẬN", 12f, Mint, FontStyles.Bold,
            TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(470f, 24f), new Vector2(0f, -18f));
        completionRewardText = Text(completionRewardCard, "Completion Reward Text", string.Empty, 23f,
            Color.white, FontStyles.Bold, TextAlignmentOptions.Center, new Vector2(0.5f, 0f),
            new Vector2(470f, 58f), new Vector2(0f, 34f));

        GameObject rewardMapObject = new GameObject("Map Fragment Reward Art", typeof(RectTransform), typeof(RawImage));
        rewardMapObject.transform.SetParent(completionRewardCard, false);
        completionRewardMapRect = rewardMapObject.GetComponent<RectTransform>();
        completionRewardMapRect.anchorMin = completionRewardMapRect.anchorMax = completionRewardMapRect.pivot = new Vector2(0.5f, 0.5f);
        // Match the source aspect ratio so the torn paper is not stretched.
        completionRewardMapRect.sizeDelta = new Vector2(90f, 82f);
        completionRewardMapRect.anchoredPosition = new Vector2(0f, -20f);
        completionRewardMapImage = rewardMapObject.GetComponent<RawImage>();
        completionRewardDefaultTexture = Resources.Load<Texture2D>("QuestUI/MapFragmentReward");
        completionRewardMapImage.texture = completionRewardDefaultTexture;
        completionRewardMapImage.color = Color.white;
        completionRewardMapImage.raycastTarget = false;
        completionRewardMapImage.gameObject.SetActive(false);

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

    private void PlayQuestCompletion(string header, string questName, string reward, Action onFinished,
        bool showMapFragmentArt = false)
    {
        if (completionRoutine != null)
            StopCoroutine(completionRoutine);
        completionRoutine = StartCoroutine(QuestCompletionRoutine(
            header, questName, reward, onFinished, showMapFragmentArt));
    }

    private IEnumerator QuestCompletionRoutine(string header, string questName, string reward, Action onFinished,
        bool showMapFragmentArt)
    {
        SetJournalOpen(false);
        if (mapPrototype != null) mapPrototype.SetOpen(false);
        completionQuestName.text = questName;
        completionRewardText.text = reward;
        bool hasRewardArt = showMapFragmentArt && completionRewardMapImage != null &&
                            completionRewardMapImage.texture != null;
        completionRewardText.gameObject.SetActive(!hasRewardArt);
        if (completionRewardMapImage != null) completionRewardMapImage.gameObject.SetActive(hasRewardArt);
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
        MakeClickable(reward, ShowHospitalRadioTranscript);

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
            "Story/CarUI/CarBattery", "ẮC QUY", true,
            new[] { "ArrivalCarBattery", "Ắc quy xe", "CarBattery", "Ắc quy" });
        BuildCarRequirementCard(root, 4, ArrivalCarItemKind.Tire,
            "Story/CarUI/CarTire", "LỐP XE", true,
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

        TextMeshProUGUI itemNameText = Text(card, "Item Name", label, 10f, Color.white, FontStyles.Bold,
            TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(101f, 22f), new Vector2(54f, -9f));
        TextMeshProUGUI requirementText = Text(card, "Requirement Type", required ? "BẮT BUỘC" : "KHÔNG BẮT BUỘC", 8f,
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
            ItemNameText = itemNameText,
            RequirementText = requirementText,
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
            view.ItemNameText.text = view.ItemKind switch
            {
                ArrivalCarItemKind.Toolbox => L("TOOLBOX", "BỘ DỤNG CỤ"),
                ArrivalCarItemKind.Hammer => L("REPAIR HAMMER", "BÚA SỬA"),
                ArrivalCarItemKind.FuelCan => L("FUEL", "NHIÊN LIỆU"),
                ArrivalCarItemKind.Battery => L("BATTERY", "ẮC QUY"),
                _ => L("TIRE", "LỐP XE")
            };
            view.RequirementText.text = view.Required ? L("REQUIRED", "BẮT BUỘC") : L("OPTIONAL", "KHÔNG BẮT BUỘC");
            view.StateText.text = retainedTool
                ? L("KEEP", "GIỮ LẠI")
                : applied
                    ? view.ItemKind == ArrivalCarItemKind.FuelCan ? L("USED", "ĐÃ DÙNG") : L("INSTALLED", "ĐÃ LẮP")
                    : available ? L("OWNED", "ĐÃ CÓ") : view.Required ? L("MISSING", "THIẾU") : L("NOT INSTALLED", "CHƯA LẮP");
            view.StateText.color = available
                ? Mint
                : view.Required ? new Color(0.95f, 0.25f, 0.2f) : Muted;
            view.CardBackground.color = available
                ? new Color(Mint.r, Mint.g, Mint.b, 0.11f)
                : new Color(0.055f, 0.06f, 0.058f, 1f);
        }

        if (selectedQuestIndex == 2 && !mainQuestProgress.ArrivalCarRepaired && requiredTotal > 0)
        {
            bool repairsComplete = ArrivalCarRepairRules.IsRequiredRepairComplete(
                mainQuestProgress.ArrivalCarRepairMask);
            SetCurrentObjective(repairsComplete
                    ? L("Return to the car and press START CAR", "Quay lại xe và bấm KHỞI ĐỘNG XE")
                    : L("Collect the required repair items", "Thu thập vật phẩm bắt buộc để sửa xe"),
                repairsComplete ? L("READY TO START", "SẴN SÀNG KHỞI ĐỘNG") : requiredOwned + " / " + requiredTotal + L(" REQUIRED", " BẮT BUỘC"),
                repairsComplete ? 1f : requiredOwned / (float)requiredTotal,
                repairsComplete || requiredOwned == requiredTotal ? Mint : Amber);
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
            ? L("ESCAPE ROUTE B  /  FINALE LOCKED", "TUYẾN THOÁT HIỂM B  /  ĐÃ KHÓA FINALE")
            : lockedEscapeRoute == EscapeEndingRoute.CivilianCar
                ? L("ESCAPE ROUTE B  /  UNAVAILABLE", "TUYẾN THOÁT HIỂM B  /  KHÔNG CÒN KHẢ DỤNG")
                : L("ESCAPE ROUTE B  /  MILITARY EVACUATION", "TUYẾN THOÁT HIỂM B  /  SƠ TÁN QUÂN SỰ");
        detailEyebrow.color = Amber;
        PreMilitaryQuestStage stage = GetPresentedQuestStage();
        if (stage == PreMilitaryQuestStage.CityMapFound && hasMilitarySnapshot)
        {
            ShowMilitaryQuestDetails();
            UpdateTrackingPresentation();
            return;
        }
        switch (stage)
        {
            case PreMilitaryQuestStage.LocateOffice:
                detailTitle.text = L("FIND THE HOSPITAL COORDINATION SECTION", "TÌM KHU ĐIỀU PHỐI TRONG BỆNH VIỆN");
                storyText.text = L(
                    "The three evacuation records all reference the same hospital dispatch channel. Their reconstructed map marks the Coordination Section inside that hospital.",
                    "Ba hồ sơ sơ tán đều nhắc tới cùng một kênh điều phối của bệnh viện. Mảnh bản đồ vừa ghép đã đánh dấu Khu Điều phối bên trong bệnh viện đó.");
                break;
            case PreMilitaryQuestStage.FindCityMap:
                detailTitle.text = L("RESTORE THE HOSPITAL RADIO ROUTE", "MỞ ĐƯỜNG TỚI TRẠM RADIO");
                storyText.text = L(
                    "The hospital shift records lead from reception to the chief-shift office and its shared spare key. Use it to open the auxiliary Radio station behind the hospital.",
                    "Sổ trực bệnh viện dẫn từ quầy tiếp tân tới văn phòng trưởng ca và chìa khóa dự phòng dùng chung. Dùng chìa khóa mở Trạm Radio phụ trợ phía sau bệnh viện.");
                break;
            case PreMilitaryQuestStage.CityMapFound:
                detailTitle.text = L("MILITARY ROUTE IDENTIFIED", "ĐÃ XÁC ĐỊNH TUYẾN QUÂN SỰ");
                storyText.text = L(
                    "The office records reveal the road to the military base. Prepare before following the marked route.",
                    "Hồ sơ trong văn phòng đã hé lộ đường tới căn cứ quân sự. Hãy chuẩn bị trước khi đi theo tuyến được đánh dấu.");
                break;
            default:
                detailTitle.text = L("RECOVER THE EVACUATION RECORDS", "THU THẬP HỒ SƠ SƠ TÁN");
                storyText.text = L(
                    "The emergency broadcast says eastern supply records may reveal another way out. Search the nearby houses while the broken car remains repairable.",
                    "Thông báo khẩn cho biết hồ sơ tiếp tế phía đông có thể hé lộ một đường thoát khác. Hãy tìm trong các căn nhà gần đó trong khi chiếc xe vẫn có thể sửa.");
                break;
        }

        bool cluesComplete = mainQuestProgress.HasMapFragment1 ||
                             stage >= PreMilitaryQuestStage.LocateOffice;
        bool officeFound = mainQuestProgress.OfficeDiscovered ||
                           stage >= PreMilitaryQuestStage.FindCityMap;
        bool militaryMapFound = mainQuestProgress.HasMapFragment2 ||
                                stage >= PreMilitaryQuestStage.CityMapFound;
        bool searchingClues = !cluesComplete;
        SetObjective(0, L("Find 3 supply and evacuation records", "Tìm 3 tài liệu về tuyến tiếp tế và sơ tán"),
            cluesComplete
                ? L("COMPLETE", "HOÀN THÀNH")
                : L("FOUND  ", "ĐÃ TÌM THẤY  ") + mainQuestProgress.RouteClueCount + " / " +
                  PreMilitaryQuestProgress.RequiredRouteClues + L(" CLUES", " MANH MỐI"),
            searchingClues, cluesComplete ? Mint : Amber);

        string officeLocationStatus;
        if (officeFound) officeLocationStatus = L("FOUND", "ĐÃ TÌM THẤY");
        else if (cluesComplete) officeLocationStatus = L("EXACT LOCATION", "VỊ TRÍ CHÍNH XÁC");
        else officeLocationStatus = L("LOCKED", "ĐANG KHÓA");
        bool locatingOffice = cluesComplete && !officeFound;
        SetObjective(1, L("Find the Coordination Section inside the marked hospital", "Tìm Khu Điều phối bên trong bệnh viện được đánh dấu"), officeLocationStatus, locatingOffice,
            officeFound ? Mint : cluesComplete ? Purple : Muted);

        int hospitalRadioPercent = Mathf.RoundToInt(authoritativeHospitalRadioProgress * 100f);
        string investigationStatus = militaryMapFound
            ? L("FRAGMENT 2 ACQUIRED", "ĐÃ CÓ MẢNH 2")
            : hasAuthoritativeQuestStage && authoritativeHospitalStage >= 5 && hospitalRadioPercent > 0
                ? L("RESTORING  ", "ĐANG KHÔI PHỤC  ") + hospitalRadioPercent + "%"
            : hasAuthoritativeQuestStage && authoritativeHospitalStage >= 5 ? L("RADIO READY", "RADIO SẴN SÀNG") :
            hasAuthoritativeQuestStage && authoritativeHospitalStage >= 4 ? L("SHARED KEY ACQUIRED", "ĐÃ CÓ CHÌA KHÓA CHUNG") :
            hasAuthoritativeQuestStage && authoritativeHospitalStage >= 3 ? L("KEY LOCATION MARKED", "ĐÃ ĐÁNH DẤU VỊ TRÍ CHÌA") :
            !hasAuthoritativeQuestStage && mainQuestProgress.OfficeInvestigationComplete ? L("INVESTIGATED", "ĐÃ KIỂM TRA") :
            !hasAuthoritativeQuestStage && officeFound ? L("FIND FRAGMENT 2", "TÌM MẢNH 2") :
            officeFound ? L("FOLLOW THE SHIFT LOGS", "LẦN THEO SỔ TRỰC") : L("NOT OPEN", "CHƯA MỞ");
        bool investigatingOffice = officeFound && !militaryMapFound;
        SetObjective(2, L("Follow the shift logs and open the Radio room", "Lần theo sổ trực và mở phòng Radio"), investigationStatus, investigatingOffice,
            militaryMapFound ? Mint : officeFound ? Purple : Muted);

        rewardLabel.text = L("MISSION REWARD", "PHẦN THƯỞNG NHIỆM VỤ");
        rewardText.text = stage switch
        {
            PreMilitaryQuestStage.LocateOffice => L("Map Fragment 1 — hospital location",
                "Mảnh bản đồ 1 — vị trí bệnh viện"),
            PreMilitaryQuestStage.FindCityMap => L("Military route map + base waypoint",
                "Bản đồ tuyến quân sự + waypoint căn cứ"),
            PreMilitaryQuestStage.CityMapFound => L("Military base route unlocked",
                "Đã mở tuyến tới căn cứ quân sự"),
            _ => L("3 records reconstruct Map Fragment 1",
                "3 hồ sơ ghép thành Mảnh bản đồ 1")
        };

        contextPanelTitle.text = L("MISSION CLUES", "MANH MỐI NHIỆM VỤ");
        contextPanelCount.text = mainQuestProgress.RouteClueCount + " / " +
                                 PreMilitaryQuestProgress.RequiredRouteClues;

        if (stage <= PreMilitaryQuestStage.SearchNeighborhood && !cluesComplete)
            SetCurrentObjective(L("Find 3 supply and evacuation records", "Tìm 3 tài liệu về tuyến tiếp tế và sơ tán"),
                mainQuestProgress.RouteClueCount + " / " + PreMilitaryQuestProgress.RequiredRouteClues,
                mainQuestProgress.RouteClueCount / (float)PreMilitaryQuestProgress.RequiredRouteClues, Amber);
        else if (stage == PreMilitaryQuestStage.LocateOffice || !officeFound)
            SetCurrentObjective(L("Find the Coordination Section inside the marked hospital", "Tìm Khu Điều phối bên trong bệnh viện được đánh dấu"),
                L("IDENTIFIED", "ĐÃ XÁC ĐỊNH"), 0f, Purple);
        else if (stage == PreMilitaryQuestStage.FindCityMap || !militaryMapFound)
            SetCurrentObjective(GetHospitalJournalObjective(), GetHospitalJournalStatus(),
                authoritativeHospitalStage >= 5 ? authoritativeHospitalRadioProgress : 0f, Purple);
        else
            SetCurrentObjective(L("Travel to the marked military base", "Đi tới căn cứ quân sự được đánh dấu"), L("ROUTE OPEN", "ĐÃ MỞ TUYẾN"), 0f, Purple);

        UpdateTrackingPresentation();
    }

    private string GetHospitalJournalObjective()
    {
        return authoritativeHospitalStage switch
        {
            1 => L("Read the shift log at reception", "Đọc sổ trực tại quầy tiếp tân"),
            2 => L("Check the chief-shift office behind reception", "Kiểm tra văn phòng trưởng ca phía sau quầy tiếp tân"),
            3 => L("Find the backup Radio key at the marked location", "Tìm chìa khóa Radio tại vị trí được đánh dấu"),
            4 => L("Use the shared key at the auxiliary Radio station", "Dùng chìa khóa mở Trạm liên lạc phụ trợ phía sau bệnh viện"),
            5 => L("Hold E to restore the Radio signal", "Giữ E để khôi phục tín hiệu Radio"),
            _ => L("Enter the hospital Coordination Section", "Đi vào Khu Điều phối trong bệnh viện")
        };
    }

    private string GetHospitalJournalStatus()
    {
        return authoritativeHospitalStage switch
        {
            3 => L("SEARCHING", "ĐANG TÌM"),
            4 => L("KEY ACQUIRED", "ĐÃ CÓ CHÌA KHÓA"),
            5 when authoritativeHospitalRadioProgress > 0f =>
                L("STAGE  ", "CHẶNG  ") +
                Mathf.Clamp(authoritativeHospitalRadioCheckpointCount + 1, 1, 3) + "/3  •  " +
                Mathf.RoundToInt(authoritativeHospitalRadioProgress * 100f) + "%",
            5 => L("RADIO READY", "RADIO SẴN SÀNG"),
            _ => L("INVESTIGATING", "ĐANG ĐIỀU TRA")
        };
    }

    private void ShowMilitaryQuestDetails()
    {
        if (carRepairRequirementsRoot != null) carRepairRequirementsRoot.SetActive(false);
        detailEyebrow.text = lockedEscapeRoute == EscapeEndingRoute.MilitaryEvacuation
            ? L("ESCAPE ROUTE B  /  FINALE ACTIVE", "TUYẾN THOÁT HIỂM B  /  FINALE ĐANG DIỄN RA")
            : lockedEscapeRoute == EscapeEndingRoute.CivilianCar
                ? L("ESCAPE ROUTE B  /  UNAVAILABLE", "TUYẾN THOÁT HIỂM B  /  KHÔNG CÒN KHẢ DỤNG")
                : L("ESCAPE ROUTE B  /  MILITARY EVACUATION", "TUYẾN THOÁT HIỂM B  /  SƠ TÁN QUÂN SỰ");
        detailEyebrow.color = Amber;

        string gateStatus = Mathf.RoundToInt(militaryGateCurrentHealth) + " / " +
                            Mathf.RoundToInt(militaryGateMaxHealth);
        switch (militaryPhase)
        {
            case RouteBMilitaryPresentationPhase.NotReached:
                detailTitle.text = L("SEARCH THE ABANDONED SCHOOL", "KHÁM PHÁ TRƯỜNG HỌC BỎ HOANG");
                storyText.text = L(
                    "The school inside the military zone is quiet. Search it for three traces of the evacuation without triggering an automatic event on entry.",
                    "Ngôi trường trong khu quân sự đang im lặng. Hãy tự do khám phá và kiểm tra ba dấu vết sơ tán; bước vào trường không tự kích hoạt sự kiện.");
                SetObjective(0, L("Search the school for three clues", "Tìm ba manh mối trong trường học"),
                    L("ACTIVE", "ĐANG LÀM"), true, Amber);
                SetObjective(1, L("Leave the school after finding all clues", "Rời trường sau khi đủ manh mối"), L("WAITING", "ĐANG CHỜ"), false, Muted);
                SetObjective(2, L("Activate the evacuation plan", "Kích hoạt kế hoạch sơ tán"), L("LOCKED", "ĐANG KHÓA"), false, Muted);
                SetCurrentObjective(L("Explore the school", "Khám phá trường học"),
                    L("3 CLUES", "3 MANH MỐI"), 0f, Amber);
                break;

            case RouteBMilitaryPresentationPhase.Investigating:
                detailTitle.text = L("INSPECT THE EVACUATION VEHICLE", "KIỂM TRA XE SƠ TÁN");
                storyText.text = L(
                    "The base is abandoned, but the convoy vehicle can still run. Inspect it to learn what the alarm will activate before making the irreversible evacuation call.",
                    "Căn cứ đã bị bỏ lại nhưng xe của đoàn sơ tán vẫn có thể hoạt động. Hãy kiểm tra xe để biết báo động sẽ kích hoạt điều gì trước khi ra lệnh sơ tán không thể hoàn tác.");
                SetObjective(0, L("Reach the military base", "Đi tới căn cứ quân sự"), L("COMPLETE", "HOÀN THÀNH"), false, Mint);
                SetObjective(1, L("Inspect the evacuation vehicle", "Kiểm tra xe sơ tán"), L("ACTIVE", "ĐANG LÀM"), true, Amber);
                SetObjective(2, L("Confirm the point of no return", "Xác nhận điểm không thể quay lại"), L("WAITING", "ĐANG CHỜ"), false, Muted);
                SetCurrentObjective(L("Inspect the military evacuation vehicle", "Kiểm tra xe sơ tán quân sự"),
                    L("PRESS E", "NHẤN E"), 0f, Amber);
                break;

            case RouteBMilitaryPresentationPhase.SiegeAndRepair:
                detailTitle.text = L("DEFEND AND RESTORE THE ESCAPE VEHICLE", "PHÒNG THỦ VÀ KHÔI PHỤC XE THOÁT HIỂM");
                storyText.text = L(
                    "The police-car alarm has drawn the horde. Hold the closed gate while the team completes all five repair jobs.",
                    "Còi báo động xe cảnh sát đã kéo bầy zombie tới. Hãy giữ cổng đóng trong lúc cả đội hoàn thành đủ năm hạng mục sửa xe.");
                SetObjective(0, L("Defend the closed gate", "Bảo vệ cổng đã đóng"),
                    L("HORDE ACTIVE", "HORDE ĐANG TẤN CÔNG"), true, Amber);
                SetObjective(1, L("Complete all five police-car repairs", "Hoàn thành đủ năm hạng mục sửa xe"),
                    militaryHasAllParts ? L("5 / 5 COMPLETE", "HOÀN THÀNH 5 / 5") : L("IN PROGRESS", "ĐANG THỰC HIỆN"),
                    !militaryHasAllParts, militaryHasAllParts ? Mint : Purple);
                SetObjective(2, L("Prepare to escape", "Chuẩn bị tẩu thoát"),
                    Mathf.RoundToInt(militaryVehicleRepairProgress) + "%",
                    false, militaryVehicleRepairProgress >= 100f ? Mint : Amber);
                SetCurrentObjective(L("Defend the gate and repair the police car", "Bảo vệ cổng và sửa xe cảnh sát"),
                    Mathf.RoundToInt(militaryVehicleRepairProgress) + "%",
                    militaryVehicleRepairProgress / 100f, Amber);
                break;

            case RouteBMilitaryPresentationPhase.ReadyToEscape:
                detailTitle.text = L("EVACUATION VEHICLE READY", "XE SƠ TÁN ĐÃ SẴN SÀNG");
                storyText.text = L("The vehicle is operational. Regroup the living team at the vehicle and leave the base.",
                    "Xe đã hoạt động. Tập hợp những người còn sống tại xe và rời khỏi căn cứ.");
                SetObjective(0, L("Defend the gate", "Bảo vệ cổng"), L("COMPLETE", "HOÀN THÀNH"), false, Mint);
                SetObjective(1, L("Prepare the evacuation vehicle", "Chuẩn bị xe sơ tán"), L("COMPLETE", "HOÀN THÀNH"), false, Mint);
                SetObjective(2, L("Regroup and escape", "Tập hợp và thoát khỏi căn cứ"), L("ACTIVE", "ĐANG LÀM"), true, Amber);
                SetCurrentObjective(L("Regroup at the vehicle and press E", "Tập hợp tại xe và nhấn E"),
                    L("READY", "SẴN SÀNG"), 1f, Mint);
                break;

            case RouteBMilitaryPresentationPhase.Escaped:
                detailTitle.text = L("MILITARY EVACUATION COMPLETE", "SƠ TÁN QUÂN SỰ HOÀN TẤT");
                storyText.text = L("The surviving team escaped through Route B.", "Đội sống sót đã thoát bằng Tuyến B.");
                SetObjective(0, L("Reach the military base", "Đi tới căn cứ quân sự"), L("COMPLETE", "HOÀN THÀNH"), false, Mint);
                SetObjective(1, L("Defend and repair the vehicle", "Phòng thủ và sửa xe"), L("COMPLETE", "HOÀN THÀNH"), false, Mint);
                SetObjective(2, L("Escape the base", "Thoát khỏi căn cứ"), L("COMPLETE", "HOÀN THÀNH"), false, Mint);
                SetCurrentObjective(L("Route B complete", "Tuyến B hoàn thành"), L("COMPLETE", "HOÀN THÀNH"), 1f, Mint);
                break;

            default:
                detailTitle.text = L("MILITARY EVACUATION FAILED", "SƠ TÁN QUÂN SỰ THẤT BẠI");
                storyText.text = L("No living survivor remained to continue the evacuation.",
                    "Không còn người sống sót để tiếp tục kế hoạch sơ tán.");
                SetObjective(0, L("Protect the team", "Bảo vệ đội sống sót"), L("FAILED", "THẤT BẠI"), true, new Color(0.95f, 0.25f, 0.2f));
                SetObjective(1, L("Repair the vehicle", "Sửa xe"), L("INCOMPLETE", "CHƯA XONG"), false, Muted);
                SetObjective(2, L("Escape the base", "Thoát khỏi căn cứ"), L("FAILED", "THẤT BẠI"), false, Muted);
                SetCurrentObjective(L("Route B failed", "Tuyến B thất bại"), L("FAILED", "THẤT BẠI"), 0f, new Color(0.95f, 0.25f, 0.2f));
                break;
        }

        bool complete = militaryPhase == RouteBMilitaryPresentationPhase.Escaped;
        rewardLabel.text = complete ? L("ESCAPE RESULT", "KẾT QUẢ THOÁT HIỂM") :
            militaryPhase == RouteBMilitaryPresentationPhase.NotReached
                ? L("MISSION REWARD  •  CLICK TO READ TRANSCRIPT",
                    "PHẦN THƯỞNG  •  NHẤN ĐỂ ĐỌC TRANSCRIPT")
                : L("MISSION REWARD", "PHẦN THƯỞNG NHIỆM VỤ");
        rewardText.text = militaryPhase switch
        {
            RouteBMilitaryPresentationPhase.NotReached => L("Military base access + vehicle assessment",
                "Quyền tiếp cận căn cứ + đánh giá xe sơ tán"),
            RouteBMilitaryPresentationPhase.Investigating => L("Evacuation checklist + final warning",
                "Danh sách chuẩn bị sơ tán + cảnh báo cuối"),
            RouteBMilitaryPresentationPhase.SiegeAndRepair when !militaryHasAllParts =>
                L("Complete all five repairs", "Hoàn thành đủ năm hạng mục sửa xe"),
            RouteBMilitaryPresentationPhase.SiegeAndRepair =>
                L("Vehicle restored — extraction point unlocked", "Khôi phục xe — mở điểm tập kết"),
            RouteBMilitaryPresentationPhase.ReadyToEscape =>
                L("Military extraction available", "Đã mở sơ tán quân sự"),
            RouteBMilitaryPresentationPhase.Escaped =>
                L("Military evacuation — Route B complete", "Sơ tán quân sự — hoàn thành Tuyến B"),
            _ => L("No reward", "Không có phần thưởng")
        };
        contextPanelTitle.text = L("BASE STATUS", "TRẠNG THÁI CĂN CỨ");
        contextPanelCount.text = militaryPhase == RouteBMilitaryPresentationPhase.NotReached
            ? L("NOT REACHED", "CHƯA TỚI")
            : militaryPhase == RouteBMilitaryPresentationPhase.Escaped
                ? L("EVACUATED", "ĐÃ SƠ TÁN")
                : L("GATE  ", "CỔNG  ") + gateStatus;
    }

    private void ShowSideQuestDetails()
    {
        if (carRepairRequirementsRoot != null) carRepairRequirementsRoot.SetActive(false);
        detailEyebrow.text = L("ESCAPE ROUTE B  /  EVACUATION RECORDS", "TUYẾN THOÁT HIỂM B  /  HỒ SƠ SƠ TÁN");
        detailEyebrow.color = Mint;
        detailTitle.text = L("RECONSTRUCT THE ROUTE", "GHÉP LẠI TUYẾN ĐƯỜNG");
        storyText.text = L("Collect three clues from the houses to assemble Map Fragment 1.", "Thu thập ba dấu vết trong các căn nhà để ghép thành Mảnh bản đồ 1.");

        SetObjective(0, L("Collect 3 route clues", "Thu thập 3 dấu vết tuyến đường"),
            mainQuestProgress.RouteClueCount + " / " + PreMilitaryQuestProgress.RequiredRouteClues,
            !mainQuestProgress.SideQuestResolved, mainQuestProgress.HasMapFragment1 ? Mint : Amber);
        SetObjective(1, L("Assemble Map Fragment 1", "Ghép thành Mảnh bản đồ 1"),
            mainQuestProgress.HasMapFragment1 ? L("ASSEMBLED", "ĐÃ GHÉP") :
            mainQuestProgress.SideQuestSkipped ? L("SKIPPED", "ĐÃ BỎ QUA") : L("LOCKED", "ĐANG KHÓA"),
            false, mainQuestProgress.HasMapFragment1 ? Mint : Muted);
        SetObjective(2, L("Mark the office's exact location", "Đánh dấu chính xác vị trí văn phòng"),
            mainQuestProgress.HasMapFragment1 ? L("MARKED", "ĐÃ ĐÁNH DẤU") :
            mainQuestProgress.SideQuestSkipped ? L("FOUND MANUALLY", "ĐÃ TỰ TÌM") : L("LOCKED", "ĐANG KHÓA"),
            false, mainQuestProgress.HasMapFragment1 ? Purple : Muted);
        rewardLabel.text = mainQuestProgress.HasMapFragment1 ? L("REWARD RECEIVED", "PHẦN THƯỞNG ĐÃ NHẬN") : L("REWARD", "PHẦN THƯỞNG");
        rewardText.text = mainQuestProgress.HasMapFragment1
            ? L("Map Fragment 1 — Office Location", "Mảnh bản đồ 1 — Vị trí văn phòng")
            : L("Unknown", "Chưa xác định");

        contextPanelTitle.text = L("CLUES COLLECTED", "DẤU VẾT ĐÃ THU THẬP");
        contextPanelCount.text = mainQuestProgress.RouteClueCount + " / 3";

        if (mainQuestProgress.HasMapFragment1)
            SetCurrentObjective(L("Map Fragment 1 assembled", "Mảnh bản đồ 1 đã được ghép"), L("COMPLETE", "HOÀN THÀNH"), 1f, Mint);
        else if (mainQuestProgress.SideQuestSkipped)
            SetCurrentObjective(L("The route was found another way", "Tuyến đường đã được tìm theo cách khác"), L("SKIPPED", "ĐÃ BỎ QUA"), 1f, Muted);
        else
            SetCurrentObjective(L("Collect 3 route clues", "Thu thập 3 dấu vết tuyến đường"),
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
        bool batteryReplaced = ArrivalCarRepairRules.IsApplied(mainQuestProgress.ArrivalCarRepairMask,
            ArrivalCarRepairAction.ReplaceBattery);
        bool tireReplaced = ArrivalCarRepairRules.IsApplied(mainQuestProgress.ArrivalCarRepairMask,
            ArrivalCarRepairAction.ReplaceTire);
        int completedRequiredActions = (coreRepaired ? 1 : 0) + (fuelAdded ? 1 : 0) +
                                       (batteryReplaced ? 1 : 0) + (tireReplaced ? 1 : 0);
        bool repairsComplete = ArrivalCarRepairRules.IsRequiredRepairComplete(
            mainQuestProgress.ArrivalCarRepairMask);
        detailEyebrow.text = lockedEscapeRoute == EscapeEndingRoute.CivilianCar
            ? L("ESCAPE ROUTE A  /  FINALE LOCKED", "TUYẾN THOÁT HIỂM A  /  ĐÃ KHÓA FINALE")
            : lockedEscapeRoute == EscapeEndingRoute.MilitaryEvacuation
                ? L("ESCAPE ROUTE A  /  UNAVAILABLE", "TUYẾN THOÁT HIỂM A  /  KHÔNG CÒN KHẢ DỤNG")
                : L("ESCAPE ROUTE A  /  CIVILIAN CAR", "TUYẾN THOÁT HIỂM A  /  CHIẾC XE DÂN SỰ");
        detailEyebrow.color = Mint;
        detailTitle.text = L("RESTORE THE CAR", "KHÔI PHỤC CHIẾC XE");
        storyText.text = L(
            "The car can start only after repairing the engine, adding fuel, replacing the battery and the punctured front-left tire. The ending locks only when the final escape is confirmed.",
            "Chiếc xe chỉ có thể khởi động sau khi sửa động cơ, bổ sung nhiên liệu, thay ắc quy và lốp trước trái bị thủng. Ending chỉ khóa khi xác nhận vượt vòng phong tỏa.");

        SetObjective(0, L("Repair the engine and add fuel", "Sửa động cơ và bổ sung nhiên liệu"),
            (coreRepaired ? 1 : 0) + (fuelAdded ? 1 : 0) + " / 2",
            !coreRepaired || !fuelAdded, coreRepaired && fuelAdded ? Mint : Amber);
        SetObjective(1, L("Replace the battery and front-left tire", "Thay ắc quy và lốp trước trái"),
            (batteryReplaced ? 1 : 0) + (tireReplaced ? 1 : 0) + " / 2",
            coreRepaired && fuelAdded && (!batteryReplaced || !tireReplaced),
            batteryReplaced && tireReplaced ? Mint : Amber);
        SetObjective(2, L("Return to the hood and press START CAR", "Quay lại mũi xe và bấm KHỞI ĐỘNG XE"),
            mainQuestProgress.ArrivalCarRepaired ? L("STARTED", "ĐÃ KHỞI ĐỘNG") : repairsComplete ? L("READY", "SẴN SÀNG") : L("REQUIREMENTS MISSING", "CHƯA ĐỦ ĐIỀU KIỆN"),
            repairsComplete && !mainQuestProgress.ArrivalCarRepaired,
            mainQuestProgress.ArrivalCarRepaired ? Mint : repairsComplete ? Amber : Muted);

        rewardLabel.text = mainQuestProgress.ArrivalCarRepaired ? L("REWARD UNLOCKED", "PHẦN THƯỞNG ĐÃ MỞ") : L("REWARD", "PHẦN THƯỞNG");
        rewardText.text = mainQuestProgress.ArrivalCarRepaired
            ? L("Exploration vehicle ready", "Phương tiện khám phá đã sẵn sàng")
            : L("Unlock a vehicle for exploring civilian exits", "Mở phương tiện khám phá các lối thoát dân sự");
        contextPanelTitle.text = L("VEHICLE CONDITION", "TÌNH TRẠNG PHƯƠNG TIỆN");
        contextPanelCount.text = mainQuestProgress.ArrivalCarRepaired ? L("OPERATIONAL", "HOẠT ĐỘNG") : L("DAMAGED", "HƯ HỎNG");

        SetCurrentObjective(mainQuestProgress.ArrivalCarRepaired
                ? L("The car is ready", "Chiếc xe đã sẵn sàng")
                : repairsComplete ? L("Return to the car and press START CAR", "Quay lại xe và bấm KHỞI ĐỘNG XE") : L("Complete all four repairs", "Hoàn tất bốn hạng mục sửa xe"),
            mainQuestProgress.ArrivalCarRepaired ? L("COMPLETE", "HOÀN THÀNH") :
            repairsComplete ? L("READY TO START", "SẴN SÀNG KHỞI ĐỘNG") : completedRequiredActions + " / 4",
            mainQuestProgress.ArrivalCarRepaired || repairsComplete ? 1f : completedRequiredActions / 4f,
            mainQuestProgress.ArrivalCarRepaired ? Mint : Amber);
        RefreshCarRepairRequirementStates();
        UpdateTrackingPresentation();
    }

    private void RefreshQuestPresentation()
    {
        SetNamedText("Main Quest Name", GetMainQuestCardName());
        if (mainQuestMetaText != null)
        {
            mainQuestMetaText.text = IsMainQuestComplete
                ? L("DONE", "XONG")
                : IsMainQuestFailed
                    ? L("FAILED", "THẤT BẠI")
                    : lockedEscapeRoute == EscapeEndingRoute.CivilianCar
                    ? L("CLOSED", "ĐÃ ĐÓNG")
                    : GetMainQuestCardMeta();
        }

        if (sideQuestMetaText != null)
        {
            sideQuestMetaText.text = mainQuestProgress.SideQuestSkipped
                ? L("SKIPPED", "BỎ QUA")
                : mainQuestProgress.HasMapFragment1
                    ? L("DONE", "XONG")
                    : mainQuestProgress.RouteClueCount + " / 3";
        }

        if (carQuestMetaText != null)
            carQuestMetaText.text = lockedEscapeRoute == EscapeEndingRoute.CivilianCar
                ? L("LOCKED", "ĐÃ KHÓA")
                : lockedEscapeRoute == EscapeEndingRoute.MilitaryEvacuation
                    ? L("CLOSED", "ĐÃ ĐÓNG")
                    : mainQuestProgress.ArrivalCarRepaired ? L("READY", "SẴN SÀNG") : L("PREPARING", "ĐANG CHUẨN BỊ");

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

    private PreMilitaryQuestStage GetPresentedQuestStage()
    {
        if (hasAuthoritativeQuestStage)
            return authoritativeQuestStage;
        if (mainQuestProgress.HasMapFragment2)
            return PreMilitaryQuestStage.CityMapFound;
        if (mainQuestProgress.OfficeDiscovered)
            return PreMilitaryQuestStage.FindCityMap;
        if (mainQuestProgress.HasMapFragment1)
            return PreMilitaryQuestStage.LocateOffice;
        return PreMilitaryQuestStage.SearchNeighborhood;
    }

    private string GetMainQuestCardName()
    {
        if (GetPresentedQuestStage() == PreMilitaryQuestStage.CityMapFound && hasMilitarySnapshot)
        {
            return militaryPhase switch
            {
                RouteBMilitaryPresentationPhase.NotReached => L("Search the abandoned school", "Khám phá trường học bỏ hoang"),
                RouteBMilitaryPresentationPhase.Investigating => L("Inspect the evacuation vehicle", "Kiểm tra xe sơ tán"),
                RouteBMilitaryPresentationPhase.SiegeAndRepair => L("Defend and restore the vehicle", "Phòng thủ và khôi phục xe"),
                RouteBMilitaryPresentationPhase.ReadyToEscape => L("Regroup and evacuate", "Tập hợp và sơ tán"),
                RouteBMilitaryPresentationPhase.Escaped => L("Military evacuation complete", "Sơ tán quân sự hoàn tất"),
                _ => L("Military evacuation failed", "Sơ tán quân sự thất bại")
            };
        }

        return GetPresentedQuestStage() switch
        {
            PreMilitaryQuestStage.LocateOffice =>
                L("Find the hospital Coordination Section", "Tìm Khu Điều phối trong bệnh viện"),
            PreMilitaryQuestStage.FindCityMap =>
                L("Open the auxiliary Radio station", "Mở Trạm Radio phụ trợ"),
            PreMilitaryQuestStage.CityMapFound =>
                L("Follow the military route", "Đi theo tuyến quân sự"),
            _ => L("Recover evacuation records", "Thu thập hồ sơ sơ tán")
        };
    }

    private string GetMainQuestCardMeta()
    {
        if (GetPresentedQuestStage() == PreMilitaryQuestStage.CityMapFound && hasMilitarySnapshot)
        {
            return militaryPhase switch
            {
                RouteBMilitaryPresentationPhase.NotReached => L("SEARCH SCHOOL", "KHÁM PHÁ TRƯỜNG"),
                RouteBMilitaryPresentationPhase.Investigating => L("FINAL CHECK", "KIỂM TRA CUỐI"),
                RouteBMilitaryPresentationPhase.SiegeAndRepair => L("FINALE ACTIVE", "FINALE ĐANG CHẠY"),
                RouteBMilitaryPresentationPhase.ReadyToEscape => L("ESCAPE READY", "SẴN SÀNG THOÁT"),
                RouteBMilitaryPresentationPhase.Escaped => L("COMPLETE", "HOÀN THÀNH"),
                _ => L("FAILED", "THẤT BẠI")
            };
        }

        return GetPresentedQuestStage() switch
        {
            PreMilitaryQuestStage.LocateOffice => L("STEP 2", "BƯỚC 2"),
            PreMilitaryQuestStage.FindCityMap => L("STEP 3", "BƯỚC 3"),
            PreMilitaryQuestStage.CityMapFound => L("ROUTE OPEN", "ĐÃ MỞ TUYẾN"),
            _ => mainQuestProgress.RouteClueCount + " / " + PreMilitaryQuestProgress.RequiredRouteClues
        };
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
        trackingButtonText.text = selectedIsTracked
            ? L("[V]  STOP TRACKING", "[V]  HỦY THEO DÕI")
            : L("[V]  TRACK", "[V]  THEO DÕI");
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
            switch (GetPresentedQuestStage())
            {
                case PreMilitaryQuestStage.LocateOffice:
                    objective = L("Find the Coordination Section inside the marked hospital", "Tìm Khu Điều phối bên trong bệnh viện được đánh dấu");
                    break;
                case PreMilitaryQuestStage.FindCityMap:
                    objective = GetHospitalJournalObjective();
                    break;
                case PreMilitaryQuestStage.CityMapFound:
                    if (!hasMilitarySnapshot || militaryPhase == RouteBMilitaryPresentationPhase.NotReached)
                        objective = L("Search the school for three military clues", "Tìm ba manh mối quân sự trong trường học");
                    else if (militaryPhase == RouteBMilitaryPresentationPhase.Investigating)
                        objective = L("Inspect the military evacuation vehicle", "Kiểm tra xe sơ tán quân sự");
                    else if (militaryPhase == RouteBMilitaryPresentationPhase.SiegeAndRepair)
                        objective = L("Defend the gate and repair the police car  •  ",
                                        "Bảo vệ cổng và sửa xe cảnh sát  •  ") +
                                    Mathf.RoundToInt(militaryVehicleRepairProgress) + "%";
                    else if (militaryPhase == RouteBMilitaryPresentationPhase.ReadyToEscape)
                        objective = L("Regroup at the vehicle and escape", "Tập hợp tại xe và thoát khỏi căn cứ");
                    else
                        return false;
                    break;
                default:
                    objective = L("Find supply and evacuation records  •  ", "Tìm tài liệu về tuyến tiếp tế và sơ tán  •  ") +
                                mainQuestProgress.RouteClueCount + "/" + PreMilitaryQuestProgress.RequiredRouteClues;
                    break;
            }
        }
        else if (trackedQuestIndex == 1)
        {
            if (mainQuestProgress.SideQuestResolved)
                return false;
            objective = L("Collect route clues  •  ", "Thu thập dấu vết tuyến đường  •  ") +
                        mainQuestProgress.RouteClueCount + "/" + PreMilitaryQuestProgress.RequiredRouteClues;
        }
        else
        {
            if (!mainQuestProgress.ArrivalCarRepairUnlocked || mainQuestProgress.ArrivalCarRepaired)
                return false;
            objective = L("ROUTE A: Find repair tools and a fuel can", "TUYẾN A: Tìm dụng cụ sửa chữa và can nhiên liệu");
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
            emptyStateTitle.text = completed
                ? L("NO COMPLETED MISSIONS", "CHƯA CÓ NHIỆM VỤ HOÀN THÀNH")
                : L("NO FAILED MISSIONS", "CHƯA CÓ NHIỆM VỤ THẤT BẠI");
            emptyStateBody.text = completed
                ? L("Completed missions are stored here for later review.", "Nhiệm vụ hoàn thành sẽ được lưu tại đây để người chơi xem lại.")
                : L("Failed missions appear here with their cause and retry conditions.", "Nhiệm vụ thất bại sẽ xuất hiện tại đây cùng nguyên nhân và điều kiện thử lại.");
        }
    }

    private void UpdateTabCounts()
    {
        if (tabTexts[0] == null)
            return;

        string[] labels = { L("ACTIVE", "ĐANG LÀM"), L("COMPLETED", "HOÀN THÀNH") };
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
            if (tabIndex == 0) return !IsMainQuestComplete;
            if (tabIndex == 1) return IsMainQuestComplete;
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
