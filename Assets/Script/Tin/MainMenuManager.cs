using Fusion;
using Fusion.Sockets;
using Photon.Voice.Fusion;
using Photon.Voice.Unity;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class AutoMainMenuManager : MonoBehaviour, INetworkRunnerCallbacks
{
    public static AutoMainMenuManager Instance { get; private set; }
    public static bool EscapeConsumedThisFrame = false;

    [Header("Cài đặt chung")]
    public TMP_FontAsset gameFont;
    public Sprite backgroundImage;

    [Header("Âm thanh Menu")]
    public AudioClip menuBGM;
    [Range(0f, 1f)] public float bgmVolume = 0.5f;
    private AudioSource bgmSource;

    [Header("Âm thanh Nút bấm (SFX)")]
    public AudioClip hoverSound;
    public AudioClip clickSound;
    [Range(0f, 1f)] public float sfxVolume = 0.8f;
    private AudioSource sfxSource;

    [Header("Cài đặt Fusion Network")]
    public NetworkRunner runnerPrefab;
    public int mainSceneIndex = 1;

    [Header("Hình ảnh Nhân vật")]
    public GameObject[] previewImages;

    private bool isCreditsOpen = false;
    public float creditsScrollSpeed = 30f;

    // 🔥 CÁC BIẾN CHỨA GIAO DIỆN
    private Canvas mainCanvas;
    private GameObject mainPanel, newGamePanel, multiplayerPanel, characterSelectPanel, optionsPanel, creditsPanel;
    private GameObject waitingRoomPanel;
    private GameObject connectionPopupPanel;

    // 🔥 BIẾN CHO MÀN HÌNH LOADING
    private GameObject loadingScreenPanel;
    private RectTransform loadingFillBar;
    private TextMeshProUGUI loadingPercentText;
    private Coroutine loadingCoroutine;

    private bool isLoadingScreenActive = false;
    private bool isLocalSceneLoaded = false; // Máy này đã tải xong Map chưa
    private bool isHostSignaledGo = false;   // Host đã phát lệnh vào game chưa

    private CanvasGroup currentActivePanel;

    private TextMeshProUGUI waitingRoomHostStatusText;
    private TextMeshProUGUI connectionPopupText;
    private Coroutine connectionAnimRoutine;

    private string pendingRoomName = "";
    private bool pendingIsHost = false;
    private bool pendingIsSolo = false;
    private string pendingJoinPassword = "";

    private int hostDifficulty = 1;
    private int hostMaxPlayers = 4;
    private bool hostHasPassword = false;
    private string hostPassword = "";

    private TextMeshProUGUI maxPlayersText; // Hiển thị con số hiện tại
    
    private int tempResIndex = 3;
    private int tempWindowMode = 0;
    private float tempBrightness = 1.0f;
    private int tempFpsIndex = 1;
    private float tempSensitivity = 1.0f;
    private float tempMasterVolume = 1.0f;
    private float tempMusicVolume = 0.5f;
    private float tempSFXVolume = 0.8f;

    // Các biến tạm thời mới
    private int tempQualityLevel = 2;       // 0=Low, 1=Medium, 2=High
    private int tempShadowQuality = 2;      // 0=Disabled, 1=Hard, 2=Soft
    private int tempAntiAliasing = 2;       // 0=Off, 1=2x, 2=4x, 3=8x
    private int tempShowFPS = 1;            // 0=Off, 1=On
    private int tempFPSPosition = 0;        // 0=TopRight, 1=TopLeft, 2=BottomRight, 3=BottomLeft, 4=Center
    private float tempZoomSensitivity = 1.0f; // 0.5f đến 2.0f

    // Đối tượng Tab area và Text hiển thị
    private int activeTab = 0; // 0 = Display, 1 = Controls, 2 = Audio
    private GameObject displayTabArea;
    private GameObject controlsTabArea;
    private GameObject audioTabArea;

    private TextMeshProUGUI displayTabBtnText;
    private TextMeshProUGUI controlsTabBtnText;
    private TextMeshProUGUI audioTabBtnText;

    private TextMeshProUGUI qualityValText;
    private TextMeshProUGUI shadowValText;
    private TextMeshProUGUI aaValText;
    private TextMeshProUGUI fpsShowValText;
    private TextMeshProUGUI zoomSensValText;

    // Pause menu options UI fields
    private TextMeshProUGUI pQualText;
    private TextMeshProUGUI pShadText;
    private TextMeshProUGUI pAAText;
    private TextMeshProUGUI pFpsText;
    private TextMeshProUGUI pBrightText;
    private TextMeshProUGUI pFpsShowText;
    private TextMeshProUGUI pSensText;
    private TextMeshProUGUI pZoomText;
    private TextMeshProUGUI pVolText;
    private TextMeshProUGUI pMusText;
    private TextMeshProUGUI pSfxText;

    // 🔥 SLIDER REFERENCES - Dùng để đồng bộ giá trị slider giữa Main Menu và Pause Menu
    // Main Menu sliders
    private Slider sliderBrightness;
    private Slider sliderSensitivity;
    private Slider sliderZoomSensitivity;
    private Slider sliderMasterVolume;
    private Slider sliderMusicVolume;
    private Slider sliderSFXVolume;
    // Pause Menu sliders
    private Slider pSliderBrightness;
    private Slider pSliderSensitivity;
    private Slider pSliderZoomSensitivity;
    private Slider pSliderMasterVolume;
    private Slider pSliderMusicVolume;
    private Slider pSliderSFXVolume;

    private Vector2Int[] commonResolutions = new Vector2Int[]
    {
        new Vector2Int(1280, 720),
        new Vector2Int(1366, 768),
        new Vector2Int(1600, 900),
        new Vector2Int(1920, 1080)
    };
    private int[] fpsOptions = new int[] { 30, 60, 120, -1 };
    private string[] fpsLabels = new string[] { "30 FPS", "60 FPS", "120 FPS", "UNLIMITED" };
    private TextMeshProUGUI fpsValText;
    private TextMeshProUGUI sensValText;
    private TextMeshProUGUI brightValText;
    private TextMeshProUGUI volValText;
    private TextMeshProUGUI musicValText;
    private TextMeshProUGUI sfxValText;
    private TextMeshProUGUI resDropdownText;
    private TextMeshProUGUI modeDropdownText;
    private TextMeshProUGUI fpsPosDropdownText;
    private TextMeshProUGUI pFpsPosDropdownText;

    private string[] windowModeLabels = new string[] { "FULLSCREEN", "BORDERLESS", "WINDOWED" };
    private GameObject activeDropdownOverlay = null;
    
    // Biến cho bảng thông tin độ khó ở mục Solo
    private GameObject diffInfoPanel;
    private TextMeshProUGUI diffTitleText;
    private TextMeshProUGUI diffDescText;
    private TextMeshProUGUI diffStatsText;
    
    // Biến cho danh sách người chơi trong Waiting Room
    private RectTransform waitingRoomPlayerListContent;

    private TextMeshProUGUI[] diffTexts = new TextMeshProUGUI[3];
    private TextMeshProUGUI toggleText;
    private GameObject passwordInputObj;

    private RectTransform serverListContent;
    private GameObject passPromptPanel;
    private TMP_InputField joinPassInput;

    private int previewID = 0;
    private string[] characterNames = { "Survivor: Unknown", "Survivor: Phantom" };
    private string[] characterStats = {
        "<color=#ff5555>SKILL: TERMINAL FRENZY</color>\nExtreme survival instinct. Killing 5 mutants triggers an adrenaline rush. Removes weapon recoil and grants infinite ammo for 10 seconds.\n<color=#aaaaaa>[Cooldown: 50s]</color>",
        "<color=#55ffff>SKILL: SILENT SHADOW</color>\nBorn to hide. Lowering your stance synchronizes your heartbeat with the environment. Completely fools mutant senses for 5 seconds.\n<color=#aaaaaa>[Cooldown: 30s]</color>"
    };

    private TextMeshProUGUI charNameText;
    private TextMeshProUGUI charStatsText;
    private TMP_InputField playerNameInput;
    private RectTransform previewContainer;

    private List<GameObject> temporarilyDisabledObjects = new List<GameObject>();

    private NetworkRunner lobbyRunner;
    private NetworkRunner activeRunner;

    private RectTransform creditsContent;
    private bool isConnecting = false;
    private bool isMenuDestroyed = false;


    private GameObject errorPopupPanel;
    private TextMeshProUGUI errorPopupText;

    private int playersLoaded = 0;

    private bool hasDetectedGameStart = false;

    // 🔥 BIẾN CHO PAUSE MENU
    private GameObject pauseMenuPanel;
    private bool isPauseMenuOpen = false;
    private GameObject pauseOptionsPanel;
    private bool isPauseOptionsOpen = false;

    // 🔥 BIẾN CHO CHARACTER ANIMATION
    private string[][] characterResourcePaths = {
        new string[] { "CharacterPreview/Survivor1", "Run", "Attack1", "Taunt" },
        new string[] { "CharacterPreview/Survivor2", "Run", "Attack1", "Taunt" }
    };
    private GameObject backgroundImageObj;

    public void UpdateBackgroundBrightness(float brightness)
    {
        if (backgroundImageObj != null)
        {
            Image bgImg = backgroundImageObj.GetComponent<Image>();
            if (bgImg != null)
            {
                float factor = Mathf.Clamp(brightness, 0.2f, 1.8f);
                if (backgroundImage != null)
                {
                    bgImg.color = new Color(factor, factor, factor, 1f);
                }
                else
                {
                    bgImg.color = new Color(0.08f * factor, 0.08f * factor, 0.08f * factor, 1f);
                }
            }
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject.transform.root.gameObject);

        // Khởi tạo GlobalSettingsManager nếu chưa có để áp dụng cấu hình âm thanh, ánh sáng, độ nhạy
        if (GlobalSettingsManager.Instance == null)
        {
            new GameObject("GlobalSettingsManager", typeof(GlobalSettingsManager));
        }

        GenerateEntireMenu();

        UpdateAudioSettings();

        if (menuBGM != null)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.clip = menuBGM; bgmSource.loop = true; bgmSource.volume = bgmVolume;
            bgmSource.Play();
        }

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
    }

    public void PlayHoverSFX() { if (hoverSound != null && sfxSource != null) sfxSource.PlayOneShot(hoverSound, sfxVolume); }
    public void PlayClickSFX() { if (clickSound != null && sfxSource != null) sfxSource.PlayOneShot(clickSound, sfxVolume); }

    private IEnumerator Start()
    {
        yield return new WaitForEndOfFrame();
        string[] targetNames = { "AutoChatCanvas", "--- AUTO CHAT MANAGER ---", "--- AUTO HEALTH CANVAS ---", "--- AUTO HEALTH MANAGER ---", "HealthPanel" };
        foreach (string target in targetNames)
        {
            GameObject foundObj = GameObject.Find(target);
            if (foundObj != null && foundObj.activeSelf) { foundObj.SetActive(false); temporarilyDisabledObjects.Add(foundObj); }
        }
    }

    private void Update()
    {
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null
            && EventSystem.current.currentSelectedGameObject.GetComponent<TMP_InputField>() == null
            && EventSystem.current.currentSelectedGameObject.GetComponent<InputField>() == null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        // GameState detection cho Client
        if (activeRunner != null && !activeRunner.IsServer && !isLoadingScreenActive && !hasDetectedGameStart && activeRunner.IsCloudReady)
        {
            if (activeRunner.SessionInfo?.Properties != null)
            {
                if (activeRunner.SessionInfo.Properties.TryGetValue("GameState", out SessionProperty stateProp))
                {
                    if ((int)stateProp == 1)
                    {
                        hasDetectedGameStart = true; // Khóa chốt lại ngay lập tức! Đừng gọi lại nữa!
                        ShowLoadingScreen();
                    }
                }
            }
        }

        if (isCreditsOpen && creditsContent != null)
        {
            // Đẩy khung chữ lên trên liên tục mỗi khung hình
            creditsContent.anchoredPosition += new Vector2(0, creditsScrollSpeed * Time.deltaTime);
        }
    }

    private void LateUpdate()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isLocalSceneLoaded && !isLoadingScreenActive)
            {
                if (EscapeConsumedThisFrame)
                {
                    EscapeConsumedThisFrame = false;
                    return;
                }

                if (isPauseOptionsOpen)
                {
                    ClosePauseOptions(false); // Close options and show pause menu
                }
                else if (isPauseMenuOpen)
                {
                    TogglePauseMenu(); // Đang bật Pause thì tắt
                }
                else
                {
                    bool isAnyUIOpen = false;

                    // Hỏi các UI khác xem có đang mở không
                    if (AutoUIManager.Instance != null && AutoUIManager.Instance.IsAnyMenuOpen()) isAnyUIOpen = true;
                    if (AutoHealthPanel.Instance != null && AutoHealthPanel.Instance.IsOpen) isAnyUIOpen = true;
                    if (AutoChatManager.Instance != null && AutoChatManager.Instance.IsTyping()) isAnyUIOpen = true;

                    // Chỉ bật Pause Menu khi KHÔNG có UI nào đang che màn hình
                    if (!isAnyUIOpen)
                    {
                        TogglePauseMenu();
                    }
                }
            }
        }
        EscapeConsumedThisFrame = false;
    }

    // 1. HÀM TẠO GIAO DIỆN IN-GAME MENU
    private void GeneratePauseMenuPanel(GameObject canvasGO)
    {
        pauseMenuPanel = CreateBasePanel("PauseMenuPanel", canvasGO);

        // Nền mờ nhẹ toàn màn hình (vẫn nhìn thấy game phía sau)
        pauseMenuPanel.AddComponent<Image>().color = new Color(0, 0, 0, 0.4f);

        // Khung Menu chính giữa
        GameObject boxObj = new GameObject("PauseBox");
        boxObj.transform.SetParent(pauseMenuPanel.transform, false);
        RectTransform boxRt = boxObj.AddComponent<RectTransform>();
        boxRt.anchorMin = new Vector2(0.35f, 0.3f); boxRt.anchorMax = new Vector2(0.65f, 0.7f);
        boxRt.offsetMin = Vector2.zero; boxRt.offsetMax = Vector2.zero;
        boxObj.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.95f);

        // Viền trang trí
        Outline outline = boxObj.AddComponent<Outline>();
        outline.effectColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        outline.effectDistance = new Vector2(2, -2);

        CreateTitleText(boxObj, "PAUSED", 0.85f, 45);

        // Nút bấm
        CreateMenuButton(boxObj, "RESUME", () => TogglePauseMenu(), new Vector2(0.5f, 0.6f), true, new Vector2(300, 50), 22);
        CreateMenuButton(boxObj, "OPTIONS", () => OpenPauseOptions(), new Vector2(0.5f, 0.45f), true, new Vector2(300, 50), 22);
        CreateMenuButton(boxObj, "QUIT", () => LeaveGame(), new Vector2(0.5f, 0.3f), true, new Vector2(300, 50), 22);

        pauseMenuPanel.SetActive(false);

        // ===== PAUSE OPTIONS PANEL =====
        GeneratePauseOptionsPanel(canvasGO);
    }

    private void GeneratePauseOptionsPanel(GameObject canvasGO)
    {
        pauseOptionsPanel = CreateBasePanel("PauseOptionsPanel", canvasGO);
        pauseOptionsPanel.AddComponent<Image>().color = new Color(0, 0, 0, 0.5f);

        // Container chính
        GameObject settingsArea = new GameObject("PauseSettings_Container");
        settingsArea.transform.SetParent(pauseOptionsPanel.transform, false);
        RectTransform rect = settingsArea.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.15f, 0.15f);
        rect.anchorMax = new Vector2(0.85f, 0.90f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        settingsArea.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.08f, 0.97f);

        CreateTitleText(pauseOptionsPanel, "OPTIONS", 0.95f, 35);

        // Tạo Tab containers
        GameObject pDisplayTab = new GameObject("PDisplayTab");
        pDisplayTab.transform.SetParent(settingsArea.transform, false);
        RectTransform pdRt = pDisplayTab.AddComponent<RectTransform>();
        pdRt.anchorMin = Vector2.zero; pdRt.anchorMax = new Vector2(1f, 0.92f);
        pdRt.offsetMin = Vector2.zero; pdRt.offsetMax = Vector2.zero;

        GameObject pControlsTab = new GameObject("PControlsTab");
        pControlsTab.transform.SetParent(settingsArea.transform, false);
        RectTransform pcRt = pControlsTab.AddComponent<RectTransform>();
        pcRt.anchorMin = Vector2.zero; pcRt.anchorMax = new Vector2(1f, 0.92f);
        pcRt.offsetMin = Vector2.zero; pcRt.offsetMax = Vector2.zero;

        GameObject pAudioTab = new GameObject("PAudioTab");
        pAudioTab.transform.SetParent(settingsArea.transform, false);
        RectTransform paRt = pAudioTab.AddComponent<RectTransform>();
        paRt.anchorMin = Vector2.zero; paRt.anchorMax = new Vector2(1f, 0.92f);
        paRt.offsetMin = Vector2.zero; paRt.offsetMax = Vector2.zero;

        // Tab bar
        GameObject pTabBar = new GameObject("PTabBar");
        pTabBar.transform.SetParent(settingsArea.transform, false);
        RectTransform ptbRt = pTabBar.AddComponent<RectTransform>();
        ptbRt.anchorMin = new Vector2(0f, 0.92f); ptbRt.anchorMax = new Vector2(1f, 1.0f);
        ptbRt.offsetMin = Vector2.zero; ptbRt.offsetMax = Vector2.zero;
        pTabBar.AddComponent<Image>().color = new Color(0.04f, 0.04f, 0.04f, 1f);

        // Biến local cho tab switching
        TextMeshProUGUI pDispBtnTxt, pCtrlBtnTxt, pAudBtnTxt;
        int pauseTabIndex = 0;

        System.Action<int> showPauseTab = null;
        showPauseTab = (int idx) => {
            pauseTabIndex = idx;
            pDisplayTab.SetActive(idx == 0);
            pControlsTab.SetActive(idx == 1);
            pAudioTab.SetActive(idx == 2);
        };

        CreateTabButton(pTabBar, "DISPLAY", () => showPauseTab(0), new Vector2(0.22f, 0.1f), new Vector2(0.38f, 0.9f), out pDispBtnTxt);
        CreateTabButton(pTabBar, "CONTROLS", () => showPauseTab(1), new Vector2(0.42f, 0.1f), new Vector2(0.58f, 0.9f), out pCtrlBtnTxt);
        CreateTabButton(pTabBar, "AUDIO", () => showPauseTab(2), new Vector2(0.62f, 0.1f), new Vector2(0.78f, 0.9f), out pAudBtnTxt);

        // === DISPLAY TAB (không có Resolution và Display Mode) ===
        float pStartY = 0.85f;
        float pSpacing = 0.105f;

        string[] pQualityLabels = new string[] { "LOW", "MEDIUM", "HIGH" };
        string[] pShadowLabels = new string[] { "DISABLED", "HARD ONLY", "ALL SHADOWS" };
        string[] pAaLabels = new string[] { "DISABLED", "2x MSAA", "4x MSAA", "8x MSAA" };
        string[] pFpsShowLabels = new string[] { "OFF", "ON" };
        string[] pFpsPosLabels = new string[] { "TOP RIGHT", "TOP LEFT", "BOTTOM RIGHT", "BOTTOM LEFT", "TOP CENTER", "BOTTOM CENTER" };

        // 1. Graphics Quality
        CreateDropdown(pDisplayTab, "GRAPHICS QUALITY:",
            new Vector2(0.05f, pStartY - 0.04f), new Vector2(0.4f, pStartY + 0.02f),
            new Vector2(0.45f, pStartY - 0.05f), new Vector2(0.95f, pStartY + 0.03f),
            pQualityLabels, () => tempQualityLevel, (idx) => {
                tempQualityLevel = idx;
                UpdateDropdownTexts();
            }, out pQualText);

        // 2. Shadow Quality
        CreateDropdown(pDisplayTab, "SHADOW QUALITY:",
            new Vector2(0.05f, pStartY - pSpacing - 0.04f), new Vector2(0.4f, pStartY - pSpacing + 0.02f),
            new Vector2(0.45f, pStartY - pSpacing - 0.05f), new Vector2(0.95f, pStartY - pSpacing + 0.03f),
            pShadowLabels, () => tempShadowQuality, (idx) => {
                tempShadowQuality = idx;
                UpdateDropdownTexts();
            }, out pShadText);

        // 3. Anti-Aliasing
        CreateDropdown(pDisplayTab, "ANTI-ALIASING:",
            new Vector2(0.05f, pStartY - pSpacing*2 - 0.04f), new Vector2(0.4f, pStartY - pSpacing*2 + 0.02f),
            new Vector2(0.45f, pStartY - pSpacing*2 - 0.05f), new Vector2(0.95f, pStartY - pSpacing*2 + 0.03f),
            pAaLabels, () => tempAntiAliasing, (idx) => {
                tempAntiAliasing = idx;
                UpdateDropdownTexts();
            }, out pAAText);

        // 4. Brightness (using custom slider)
        CreateLabel(pDisplayTab, "BRIGHTNESS:", new Vector2(0.05f, pStartY - pSpacing*3 - 0.04f), new Vector2(0.4f, pStartY - pSpacing*3 + 0.02f));
        GameObject pBrightSliderObj = CreateSlider(pDisplayTab, "BRIGHTNESS", new Vector2(0.45f, pStartY - pSpacing*3 - 0.05f), new Vector2(0.95f, pStartY - pSpacing*3 + 0.03f),
            0.5f, 1.0f, () => tempBrightness, (val) => {
                tempBrightness = val;
                if (GlobalSettingsManager.Instance != null)
                {
                    GlobalSettingsManager.Instance.ApplyBrightness(val);
                }
            }, out pBrightText, "%");
        pSliderBrightness = pBrightSliderObj.GetComponent<Slider>();

        // 5. FPS Limit
        CreateDropdown(pDisplayTab, "FPS LIMIT:",
            new Vector2(0.05f, pStartY - pSpacing*4 - 0.04f), new Vector2(0.4f, pStartY - pSpacing*4 + 0.02f),
            new Vector2(0.45f, pStartY - pSpacing*4 - 0.05f), new Vector2(0.95f, pStartY - pSpacing*4 + 0.03f),
            fpsLabels, () => tempFpsIndex, (idx) => {
                tempFpsIndex = idx;
                UpdateDropdownTexts();
            }, out pFpsText);

        // 6. Show FPS
        CreateDropdown(pDisplayTab, "SHOW FPS:",
            new Vector2(0.05f, pStartY - pSpacing*5 - 0.04f), new Vector2(0.4f, pStartY - pSpacing*5 + 0.02f),
            new Vector2(0.45f, pStartY - pSpacing*5 - 0.05f), new Vector2(0.95f, pStartY - pSpacing*5 + 0.03f),
            pFpsShowLabels, () => tempShowFPS, (idx) => {
                tempShowFPS = idx;
                UpdateDropdownTexts();
            }, out pFpsShowText);

        // 7. FPS Position (immediate effect on selection)
        CreateDropdown(pDisplayTab, "FPS POSITION:",
            new Vector2(0.05f, pStartY - pSpacing*6 - 0.04f), new Vector2(0.4f, pStartY - pSpacing*6 + 0.02f),
            new Vector2(0.45f, pStartY - pSpacing*6 - 0.05f), new Vector2(0.95f, pStartY - pSpacing*6 + 0.03f),
            pFpsPosLabels, () => tempFPSPosition, (idx) => {
                tempFPSPosition = idx;
                UpdateDropdownTexts();
                if (GlobalSettingsManager.Instance != null)
                {
                    GlobalSettingsManager.Instance.ApplyFPSPosition(idx);
                }
            }, out pFpsPosDropdownText);

        // === CONTROLS TAB ===
        float pStartYCtrl = 0.70f;
        float pSpacingCtrl = 0.15f;

        // 1. Aim Sensitivity (Slider)
        CreateLabel(pControlsTab, "AIM SENSITIVITY:", new Vector2(0.05f, pStartYCtrl - 0.04f), new Vector2(0.4f, pStartYCtrl + 0.02f));
        GameObject pSensSliderObj = CreateSlider(pControlsTab, "AIM SENSITIVITY", new Vector2(0.45f, pStartYCtrl - 0.05f), new Vector2(0.95f, pStartYCtrl + 0.03f),
            0.1f, 1.0f, () => tempSensitivity, (val) => {
                tempSensitivity = val;
            }, out pSensText, "x");
        pSliderSensitivity = pSensSliderObj.GetComponent<Slider>();

        // 2. Zoom Sensitivity (Slider)
        CreateLabel(pControlsTab, "ZOOM SENSITIVITY:", new Vector2(0.05f, pStartYCtrl - pSpacingCtrl - 0.04f), new Vector2(0.4f, pStartYCtrl - pSpacingCtrl + 0.02f));
        GameObject pZoomSliderObj = CreateSlider(pControlsTab, "ZOOM SENSITIVITY", new Vector2(0.45f, pStartYCtrl - pSpacingCtrl - 0.05f), new Vector2(0.95f, pStartYCtrl - pSpacingCtrl + 0.03f),
            0.5f, 2.0f, () => tempZoomSensitivity, (val) => {
                tempZoomSensitivity = val;
                if (PZ_CameraController.Instance != null)
                {
                    PZ_CameraController.Instance.UpdateSensitivity();
                }
            }, out pZoomText, "x");
        pSliderZoomSensitivity = pZoomSliderObj.GetComponent<Slider>();

        // === AUDIO TAB ===
        float pStartYAud = 0.70f;
        float pSpacingAud = 0.15f;

        // 1. Master Volume (Slider)
        CreateLabel(pAudioTab, "MASTER VOLUME:", new Vector2(0.05f, pStartYAud - 0.04f), new Vector2(0.4f, pStartYAud + 0.02f));
        GameObject pVolSliderObj = CreateSlider(pAudioTab, "MASTER VOLUME", new Vector2(0.45f, pStartYAud - 0.05f), new Vector2(0.95f, pStartYAud + 0.03f),
            0f, 1.0f, () => tempMasterVolume, (val) => {
                tempMasterVolume = val;
                AudioListener.volume = val;
            }, out pVolText, "%");
        pSliderMasterVolume = pVolSliderObj.GetComponent<Slider>();

        // 2. Music Volume (Slider)
        CreateLabel(pAudioTab, "MUSIC VOLUME:", new Vector2(0.05f, pStartYAud - pSpacingAud - 0.04f), new Vector2(0.4f, pStartYAud - pSpacingAud + 0.02f));
        GameObject pMusSliderObj = CreateSlider(pAudioTab, "MUSIC VOLUME", new Vector2(0.45f, pStartYAud - pSpacingAud - 0.05f), new Vector2(0.95f, pStartYAud - pSpacingAud + 0.03f),
            0f, 1.0f, () => tempMusicVolume, (val) => {
                tempMusicVolume = val;
                bgmVolume = val;
                if (bgmSource != null) bgmSource.volume = bgmVolume;
            }, out pMusText, "%");
        pSliderMusicVolume = pMusSliderObj.GetComponent<Slider>();

        // 3. SFX Volume (Slider)
        CreateLabel(pAudioTab, "SFX VOLUME:", new Vector2(0.05f, pStartYAud - pSpacingAud*2 - 0.04f), new Vector2(0.4f, pStartYAud - pSpacingAud*2 + 0.02f));
        GameObject pSfxSliderObj = CreateSlider(pAudioTab, "SFX VOLUME", new Vector2(0.45f, pStartYAud - pSpacingAud*2 - 0.05f), new Vector2(0.95f, pStartYAud - pSpacingAud*2 + 0.03f),
            0f, 1.0f, () => tempSFXVolume, (val) => {
                tempSFXVolume = val;
                sfxVolume = val;
            }, out pSfxText, "%");
        pSliderSFXVolume = pSfxSliderObj.GetComponent<Slider>();

        // Nút BACK + SAVE
        CreateMenuButton(pauseOptionsPanel, "BACK", () => ClosePauseOptions(false), new Vector2(0.1f, 0.08f));
        CreateMenuButton(pauseOptionsPanel, "SAVE", () => ClosePauseOptions(true), new Vector2(0.9f, 0.08f));

        // Mặc định ẩn, hiện tab DISPLAY
        showPauseTab(0);
        pauseOptionsPanel.SetActive(false);
    }

    private void OpenPauseOptions()
    {
        isPauseOptionsOpen = true;
        LoadSavedSettingsToTemp();
        pauseOptionsPanel.transform.SetAsLastSibling();
        pauseOptionsPanel.SetActive(true);
        pauseMenuPanel.SetActive(false);
    }

    private void ClosePauseOptions(bool save)
    {
        if (save)
        {
            SaveSettings();
        }
        else
        {
            // Revert to saved settings
            LoadSavedSettingsToTemp();
        }
        isPauseOptionsOpen = false;
        pauseOptionsPanel.SetActive(false);
        pauseMenuPanel.transform.SetAsLastSibling();
        pauseMenuPanel.SetActive(true);
    }

    // 2. HÀM BẬT/TẮT MENU (KHÔNG CÓ TIME.TIMESCALE)
    private void TogglePauseMenu()
    {
        isPauseMenuOpen = !isPauseMenuOpen;

        if (isPauseMenuOpen)
        {
            mainCanvas.gameObject.SetActive(true); // Bật Canvas UI lên
            backgroundImageObj.SetActive(false);   // NHƯNG tắt tấm ảnh nền đi để lộ game ra

            if (currentActivePanel != null) currentActivePanel.alpha = 0f;

            pauseMenuPanel.transform.SetAsLastSibling();
            pauseMenuPanel.SetActive(true);

            // Mở khóa chuột để user bấm nút
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            pauseMenuPanel.SetActive(false);
            // Đóng pause options panel nếu đang mở
            if (pauseOptionsPanel != null) pauseOptionsPanel.SetActive(false);
            isPauseOptionsOpen = false;
            mainCanvas.gameObject.SetActive(false); // Trả lại toàn bộ màn hình cho game

            // Khóa chuột lại (NẾU game của bạn là dạng bắn súng góc nhìn thứ 1/thứ 3)
            // Cursor.lockState = CursorLockMode.Locked; 
        }
    }

    // 3. HÀM XỬ LÝ RỜI GAME
    private void LeaveGame()
    {
        pauseMenuPanel.SetActive(false);
        isPauseMenuOpen = false;

        // Bật Loading Screen che màn hình lại
        mainCanvas.gameObject.SetActive(true);
        backgroundImageObj.SetActive(true); // Bật lại nền đen thui

        loadingScreenPanel.transform.SetAsLastSibling();
        loadingScreenPanel.SetActive(true);
        if (loadingScreenPanel.TryGetComponent<CanvasGroup>(out var cg)) cg.alpha = 1f;

        loadingPercentText.text = "ESCAPING FROM REALITY...";
        loadingFillBar.anchorMax = new Vector2(1, 1);

        // Rút dây mạng, tự động kích hoạt OnShutdown để về sảnh
        if (activeRunner != null)
        {
            activeRunner.Shutdown();
        }
    }

    private void GenerateEntireMenu()
    {
        if (FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject esObj = new GameObject("EventSystem");
            EventSystem es = esObj.AddComponent<EventSystem>(); es.sendNavigationEvents = false;
            esObj.AddComponent<StandaloneInputModule>();
            DontDestroyOnLoad(esObj);
        }

        GameObject canvasGO = new GameObject("AutoMenuCanvas");
        DontDestroyOnLoad(canvasGO);

        mainCanvas = canvasGO.AddComponent<Canvas>();
        mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay; mainCanvas.sortingOrder = 999;
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // Thay chữ bgObj thành backgroundImageObj
        backgroundImageObj = new GameObject("Background");
        backgroundImageObj.transform.SetParent(canvasGO.transform, false);
        Image bgImg = backgroundImageObj.AddComponent<Image>();
        float initialBrightness = PlayerPrefs.GetFloat("Brightness", 1.0f);
        float initFactor = Mathf.Clamp(initialBrightness, 0.2f, 1.8f);
        if (backgroundImage != null) 
        { 
            bgImg.sprite = backgroundImage; 
            bgImg.color = new Color(initFactor, initFactor, initFactor, 1f); 
        } 
        else 
        { 
            bgImg.color = new Color(0.08f * initFactor, 0.08f * initFactor, 0.08f * initFactor, 1f); 
        }
        RectTransform bgRect = backgroundImageObj.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero; bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero; bgRect.offsetMax = Vector2.zero;
        GenerateMainPanel(canvasGO); GenerateNewGamePanel(canvasGO); GenerateMultiplayerPanel_NEW(canvasGO);
        GenerateCharacterSelectPanel(canvasGO); GenerateOptionsPanel(canvasGO); GenerateCreditsPanel(canvasGO);

        GenerateWaitingRoomPanel(canvasGO); GenerateConnectionPopup(canvasGO);
        GenerateLoadingScreen(canvasGO);
        GenerateErrorPopup(canvasGO);
        GeneratePauseMenuPanel(canvasGO);

        OpenPanel(mainPanel.GetComponent<CanvasGroup>());
        Canvas.ForceUpdateCanvases();
    }

    #region TẠO PANEL CƠ BẢN VÀ MULTIPLAYER
    private void GenerateMainPanel(GameObject canvasGO)
    {
        mainPanel = CreateBasePanel("MainPanel", canvasGO); CanvasGroup cg = mainPanel.AddComponent<CanvasGroup>(); cg.alpha = 0f; cg.interactable = false; cg.blocksRaycasts = false;
        
        // Tiêu đề game dạng tĩnh nguyên bản đơn giản
        CreateTitleText(mainPanel, "FRAGMENTS\nOF SURVIVAL", 0.95f, 80, TextAlignmentOptions.TopLeft, new Vector2(0.1f, 0.7f), new Vector2(0.5f, 0.95f));

        GameObject btnContainer = new GameObject("ButtonContainer"); btnContainer.transform.SetParent(mainPanel.transform, false);
        RectTransform btnRect = btnContainer.AddComponent<RectTransform>(); btnRect.anchorMin = new Vector2(0.1f, 0.1f); btnRect.anchorMax = new Vector2(0.3f, 0.6f); btnRect.offsetMin = Vector2.zero; btnRect.offsetMax = Vector2.zero;
        VerticalLayoutGroup vlg = btnContainer.AddComponent<VerticalLayoutGroup>(); vlg.spacing = 15; vlg.childAlignment = TextAnchor.MiddleLeft; vlg.childControlHeight = false; vlg.childControlWidth = true;
        CreateMenuButton(btnContainer, "SOLO", () => { pendingIsSolo = true; pendingIsHost = false; OpenPanel(newGamePanel.GetComponent<CanvasGroup>()); });
        CreateMenuButton(btnContainer, "MULTIPLAYER", () => { pendingIsSolo = false; OpenPanel(multiplayerPanel.GetComponent<CanvasGroup>()); });
        CreateMenuButton(btnContainer, "OPTIONS", () => OpenPanel(optionsPanel.GetComponent<CanvasGroup>()));
        CreateMenuButton(btnContainer, "CREDITS", () => OpenPanel(creditsPanel.GetComponent<CanvasGroup>()));
        CreateMenuButton(btnContainer, "QUIT", () => Application.Quit());
    }

    private void GenerateNewGamePanel(GameObject canvasGO)
    {
        newGamePanel = CreateBasePanel("NewGamePanel", canvasGO); CanvasGroup cg = newGamePanel.AddComponent<CanvasGroup>(); cg.alpha = 0f; cg.interactable = false; cg.blocksRaycasts = false;
        CreateTitleText(newGamePanel, "SELECT DIFFICULTY");
        
        // 1. Cột chọn bên trái
        GameObject btnContainer = new GameObject("DiffContainer"); 
        btnContainer.transform.SetParent(newGamePanel.transform, false);
        RectTransform btnRect = btnContainer.AddComponent<RectTransform>(); 
        btnRect.anchorMin = new Vector2(0.15f, 0.25f); 
        btnRect.anchorMax = new Vector2(0.45f, 0.75f); 
        btnRect.offsetMin = Vector2.zero; 
        btnRect.offsetMax = Vector2.zero;
        
        VerticalLayoutGroup vlg = btnContainer.AddComponent<VerticalLayoutGroup>(); 
        vlg.spacing = 30;
        vlg.childAlignment = TextAnchor.MiddleCenter;

        CreateMenuButton(btnContainer, "EASY", () => { SetDifficulty(0); pendingRoomName = "Solo_Easy_" + Random.Range(1000, 9999); OpenPanel(characterSelectPanel.GetComponent<CanvasGroup>()); }); 
        CreateMenuButton(btnContainer, "MEDIUM", () => { SetDifficulty(1); pendingRoomName = "Solo_Normal_" + Random.Range(1000, 9999); OpenPanel(characterSelectPanel.GetComponent<CanvasGroup>()); }); 
        CreateMenuButton(btnContainer, "HARD", () => { SetDifficulty(2); pendingRoomName = "Solo_Hard_" + Random.Range(1000, 9999); OpenPanel(characterSelectPanel.GetComponent<CanvasGroup>()); });

        // 2. Bảng thông tin bên phải
        diffInfoPanel = new GameObject("DifficultyInfoPanel");
        diffInfoPanel.transform.SetParent(newGamePanel.transform, false);
        RectTransform infoRect = diffInfoPanel.AddComponent<RectTransform>();
        infoRect.anchorMin = new Vector2(0.52f, 0.25f);
        infoRect.anchorMax = new Vector2(0.85f, 0.75f);
        infoRect.offsetMin = Vector2.zero;
        infoRect.offsetMax = Vector2.zero;

        Image infoBg = diffInfoPanel.AddComponent<Image>();
        infoBg.color = new Color(0.06f, 0.06f, 0.06f, 0.95f);
        Outline outline = diffInfoPanel.AddComponent<Outline>();
        outline.effectColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
        outline.effectDistance = new Vector2(1, -1);

        // Tiêu đề bảng thông tin
        GameObject titleObj = new GameObject("InfoTitle");
        titleObj.transform.SetParent(diffInfoPanel.transform, false);
        diffTitleText = titleObj.AddComponent<TextMeshProUGUI>();
        if (gameFont != null) diffTitleText.font = gameFont;
        diffTitleText.fontSize = 32;
        diffTitleText.fontStyle = FontStyles.Bold;
        diffTitleText.alignment = TextAlignmentOptions.Center;
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.05f, 0.8f);
        titleRect.anchorMax = new Vector2(0.95f, 0.95f);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;

        // Các thông số đo lường
        GameObject statsObj = new GameObject("InfoStats");
        statsObj.transform.SetParent(diffInfoPanel.transform, false);
        diffStatsText = statsObj.AddComponent<TextMeshProUGUI>();
        if (gameFont != null) diffStatsText.font = gameFont;
        diffStatsText.fontSize = 22;
        diffStatsText.lineSpacing = 10;
        diffStatsText.alignment = TextAlignmentOptions.TopLeft;
        RectTransform statsRect = statsObj.GetComponent<RectTransform>();
        statsRect.anchorMin = new Vector2(0.08f, 0.45f);
        statsRect.anchorMax = new Vector2(0.92f, 0.75f);
        statsRect.offsetMin = Vector2.zero;
        statsRect.offsetMax = Vector2.zero;

        // Mô tả chi tiết
        GameObject descObj = new GameObject("InfoDesc");
        descObj.transform.SetParent(diffInfoPanel.transform, false);
        diffDescText = descObj.AddComponent<TextMeshProUGUI>();
        if (gameFont != null) diffDescText.font = gameFont;
        diffDescText.fontSize = 20;
        diffDescText.alignment = TextAlignmentOptions.TopLeft;
        RectTransform descRect = descObj.GetComponent<RectTransform>();
        descRect.anchorMin = new Vector2(0.08f, 0.05f);
        descRect.anchorMax = new Vector2(0.92f, 0.4f);
        descRect.offsetMin = Vector2.zero;
        descRect.offsetMax = Vector2.zero;

        // Gắn Trigger di chuột vào các nút để thay đổi thông tin động
        var easyTrigger = btnContainer.transform.Find("Btn_EASY").gameObject.AddComponent<DifficultyHoverTrigger>();
        easyTrigger.difficultyIndex = 0;
        easyTrigger.menuManager = this;

        var mediumTrigger = btnContainer.transform.Find("Btn_MEDIUM").gameObject.AddComponent<DifficultyHoverTrigger>();
        mediumTrigger.difficultyIndex = 1;
        mediumTrigger.menuManager = this;

        var hardTrigger = btnContainer.transform.Find("Btn_HARD").gameObject.AddComponent<DifficultyHoverTrigger>();
        hardTrigger.difficultyIndex = 2;
        hardTrigger.menuManager = this;

        // Thiết lập hiển thị mặc định ban đầu là MEDIUM
        ShowDifficultyInfo(1);

        CreateMenuButton(newGamePanel, "BACK", () => OpenPanel(mainPanel.GetComponent<CanvasGroup>()), new Vector2(0.1f, 0.1f));
    }

    public void ShowDifficultyInfo(int id)
    {
        if (diffInfoPanel == null || diffTitleText == null || diffDescText == null || diffStatsText == null) return;

        string title = "";
        string desc = "";
        string stats = "";
        Color themeColor = Color.white;

        switch (id)
        {
            case 0:
                title = "★ EASY MODE ★";
                themeColor = new Color(0.2f, 0.8f, 0.2f);
                stats = "<color=#99FF99>ZOMBIE DENSITY:</color> Low (-50% Spawn Rate)\n" +
                        "<color=#99FF99>RESOURCES:</color> Abundant (Loot rate 150%)\n" +
                        "<color=#99FF99>DAMAGE TAKEN:</color> Reduced (-30% Damage)\n" +
                        "<color=#99FF99>STARTING GEAR:</color> Pistol + Ammo & Canned Food\n" +
                        "<color=#99FF99>SURVIVAL RATE:</color> Very High (90%)";
                desc = "<b>OVERVIEW:</b>\n" +
                       "Zombie spawn count is reduced. Ideal for exploring, gathering resources, and learning basic survival mechanics without heavy pressure.";
                break;
            case 1:
                title = "✦ SURVIVAL MODE ✦";
                themeColor = new Color(1f, 0.8f, 0.2f);
                stats = "<color=#FFFF99>ZOMBIE DENSITY:</color> Standard (100% Spawn Rate)\n" +
                        "<color=#FFFF99>RESOURCES:</color> Balanced distribution\n" +
                        "<color=#FFFF99>DAMAGE TAKEN:</color> Normal (100% Damage)\n" +
                        "<color=#FFFF99>STARTING GEAR:</color> Flashlight & Bandage\n" +
                        "<color=#FFFF99>SURVIVAL RATE:</color> Balanced (50%)";
                desc = "<b>OVERVIEW:</b>\n" +
                       "The standard zombie survival experience. Spawn rates and cooldown values are set to their default balanced values. Requires strategic thinking.";
                break;
            case 2:
                title = "☠ HARDCORE MODE ☠";
                themeColor = new Color(0.9f, 0.15f, 0.15f);
                stats = "<color=#FF9999>ZOMBIE DENSITY:</color> Extreme (+150% Spawn Rate)\n" +
                        "<color=#FF9999>RESOURCES:</color> Scarce & Depleted (Loot rate 40%)\n" +
                        "<color=#FF9999>DAMAGE TAKEN:</color> Increased (+50% Damage)\n" +
                        "<color=#FF9999>STARTING GEAR:</color> None (Empty hands)\n" +
                        "<color=#FF9999>SURVIVAL RATE:</color> Near Zero (<10%)";
                desc = "<b>OVERVIEW:</b>\n" +
                       "A relentless nightmare. Zombies are extremely numerous and spawn very quickly. Demands maximum skill and tactical planning.";
                break;
        }

        diffTitleText.text = title;
        diffTitleText.color = themeColor;
        diffStatsText.text = stats;
        diffDescText.text = desc;

        // Dynamic outline color based on difficulty theme
        Outline outline = diffInfoPanel.GetComponent<Outline>();
        if (outline != null)
        {
            outline.effectColor = new Color(themeColor.r, themeColor.g, themeColor.b, 0.5f);
        }
    }

    private void GenerateMultiplayerPanel_NEW(GameObject canvasGO)
    {
        multiplayerPanel = CreateBasePanel("MultiplayerPanel", canvasGO); CanvasGroup cg = multiplayerPanel.AddComponent<CanvasGroup>(); cg.alpha = 0f; cg.interactable = false; cg.blocksRaycasts = false;
        GameObject hostArea = new GameObject("Host_Container"); hostArea.transform.SetParent(multiplayerPanel.transform, false); RectTransform hostRect = hostArea.AddComponent<RectTransform>(); hostRect.anchorMin = new Vector2(0.15f, 0.15f); hostRect.anchorMax = new Vector2(0.85f, 0.8f); hostRect.offsetMin = Vector2.zero; hostRect.offsetMax = Vector2.zero; hostArea.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.08f, 0.95f);
        GameObject joinArea = new GameObject("Join_Container"); joinArea.transform.SetParent(multiplayerPanel.transform, false); RectTransform joinRect = joinArea.AddComponent<RectTransform>(); joinRect.anchorMin = new Vector2(0.15f, 0.15f); joinRect.anchorMax = new Vector2(0.85f, 0.8f); joinRect.offsetMin = Vector2.zero; joinRect.offsetMax = Vector2.zero; joinArea.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.08f, 0.95f); joinArea.SetActive(false);

        CreateMenuButton(multiplayerPanel, "HOST GAME", () => { hostArea.SetActive(true); joinArea.SetActive(false); }, new Vector2(0.3f, 0.85f), true, new Vector2(350, 50));
        CreateMenuButton(multiplayerPanel, "JOIN GAME", () => { hostArea.SetActive(false); joinArea.SetActive(true); ConnectToLobby(); }, new Vector2(0.7f, 0.85f), true, new Vector2(350, 50));

        CreateTitleText(hostArea, "HOST SETTINGS", 0.9f); CreateLabel(hostArea, "ROOM NAME:", new Vector2(0.1f, 0.7f), new Vector2(0.3f, 0.75f));
        GameObject roomInputObj = CreateInputField(hostArea, "HostRoomName", "VD: Refugee Camp...", new Vector2(0.35f, 0.68f), new Vector2(0.9f, 0.77f)); TMP_InputField roomInput = roomInputObj.GetComponent<TMP_InputField>();
        // --- PHẦN CHỈNH SỐ NGƯỜI CHƠI (THAY CHO INPUT FIELD) ---
        CreateLabel(hostArea, "MAX PLAYERS:", new Vector2(0.1f, 0.55f), new Vector2(0.3f, 0.6f));

        GameObject maxPlayerContainer = new GameObject("MaxPlayerControl");
        maxPlayerContainer.transform.SetParent(hostArea.transform, false);
        RectTransform mpRect = maxPlayerContainer.AddComponent<RectTransform>();
        mpRect.anchorMin = new Vector2(0.35f, 0.53f); mpRect.anchorMax = new Vector2(0.6f, 0.62f);
        mpRect.offsetMin = Vector2.zero; mpRect.offsetMax = Vector2.zero;

        // Nút Giảm [-]
        CreateMenuButton(maxPlayerContainer, "-", () => {
            hostMaxPlayers = Mathf.Clamp(hostMaxPlayers - 1, 1, 4);
            maxPlayersText.text = hostMaxPlayers.ToString();
        }, new Vector2(0f, 0.5f), true, new Vector2(40, 40), 30);

        // Text hiển thị số
        GameObject valObj = new GameObject("Value");
        valObj.transform.SetParent(maxPlayerContainer.transform, false);
        maxPlayersText = valObj.AddComponent<TextMeshProUGUI>();
        if (gameFont != null) maxPlayersText.font = gameFont;
        maxPlayersText.text = hostMaxPlayers.ToString();
        maxPlayersText.alignment = TextAlignmentOptions.Center;
        maxPlayersText.fontSize = 30;
        maxPlayersText.color = Color.white;
        RectTransform valRect = valObj.GetComponent<RectTransform>();
        valRect.anchorMin = new Vector2(0.2f, 0); valRect.anchorMax = new Vector2(0.5f, 1);
        valRect.offsetMin = Vector2.zero; valRect.offsetMax = Vector2.zero;

        // Nút Tăng [+]
        CreateMenuButton(maxPlayerContainer, "+", () => {
            hostMaxPlayers = Mathf.Clamp(hostMaxPlayers + 1, 1, 4);
            maxPlayersText.text = hostMaxPlayers.ToString();
        }, new Vector2(0.7f, 0.5f), true, new Vector2(40, 40), 30);

        CreateLabel(hostArea, "DIFFICULTY:", new Vector2(0.1f, 0.4f), new Vector2(0.3f, 0.45f));
        diffTexts[0] = CreateTextBtn(hostArea, "EASY", new Vector2(0.4f, 0.425f), () => SetDifficulty(0)); diffTexts[1] = CreateTextBtn(hostArea, "NORMAL", new Vector2(0.6f, 0.425f), () => SetDifficulty(1)); diffTexts[2] = CreateTextBtn(hostArea, "HARDCORE", new Vector2(0.8f, 0.425f), () => SetDifficulty(2)); SetDifficulty(1);
        CreateLabel(hostArea, "PASSWORD:", new Vector2(0.1f, 0.25f), new Vector2(0.3f, 0.3f)); toggleText = CreateTextBtn(hostArea, "[ NO ]", new Vector2(0.4f, 0.275f), TogglePassword);
        passwordInputObj = CreateInputField(hostArea, "HostPassword", "Enter password...", new Vector2(0.55f, 0.23f), new Vector2(0.9f, 0.32f)); passwordInputObj.GetComponent<TMP_InputField>().contentType = TMP_InputField.ContentType.Password; passwordInputObj.SetActive(false);

        CreateMenuButton(hostArea, "SELECT SURVIVOR", () =>
        {
            if (string.IsNullOrWhiteSpace(roomInput.text)) { roomInput.placeholder.GetComponent<TextMeshProUGUI>().text = "<color=red>YOU MUST ENTER THE BASE NAME!</color>"; PlayClickSFX(); return; }
            pendingRoomName = roomInput.text;
            if (hostHasPassword) hostPassword = passwordInputObj.GetComponent<TMP_InputField>().text; else hostPassword = "";
            pendingIsHost = true; OpenPanel(characterSelectPanel.GetComponent<CanvasGroup>());
        }, new Vector2(0.5f, 0.08f), true, new Vector2(500, 60), 25f);

        CreateTitleText(joinArea, "SERVER LIST", 0.9f); GameObject scrollObj = new GameObject("Scroll View"); scrollObj.transform.SetParent(joinArea.transform, false); RectTransform scrollRectT = scrollObj.AddComponent<RectTransform>(); scrollRectT.anchorMin = new Vector2(0.1f, 0.2f); scrollRectT.anchorMax = new Vector2(0.9f, 0.75f); scrollRectT.offsetMin = Vector2.zero; scrollRectT.offsetMax = Vector2.zero; ScrollRect scrollRect = scrollObj.AddComponent<ScrollRect>(); scrollRect.horizontal = false; scrollRect.vertical = true; scrollRect.scrollSensitivity = 20f; GameObject viewport = new GameObject("Viewport"); viewport.transform.SetParent(scrollObj.transform, false); RectTransform vpRect = viewport.AddComponent<RectTransform>(); vpRect.anchorMin = Vector2.zero; vpRect.anchorMax = Vector2.one; vpRect.offsetMin = Vector2.zero; vpRect.offsetMax = Vector2.zero; viewport.AddComponent<Image>().color = new Color(0, 0, 0, 0.5f); viewport.AddComponent<RectMask2D>(); GameObject content = new GameObject("Content"); content.transform.SetParent(viewport.transform, false); serverListContent = content.AddComponent<RectTransform>(); serverListContent.anchorMin = new Vector2(0, 1); serverListContent.anchorMax = new Vector2(1, 1); serverListContent.pivot = new Vector2(0.5f, 1); serverListContent.offsetMin = Vector2.zero; serverListContent.offsetMax = Vector2.zero; serverListContent.sizeDelta = new Vector2(0, 0); VerticalLayoutGroup vlgList = content.AddComponent<VerticalLayoutGroup>(); vlgList.childAlignment = TextAnchor.UpperCenter; vlgList.childControlHeight = false; vlgList.childControlWidth = true; vlgList.childForceExpandHeight = false; vlgList.spacing = 10; vlgList.padding = new RectOffset(10, 10, 10, 10); ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>(); csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize; scrollRect.viewport = vpRect; scrollRect.content = serverListContent;

        passPromptPanel = new GameObject("PasswordPrompt"); passPromptPanel.transform.SetParent(joinArea.transform, false); RectTransform promptRect = passPromptPanel.AddComponent<RectTransform>(); promptRect.anchorMin = new Vector2(0.2f, 0.3f); promptRect.anchorMax = new Vector2(0.8f, 0.7f); promptRect.offsetMin = Vector2.zero; promptRect.offsetMax = Vector2.zero; passPromptPanel.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 1f); passPromptPanel.SetActive(false);
        CreateLabel(passPromptPanel, "ENTER PASSWORD", new Vector2(0f, 0.65f), new Vector2(1f, 0.85f));

        // 2. Lấy component Text ra để ép kích thước chữ thủ công (tắt AutoSizing của hàm gốc)
        TextMeshProUGUI promptTxt = passPromptPanel.transform.Find("Label").GetComponent<TextMeshProUGUI>();
        promptTxt.enableAutoSizing = false;
        promptTxt.fontSize = 30; // Cỡ chữ 30 như bạn muốn
        promptTxt.alignment = TextAlignmentOptions.Center; // Đảm bảo chữ căn giữa hoàn toàn

        // 3. Khởi tạo InputField bên dưới dòng chữ
        GameObject joinPassInputObj = CreateInputField(passPromptPanel, "JoinPass", "...", new Vector2(0.2f, 0.3f), new Vector2(0.8f, 0.5f));
        joinPassInput = joinPassInputObj.GetComponent<TMP_InputField>();
        joinPassInput.contentType = TMP_InputField.ContentType.Password; CreateMenuButton(passPromptPanel, "CLOSE", () => { passPromptPanel.SetActive(false); }, new Vector2(0.25f, 0.15f), true, new Vector2(150, 40), 30f);
        CreateMenuButton(passPromptPanel, "CONFIRM", () => { passPromptPanel.SetActive(false); pendingJoinPassword = joinPassInput.text; pendingIsHost = false; OpenPanel(characterSelectPanel.GetComponent<CanvasGroup>()); }, new Vector2(0.75f, 0.15f), true, new Vector2(150, 40), 30f);
        CreateMenuButton(joinArea, "REFRESH LIST", () => { ConnectToLobby(); }, new Vector2(0.5f, 0.08f), true, new Vector2(300, 50), 20f);
        CreateMenuButton(multiplayerPanel, "BACK", () => OpenPanel(mainPanel.GetComponent<CanvasGroup>()), new Vector2(0.1f, 0.05f));
    }

    public void UpdateServerListUI(List<SessionInfo> sessionList)
    {
        if (serverListContent == null) return;
        foreach (Transform child in serverListContent) Destroy(child.gameObject);

        foreach (SessionInfo session in sessionList)
        {
            string roomName = session.Name; int currentPlayers = session.PlayerCount; int maxPlayers = session.MaxPlayers;
            bool isLocked = false; bool hasPassword = false; int gameState = 0;
            if (session.Properties != null) 
            { 
                if (session.Properties.TryGetValue("IsLocked", out SessionProperty lockedProp)) isLocked = (int)lockedProp == 1; 
                if (session.Properties.TryGetValue("HasPassword", out SessionProperty hasPassProp)) hasPassword = (int)hasPassProp == 1;
                else hasPassword = isLocked; // Fallback
                if (session.Properties.TryGetValue("GameState", out SessionProperty stateProp)) gameState = (int)stateProp; 
            }
            bool isFull = currentPlayers >= maxPlayers;

            string statusString = "<color=white>WAITING</color>";
            if (isFull) statusString = "<color=red>FULL</color>"; else if (gameState == 1) statusString = "<color=orange>IN COMBAT</color>";
            string lockText = hasPassword ? "<color=red>[LOCKED]</color>" : "<color=green>[OPEN]</color>";
            if (isFull) lockText = "<color=gray>[FULL]</color>";

            string finalDisplayString = $"{lockText} Base: {roomName} | Players: {currentPlayers}/{maxPlayers} | Status: {statusString}";

            CreateDynamicServerItem(finalDisplayString, () =>
            {
                if (isFull) { ShowError("BASE IS FULL! CANNOT JOIN."); return; }
                pendingRoomName = roomName;
                if (hasPassword) { joinPassInput.text = ""; passPromptPanel.SetActive(true); } else { pendingJoinPassword = ""; pendingIsHost = false; OpenPanel(characterSelectPanel.GetComponent<CanvasGroup>()); }
            });
        }
    }

    private void CreateDynamicServerItem(string displayText, UnityEngine.Events.UnityAction action)
    {
        GameObject btnObj = new GameObject("RoomItem"); btnObj.transform.SetParent(serverListContent, false); LayoutElement le = btnObj.AddComponent<LayoutElement>(); le.minHeight = 50f;
        Image btnImg = btnObj.AddComponent<Image>(); btnImg.color = new Color(0.2f, 0.2f, 0.2f, 0.8f); Button btn = btnObj.AddComponent<Button>(); btn.onClick.AddListener(action);
        GameObject txtObj = new GameObject("Text"); txtObj.transform.SetParent(btnObj.transform, false); TextMeshProUGUI tmpText = txtObj.AddComponent<TextMeshProUGUI>(); if (gameFont != null) tmpText.font = gameFont;
        tmpText.text = displayText; tmpText.alignment = TextAlignmentOptions.Center; tmpText.color = Color.white; tmpText.enableAutoSizing = true; tmpText.fontSizeMin = 14; tmpText.fontSizeMax = 22;
        RectTransform txtRect = txtObj.GetComponent<RectTransform>(); txtRect.anchorMin = Vector2.zero; txtRect.anchorMax = Vector2.one; txtRect.offsetMin = Vector2.zero; txtRect.offsetMax = Vector2.zero;
        AutoMenuButtonEffect effect = btnObj.AddComponent<AutoMenuButtonEffect>(); effect.Setup(tmpText, true);
    }
    #endregion

    #region CHỌN NHÂN VẬT
    private void GenerateCharacterSelectPanel(GameObject canvasGO)
    {
        characterSelectPanel = CreateBasePanel("CharacterSelectPanel", canvasGO); CanvasGroup cg = characterSelectPanel.AddComponent<CanvasGroup>(); cg.alpha = 0f; cg.interactable = false; cg.blocksRaycasts = false;
        CreateTitleText(characterSelectPanel, "CUSTOMIZE SURVIVOR");
        GameObject customArea = new GameObject("CustomArea"); customArea.transform.SetParent(characterSelectPanel.transform, false); RectTransform customRect = customArea.AddComponent<RectTransform>(); customRect.anchorMin = new Vector2(0.2f, 0.1f); customRect.anchorMax = new Vector2(0.8f, 0.85f); customRect.offsetMin = Vector2.zero; customRect.offsetMax = Vector2.zero; customArea.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.05f, 0.9f);
        CreateMenuButton(customArea, "<", () => ChangeCharacter(-1), new Vector2(0.1f, 0.92f), true, new Vector2(60, 60)); CreateMenuButton(customArea, ">", () => ChangeCharacter(1), new Vector2(0.9f, 0.92f), true, new Vector2(60, 60));
        GameObject nameObj = new GameObject("CharNameText"); nameObj.transform.SetParent(customArea.transform, false); charNameText = nameObj.AddComponent<TextMeshProUGUI>(); if (gameFont != null) charNameText.font = gameFont;
        charNameText.text = characterNames[0]; charNameText.fontSize = 30; charNameText.fontStyle = FontStyles.Bold; charNameText.color = Color.yellow; charNameText.alignment = TextAlignmentOptions.Center; charNameText.enableAutoSizing = true; charNameText.fontSizeMin = 20; charNameText.fontSizeMax = 40; RectTransform nameRect = nameObj.GetComponent<RectTransform>(); nameRect.anchorMin = new Vector2(0.2f, 0.85f); nameRect.anchorMax = new Vector2(0.8f, 1f); nameRect.offsetMin = Vector2.zero; nameRect.offsetMax = Vector2.zero;
        GameObject previewBox = new GameObject("PreviewContainer"); previewBox.transform.SetParent(customArea.transform, false); previewContainer = previewBox.AddComponent<RectTransform>(); previewContainer.anchorMin = new Vector2(0.3f, 0.55f); previewContainer.anchorMax = new Vector2(0.7f, 0.85f); previewContainer.offsetMin = Vector2.zero; previewContainer.offsetMax = Vector2.zero;
        GameObject statsObj = new GameObject("CharStatsText"); statsObj.transform.SetParent(customArea.transform, false); charStatsText = statsObj.AddComponent<TextMeshProUGUI>(); if (gameFont != null) charStatsText.font = gameFont; charStatsText.text = characterStats[0]; charStatsText.fontSize = 25; charStatsText.alignment = TextAlignmentOptions.Top; charStatsText.richText = true; charStatsText.enableAutoSizing = true; charStatsText.fontSizeMin = 14; charStatsText.fontSizeMax = 30; RectTransform statsRect = statsObj.GetComponent<RectTransform>(); statsRect.anchorMin = new Vector2(0.1f, 0.35f); statsRect.anchorMax = new Vector2(0.9f, 0.52f); statsRect.offsetMin = Vector2.zero; statsRect.offsetMax = Vector2.zero;
        CreateLabel(customArea, "SURVIVOR IDENTITY", new Vector2(0.2f, 0.26f), new Vector2(0.8f, 0.32f));
        GameObject inputObj = CreateInputField(customArea, "PlayerNameInput", "Enter name...", new Vector2(0.3f, 0.15f), new Vector2(0.7f, 0.25f)); playerNameInput = inputObj.GetComponent<TMP_InputField>(); playerNameInput.text = PlayerPrefs.GetString("MyPlayerName", "Survivor_" + Random.Range(100, 999));

        CreateMenuButton(customArea, "ENTER THE DEAD ZONE", async () =>
        {
            if (isConnecting) return;

            isConnecting = true;
            PlayerPrefs.SetString("MyPlayerName", playerNameInput.text);
            PlayerPrefs.SetInt("SelectedCharacterID", previewID);
            PlayerPrefs.Save();

            await Task.Yield(); // Đợi 1 frame cho UI cập nhật

            // Gọi trực tiếp thay vì qua StartHostGame / StartClientGame
            if (pendingIsSolo)
                StartGameInternal(GameMode.Single, pendingRoomName);
            else if (pendingIsHost)
                StartGameInternal(GameMode.Host, pendingRoomName);
            else
                StartGameInternal(GameMode.Client, pendingRoomName);

        }, new Vector2(0.5f, 0.1f), true, new Vector2(450, 70), 25f);

        CreateMenuButton(characterSelectPanel, "BACK", () => 
        { 
            isConnecting = false; 
            if (pendingIsSolo)
                OpenPanel(newGamePanel.GetComponent<CanvasGroup>());
            else
                OpenPanel(multiplayerPanel.GetComponent<CanvasGroup>()); 
        }, new Vector2(0.1f, 0.1f), false, new Vector2(300, 50));
    }
    #endregion

    #region BẢNG KẾT NỐI VÀ BẢNG LỖI
    private void GenerateConnectionPopup(GameObject canvasGO)
    {
        connectionPopupPanel = CreateBasePanel("ConnectionPopup", canvasGO);
        connectionPopupPanel.AddComponent<Image>().color = new Color(0, 0, 0, 0.85f);
        GameObject txtObj = new GameObject("Text"); txtObj.transform.SetParent(connectionPopupPanel.transform, false);
        connectionPopupText = txtObj.AddComponent<TextMeshProUGUI>(); if (gameFont != null) connectionPopupText.font = gameFont;
        connectionPopupText.alignment = TextAlignmentOptions.Center; connectionPopupText.color = Color.cyan; connectionPopupText.fontSize = 30;
        RectTransform txtRt = txtObj.GetComponent<RectTransform>(); txtRt.anchorMin = new Vector2(0, 0.4f); txtRt.anchorMax = new Vector2(1, 0.6f); txtRt.offsetMin = Vector2.zero; txtRt.offsetMax = Vector2.zero;
        connectionPopupPanel.SetActive(false);
    }

    private void ShowConnectionPopup(string initialMsg)
    {
        Debug.Log($"[DEBUG] ShowConnectionPopup called: {initialMsg}");

        // Ép tắt mọi panel khác trước
        if (currentActivePanel != null)
        {
            currentActivePanel.alpha = 0f;
            currentActivePanel.blocksRaycasts = false;
            currentActivePanel.interactable = false;
            currentActivePanel = null;
        }

        characterSelectPanel?.SetActive(false);
        multiplayerPanel?.SetActive(false);
        waitingRoomPanel?.SetActive(false);
        mainPanel?.SetActive(false);
        errorPopupPanel?.SetActive(false);

        connectionPopupPanel.transform.SetAsLastSibling();
        connectionPopupPanel.SetActive(true);

        if (connectionPopupPanel.TryGetComponent<CanvasGroup>(out var cg))
        {
            cg.alpha = 1f;
            cg.blocksRaycasts = true;
            cg.interactable = true;
        }

        connectionPopupText.text = initialMsg;

        if (connectionAnimRoutine != null)
            StopCoroutine(connectionAnimRoutine);

        connectionAnimRoutine = StartCoroutine(ConnectionTextAnimation());

        isConnecting = true;   // ← Đảm bảo luôn set true ở đây nữa
    }

    private IEnumerator ConnectionTextAnimation()
    {
        yield return new WaitForSeconds(0.4f);
        connectionPopupText.text = "SCANNING RADIO FREQUENCIES...";
        yield return new WaitForSeconds(0.4f);
        connectionPopupText.text = "ONLY STATIC NOISE REMAINS...";
        yield return new WaitForSeconds(0.4f);
        connectionPopupText.text = "ENTERING THE DEAD ZONE...";
    }

    private void GenerateErrorPopup(GameObject canvasGO)
    {
        errorPopupPanel = CreateBasePanel("ErrorPopupPanel", canvasGO); errorPopupPanel.AddComponent<Image>().color = new Color(0, 0, 0, 0.85f);
        GameObject boxObj = new GameObject("Box"); boxObj.transform.SetParent(errorPopupPanel.transform, false); RectTransform boxRt = boxObj.AddComponent<RectTransform>(); boxRt.anchorMin = new Vector2(0.3f, 0.4f); boxRt.anchorMax = new Vector2(0.7f, 0.6f); boxRt.offsetMin = Vector2.zero; boxRt.offsetMax = Vector2.zero; boxObj.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 1f);
        GameObject txtObj = new GameObject("ErrorText"); txtObj.transform.SetParent(boxObj.transform, false); errorPopupText = txtObj.AddComponent<TextMeshProUGUI>(); if (gameFont != null) errorPopupText.font = gameFont; errorPopupText.alignment = TextAlignmentOptions.Center; errorPopupText.color = new Color(1f, 0.4f, 0.4f); errorPopupText.fontSize = 24; RectTransform txtRt = txtObj.GetComponent<RectTransform>(); txtRt.anchorMin = new Vector2(0.1f, 0.4f); txtRt.anchorMax = new Vector2(0.9f, 0.9f); txtRt.offsetMin = Vector2.zero; txtRt.offsetMax = Vector2.zero;
        CreateMenuButton(boxObj, "ĐÓNG", () => { errorPopupPanel.SetActive(false); PlayClickSFX(); }, new Vector2(0.5f, 0.2f), true, new Vector2(150, 45), 20);
        errorPopupPanel.SetActive(false);
    }

    public void ShowError(string msg)
    {
        if (errorPopupText != null) errorPopupText.text = msg;
        if (errorPopupPanel != null) { errorPopupPanel.transform.SetAsLastSibling(); errorPopupPanel.SetActive(true); }
        isConnecting = false; // Phải nhả biến kết nối ra khi bị lỗi
    }
    #endregion

    #region BẢNG SẢNH CHỜ VÀ LOADING CHUẨN
    private void GenerateWaitingRoomPanel(GameObject canvasGO)
    {
        waitingRoomPanel = CreateBasePanel("WaitingRoomPanel", canvasGO);
        CanvasGroup cg = waitingRoomPanel.AddComponent<CanvasGroup>();
        cg.alpha = 0f; cg.interactable = false; cg.blocksRaycasts = false;

        // Nền tối với chút sắc xám xanh quân đội
        waitingRoomPanel.AddComponent<Image>().color = new Color(0.06f, 0.07f, 0.06f, 0.98f);

        CreateTitleText(waitingRoomPanel, "CAMPAIGN LOBBY", 0.9f, 60, TextAlignmentOptions.Center);

        // Đường gạch ngang trang trí dưới Title
        GameObject lineObj = new GameObject("DividerLine");
        lineObj.transform.SetParent(waitingRoomPanel.transform, false);
        RectTransform lineRt = lineObj.AddComponent<RectTransform>();
        lineRt.anchorMin = new Vector2(0.3f, 0.85f); lineRt.anchorMax = new Vector2(0.7f, 0.85f);
        lineRt.offsetMin = Vector2.zero; lineRt.offsetMax = new Vector2(0, 2); // Cao 2px
        lineObj.AddComponent<Image>().color = new Color(0.4f, 0.4f, 0.4f, 0.5f);

        // Khu vực chứa Thẻ Người Chơi (Player Cards)
        GameObject listObj = new GameObject("PlayerCardsContainer");
        listObj.transform.SetParent(waitingRoomPanel.transform, false);
        waitingRoomPlayerListContent = listObj.AddComponent<RectTransform>();
        waitingRoomPlayerListContent.anchorMin = new Vector2(0.1f, 0.4f);
        waitingRoomPlayerListContent.anchorMax = new Vector2(0.9f, 0.75f);
        waitingRoomPlayerListContent.offsetMin = Vector2.zero;
        waitingRoomPlayerListContent.offsetMax = Vector2.zero;

        HorizontalLayoutGroup hlg = listObj.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 30; // Khoảng cách giữa các thẻ rộng ra
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = true;
        hlg.childAlignment = TextAnchor.MiddleCenter;

        // Text báo trạng thái chung
        GameObject statusObj = new GameObject("HostStatus");
        statusObj.transform.SetParent(waitingRoomPanel.transform, false);
        waitingRoomHostStatusText = statusObj.AddComponent<TextMeshProUGUI>();
        if (gameFont != null) waitingRoomHostStatusText.font = gameFont;
        waitingRoomHostStatusText.alignment = TextAlignmentOptions.Center;
        waitingRoomHostStatusText.color = new Color(0.8f, 0.8f, 0.4f); // Màu vàng nhạt cảnh báo
        waitingRoomHostStatusText.fontSize = 24;
        waitingRoomHostStatusText.fontStyle = FontStyles.Italic;
        RectTransform statusRt = statusObj.GetComponent<RectTransform>();
        statusRt.anchorMin = new Vector2(0, 0.3f); statusRt.anchorMax = new Vector2(1, 0.35f);
        statusRt.offsetMin = Vector2.zero; statusRt.offsetMax = Vector2.zero;

        // Các nút điều khiển
        CreateMenuButton(waitingRoomPanel, "START CAMPAIGN", async () =>
        {
            if (activeRunner == null || !activeRunner.IsServer) return;
            var props = new Dictionary<string, SessionProperty> { { "IsLocked", 1 }, { "GameState", 1 } };
            activeRunner.SessionInfo.UpdateCustomProperties(props);
            ShowLoadingScreen();
            await Task.Delay(800);
            playersLoaded = 0;
            await activeRunner.LoadScene(SceneRef.FromIndex(mainSceneIndex));
        }, new Vector2(0.5f, 0.2f), true, new Vector2(400, 60), 25f);

        CreateMenuButton(waitingRoomPanel, "QUIT", () =>
        {
            if (activeRunner != null) activeRunner.Shutdown();
        }, new Vector2(0.5f, 0.1f), true, new Vector2(250, 50), 20f);
    }

    private void GenerateLoadingScreen(GameObject canvasGO)
    {
        loadingScreenPanel = CreateBasePanel("LoadingScreenPanel", canvasGO);
        loadingScreenPanel.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.05f, 1f);

        CreateTitleText(loadingScreenPanel, "<color=#990000>THIS IS HOW YOU DIED...</color>", 0.6f);

        GameObject borderBar = new GameObject("BorderBar"); borderBar.transform.SetParent(loadingScreenPanel.transform, false);
        RectTransform borderRt = borderBar.AddComponent<RectTransform>(); borderRt.anchorMin = new Vector2(0.19f, 0.38f); borderRt.anchorMax = new Vector2(0.81f, 0.47f); borderRt.offsetMin = Vector2.zero; borderRt.offsetMax = Vector2.zero;
        borderBar.AddComponent<Image>().color = new Color(0.5f, 0.5f, 0.5f, 1f);

        GameObject bgBar = new GameObject("BgBar"); bgBar.transform.SetParent(borderBar.transform, false);
        RectTransform bgRt = bgBar.AddComponent<RectTransform>(); bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one; bgRt.offsetMin = new Vector2(5, 5); bgRt.offsetMax = new Vector2(-5, -5);
        bgBar.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 1f);

        GameObject fillBar = new GameObject("FillBar"); fillBar.transform.SetParent(bgBar.transform, false);
        loadingFillBar = fillBar.AddComponent<RectTransform>(); loadingFillBar.anchorMin = new Vector2(0, 0); loadingFillBar.anchorMax = new Vector2(0, 1); loadingFillBar.offsetMin = Vector2.zero; loadingFillBar.offsetMax = Vector2.zero;
        fillBar.AddComponent<Image>().color = new Color(1f, 0.8f, 0f, 1f);

        GameObject pctObj = new GameObject("PercentText"); pctObj.transform.SetParent(loadingScreenPanel.transform, false);
        loadingPercentText = pctObj.AddComponent<TextMeshProUGUI>(); if (gameFont != null) loadingPercentText.font = gameFont;
        loadingPercentText.alignment = TextAlignmentOptions.Center; loadingPercentText.color = Color.white; loadingPercentText.fontSize = 28; loadingPercentText.fontStyle = FontStyles.Bold;
        loadingPercentText.text = "0%";
        loadingPercentText.outlineWidth = 0.2f; loadingPercentText.outlineColor = Color.black;
        RectTransform pctRt = pctObj.GetComponent<RectTransform>(); pctRt.anchorMin = new Vector2(0.1f, 0.3f); pctRt.anchorMax = new Vector2(0.9f, 0.35f); pctRt.offsetMin = Vector2.zero; pctRt.offsetMax = Vector2.zero;

        loadingScreenPanel.AddComponent<CanvasGroup>();
        loadingScreenPanel.SetActive(false);
    }
    #endregion

    #region HỆ THỐNG MẠNG
    /// <summary>
    /// Voice phải được tạo trên đúng NetworkRunner trước khi map spawn người chơi.
    /// Mỗi máy chỉ có một client Voice và một Recorder cục bộ; VoiceNetworkObject
    /// trên prefab người chơi sẽ liên kết luồng âm thanh với đúng nhân vật đó.
    /// </summary>
    private static void ConfigureVoiceForRunner(NetworkRunner runner)
    {
        var voiceClient = runner.GetComponent<FusionVoiceClient>();
        if (voiceClient == null)
        {
            voiceClient = runner.gameObject.AddComponent<FusionVoiceClient>();
        }

        voiceClient.UseFusionAppSettings = true;
        voiceClient.UseFusionAuthValues = true;

        // Nếu voice info đến trước NetworkObject của player ở máy nhận, Photon
        // Voice không thể gắn nó với Speaker trên player. Fallback này vẫn phát
        // stream ở dạng âm thanh toàn cục thay vì làm mất hoàn toàn tiếng nói.
        if (voiceClient.SpeakerPrefab == null)
        {
            var fallbackSpeaker = new GameObject("[Voice] Fallback Speaker");
            fallbackSpeaker.transform.SetParent(runner.transform, false);
            var audioSource = fallbackSpeaker.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            audioSource.volume = 1f;
            fallbackSpeaker.AddComponent<Speaker>();
            voiceClient.SpeakerPrefab = fallbackSpeaker;
        }

        voiceClient.RemoteVoiceAdded += remoteVoice =>
            Debug.Log($"[VOICE RECEIVE] Remote stream received: {remoteVoice}");
        voiceClient.SpeakerLinked += speaker =>
            Debug.Log($"[VOICE RECEIVE] Speaker linked: {speaker.name} | SpeakerVolume: {speaker.GetComponent<AudioSource>()?.volume} | MasterVolume: {AudioListener.volume} | ListenerPaused: {AudioListener.pause}");

        var recorder = runner.GetComponent<Recorder>();
        if (recorder == null)
        {
            recorder = runner.gameObject.AddComponent<Recorder>();
        }

        // Unity microphone là mặc định ổn định trên Windows. Photon Voice tự
        // chuyển sang driver còn lại nếu thiết bị này không khởi tạo được.
        recorder.MicrophoneType = Recorder.MicType.Unity;
        recorder.UseMicrophoneTypeFallback = true;
        recorder.RecordWhenJoined = true;
        recorder.RecordingEnabled = true;
        recorder.TransmitEnabled = false; // Push-to-talk chỉ mở khi người chơi giữ V.
        voiceClient.PrimaryRecorder = recorder;
    }

    private async void StartGameInternal(GameMode mode, string roomName)
    {
        string popupMsg = mode == GameMode.Single
            ? "INITIALIZING SOLO PROTOCOL..."
            : (mode == GameMode.Host ? "PLANNING SURVIVAL PROTOCOL..." : "SEARCHING FOR SURVIVORS...");

        ShowConnectionPopup(popupMsg);
        isConnecting = true;

        await CleanupOldRunnersAsync();

        activeRunner = Instantiate(runnerPrefab);
        ConfigureVoiceForRunner(activeRunner);
        activeRunner.AddCallbacks(this);

        var sceneManager = activeRunner.GetComponent<NetworkSceneManagerDefault>()
            ?? activeRunner.gameObject.AddComponent<NetworkSceneManagerDefault>();

        StartGameArgs args = new StartGameArgs
        {
            GameMode = mode,
            SessionName = roomName,
            SceneManager = sceneManager
        };

        if (mode == GameMode.Host)
        {
            var roomProps = new Dictionary<string, SessionProperty>
            {
                { "IsLocked", hostHasPassword ? 1 : 0 },
                { "HasPassword", hostHasPassword ? 1 : 0 },
                { "GameState", 0 }
            };
            args.SessionProperties = roomProps;
            args.PlayerCount = hostMaxPlayers;
        }
        else if (mode == GameMode.Client)
        {
            if (!string.IsNullOrEmpty(pendingJoinPassword))
            {
                args.ConnectionToken = System.Text.Encoding.UTF8.GetBytes(pendingJoinPassword);
            }
        }

        Debug.Log($"=== Gọi StartGame({mode}) ===");

        var result = await activeRunner.StartGame(args);

        await Task.Delay(600); // Đợi UI ổn định

        if (this == null || isMenuDestroyed) return;

        isConnecting = false;

        Debug.Log($"=== StartGame finished. OK = {result.Ok} | ShutdownReason = {result.ShutdownReason} ===");

        if (result.Ok)
        {
            connectionPopupPanel.SetActive(false);
            if (connectionAnimRoutine != null)
            {
                StopCoroutine(connectionAnimRoutine);
                connectionAnimRoutine = null;
            }

            if (mode == GameMode.Single)
            {
                ShowLoadingScreen();
                await Task.Delay(800);
                playersLoaded = 0;
                await activeRunner.LoadScene(SceneRef.FromIndex(mainSceneIndex));
            }
            else if (mode == GameMode.Host)
            {
                waitingRoomHostStatusText.text = "You are the Host. Wait for your team and press START!";
                OpenPanel(waitingRoomPanel.GetComponent<CanvasGroup>());
            }
            else // Client
            {
                int currentState = 0;
                if (activeRunner.SessionInfo?.Properties != null &&
                    activeRunner.SessionInfo.Properties.TryGetValue("GameState", out SessionProperty prop))
                {
                    currentState = (int)prop;
                }

                if (currentState == 0)
                {
                    waitingRoomHostStatusText.text = "Waiting for the Host to START...";
                    OpenPanel(waitingRoomPanel.GetComponent<CanvasGroup>());

                    if (activeRunner != null)
                        activeRunner.ProvideInput = true;
                }
                else
                {
                    ShowLoadingScreen();
                }
            }
        }
        else
        {
            connectionPopupPanel.SetActive(false);
            string errorMsg = $"CONNECTION FAILED! ({result.ShutdownReason}))";
            ShowError(errorMsg);
            OpenPanel(characterSelectPanel.GetComponent<CanvasGroup>());
        }
    }

    private async void ConnectToLobby()
    {
        if (lobbyRunner != null && lobbyRunner.IsCloudReady) return;

        if (lobbyRunner == null)
        {
            GameObject lobbyObj = new GameObject("FusionLobbyDigger");
            DontDestroyOnLoad(lobbyObj);
            lobbyRunner = lobbyObj.AddComponent<NetworkRunner>();
        }

        lobbyRunner.AddCallbacks(this);
        var result = await lobbyRunner.JoinSessionLobby(SessionLobby.ClientServer);

        if (this == null) return;
        if (!result.Ok)
        {
            Destroy(lobbyRunner.gameObject);
            lobbyRunner = null;
        }
    }

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)

    {
        UpdateServerListUI(sessionList);
    }

    public void OnSceneLoadStart(NetworkRunner runner)
    {
        isLocalSceneLoaded = false;
        isHostSignaledGo = false;
        ShowLoadingScreen();
    }

    private void ShowLoadingScreen()
    {
        if (isLoadingScreenActive) return;
        isLoadingScreenActive = true;

        if (bgmSource != null) bgmSource.Stop();

        // Tắt các panel menu
        waitingRoomPanel?.SetActive(false);
        characterSelectPanel?.SetActive(false);
        multiplayerPanel?.SetActive(false);
        mainPanel?.SetActive(false);

        loadingScreenPanel.transform.SetAsLastSibling();
        if (loadingScreenPanel.TryGetComponent<CanvasGroup>(out var cg))
            cg.alpha = 1f;

        loadingScreenPanel.SetActive(true);
        Application.backgroundLoadingPriority = ThreadPriority.High;
        // === GIẢM NGUY CƠ TIMEOUT KHI LOAD SCENE ===
        if (activeRunner != null)
        {
            activeRunner.ProvideInput = false;        
        }

        Application.backgroundLoadingPriority = ThreadPriority.Low;   // Giúp Unity ưu tiên load background

        if (loadingCoroutine != null) StopCoroutine(loadingCoroutine);
        loadingCoroutine = StartCoroutine(SmoothLoadingLogic());
    }

    private IEnumerator SmoothLoadingLogic()
    {
        float progress = 0f;

        // Fake progress đến 95%
        while (progress < 0.95f)
        {
            progress += Time.unscaledDeltaTime * 0.6f;
            if (progress > 0.95f) progress = 0.95f;

            loadingFillBar.anchorMax = new Vector2(progress, 1);
            loadingPercentText.text = Mathf.RoundToInt(progress * 100) + "%";
            yield return null;
        }

        loadingPercentText.text = "<color=#777777>No hope left. Waiting for other doomed souls...</color>";

        // Chờ Host báo hiệu tất cả sẵn sàng
        while (!isHostSignaledGo)
            yield return null;

        // Hoàn tất 100%
        while (progress < 1f)
        {
            progress += Time.unscaledDeltaTime * 5f;
            if (progress > 1f) progress = 1f;
            loadingFillBar.anchorMax = new Vector2(progress, 1);
            yield return null;
        }

        yield return new WaitForSeconds(0.6f);

        // Fade out
        if (loadingScreenPanel.TryGetComponent<CanvasGroup>(out var cg))
        {
            float t = 1f;
            while (t > 0f)
            {
                t -= Time.unscaledDeltaTime * 5f;
                cg.alpha = t;
                yield return null;
            }
        }

        loadingScreenPanel.SetActive(false);
        isLoadingScreenActive = false;
        Application.backgroundLoadingPriority = ThreadPriority.BelowNormal; // Hoặc Low
        RestoreNetworkAfterLoading();
        EnableGameplayUI();

        if (mainCanvas != null) mainCanvas.gameObject.SetActive(false);

        Debug.Log("=== LOADING HOÀN TẤT ===");
    }
    private void RestoreNetworkAfterLoading()
    {
        if (activeRunner != null)
        {
            activeRunner.ProvideInput = true;
        }
    }

    public void ForceCloseLoadingScreen()
    {
        isHostSignaledGo = true;
    }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
        isLocalSceneLoaded = true;

        // Tất cả người chơi (Host + Client) đều báo đã load xong
        if (activeRunner != null)
        {
            RPC_PlayerLoadedScene();
        }
    }

    private async Task CleanupOldRunnersAsync()
    {
        Debug.Log("[DEBUG] CleanupOldRunnersAsync - Finding all runners...");

        var allRunners = FindObjectsByType<NetworkRunner>(FindObjectsSortMode.None);

        foreach (var r in allRunners)
        {
            if (r == null) continue;
            if (r.gameObject == gameObject || r.transform.root == transform.root)
                continue;

            Debug.Log($"[DEBUG] Destroying old runner: {r.gameObject.name}");
            Destroy(r.gameObject);
        }

        // Chờ runner thực sự bị destroy
        float timeout = 2f;
        while (timeout > 0)
        {
            bool stillExists = FindObjectsByType<NetworkRunner>(FindObjectsSortMode.None)
                .Any(r => r != null && r.gameObject != gameObject && r.transform.root != transform.root);

            if (!stillExists) break;

            await Task.Delay(50);
            timeout -= 0.05f;
        }

        if (timeout <= 0)
            Debug.LogWarning("[DEBUG] CleanupOldRunners timeout!");
    }

    // ====================== CALLBACKS ======================

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        Debug.Log($"[DEBUG] OnShutdown called - Reason: {shutdownReason}");

        bool wasConnecting = isConnecting;
        isConnecting = false;

        if (wasConnecting && connectionPopupPanel != null && connectionPopupPanel.activeSelf) return;

        // Tắt hết râu ria
        if (waitingRoomPanel != null) waitingRoomPanel.SetActive(false);
        if (characterSelectPanel != null) characterSelectPanel.SetActive(false);
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);

        isPauseMenuOpen = false;
        isLoadingScreenActive = false;
        isLocalSceneLoaded = false; // Xóa cờ đã load map

        hasDetectedGameStart = false;

        // 🔥 FIX: Hủy diệt UIManager GameObject để scene mới tự load lại GameManager mới
        if (AutoUIManager.Instance != null)
        {
            Destroy(AutoUIManager.Instance.gameObject);
        }

        string[] targetNames = { "ChatCanvas", "--- AUTO CHAT MANAGER ---", "--- AUTO HEALTH CANVAS ---", "--- AUTO HEALTH MANAGER ---", "HealthPanel" };
        foreach (string target in targetNames)
        {
            GameObject oldUI = GameObject.Find(target);
            if (oldUI != null)
            {
                Destroy(oldUI);
            }
        }
        temporarilyDisabledObjects.Clear();

        // Khởi chạy Coroutine để về Menu chính với màn hình Loading
        StartCoroutine(ReturnToMenuSmoothly());
    }

    private IEnumerator ReturnToMenuSmoothly()
    {
        // 👇 THÊM Ở ĐÂY: Bật Canvas và Hình nền lên NGAY LẬP TỨC khi bắt đầu rút lui
        if (mainCanvas != null) mainCanvas.gameObject.SetActive(true);
        if (backgroundImageObj != null) backgroundImageObj.SetActive(true);

        // Nếu đang ở Map chiến đấu (Scene khác 0), thì bật loading
        if (SceneManager.GetActiveScene().buildIndex != 0)
        {
            loadingScreenPanel.transform.SetAsLastSibling();
            loadingScreenPanel.SetActive(true);
            if (loadingScreenPanel.TryGetComponent<CanvasGroup>(out var cg)) cg.alpha = 1f;
            loadingPercentText.text = "FINDING A WAY BACK TO SHELTER...";

            // Đợi 0.5s cho UI loading hiện lên rõ ràng rồi mới load scene
            yield return new WaitForSecondsRealtime(0.5f);

            // Chạy tải Scene bất đồng bộ để thanh Loading có thể nhích
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(0);
            while (!asyncLoad.isDone)
            {
                loadingFillBar.anchorMax = new Vector2(asyncLoad.progress, 1);
                loadingPercentText.text = "ESCAPING..." + Mathf.RoundToInt(asyncLoad.progress * 100) + "%";
                yield return null;
            }
        }

        // Chờ 1 chút sau khi load xong rồi mới tắt Loading screen
        yield return new WaitForSecondsRealtime(0.5f);

        loadingScreenPanel.SetActive(false);

        // Đảm bảo mở đúng Sảnh Chính (Main Panel)
        if (mainPanel != null) OpenPanel(mainPanel.GetComponent<CanvasGroup>());
    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        Debug.LogError($"[DEBUG] OnConnectFailed: {reason}");
        isConnecting = false;

        // Dọn dẹp màn hình chờ
        if (connectionPopupPanel != null) connectionPopupPanel.SetActive(false);
        if (loadingScreenPanel != null) loadingScreenPanel.SetActive(false);

        // 🔥 NẾU SERVER TỪ CHỐI (Chỉ xảy ra khi nhập sai Pass do Host refuse)
        if (reason == NetConnectFailedReason.ServerRefused)
        {
            ShowError("WRONG PASSWORD!");
        }
        else
        {
            ShowError($"CONNECTION FAILED! {reason}");
        }

        // Mở lại bảng Multiplayer để tìm phòng khác
        OpenPanel(multiplayerPanel.GetComponent<CanvasGroup>());
    }

    void INetworkRunnerCallbacks.OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        Debug.Log($"[DEBUG] OnDisconnectedFromServer: {reason}");

        isConnecting = false;

        // Dọn dẹp màn hình chờ
        if (connectionPopupPanel != null) connectionPopupPanel.SetActive(false);
        if (loadingScreenPanel != null) loadingScreenPanel.SetActive(false);

        // Hiển thị lỗi nếu rớt mạng bất thường (Không phải do mình tự bấm Quit)
        if (reason != NetDisconnectReason.Requested)
        {
            ShowError($"Lost connection to server: {reason}");
        }

        // Trở về menu chính hoặc bảng Multiplayer
        if (multiplayerPanel != null)
        {
            OpenPanel(multiplayerPanel.GetComponent<CanvasGroup>());
        }
    }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
    {
        if (runner.IsServer)
        {
            if (hostHasPassword)
            {
                if (token == null) { request.Refuse(); return; }
                string clientPass = System.Text.Encoding.UTF8.GetString(token);
                if (clientPass == hostPassword) request.Accept();
                else request.Refuse();
            }
            else
            {
                request.Accept();
            }
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayerLoadedScene()
    {
        playersLoaded++;
        Debug.Log($"[Loaded] Player loaded. Total: {playersLoaded}/{activeRunner?.SessionInfo?.PlayerCount ?? 0}");

        // Chỉ Host kiểm tra
        if (activeRunner != null && activeRunner.IsServer)
        {
            if (playersLoaded >= (activeRunner.SessionInfo?.PlayerCount ?? 1))
            {
                Debug.Log("=== TẤT CẢ NGƯỜI CHƠI ĐÃ LOAD XONG ===");
                RPC_StartGameplay();        // Gọi RPC báo tất cả bắt đầu
            }
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_StartGameplay()
    {
        ForceCloseLoadingScreen();      // Thoát loading screen
        RestoreNetworkAfterLoading();   // Bật lại input

        // === BẮT ĐẦU GAMEPLAY Ở ĐÂY ===
        // Ví dụ: Bật AI, timer, cho phép player di chuyển, spawn zombie...
        Debug.Log("=== GAMEPLAY BẮT ĐẦU ĐỒNG BỘ ===");
        // Bạn có thể gọi một hàm EnableGameplay() ở đây
    }

    // Các callback còn lại để trống hoặc giữ nguyên
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { UpdateWaitingRoomUI(); }
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { UpdateWaitingRoomUI(); }

    private void UpdateWaitingRoomUI()
    {
        if (waitingRoomPlayerListContent == null || activeRunner == null) return;

        // Xóa thẻ cũ
        foreach (Transform child in waitingRoomPlayerListContent) Destroy(child.gameObject);

        int maxSlots = activeRunner.SessionInfo.MaxPlayers;
        List<PlayerRef> activePlayers = activeRunner.ActivePlayers.ToList();
        int playerCount = activePlayers.Count;

        for (int i = 0; i < maxSlots; i++)
        {
            bool hasPlayer = i < playerCount;
            PlayerRef currentSlotPlayer = hasPlayer ? activePlayers[i] : default;
            bool isLocal = hasPlayer && (currentSlotPlayer == activeRunner.LocalPlayer);
            bool isHostSlot = hasPlayer && (i == 0); // Giả định người đầu tiên trong list là Host

            // 1. Khung nền của Thẻ
            GameObject slotObj = new GameObject("PlayerCard_" + i);
            slotObj.transform.SetParent(waitingRoomPlayerListContent, false);
            Image slotBg = slotObj.AddComponent<Image>();
            slotBg.color = hasPlayer ? new Color(0.12f, 0.15f, 0.12f, 1f) : new Color(0.05f, 0.05f, 0.05f, 0.6f);
            Outline outline = slotObj.AddComponent<Outline>();
            outline.effectColor = hasPlayer ? new Color(0.3f, 0.5f, 0.3f, 1f) : new Color(0.2f, 0.2f, 0.2f, 1f);
            outline.effectDistance = new Vector2(2, -2);

            // 2. Banner Vai trò (Nằm ở trên cùng thẻ)
            GameObject roleObj = new GameObject("RoleBanner");
            roleObj.transform.SetParent(slotObj.transform, false);
            Image roleBg = roleObj.AddComponent<Image>();
            RectTransform roleRt = roleObj.GetComponent<RectTransform>();
            roleRt.anchorMin = new Vector2(0, 0.8f); roleRt.anchorMax = new Vector2(1, 1);
            roleRt.offsetMin = Vector2.zero; roleRt.offsetMax = Vector2.zero;

            GameObject roleTxtObj = new GameObject("RoleText");
            roleTxtObj.transform.SetParent(roleObj.transform, false);
            TextMeshProUGUI roleTxt = roleTxtObj.AddComponent<TextMeshProUGUI>();
            if (gameFont != null) roleTxt.font = gameFont;
            roleTxt.alignment = TextAlignmentOptions.Center;
            roleTxt.fontSize = 20; roleTxt.fontStyle = FontStyles.Bold;
            RectTransform rtxtRt = roleTxtObj.GetComponent<RectTransform>();
            rtxtRt.anchorMin = Vector2.zero; rtxtRt.anchorMax = Vector2.one;
            rtxtRt.offsetMin = Vector2.zero; rtxtRt.offsetMax = Vector2.zero;

            // 3. Tên Người Chơi (Giữa thẻ)
            GameObject nameObj = new GameObject("NameText");
            nameObj.transform.SetParent(slotObj.transform, false);
            TextMeshProUGUI nameTxt = nameObj.AddComponent<TextMeshProUGUI>();
            if (gameFont != null) nameTxt.font = gameFont;
            nameTxt.alignment = TextAlignmentOptions.Center;
            nameTxt.fontSize = 26;
            RectTransform nameRt = nameObj.GetComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0.1f, 0.3f); nameRt.anchorMax = new Vector2(0.9f, 0.7f);
            nameRt.offsetMin = Vector2.zero; nameRt.offsetMax = Vector2.zero;

            // Cập nhật thông tin theo trạng thái
            if (hasPlayer)
            {
                if (isHostSlot)
                {
                    roleBg.color = new Color(0.6f, 0.4f, 0.1f, 1f); // Vàng đất cho Host
                    roleTxt.text = "HOST";
                }
                else
                {
                    roleBg.color = new Color(0.2f, 0.3f, 0.4f, 1f); // Xanh biển tối cho Thành viên
                    roleTxt.text = "TEAMMATE";
                }

                roleTxt.color = Color.white;

                if (isLocal)
                {
                    string myName = PlayerPrefs.GetString("MyPlayerName", "Survivor");
                    nameTxt.text = $"<color=#ffffff>YOU</color>\n<size=16><color=#aaaaaa>({myName})</color></size>";
                }
                else
                {
                    nameTxt.text = $"<color=#dddddd>SURVIVOR {i + 1}</color>\n<size=16><color=#55ff55>CONNECTED</color></size>";
                }
            }
            else
            {
                roleBg.color = new Color(0.1f, 0.1f, 0.1f, 1f);
                roleTxt.text = "EMPTY SLOT";
                roleTxt.color = new Color(0.4f, 0.4f, 0.4f, 1f);
                nameTxt.text = "<color=#333333>Waiting for signal...</color>";
            }
        }

        // Cập nhật trạng thái góc dưới
        if (!activeRunner.IsServer)
        {
            waitingRoomHostStatusText.text = "Device connected. Waiting for Host's orders...";
        }
        else
        {
            waitingRoomHostStatusText.text = $"Outpost report: {playerCount}/{maxSlots} personnel in sector.";
        }
    }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, System.ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    void INetworkRunnerCallbacks.OnConnectedToServer(NetworkRunner runner) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    #endregion

    // Các hàm tạo UI rút gọn
    private TextMeshProUGUI CreateTitleText(GameObject parent, string text, float height = 0.9f, int fontSize = 40, TextAlignmentOptions align = TextAlignmentOptions.Center, Vector2? aMin = null, Vector2? aMax = null) { GameObject txtObj = new GameObject("Title"); txtObj.transform.SetParent(parent.transform, false); TextMeshProUGUI txt = txtObj.AddComponent<TextMeshProUGUI>(); if (gameFont != null) txt.font = gameFont; txt.text = text; txt.fontSize = fontSize; txt.fontStyle = FontStyles.Bold; txt.alignment = align; txt.color = new Color(0.8f, 0.8f, 0.8f, 1f); RectTransform rect = txtObj.GetComponent<RectTransform>(); rect.anchorMin = aMin ?? new Vector2(0, height); rect.anchorMax = aMax ?? new Vector2(1, height); rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero; return txt; }
    private void SetDifficulty(int id) { hostDifficulty = id; PlayerPrefs.SetInt("GameDifficulty", id); PlayerPrefs.Save(); for (int i = 0; i < diffTexts.Length; i++) { if (i == id) { diffTexts[i].color = Color.yellow; diffTexts[i].fontStyle = FontStyles.Bold; } else { diffTexts[i].color = Color.gray; diffTexts[i].fontStyle = FontStyles.Normal; } } }
    private void TogglePassword() { hostHasPassword = !hostHasPassword; if (hostHasPassword) { toggleText.text = "[ YES ]"; toggleText.color = Color.red; passwordInputObj.SetActive(true); } else { toggleText.text = "[ NO ]"; toggleText.color = Color.gray; passwordInputObj.SetActive(false); } }
    private void ChangeCharacter(int direction) { previewID = (previewID + direction + characterNames.Length) % characterNames.Length; charNameText.text = characterNames[previewID]; charStatsText.text = characterStats[previewID]; UpdatePreview(); }
    private void UpdatePreview()
    {
        if (previewImages == null || previewContainer == null) return;
        for (int i = 0; i < previewImages.Length; i++)
        {
            if (previewImages[i] != null)
            {
                bool isActive = (i == previewID);
                previewImages[i].SetActive(isActive);
                if (isActive)
                {
                    previewImages[i].transform.SetParent(previewContainer, false);
                    RectTransform rt = previewImages[i].GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        rt.anchorMin = Vector2.zero;
                        rt.anchorMax = Vector2.one;
                        rt.offsetMin = Vector2.zero;
                        rt.offsetMax = Vector2.zero;
                    }

                    // Đảm bảo có Image component
                    Image img = previewImages[i].GetComponent<Image>();
                    if (img == null) img = previewImages[i].AddComponent<Image>();
                    img.preserveAspect = true;

                    // Gắn UICharacterAnimator nếu chưa có, và khởi chạy animation
                    UICharacterAnimator animator = previewImages[i].GetComponent<UICharacterAnimator>();
                    if (animator == null)
                        animator = previewImages[i].AddComponent<UICharacterAnimator>();

                    // Lấy dữ liệu sprite sheet cho nhân vật hiện tại
                    if (i < characterResourcePaths.Length)
                    {
                        string[] paths = characterResourcePaths[i];
                        string folder = paths[0];
                        string idleSheet = paths[1];
                        string[] actionSheets = new string[paths.Length - 2];
                        System.Array.Copy(paths, 2, actionSheets, 0, actionSheets.Length);
                        animator.Initialize(folder, idleSheet, actionSheets);
                    }
                }
                else
                {
                    // Dừng animation khi không hiển thị
                    UICharacterAnimator animator = previewImages[i].GetComponent<UICharacterAnimator>();
                    if (animator != null) animator.StopAnimation();
                }
            }
        }
    }
    private void EnableGameplayUI() { foreach (var obj in temporarilyDisabledObjects) { if (obj != null) obj.SetActive(true); } temporarilyDisabledObjects.Clear(); }
    private void OnDestroy() { EnableGameplayUI(); isMenuDestroyed = true; }
    private GameObject CreateBasePanel(string name, GameObject parent) { GameObject p = new GameObject(name); p.transform.SetParent(parent.transform, false); RectTransform r = p.AddComponent<RectTransform>(); r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero; return p; }
    private void CreateLabel(GameObject parent, string text, Vector2 anchorMin, Vector2 anchorMax) { GameObject labelObj = new GameObject("Label"); labelObj.transform.SetParent(parent.transform, false); TextMeshProUGUI labelTxt = labelObj.AddComponent<TextMeshProUGUI>(); if (gameFont != null) labelTxt.font = gameFont; labelTxt.text = text; labelTxt.color = new Color(0.8f, 0.8f, 0.8f, 1f); labelTxt.alignment = TextAlignmentOptions.Center; labelTxt.enableAutoSizing = true; labelTxt.fontSizeMin = 14; labelTxt.fontSizeMax = 20; RectTransform labelRect = labelObj.GetComponent<RectTransform>(); labelRect.anchorMin = anchorMin; labelRect.anchorMax = anchorMax; labelRect.offsetMin = Vector2.zero; labelRect.offsetMax = Vector2.zero; }
    private GameObject CreateInputField(GameObject parent, string name, string placeholderTxt, Vector2 anchorMin, Vector2 anchorMax) { GameObject inputObj = new GameObject(name); inputObj.transform.SetParent(parent.transform, false); RectTransform rect = inputObj.AddComponent<RectTransform>(); rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero; Image bg = inputObj.AddComponent<Image>(); bg.color = new Color(0.05f, 0.05f, 0.05f, 1f); TMP_InputField inputField = inputObj.AddComponent<TMP_InputField>(); inputField.targetGraphic = bg; inputField.characterLimit = 20; GameObject viewportObj = new GameObject("Viewport"); viewportObj.transform.SetParent(inputObj.transform, false); RectTransform vpRect = viewportObj.AddComponent<RectTransform>(); vpRect.anchorMin = Vector2.zero; vpRect.anchorMax = Vector2.one; vpRect.offsetMin = new Vector2(15, 0); vpRect.offsetMax = new Vector2(-15, 0); viewportObj.AddComponent<RectMask2D>(); GameObject textObj = new GameObject("Text"); textObj.transform.SetParent(viewportObj.transform, false); TextMeshProUGUI txt = textObj.AddComponent<TextMeshProUGUI>(); if (gameFont != null) txt.font = gameFont; txt.color = Color.white; txt.alignment = TextAlignmentOptions.Left; txt.enableAutoSizing = true; txt.fontSizeMin = 15; txt.fontSizeMax = 30; txt.textWrappingMode = TextWrappingModes.NoWrap; txt.overflowMode = TextOverflowModes.Truncate; RectTransform txtRect = textObj.GetComponent<RectTransform>(); txtRect.anchorMin = Vector2.zero; txtRect.anchorMax = Vector2.one; txtRect.offsetMin = Vector2.zero; txtRect.offsetMax = Vector2.zero; GameObject phObj = new GameObject("Placeholder"); phObj.transform.SetParent(viewportObj.transform, false); TextMeshProUGUI pTxt = phObj.AddComponent<TextMeshProUGUI>(); if (gameFont != null) pTxt.font = gameFont; pTxt.text = placeholderTxt; pTxt.color = Color.gray; pTxt.alignment = TextAlignmentOptions.Left; pTxt.enableAutoSizing = true; pTxt.fontSizeMin = 15; pTxt.fontSizeMax = 30; pTxt.textWrappingMode = TextWrappingModes.NoWrap; pTxt.overflowMode = TextOverflowModes.Truncate; RectTransform phRect = phObj.GetComponent<RectTransform>(); phRect.anchorMin = Vector2.zero; phRect.anchorMax = Vector2.one; phRect.offsetMin = Vector2.zero; phRect.offsetMax = Vector2.zero; inputField.textViewport = vpRect; inputField.textComponent = txt; inputField.placeholder = pTxt; return inputObj; }
    private void CreateMenuButton(GameObject parent, string text, UnityEngine.Events.UnityAction action, Vector2? customAnchor = null, bool isCenter = false, Vector2? customSize = null, float customFontSize = 35f) { GameObject btnObj = new GameObject("Btn_" + text); btnObj.transform.SetParent(parent.transform, false); RectTransform rect = btnObj.AddComponent<RectTransform>(); if (customAnchor.HasValue) { rect.anchorMin = customAnchor.Value; rect.anchorMax = customAnchor.Value; if (isCenter) { rect.pivot = new Vector2(0.5f, 0.5f); } else { rect.pivot = (customAnchor.Value.x > 0.5f) ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f); } } rect.sizeDelta = customSize.HasValue ? customSize.Value : new Vector2(300, 50); Image btnImg = btnObj.AddComponent<Image>(); btnImg.color = new Color(1, 1, 1, 0); Button btn = btnObj.AddComponent<Button>(); btn.onClick.AddListener(action); GameObject txtObj = new GameObject("Text"); txtObj.transform.SetParent(btnObj.transform, false); TextMeshProUGUI tmpText = txtObj.AddComponent<TextMeshProUGUI>(); if (gameFont != null) tmpText.font = gameFont; tmpText.text = text; bool isRightAligned = !isCenter && customAnchor.HasValue && customAnchor.Value.x > 0.5f; tmpText.alignment = isCenter ? TextAlignmentOptions.Center : (isRightAligned ? TextAlignmentOptions.Right : TextAlignmentOptions.Left); tmpText.color = new Color(0.7f, 0.7f, 0.7f, 1f); tmpText.textWrappingMode = TextWrappingModes.NoWrap; tmpText.enableAutoSizing = false; tmpText.fontSize = customFontSize; RectTransform txtRect = txtObj.GetComponent<RectTransform>(); txtRect.anchorMin = Vector2.zero; txtRect.anchorMax = Vector2.one; txtRect.offsetMin = Vector2.zero; txtRect.offsetMax = Vector2.zero; AutoMenuButtonEffect effect = btnObj.AddComponent<AutoMenuButtonEffect>(); effect.Setup(tmpText, isCenter || isRightAligned); }
    private TextMeshProUGUI CreateTextBtn(GameObject parent, string text, Vector2 anchorValue, UnityEngine.Events.UnityAction action) { GameObject btnObj = new GameObject("TextBtn_" + text); btnObj.transform.SetParent(parent.transform, false); RectTransform rect = btnObj.AddComponent<RectTransform>(); rect.anchorMin = anchorValue; rect.anchorMax = anchorValue; rect.pivot = new Vector2(0.5f, 0.5f); rect.sizeDelta = new Vector2(150, 40); Image btnImg = btnObj.AddComponent<Image>(); btnImg.color = new Color(1, 1, 1, 0); Button btn = btnObj.AddComponent<Button>(); btn.onClick.AddListener(action); GameObject txtObj = new GameObject("Text"); txtObj.transform.SetParent(btnObj.transform, false); TextMeshProUGUI tmpText = txtObj.AddComponent<TextMeshProUGUI>(); if (gameFont != null) tmpText.font = gameFont; tmpText.text = text; tmpText.alignment = TextAlignmentOptions.Center; tmpText.color = Color.gray; tmpText.enableAutoSizing = true; tmpText.fontSizeMin = 14; tmpText.fontSizeMax = 20; RectTransform txtRect = txtObj.GetComponent<RectTransform>(); txtRect.anchorMin = Vector2.zero; txtRect.anchorMax = Vector2.one; txtRect.offsetMin = Vector2.zero; txtRect.offsetMax = Vector2.zero; AutoMenuButtonEffect effect = btnObj.AddComponent<AutoMenuButtonEffect>(); effect.Setup(tmpText, true); return tmpText; }

    private void GenerateOptionsPanel(GameObject canvasGO)
    {
        optionsPanel = CreateBasePanel("OptionsPanel", canvasGO);
        CanvasGroup cg = optionsPanel.AddComponent<CanvasGroup>();
        cg.alpha = 0f; cg.interactable = false; cg.blocksRaycasts = false;
        
        CreateTitleText(optionsPanel, "OPTIONS", 0.95f);

        GameObject settingsArea = new GameObject("Settings_Container");
        settingsArea.transform.SetParent(optionsPanel.transform, false);
        RectTransform rect = settingsArea.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.2f, 0.18f);
        rect.anchorMax = new Vector2(0.8f, 0.90f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        settingsArea.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.08f, 0.95f);

        // Khởi tạo các Container Area cho từng Tab
        displayTabArea = new GameObject("DisplayTabArea");
        displayTabArea.transform.SetParent(settingsArea.transform, false);
        RectTransform dRt = displayTabArea.AddComponent<RectTransform>();
        dRt.anchorMin = new Vector2(0f, 0f);
        dRt.anchorMax = new Vector2(1f, 0.92f);
        dRt.offsetMin = Vector2.zero;
        dRt.offsetMax = Vector2.zero;

        controlsTabArea = new GameObject("ControlsTabArea");
        controlsTabArea.transform.SetParent(settingsArea.transform, false);
        RectTransform cRt = controlsTabArea.AddComponent<RectTransform>();
        cRt.anchorMin = new Vector2(0f, 0f);
        cRt.anchorMax = new Vector2(1f, 0.92f);
        cRt.offsetMin = Vector2.zero;
        cRt.offsetMax = Vector2.zero;

        audioTabArea = new GameObject("AudioTabArea");
        audioTabArea.transform.SetParent(settingsArea.transform, false);
        RectTransform aRt = audioTabArea.AddComponent<RectTransform>();
        aRt.anchorMin = new Vector2(0f, 0f);
        aRt.anchorMax = new Vector2(1f, 0.92f);
        aRt.offsetMin = Vector2.zero;
        aRt.offsetMax = Vector2.zero;

        // Khởi tạo Tab Bar ở trên đầu
        GameObject tabBar = new GameObject("TabBar");
        tabBar.transform.SetParent(settingsArea.transform, false);
        RectTransform tbRt = tabBar.AddComponent<RectTransform>();
        tbRt.anchorMin = new Vector2(0f, 0.92f);
        tbRt.anchorMax = new Vector2(1f, 1.0f);
        tbRt.offsetMin = Vector2.zero;
        tbRt.offsetMax = Vector2.zero;
        tabBar.AddComponent<Image>().color = new Color(0.04f, 0.04f, 0.04f, 1.5f);

        CreateTabButton(tabBar, "DISPLAY", () => ShowTab(0), new Vector2(0.22f, 0.1f), new Vector2(0.38f, 0.9f), out displayTabBtnText);
        CreateTabButton(tabBar, "CONTROLS", () => ShowTab(1), new Vector2(0.42f, 0.1f), new Vector2(0.58f, 0.9f), out controlsTabBtnText);
        CreateTabButton(tabBar, "AUDIO", () => ShowTab(2), new Vector2(0.62f, 0.1f), new Vector2(0.78f, 0.9f), out audioTabBtnText);

        // POPULATE TAB 1: DISPLAY (startY = 0.88f, spacingY = 0.095f)
        float startY = 0.88f;
        float spacingY = 0.095f;

        string[] qualityLabels = new string[] { "LOW", "MEDIUM", "HIGH" };
        string[] shadowLabels = new string[] { "DISABLED", "HARD ONLY", "ALL SHADOWS" };
        string[] aaLabels = new string[] { "DISABLED", "2x MSAA", "4x MSAA", "8x MSAA" };
        string[] fpsShowLabels = new string[] { "OFF", "ON" };
        string[] fpsPosLabels = new string[] { "TOP RIGHT", "TOP LEFT", "BOTTOM RIGHT", "BOTTOM LEFT", "TOP CENTER", "BOTTOM CENTER" };

        // 1. Resolution
        string[] resStrings = new string[commonResolutions.Length];
        for (int i = 0; i < commonResolutions.Length; i++)
        {
            resStrings[i] = $"{commonResolutions[i].x} x {commonResolutions[i].y}";
        }
        CreateDropdown(displayTabArea, "RESOLUTION:", 
            new Vector2(0.05f, startY - 0.04f), new Vector2(0.4f, startY + 0.02f),
            new Vector2(0.45f, startY - 0.05f), new Vector2(0.95f, startY + 0.03f),
            resStrings, () => tempResIndex, (idx) => {
                tempResIndex = idx;
                UpdateDropdownTexts();
            }, out resDropdownText);

        // 2. Display Mode
        CreateDropdown(displayTabArea, "DISPLAY MODE:", 
            new Vector2(0.05f, startY - spacingY - 0.04f), new Vector2(0.4f, startY - spacingY + 0.02f),
            new Vector2(0.45f, startY - spacingY - 0.05f), new Vector2(0.95f, startY - spacingY + 0.03f),
            windowModeLabels, () => tempWindowMode, (idx) => {
                tempWindowMode = idx;
                UpdateDropdownTexts();
            }, out modeDropdownText);

        // 3. Graphics Quality
        CreateDropdown(displayTabArea, "GRAPHICS QUALITY:",
            new Vector2(0.05f, startY - spacingY*2 - 0.04f), new Vector2(0.4f, startY - spacingY*2 + 0.02f),
            new Vector2(0.45f, startY - spacingY*2 - 0.05f), new Vector2(0.95f, startY - spacingY*2 + 0.03f),
            qualityLabels, () => tempQualityLevel, (idx) => {
                tempQualityLevel = idx;
                UpdateDropdownTexts();
            }, out qualityValText);

        // 4. Shadow Quality
        CreateDropdown(displayTabArea, "SHADOW QUALITY:",
            new Vector2(0.05f, startY - spacingY*3 - 0.04f), new Vector2(0.4f, startY - spacingY*3 + 0.02f),
            new Vector2(0.45f, startY - spacingY*3 - 0.05f), new Vector2(0.95f, startY - spacingY*3 + 0.03f),
            shadowLabels, () => tempShadowQuality, (idx) => {
                tempShadowQuality = idx;
                UpdateDropdownTexts();
            }, out shadowValText);

        // 5. Anti Aliasing
        CreateDropdown(displayTabArea, "ANTI-ALIASING:",
            new Vector2(0.05f, startY - spacingY*4 - 0.04f), new Vector2(0.4f, startY - spacingY*4 + 0.02f),
            new Vector2(0.45f, startY - spacingY*4 - 0.05f), new Vector2(0.95f, startY - spacingY*4 + 0.03f),
            aaLabels, () => tempAntiAliasing, (idx) => {
                tempAntiAliasing = idx;
                UpdateDropdownTexts();
            }, out aaValText);

        // 6. Brightness (using custom slider)
        CreateLabel(displayTabArea, "BRIGHTNESS:", new Vector2(0.05f, startY - spacingY*5 - 0.04f), new Vector2(0.4f, startY - spacingY*5 + 0.02f));
        GameObject brightSliderObj = CreateSlider(displayTabArea, "BRIGHTNESS", new Vector2(0.45f, startY - spacingY*5 - 0.05f), new Vector2(0.95f, startY - spacingY*5 + 0.03f),
            0.5f, 1.0f, () => tempBrightness, (val) => {
                tempBrightness = val;
                if (GlobalSettingsManager.Instance != null)
                {
                    GlobalSettingsManager.Instance.ApplyBrightness(val);
                }
            }, out brightValText, "%");
        sliderBrightness = brightSliderObj.GetComponent<Slider>();

        // 7. FPS Limit
        CreateDropdown(displayTabArea, "FPS LIMIT:",
            new Vector2(0.05f, startY - spacingY*6 - 0.04f), new Vector2(0.4f, startY - spacingY*6 + 0.02f),
            new Vector2(0.45f, startY - spacingY*6 - 0.05f), new Vector2(0.95f, startY - spacingY*6 + 0.03f),
            fpsLabels, () => tempFpsIndex, (idx) => {
                tempFpsIndex = idx;
                UpdateDropdownTexts();
            }, out fpsValText);

        // 8. Show FPS
        CreateDropdown(displayTabArea, "SHOW FPS:",
            new Vector2(0.05f, startY - spacingY*7 - 0.04f), new Vector2(0.4f, startY - spacingY*7 + 0.02f),
            new Vector2(0.45f, startY - spacingY*7 - 0.05f), new Vector2(0.95f, startY - spacingY*7 + 0.03f),
            fpsShowLabels, () => tempShowFPS, (idx) => {
                tempShowFPS = idx;
                UpdateDropdownTexts();
            }, out fpsShowValText);

        // 9. FPS Position (immediate effect on selection)
        CreateDropdown(displayTabArea, "FPS POSITION:",
            new Vector2(0.05f, startY - spacingY*8 - 0.04f), new Vector2(0.4f, startY - spacingY*8 + 0.02f),
            new Vector2(0.45f, startY - spacingY*8 - 0.05f), new Vector2(0.95f, startY - spacingY*8 + 0.03f),
            fpsPosLabels, () => tempFPSPosition, (idx) => {
                tempFPSPosition = idx;
                UpdateDropdownTexts();
                if (GlobalSettingsManager.Instance != null)
                {
                    GlobalSettingsManager.Instance.ApplyFPSPosition(idx);
                }
            }, out fpsPosDropdownText);


        // POPULATE TAB 2: CONTROLS
        float startYCtrl = 0.70f;
        float spacingYCtrl = 0.15f;

        // 1. Aim Sensitivity (Slider)
        CreateLabel(controlsTabArea, "AIM SENSITIVITY:", new Vector2(0.05f, startYCtrl - 0.04f), new Vector2(0.4f, startYCtrl + 0.02f));
        GameObject sensSliderObj = CreateSlider(controlsTabArea, "AIM SENSITIVITY", new Vector2(0.45f, startYCtrl - 0.05f), new Vector2(0.95f, startYCtrl + 0.03f),
            0.1f, 1.0f, () => tempSensitivity, (val) => {
                tempSensitivity = val;
            }, out sensValText, "x");
        sliderSensitivity = sensSliderObj.GetComponent<Slider>();

        // 2. Zoom Sensitivity (Slider)
        CreateLabel(controlsTabArea, "ZOOM SENSITIVITY:", new Vector2(0.05f, startYCtrl - spacingYCtrl - 0.04f), new Vector2(0.4f, startYCtrl - spacingYCtrl + 0.02f));
        GameObject zoomSliderObj = CreateSlider(controlsTabArea, "ZOOM SENSITIVITY", new Vector2(0.45f, startYCtrl - spacingYCtrl - 0.05f), new Vector2(0.95f, startYCtrl - spacingYCtrl + 0.03f),
            0.5f, 2.0f, () => tempZoomSensitivity, (val) => {
                tempZoomSensitivity = val;
                if (PZ_CameraController.Instance != null)
                {
                    PZ_CameraController.Instance.UpdateSensitivity();
                }
            }, out zoomSensValText, "x");
        sliderZoomSensitivity = zoomSliderObj.GetComponent<Slider>();


        // POPULATE TAB 3: AUDIO
        float startYAud = 0.70f;
        float spacingYAud = 0.15f;

        // 1. Master Volume (Slider)
        CreateLabel(audioTabArea, "MASTER VOLUME:", new Vector2(0.05f, startYAud - 0.04f), new Vector2(0.4f, startYAud + 0.02f));
        GameObject volSliderObj = CreateSlider(audioTabArea, "MASTER VOLUME", new Vector2(0.45f, startYAud - 0.05f), new Vector2(0.95f, startYAud + 0.03f),
            0f, 1.0f, () => tempMasterVolume, (val) => {
                tempMasterVolume = val;
                AudioListener.volume = val;
            }, out volValText, "%");
        sliderMasterVolume = volSliderObj.GetComponent<Slider>();

        // 2. Music Volume (Slider)
        CreateLabel(audioTabArea, "MUSIC VOLUME:", new Vector2(0.05f, startYAud - spacingYAud - 0.04f), new Vector2(0.4f, startYAud - spacingYAud + 0.02f));
        GameObject musSliderObj = CreateSlider(audioTabArea, "MUSIC VOLUME", new Vector2(0.45f, startYAud - spacingYAud - 0.05f), new Vector2(0.95f, startYAud - spacingYAud + 0.03f),
            0f, 1.0f, () => tempMusicVolume, (val) => {
                tempMusicVolume = val;
                bgmVolume = val;
                if (bgmSource != null) bgmSource.volume = bgmVolume;
            }, out musicValText, "%");
        sliderMusicVolume = musSliderObj.GetComponent<Slider>();

        // 3. SFX Volume (Slider)
        CreateLabel(audioTabArea, "SFX VOLUME:", new Vector2(0.05f, startYAud - spacingYAud*2 - 0.04f), new Vector2(0.4f, startYAud - spacingYAud*2 + 0.02f));
        GameObject sfxSliderObj = CreateSlider(audioTabArea, "SFX VOLUME", new Vector2(0.45f, startYAud - spacingYAud*2 - 0.05f), new Vector2(0.95f, startYAud - spacingYAud*2 + 0.03f),
            0f, 1.0f, () => tempSFXVolume, (val) => {
                tempSFXVolume = val;
                sfxVolume = val;
            }, out sfxValText, "%");
        sliderSFXVolume = sfxSliderObj.GetComponent<Slider>();


        // Nút BACK (Bên trái)
        CreateMenuButton(optionsPanel, "BACK", () => {
            if (IsSettingsModified())
            {
                ShowUnsavedChangesPopup();
            }
            else
            {
                OpenPanel(mainPanel.GetComponent<CanvasGroup>());
            }
        }, new Vector2(0.1f, 0.1f));

        // Nút SAVE (Đôi xứng với nút BACK ở góc phải)
        CreateMenuButton(optionsPanel, "SAVE", () => {
            SaveSettings();
            OpenPanel(mainPanel.GetComponent<CanvasGroup>());
        }, new Vector2(0.9f, 0.1f));

        LoadSavedSettingsToTemp();
    }

    private GameObject CreateTabButton(GameObject parent, string text, System.Action onClick, Vector2 anchorMin, Vector2 anchorMax, out TextMeshProUGUI textMesh)
    {
        GameObject btnObj = new GameObject("TabBtn_" + text);
        btnObj.transform.SetParent(parent.transform, false);
        RectTransform rect = btnObj.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image img = btnObj.AddComponent<Image>();
        img.color = new Color(0.06f, 0.06f, 0.06f, 1f);

        Button btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(() => {
            PlayClickSFX();
            onClick();
        });

        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(btnObj.transform, false);
        RectTransform txtRt = txtObj.AddComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = Vector2.zero;
        txtRt.offsetMax = Vector2.zero;

        textMesh = txtObj.AddComponent<TextMeshProUGUI>();
        if (gameFont != null) textMesh.font = gameFont;
        textMesh.text = text;
        textMesh.fontSize = 18f;
        textMesh.alignment = TextAlignmentOptions.Center;
        textMesh.color = new Color(0.7f, 0.7f, 0.7f);

        AutoMenuButtonEffect effect = btnObj.AddComponent<AutoMenuButtonEffect>();
        effect.Setup(textMesh, true);

        return btnObj;
    }

    private void CreateDropdown(GameObject parent, string title, Vector2 labelMin, Vector2 labelMax, Vector2 btnMin, Vector2 btnMax, string[] options, System.Func<int> getCurrentIndex, System.Action<int> onSelect, out TextMeshProUGUI valueTextObj)
    {
        // 1. Tên nhãn
        CreateLabel(parent, title, labelMin, labelMax);

        // 2. Nút bấm chính
        GameObject btnObj = new GameObject("Dropdown_" + title);
        btnObj.transform.SetParent(parent.transform, false);
        RectTransform btnRect = btnObj.AddComponent<RectTransform>();
        btnRect.anchorMin = btnMin;
        btnRect.anchorMax = btnMax;
        btnRect.offsetMin = Vector2.zero;
        btnRect.offsetMax = Vector2.zero;

        Image btnImg = btnObj.AddComponent<Image>();
        btnImg.color = new Color(0.04f, 0.04f, 0.04f, 1f);

        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(btnObj.transform, false);
        TextMeshProUGUI tmpText = txtObj.AddComponent<TextMeshProUGUI>();
        if (gameFont != null) tmpText.font = gameFont;
        tmpText.text = $"{options[getCurrentIndex()]}  ▼";
        tmpText.alignment = TextAlignmentOptions.Center;
        tmpText.color = Color.yellow;
        tmpText.fontSize = 22;
        RectTransform txtRect = txtObj.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = Vector2.zero;
        txtRect.offsetMax = Vector2.zero;
        
        valueTextObj = tmpText;

        Button btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(() =>
        {
            if (activeDropdownOverlay != null)
            {
                Destroy(activeDropdownOverlay);
                activeDropdownOverlay = null;
                return;
            }

            int currentIndex = getCurrentIndex(); // Query live index when opened

            activeDropdownOverlay = new GameObject("DropdownOverlay");
            activeDropdownOverlay.transform.SetParent(parent.transform, false);
            activeDropdownOverlay.transform.SetAsLastSibling();

            bool growUpwards = (btnMin.y < 0.35f);
            RectTransform overlayRect = activeDropdownOverlay.AddComponent<RectTransform>();
            if (growUpwards)
            {
                overlayRect.anchorMin = new Vector2(btnMin.x, btnMax.y);
                overlayRect.anchorMax = new Vector2(btnMax.x, btnMax.y);
                overlayRect.pivot = new Vector2(0.5f, 0f); // Grow upwards
                overlayRect.offsetMin = new Vector2(0, 5);
                overlayRect.offsetMax = new Vector2(0, 5);
            }
            else
            {
                overlayRect.anchorMin = new Vector2(btnMin.x, btnMin.y);
                overlayRect.anchorMax = new Vector2(btnMax.x, btnMin.y);
                overlayRect.pivot = new Vector2(0.5f, 1f); // Grow downwards
                overlayRect.offsetMin = new Vector2(0, -5);
                overlayRect.offsetMax = new Vector2(0, -5);
            }

            ContentSizeFitter csf = activeDropdownOverlay.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            Image overlayImg = activeDropdownOverlay.AddComponent<Image>();
            overlayImg.color = new Color(0.04f, 0.04f, 0.04f, 1f);

            VerticalLayoutGroup vlg = activeDropdownOverlay.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 2;
            vlg.padding = new RectOffset(2, 2, 2, 2);
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;

            for (int i = 0; i < options.Length; i++)
            {
                int index = i;
                GameObject itemObj = new GameObject("Item_" + index);
                itemObj.transform.SetParent(activeDropdownOverlay.transform, false);
                
                LayoutElement le = itemObj.AddComponent<LayoutElement>();
                le.preferredHeight = 45f;

                Image itemImg = itemObj.AddComponent<Image>();
                itemImg.color = new Color(0.06f, 0.06f, 0.06f, 1f); // Uniform dark background

                Button itemBtn = itemObj.AddComponent<Button>();
                itemBtn.targetGraphic = itemImg;
                itemBtn.transition = Selectable.Transition.ColorTint;
                
                // Color Block for clean hover states, no sticky selected color blocks
                ColorBlock cb = itemBtn.colors;
                cb.normalColor = new Color(0.06f, 0.06f, 0.06f, 1f);
                cb.highlightedColor = new Color(0.15f, 0.15f, 0.15f, 1f); // Subtle hover color
                cb.pressedColor = new Color(0.03f, 0.03f, 0.03f, 1f);
                cb.selectedColor = new Color(0.06f, 0.06f, 0.06f, 1f); // Keep normal when selected, no sticky background selection bars
                cb.disabledColor = new Color(0.06f, 0.06f, 0.06f, 1f);
                itemBtn.colors = cb;

                Navigation nav = new Navigation();
                nav.mode = Navigation.Mode.None;
                itemBtn.navigation = nav;
                
                GameObject itemTxtObj = new GameObject("Text");
                itemTxtObj.transform.SetParent(itemObj.transform, false);
                TextMeshProUGUI itemTmpText = itemTxtObj.AddComponent<TextMeshProUGUI>();
                if (gameFont != null) itemTmpText.font = gameFont;
                itemTmpText.text = options[index];
                itemTmpText.alignment = TextAlignmentOptions.Center;
                itemTmpText.fontSize = 20;
                itemTmpText.color = (index == currentIndex) ? Color.yellow : Color.white; // Text highlighted in yellow
                RectTransform itemTxtRect = itemTxtObj.GetComponent<RectTransform>();
                itemTxtRect.anchorMin = Vector2.zero;
                itemTxtRect.anchorMax = Vector2.one;
                itemTxtRect.offsetMin = Vector2.zero;
                itemTxtRect.offsetMax = Vector2.zero;

                itemBtn.onClick.AddListener(() =>
                {
                    tmpText.text = $"{options[index]}  ▼";
                    onSelect(index);
                    Destroy(activeDropdownOverlay);
                    activeDropdownOverlay = null;
                });

                AutoMenuButtonEffect eff = itemObj.AddComponent<AutoMenuButtonEffect>();
                eff.Setup(itemTmpText, true);
            }
        });
    }
    private Sprite cachedCapsuleSprite;
    private Sprite cachedKnobSprite;

    private Sprite GetOrCreateCapsuleSprite()
    {
        if (cachedCapsuleSprite != null) return cachedCapsuleSprite;

        int width = 256;
        int height = 64;
        float cornerRadius = 32f;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float dist = 0f;
                if (x < cornerRadius)
                {
                    if (y < cornerRadius)
                        dist = Vector2.Distance(new Vector2(x, y), new Vector2(cornerRadius, cornerRadius));
                    else if (y >= height - cornerRadius)
                        dist = Vector2.Distance(new Vector2(x, y), new Vector2(cornerRadius, height - cornerRadius));
                    else
                        dist = cornerRadius - x;
                }
                else if (x >= width - cornerRadius)
                {
                    if (y < cornerRadius)
                        dist = Vector2.Distance(new Vector2(x, y), new Vector2(width - cornerRadius, cornerRadius));
                    else if (y >= height - cornerRadius)
                        dist = Vector2.Distance(new Vector2(x, y), new Vector2(width - cornerRadius, height - cornerRadius));
                    else
                        dist = x - (width - cornerRadius - 1);
                }
                else
                {
                    float yDist = Mathf.Min(y, height - 1 - y);
                    dist = cornerRadius - yDist;
                }

                // Áp dụng Smooth Anti-Aliasing ở rìa ngoài của Capsule
                float alpha = 1f;
                if (dist > cornerRadius - 1.5f)
                {
                    alpha = Mathf.Clamp01((cornerRadius - dist) / 1.5f);
                }

                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        texture.Apply();
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        
        cachedCapsuleSprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
        return cachedCapsuleSprite;
    }

    private Sprite GetOrCreateKnobSprite()
    {
        if (cachedKnobSprite != null) return cachedKnobSprite;

        int size = 64;
        float radius = 32f;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = size / 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                
                // Áp dụng Smooth Anti-Aliasing ở rìa ngoài của hình tròn Knob
                float alpha = 1f;
                if (dist > radius - 1.5f)
                {
                    alpha = Mathf.Clamp01((radius - dist) / 1.5f);
                }
                
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        texture.Apply();
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        cachedKnobSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        return cachedKnobSprite;
    }

    private GameObject CreateSlider(GameObject parent, string title, Vector2 anchorMin, Vector2 anchorMax, float minValue, float maxValue, System.Func<float> getCurrentValue, System.Action<float> onValueChanged, out TextMeshProUGUI valueTextObj, string valueFormat = "0.0")
    {
        GameObject container = new GameObject("SliderContainer");
        container.transform.SetParent(parent.transform, false);
        RectTransform rect = container.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Sprite bgSprite = GetOrCreateCapsuleSprite();
        Sprite knobSprite = GetOrCreateKnobSprite();

        // Determine slider color based on setting title
        Color themeColor = new Color(0.85f, 0.24f, 0.2f, 1f); // Red for brightness/other
        if (title.Contains("VOLUME"))
        {
            themeColor = new Color(0.12f, 0.58f, 0.89f, 1f); // Blue for volume
        }
        else if (title.Contains("SENSITIVITY"))
        {
            themeColor = new Color(0.95f, 0.6f, 0.1f, 1f); // Yellow/Orange for sensitivity
        }

        // Background Track (Capsule track)
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(container.transform, false);
        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0f, 0.5f);
        bgRect.anchorMax = new Vector2(0.75f, 0.5f);
        bgRect.anchoredPosition = Vector2.zero;
        bgRect.sizeDelta = new Vector2(0, 16); // Giảm độ dày thanh trượt xuống 16px để thanh thoát hơn
        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.sprite = bgSprite;
        bgImg.type = Image.Type.Sliced;
        bgImg.color = new Color(0.06f, 0.07f, 0.1f, 1f); // Very dark navy-gray track background

        Outline trackOutline = bgObj.AddComponent<Outline>();
        trackOutline.effectColor = new Color(themeColor.r, themeColor.g, themeColor.b, 0.25f); // Subtle tinted track outline
        trackOutline.effectDistance = new Vector2(1f, 1f);

        // Fill Area
        GameObject fillAreaObj = new GameObject("FillArea");
        fillAreaObj.transform.SetParent(container.transform, false);
        RectTransform fillAreaRect = fillAreaObj.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0f, 0.5f);
        fillAreaRect.anchorMax = new Vector2(0.75f, 0.5f);
        fillAreaRect.anchoredPosition = Vector2.zero;
        fillAreaRect.sizeDelta = new Vector2(0, 16);
        fillAreaRect.offsetMin = new Vector2(4, 0);
        fillAreaRect.offsetMax = new Vector2(-4, 0);

        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(fillAreaObj.transform, false);
        RectTransform fillRect = fillObj.AddComponent<RectTransform>();
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        Image fillImg = fillObj.AddComponent<Image>();
        fillImg.sprite = bgSprite;
        fillImg.type = Image.Type.Sliced;
        fillImg.color = themeColor;

        // Handle Area
        GameObject handleAreaObj = new GameObject("HandleArea");
        handleAreaObj.transform.SetParent(container.transform, false);
        RectTransform handleAreaRect = handleAreaObj.AddComponent<RectTransform>();
        handleAreaRect.anchorMin = new Vector2(0f, 0.5f);
        handleAreaRect.anchorMax = new Vector2(0.75f, 0.5f);
        handleAreaRect.anchoredPosition = Vector2.zero;
        handleAreaRect.sizeDelta = new Vector2(0, 16);
        handleAreaRect.offsetMin = new Vector2(10, 0);
        handleAreaRect.offsetMax = new Vector2(-10, 0);

        GameObject handleObj = new GameObject("Handle");
        handleObj.transform.SetParent(handleAreaObj.transform, false);
        RectTransform handleRect = handleObj.AddComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(20, 20); // Giảm nút trượt (Knob) xuống 20px ôm khít thanh trượt hơn
        Image handleImg = handleObj.AddComponent<Image>();
        handleImg.sprite = knobSprite;
        handleImg.color = themeColor; // Knob matches the fill track theme color

        Outline handleOutline = handleObj.AddComponent<Outline>();
        handleOutline.effectColor = new Color(0f, 0f, 0f, 0.7f); // Subtle outline to make knob pop
        handleOutline.effectDistance = new Vector2(1f, -1f);

        // Text Value (on the right side)
        GameObject valObj = new GameObject("ValueText");
        valObj.transform.SetParent(container.transform, false);
        TextMeshProUGUI tmpValText = valObj.AddComponent<TextMeshProUGUI>();
        if (gameFont != null) tmpValText.font = gameFont;
        tmpValText.alignment = TextAlignmentOptions.Right;
        tmpValText.fontSize = 20;
        tmpValText.color = Color.yellow;
        RectTransform valRect = valObj.GetComponent<RectTransform>();
        valRect.anchorMin = new Vector2(0.80f, 0f);
        valRect.anchorMax = new Vector2(1f, 1f);
        valRect.offsetMin = Vector2.zero;
        valRect.offsetMax = Vector2.zero;

        // Slider Component
        Slider slider = container.AddComponent<Slider>();
        slider.minValue = minValue;
        slider.maxValue = maxValue;
        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImg;
        slider.direction = Slider.Direction.LeftToRight;
        
        Navigation nav = new Navigation();
        nav.mode = Navigation.Mode.None;
        slider.navigation = nav;

        // Set initial value
        float currentVal = getCurrentValue();
        slider.value = currentVal;
        if (valueFormat == "%")
            tmpValText.text = Mathf.RoundToInt(currentVal * 100f) + "%";
        else if (valueFormat == "x")
            tmpValText.text = currentVal.ToString("F1") + "x";
        else
            tmpValText.text = currentVal.ToString(valueFormat);

        slider.onValueChanged.AddListener((val) => {
            onValueChanged(val);
            if (tmpValText != null)
            {
                if (valueFormat == "%")
                    tmpValText.text = Mathf.RoundToInt(val * 100f) + "%";
                else if (valueFormat == "x")
                    tmpValText.text = val.ToString("F1") + "x";
                else
                    tmpValText.text = val.ToString(valueFormat);
            }
        });

        valueTextObj = tmpValText;
        return container;
    }

    private GameObject CreateHorizontalSelector(GameObject parent, Vector2 anchorMin, Vector2 anchorMax, UnityEngine.Events.UnityAction onLeft, UnityEngine.Events.UnityAction onRight, out TextMeshProUGUI valueTextObj)
    {
        GameObject container = new GameObject("SelectorContainer");
        container.transform.SetParent(parent.transform, false);
        RectTransform rect = container.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        // Nút trái [-]
        CreateMenuButton(container, " - ", onLeft, new Vector2(0f, 0.5f), true, new Vector2(50, 40), 25f);

        // Ô hiển thị giá trị
        GameObject valObj = new GameObject("ValueText");
        valObj.transform.SetParent(container.transform, false);
        valueTextObj = valObj.AddComponent<TextMeshProUGUI>();
        if (gameFont != null) valueTextObj.font = gameFont;
        valueTextObj.alignment = TextAlignmentOptions.Center;
        valueTextObj.fontSize = 22;
        valueTextObj.color = Color.yellow;
        RectTransform valRect = valObj.GetComponent<RectTransform>();
        valRect.anchorMin = new Vector2(0.2f, 0);
        valRect.anchorMax = new Vector2(0.8f, 1);
        valRect.offsetMin = Vector2.zero;
        valRect.offsetMax = Vector2.zero;

        // Nút phải [+]
        CreateMenuButton(container, " + ", onRight, new Vector2(0.95f, 0.5f), true, new Vector2(50, 40), 25f);

        return container;
    }

    private void AdjustBrightness(float delta)
    {
        tempBrightness = Mathf.Clamp(tempBrightness + delta, 0.5f, 1.0f);
        UpdateBrightText();
        if (GlobalSettingsManager.Instance != null)
        {
            GlobalSettingsManager.Instance.ApplyBrightness(tempBrightness);
        }
    }

    private void UpdateBrightText()
    {
        string val = Mathf.RoundToInt(tempBrightness * 100f) + "%";
        if (brightValText != null) brightValText.text = val;
        if (pBrightText != null) pBrightText.text = val;
    }

    private void AdjustQuality(int delta)
    {
        tempQualityLevel = Mathf.Clamp(tempQualityLevel + delta, 0, 2);
        UpdateQualityText();
        if (GlobalSettingsManager.Instance != null)
        {
            GlobalSettingsManager.Instance.ApplyGraphicsQuality(tempQualityLevel);
        }
    }

    private void UpdateQualityText()
    {
        UpdateDropdownTexts();
    }

    private void AdjustShadows(int delta)
    {
        tempShadowQuality = Mathf.Clamp(tempShadowQuality + delta, 0, 2);
        UpdateShadowText();
        if (GlobalSettingsManager.Instance != null)
        {
            GlobalSettingsManager.Instance.ApplyShadowQuality(tempShadowQuality);
        }
    }

    private void UpdateShadowText()
    {
        UpdateDropdownTexts();
    }

    private void AdjustAntiAliasing(int delta)
    {
        tempAntiAliasing = Mathf.Clamp(tempAntiAliasing + delta, 0, 3);
        UpdateAAText();
        if (GlobalSettingsManager.Instance != null)
        {
            GlobalSettingsManager.Instance.ApplyAntiAliasing(tempAntiAliasing);
        }
    }

    private void UpdateAAText()
    {
        UpdateDropdownTexts();
    }

    private void AdjustShowFPS(int delta)
    {
        tempShowFPS = (tempShowFPS == 1) ? 0 : 1;
        UpdateShowFPSText();
        if (GlobalSettingsManager.Instance != null)
        {
            GlobalSettingsManager.Instance.ApplyShowFPS(tempShowFPS == 1);
        }
    }

    private void UpdateShowFPSText()
    {
        UpdateDropdownTexts();
    }

    private void AdjustZoomSensitivity(float delta)
    {
        tempZoomSensitivity = Mathf.Clamp(tempZoomSensitivity + delta, 0.5f, 2.0f);
        UpdateZoomSensText();
        if (PZ_CameraController.Instance != null)
        {
            PZ_CameraController.Instance.UpdateSensitivity();
        }
    }

    private void UpdateZoomSensText()
    {
        string val = tempZoomSensitivity.ToString("F1") + "x";
        if (zoomSensValText != null) zoomSensValText.text = val;
        if (pZoomText != null) pZoomText.text = val;
    }

    private void AdjustFPS(int delta)
    {
        tempFpsIndex = Mathf.Clamp(tempFpsIndex + delta, 0, fpsOptions.Length - 1);
        UpdateFPSText();
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = fpsOptions[tempFpsIndex];
    }

    private void UpdateFPSText()
    {
        UpdateDropdownTexts();
    }

    private void AdjustSensitivity(float delta)
    {
        tempSensitivity = Mathf.Clamp(tempSensitivity + delta, 0.1f, 1.0f);
        UpdateSensText();
    }

    private void UpdateSensText()
    {
        string val = tempSensitivity.ToString("F1") + "x";
        if (sensValText != null) sensValText.text = val;
        if (pSensText != null) pSensText.text = val;
    }

    public void UpdateAudioSettings()
    {
        bgmVolume = PlayerPrefs.GetFloat("GameMusicVolume", 0.5f);
        sfxVolume = PlayerPrefs.GetFloat("GameSFXVolume", 0.8f);
        if (bgmSource != null)
        {
            bgmSource.volume = bgmVolume;
        }
    }

    private void AdjustVolume(float delta)
    {
        tempMasterVolume = Mathf.Clamp(tempMasterVolume + delta, 0f, 1.0f);
        UpdateVolumeText();
        AudioListener.volume = tempMasterVolume;
    }

    private void UpdateVolumeText()
    {
        string val = Mathf.RoundToInt(tempMasterVolume * 100f) + "%";
        if (volValText != null) volValText.text = val;
        if (pVolText != null) pVolText.text = val;
    }

    private void AdjustMusicVolume(float delta)
    {
        tempMusicVolume = Mathf.Clamp(tempMusicVolume + delta, 0f, 1.0f);
        UpdateMusicVolumeText();
        bgmVolume = tempMusicVolume;
        if (bgmSource != null)
        {
            bgmSource.volume = bgmVolume;
        }
    }

    private void UpdateMusicVolumeText()
    {
        string val = Mathf.RoundToInt(tempMusicVolume * 100f) + "%";
        if (musicValText != null) musicValText.text = val;
        if (pMusText != null) pMusText.text = val;
    }

    private void AdjustSFXVolume(float delta)
    {
        tempSFXVolume = Mathf.Clamp(tempSFXVolume + delta, 0f, 1.0f);
        UpdateSFXVolumeText();
        sfxVolume = tempSFXVolume;
    }

    private void UpdateSFXVolumeText()
    {
        string val = Mathf.RoundToInt(tempSFXVolume * 100f) + "%";
        if (sfxValText != null) sfxValText.text = val;
        if (pSfxText != null) pSfxText.text = val;
    }
    private void GenerateCreditsPanel(GameObject canvasGO) { creditsPanel = CreateBasePanel("CreditsPanel", canvasGO); CanvasGroup cg = creditsPanel.AddComponent<CanvasGroup>(); cg.alpha = 0f; cg.interactable = false; cg.blocksRaycasts = false; CreateTitleText(creditsPanel, "SURVIVAL TEAM", 0.9f); GameObject scrollObj = new GameObject("Credits_Scroll"); scrollObj.transform.SetParent(creditsPanel.transform, false); RectTransform scrollRectT = scrollObj.AddComponent<RectTransform>(); scrollRectT.anchorMin = new Vector2(0.15f, 0.2f); scrollRectT.anchorMax = new Vector2(0.85f, 0.8f); scrollRectT.offsetMin = Vector2.zero; scrollRectT.offsetMax = Vector2.zero; ScrollRect sr = scrollObj.AddComponent<ScrollRect>(); sr.horizontal = false; sr.vertical = true; sr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide; GameObject vp = new GameObject("Viewport"); vp.transform.SetParent(scrollObj.transform, false); RectTransform vpRT = vp.AddComponent<RectTransform>(); vpRT.anchorMin = Vector2.zero; vpRT.anchorMax = Vector2.one; vpRT.offsetMin = Vector2.zero; vpRT.offsetMax = Vector2.zero; vp.AddComponent<RectMask2D>(); GameObject content = new GameObject("Content"); content.transform.SetParent(vp.transform, false); RectTransform contentRT = content.AddComponent<RectTransform>(); contentRT.anchorMin = new Vector2(0, 1); contentRT.anchorMax = new Vector2(1, 1); contentRT.pivot = new Vector2(0.5f, 1); contentRT.offsetMin = Vector2.zero; contentRT.offsetMax = Vector2.zero; VerticalLayoutGroup vlg = content.AddComponent<VerticalLayoutGroup>(); vlg.childAlignment = TextAnchor.UpperCenter; vlg.spacing = 40; vlg.padding = new RectOffset(0, 0, 400, 400); ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>(); csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize; sr.content = contentRT; creditsContent = contentRT; CreateCreditLine(content, "LEAD PROGRAMMER", "TRẦN NGỌC ĐĂNG KHOA", Color.cyan); CreateCreditLine(content, "SYSTEM & PLAYER UI", "NGUYỄN TRÍ TÍN", Color.yellow); CreateCreditLine(content, "WORLD ARCHITECT (MAP)", "YÊN NHI", Color.white); CreateCreditLine(content, "LEAD AI & ZOMBIE BOSS", "HOÀNG THÁI", Color.red); CreateCreditLine(content, "VEHICLE MECHANICS", "VĂN HẬU", Color.green); CreateCreditLine(content, "TECHNICAL ARTIST (LOS FOW)", "ĐĂNG KHOA", Color.white); CreateCreditLine(content, "POWERED BY", "UNITY 6.0 / PHOTON FUSION", new Color(0.7f, 0.7f, 0.7f)); CreateCreditLine(content, "AUDIO DESIGN", "BGM: PROJECT ZOMBOID\nSFX: KENNEY / FREESOUND", new Color(0.7f, 0.7f, 0.7f)); CreateCreditLine(content, "SPECIAL THANKS", "TO ALL SURVIVORS WHO TESTED THIS GAME", Color.white); CreateMenuButton(creditsPanel, "BACK", () => { isCreditsOpen = false; OpenPanel(mainPanel.GetComponent<CanvasGroup>()); }, new Vector2(0.1f, 0.1f)); }
    private void CreateCreditLine(GameObject parent, string role, string name, Color nameColor) { GameObject lineObj = new GameObject("CreditLine"); lineObj.transform.SetParent(parent.transform, false); TextMeshProUGUI txt = lineObj.AddComponent<TextMeshProUGUI>(); if (gameFont != null) txt.font = gameFont; txt.text = $"<size=20><color=#aaaaaa>{role}</color></size>\n<size=32><color=#{ColorUtility.ToHtmlStringRGB(nameColor)}>{name}</color></size>"; txt.alignment = TextAlignmentOptions.Center; }
    private void OpenPanel(CanvasGroup targetPanel)
    {
        if (activeDropdownOverlay != null)
        {
            Destroy(activeDropdownOverlay);
            activeDropdownOverlay = null;
        }

        if (connectionPopupPanel != null && connectionPopupPanel.activeSelf)
            connectionPopupPanel.SetActive(false);

        if (currentActivePanel == targetPanel) return;

        if (currentActivePanel != null)
            StartCoroutine(FadePanel(currentActivePanel, 0f, false));

        currentActivePanel = targetPanel;
        StartCoroutine(FadePanel(currentActivePanel, 1f, true));

        if (targetPanel.gameObject.name == "OptionsPanel") LoadSavedSettingsToTemp();
        if (targetPanel.gameObject.name == "CharacterSelectPanel") UpdatePreview();
        isCreditsOpen = (targetPanel.gameObject.name == "CreditsPanel");
        if (isCreditsOpen && creditsContent != null) creditsContent.anchoredPosition = Vector2.zero;
    }
    private void LoadSavedSettingsToTemp()
    {
        tempResIndex = Mathf.Clamp(PlayerPrefs.GetInt("SelectedResIndex", 3), 0, commonResolutions.Length - 1);
        tempWindowMode = Mathf.Clamp(PlayerPrefs.GetInt("GameWindowMode", 0), 0, windowModeLabels.Length - 1);
        tempBrightness = PlayerPrefs.GetFloat("GameBrightness", 1.0f);

        int savedFps = PlayerPrefs.GetInt("GameFPSLimit", 60);
        tempFpsIndex = 1;
        for (int i = 0; i < fpsOptions.Length; i++)
        {
            if (fpsOptions[i] == savedFps) { tempFpsIndex = i; break; }
        }

        tempSensitivity = PlayerPrefs.GetFloat("MouseSensitivity", 1.0f);
        tempMasterVolume = PlayerPrefs.GetFloat("GameMasterVolume", 1.0f);
        tempMusicVolume = PlayerPrefs.GetFloat("GameMusicVolume", 0.5f);
        tempSFXVolume = PlayerPrefs.GetFloat("GameSFXVolume", 0.8f);

        // Load các cấu hình đồ họa/zoom mới
        tempQualityLevel = Mathf.Clamp(PlayerPrefs.GetInt("GameQualityLevel", 2), 0, 2);
        tempShadowQuality = Mathf.Clamp(PlayerPrefs.GetInt("GameShadowQuality", 2), 0, 2);
        tempAntiAliasing = Mathf.Clamp(PlayerPrefs.GetInt("GameAntiAliasing", 2), 0, 3);
        tempShowFPS = Mathf.Clamp(PlayerPrefs.GetInt("GameShowFPS", 1), 0, 1);
        tempFPSPosition = Mathf.Clamp(PlayerPrefs.GetInt("GameFPSPosition", 0), 0, 5);
        tempZoomSensitivity = PlayerPrefs.GetFloat("ZoomSensitivity", 1.0f);

        UpdateFPSText();
        UpdateBrightText();
        UpdateVolumeText();
        UpdateMusicVolumeText();
        UpdateSFXVolumeText();
        UpdateSensText();
        UpdateDropdownTexts();

        // Refresh UI cho các cài đặt mới
        UpdateQualityText();
        UpdateShadowText();
        UpdateAAText();
        UpdateShowFPSText();
        UpdateZoomSensText();

        // 🔥 ĐỒNG BỘ GIÁ TRỊ SLIDER LÊN UI THỰC TẾ
        if (sliderBrightness != null) sliderBrightness.value = tempBrightness;
        if (sliderSensitivity != null) sliderSensitivity.value = tempSensitivity;
        if (sliderZoomSensitivity != null) sliderZoomSensitivity.value = tempZoomSensitivity;
        if (sliderMasterVolume != null) sliderMasterVolume.value = tempMasterVolume;
        if (sliderMusicVolume != null) sliderMusicVolume.value = tempMusicVolume;
        if (sliderSFXVolume != null) sliderSFXVolume.value = tempSFXVolume;

        if (pSliderBrightness != null) pSliderBrightness.value = tempBrightness;
        if (pSliderSensitivity != null) pSliderSensitivity.value = tempSensitivity;
        if (pSliderZoomSensitivity != null) pSliderZoomSensitivity.value = tempZoomSensitivity;
        if (pSliderMasterVolume != null) pSliderMasterVolume.value = tempMasterVolume;
        if (pSliderMusicVolume != null) pSliderMusicVolume.value = tempMusicVolume;
        if (pSliderSFXVolume != null) pSliderSFXVolume.value = tempSFXVolume;

        // Áp dụng lập tức vào thực tế game
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = fpsOptions[tempFpsIndex];
        AudioListener.volume = tempMasterVolume;
        bgmVolume = tempMusicVolume;
        if (bgmSource != null) bgmSource.volume = bgmVolume;
        sfxVolume = tempSFXVolume;
        
        if (GlobalSettingsManager.Instance != null)
        {
            GlobalSettingsManager.Instance.ApplyBrightness(tempBrightness);
            GlobalSettingsManager.Instance.ApplyGraphicsQuality(tempQualityLevel);
            GlobalSettingsManager.Instance.ApplyShadowQuality(tempShadowQuality);
            GlobalSettingsManager.Instance.ApplyAntiAliasing(tempAntiAliasing);
            GlobalSettingsManager.Instance.ApplyShowFPS(tempShowFPS == 1);
            GlobalSettingsManager.Instance.ApplyFPSPosition(tempFPSPosition);
        }

        if (PZ_CameraController.Instance != null)
        {
            PZ_CameraController.Instance.UpdateSensitivity();
        }

        ShowTab(0);
    }

    private void SaveSettings()
    {
        PlayerPrefs.SetInt("SelectedResIndex", tempResIndex);
        PlayerPrefs.SetInt("GameWindowMode", tempWindowMode);
        PlayerPrefs.SetFloat("GameBrightness", tempBrightness);
        PlayerPrefs.SetInt("GameFPSLimit", fpsOptions[tempFpsIndex]);
        PlayerPrefs.SetFloat("MouseSensitivity", tempSensitivity);
        PlayerPrefs.SetFloat("GameMasterVolume", tempMasterVolume);
        PlayerPrefs.SetFloat("GameMusicVolume", tempMusicVolume);
        PlayerPrefs.SetFloat("GameSFXVolume", tempSFXVolume);

        // Lưu các cấu hình đồ họa/zoom mới
        PlayerPrefs.SetInt("GameQualityLevel", tempQualityLevel);
        PlayerPrefs.SetInt("GameShadowQuality", tempShadowQuality);
        PlayerPrefs.SetInt("GameAntiAliasing", tempAntiAliasing);
        PlayerPrefs.SetInt("GameShowFPS", tempShowFPS);
        PlayerPrefs.SetInt("GameFPSPosition", tempFPSPosition);
        PlayerPrefs.SetFloat("ZoomSensitivity", tempZoomSensitivity);
        PlayerPrefs.Save();

        FullScreenMode mode = FullScreenMode.ExclusiveFullScreen;
        if (tempWindowMode == 1) mode = FullScreenMode.FullScreenWindow;
        else if (tempWindowMode == 2) mode = FullScreenMode.Windowed;
        Vector2Int res = commonResolutions[tempResIndex];
        Screen.SetResolution(res.x, res.y, mode);

        if (GlobalSettingsManager.Instance != null)
        {
            GlobalSettingsManager.Instance.ApplyAllSettings();
        }
    }

    private bool IsSettingsModified()
    {
        if (tempResIndex != Mathf.Clamp(PlayerPrefs.GetInt("SelectedResIndex", 3), 0, commonResolutions.Length - 1)) return true;
        if (tempWindowMode != Mathf.Clamp(PlayerPrefs.GetInt("GameWindowMode", 0), 0, windowModeLabels.Length - 1)) return true;
        if (Mathf.Abs(tempBrightness - PlayerPrefs.GetFloat("GameBrightness", 1.0f)) > 0.01f) return true;

        int savedFps = PlayerPrefs.GetInt("GameFPSLimit", 60);
        int savedFpsIndex = 1;
        for (int i = 0; i < fpsOptions.Length; i++)
        {
            if (fpsOptions[i] == savedFps) { savedFpsIndex = i; break; }
        }
        if (tempFpsIndex != savedFpsIndex) return true;

        if (Mathf.Abs(tempSensitivity - PlayerPrefs.GetFloat("MouseSensitivity", 1.0f)) > 0.01f) return true;
        if (Mathf.Abs(tempMasterVolume - PlayerPrefs.GetFloat("GameMasterVolume", 1.0f)) > 0.01f) return true;
        if (Mathf.Abs(tempMusicVolume - PlayerPrefs.GetFloat("GameMusicVolume", 0.5f)) > 0.01f) return true;
        if (Mathf.Abs(tempSFXVolume - PlayerPrefs.GetFloat("GameSFXVolume", 0.8f)) > 0.01f) return true;

        // Check các cài đặt mới
        if (tempQualityLevel != PlayerPrefs.GetInt("GameQualityLevel", 2)) return true;
        if (tempShadowQuality != PlayerPrefs.GetInt("GameShadowQuality", 2)) return true;
        if (tempAntiAliasing != PlayerPrefs.GetInt("GameAntiAliasing", 2)) return true;
        if (tempShowFPS != PlayerPrefs.GetInt("GameShowFPS", 1)) return true;
        if (tempFPSPosition != PlayerPrefs.GetInt("GameFPSPosition", 0)) return true;
        if (Mathf.Abs(tempZoomSensitivity - PlayerPrefs.GetFloat("ZoomSensitivity", 1.0f)) > 0.01f) return true;

        return false;
    }

    private void UpdateDropdownTexts()
    {
        if (resDropdownText != null)
        {
            resDropdownText.text = $"{commonResolutions[tempResIndex].x} x {commonResolutions[tempResIndex].y}  ▼";
        }
        if (modeDropdownText != null)
        {
            modeDropdownText.text = $"{windowModeLabels[tempWindowMode]}  ▼";
        }
        if (qualityValText != null)
        {
            qualityValText.text = $"{new string[] { "LOW", "MEDIUM", "HIGH" }[tempQualityLevel]}  ▼";
        }
        if (shadowValText != null)
        {
            shadowValText.text = $"{new string[] { "DISABLED", "HARD ONLY", "ALL SHADOWS" }[tempShadowQuality]}  ▼";
        }
        if (aaValText != null)
        {
            aaValText.text = $"{new string[] { "DISABLED", "2x MSAA", "4x MSAA", "8x MSAA" }[tempAntiAliasing]}  ▼";
        }
        if (fpsValText != null)
        {
            fpsValText.text = $"{fpsLabels[tempFpsIndex]}  ▼";
        }
        if (fpsShowValText != null)
        {
            fpsShowValText.text = $"{new string[] { "OFF", "ON" }[tempShowFPS]}  ▼";
        }
        if (fpsPosDropdownText != null)
        {
            fpsPosDropdownText.text = $"{new string[] { "TOP RIGHT", "TOP LEFT", "BOTTOM RIGHT", "BOTTOM LEFT", "TOP CENTER", "BOTTOM CENTER" }[tempFPSPosition]}  ▼";
        }

        // --- PAUSE MENU DROPDOWNS ---
        if (pQualText != null)
        {
            pQualText.text = $"{new string[] { "LOW", "MEDIUM", "HIGH" }[tempQualityLevel]}  ▼";
        }
        if (pShadText != null)
        {
            pShadText.text = $"{new string[] { "DISABLED", "HARD ONLY", "ALL SHADOWS" }[tempShadowQuality]}  ▼";
        }
        if (pAAText != null)
        {
            pAAText.text = $"{new string[] { "DISABLED", "2x MSAA", "4x MSAA", "8x MSAA" }[tempAntiAliasing]}  ▼";
        }
        if (pFpsText != null)
        {
            pFpsText.text = $"{fpsLabels[tempFpsIndex]}  ▼";
        }
        if (pFpsShowText != null)
        {
            pFpsShowText.text = $"{new string[] { "OFF", "ON" }[tempShowFPS]}  ▼";
        }
        if (pFpsPosDropdownText != null)
        {
            pFpsPosDropdownText.text = $"{new string[] { "TOP RIGHT", "TOP LEFT", "BOTTOM RIGHT", "BOTTOM LEFT", "TOP CENTER", "BOTTOM CENTER" }[tempFPSPosition]}  ▼";
        }
    }

    private void ShowTab(int tabIndex)
    {
        activeTab = tabIndex;

        if (displayTabArea != null) displayTabArea.SetActive(activeTab == 0);
        if (controlsTabArea != null) controlsTabArea.SetActive(activeTab == 1);
        if (audioTabArea != null) audioTabArea.SetActive(activeTab == 2);

        if (displayTabBtnText != null) displayTabBtnText.color = (activeTab == 0) ? Color.yellow : new Color(0.7f, 0.7f, 0.7f);
        if (controlsTabBtnText != null) controlsTabBtnText.color = (activeTab == 1) ? Color.yellow : new Color(0.7f, 0.7f, 0.7f);
        if (audioTabBtnText != null) audioTabBtnText.color = (activeTab == 2) ? Color.yellow : new Color(0.7f, 0.7f, 0.7f);

        if (activeDropdownOverlay != null)
        {
            Destroy(activeDropdownOverlay);
            activeDropdownOverlay = null;
        }
    }


    public bool IsOptionsOpen => optionsPanel != null && optionsPanel.activeSelf;
    public bool IsPauseMenuOpen => isPauseMenuOpen || (pauseMenuPanel != null && pauseMenuPanel.activeSelf);
    public bool IsPauseOptionsOpen => isPauseOptionsOpen || (pauseOptionsPanel != null && pauseOptionsPanel.activeSelf);

    public float GetTempSensitivity()
    {
        return tempSensitivity;
    }

    public float GetTempZoomSensitivity()
    {
        return tempZoomSensitivity;
    }

    private void ShowUnsavedChangesPopup()
    {
        GameObject unsavedPopup = new GameObject("UnsavedChangesPopup");
        unsavedPopup.transform.SetParent(optionsPanel.transform, false);
        unsavedPopup.transform.SetAsLastSibling();

        RectTransform overlayRect = unsavedPopup.AddComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image overlayImg = unsavedPopup.AddComponent<Image>();
        overlayImg.color = new Color(0f, 0f, 0f, 0.75f);

        GameObject dialogBox = new GameObject("DialogBox");
        dialogBox.transform.SetParent(unsavedPopup.transform, false);
        RectTransform dialogRect = dialogBox.AddComponent<RectTransform>();
        dialogRect.anchorMin = new Vector2(0.3f, 0.35f);
        dialogRect.anchorMax = new Vector2(0.7f, 0.65f);
        dialogRect.offsetMin = Vector2.zero;
        dialogRect.offsetMax = Vector2.zero;
        dialogBox.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 1f);

        GameObject border = new GameObject("Border");
        border.transform.SetParent(dialogBox.transform, false);
        RectTransform borderRect = border.AddComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.offsetMin = new Vector2(4, 4);
        borderRect.offsetMax = new Vector2(-4, -4);
        Image bImg = border.AddComponent<Image>();
        bImg.color = new Color(0.3f, 0.05f, 0.05f, 1f);

        GameObject innerBox = new GameObject("Inner");
        innerBox.transform.SetParent(border.transform, false);
        RectTransform innerRect = innerBox.AddComponent<RectTransform>();
        innerRect.anchorMin = Vector2.zero;
        innerRect.anchorMax = Vector2.one;
        innerRect.offsetMin = new Vector2(2, 2);
        innerRect.offsetMax = new Vector2(-2, -2);
        innerBox.AddComponent<Image>().color = new Color(0.06f, 0.06f, 0.06f, 1f);

        GameObject txtObj = new GameObject("MessageText");
        txtObj.transform.SetParent(innerBox.transform, false);
        TextMeshProUGUI tmpText = txtObj.AddComponent<TextMeshProUGUI>();
        if (gameFont != null) tmpText.font = gameFont;
        tmpText.text = "UNSAVED CHANGES\n\nDo you want to save changes before exiting?";
        tmpText.alignment = TextAlignmentOptions.Center;
        tmpText.color = Color.white;
        tmpText.fontSize = 24;
        RectTransform txtRect = txtObj.GetComponent<RectTransform>();
        txtRect.anchorMin = new Vector2(0.1f, 0.4f);
        txtRect.anchorMax = new Vector2(0.9f, 0.9f);
        txtRect.offsetMin = Vector2.zero;
        txtRect.offsetMax = Vector2.zero;

        CreateMenuButton(innerBox, "SAVE", () =>
        {
            SaveSettings();
            Destroy(unsavedPopup);
            OpenPanel(mainPanel.GetComponent<CanvasGroup>());
        }, new Vector2(0.2f, 0.2f), true, new Vector2(120, 45), 20f);

        CreateMenuButton(innerBox, "DON'T SAVE", () =>
        {
            LoadSavedSettingsToTemp(); // Immediately revert changes and restore original settings
            Destroy(unsavedPopup);
            OpenPanel(mainPanel.GetComponent<CanvasGroup>());
        }, new Vector2(0.5f, 0.2f), true, new Vector2(180, 45), 20f);

        CreateMenuButton(innerBox, "CANCEL", () =>
        {
            Destroy(unsavedPopup);
        }, new Vector2(0.8f, 0.2f), true, new Vector2(120, 45), 20f);
    }

    private IEnumerator FadePanel(CanvasGroup panel, float targetAlpha, bool show) { if (show) { panel.gameObject.SetActive(true); panel.blocksRaycasts = true; panel.interactable = true; } else { panel.blocksRaycasts = false; panel.interactable = false; } float startAlpha = panel.alpha; float time = 0f; while (time < 0.25f) { time += Time.unscaledDeltaTime; panel.alpha = Mathf.Lerp(startAlpha, targetAlpha, time / 0.25f); yield return null; } panel.alpha = targetAlpha; if (!show) panel.gameObject.SetActive(false); }

}

public class AutoMenuButtonEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    private TextMeshProUGUI btnText;
    private Color normalColor = new Color(0.7f, 0.7f, 0.7f, 1f);
    private Color hoverColor = new Color(0.7f, 0.15f, 0.15f, 1f);
    private Vector3 originalPos;
    private bool originalPosCaptured = false;
    private bool isCentered;
    private Coroutine colorRoutine, moveRoutine;

    public void Setup(TextMeshProUGUI textComponent, bool center) 
    { 
        btnText = textComponent; 
        btnText.color = normalColor; 
        originalPos = btnText.transform.localPosition; 
        originalPosCaptured = true;
        isCentered = center; 
    }

    private void OnEnable()
    {
        ResetVisuals();
    }

    public void ResetVisuals()
    {
        if (btnText != null)
        {
            if (colorRoutine != null) StopCoroutine(colorRoutine);
            if (moveRoutine != null) StopCoroutine(moveRoutine);
            btnText.color = normalColor;
            btnText.transform.localScale = Vector3.one;
            if (originalPosCaptured)
            {
                btnText.transform.localPosition = originalPos;
            }
        }
    }

    public void OnPointerEnter(PointerEventData eventData) { AnimateColor(hoverColor); if (!isCentered && originalPosCaptured) AnimateMove(originalPos + new Vector3(15f, 0, 0)); if (AutoMainMenuManager.Instance != null) AutoMainMenuManager.Instance.PlayHoverSFX(); }
    public void OnPointerExit(PointerEventData eventData) { AnimateColor(normalColor); if (!isCentered && originalPosCaptured) AnimateMove(originalPos); }
    public void OnPointerDown(PointerEventData eventData) { if (btnText != null) btnText.transform.localScale = Vector3.one * 0.9f; if (AutoMainMenuManager.Instance != null) AutoMainMenuManager.Instance.PlayClickSFX(); }
    public void OnPointerUp(PointerEventData eventData) { if (btnText != null) btnText.transform.localScale = Vector3.one; }
    private void AnimateColor(Color target) { if (btnText == null) return; if (colorRoutine != null) StopCoroutine(colorRoutine); colorRoutine = StartCoroutine(DoColor(target, 0.15f)); }
    private void AnimateMove(Vector3 target) { if (btnText == null) return; if (moveRoutine != null) StopCoroutine(moveRoutine); moveRoutine = StartCoroutine(DoMove(target, 0.15f)); }
    private IEnumerator DoColor(Color targetColor, float duration) { Color startColor = btnText.color; float t = 0; while (t < duration) { t += Time.unscaledDeltaTime; btnText.color = Color.Lerp(startColor, targetColor, t / duration); yield return null; } btnText.color = targetColor; }
    private IEnumerator DoMove(Vector3 targetPos, float duration) { Vector3 startPos = btnText.transform.localPosition; float t = 0; while (t < duration) { t += Time.unscaledDeltaTime; float percent = t / duration; percent = percent * (2f - percent); btnText.transform.localPosition = Vector3.Lerp(startPos, targetPos, percent); yield return null; } btnText.transform.localPosition = targetPos; }
}

public class DifficultyHoverTrigger : MonoBehaviour, IPointerEnterHandler
{
    public int difficultyIndex;
    public AutoMainMenuManager menuManager;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (menuManager != null)
        {
            menuManager.ShowDifficultyInfo(difficultyIndex);
        }
    }
}
