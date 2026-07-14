using Fusion;
using Fusion.Sockets;
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
    
    private int selectedResIndex = 3; // Mặc định 1920x1080
    private Vector2Int[] commonResolutions = new Vector2Int[]
    {
        new Vector2Int(1280, 720),
        new Vector2Int(1366, 768),
        new Vector2Int(1600, 900),
        new Vector2Int(1920, 1080)
    };
    private int selectedFpsIndex = 1; // Mặc định 60 FPS
    private int[] fpsOptions = new int[] { 30, 60, 120, -1 };
    private string[] fpsLabels = new string[] { "30 FPS", "60 FPS", "120 FPS", "UNLIMITED" };
    private TextMeshProUGUI fpsValText;
    private TextMeshProUGUI sensValText;
    private TextMeshProUGUI brightValText;
    private TextMeshProUGUI volValText;
    private TextMeshProUGUI musicValText;
    private TextMeshProUGUI sfxValText;
    
    private int selectedWindowMode = 0; // 0 = Fullscreen, 1 = Borderless, 2 = Windowed
    private string[] windowModeLabels = new string[] { "FULLSCREEN", "BORDERLESS", "WINDOWED" };
    private GameObject activeDropdownOverlay = null;
    
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
    private GameObject backgroundImageObj;

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

                if (isPauseMenuOpen)
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
        boxRt.anchorMin = new Vector2(0.35f, 0.35f); boxRt.anchorMax = new Vector2(0.65f, 0.65f);
        boxRt.offsetMin = Vector2.zero; boxRt.offsetMax = Vector2.zero;
        boxObj.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.95f); // Xám đen

        // Viền trang trí
        Outline outline = boxObj.AddComponent<Outline>();
        outline.effectColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        outline.effectDistance = new Vector2(2, -2);

        CreateTitleText(boxObj, "PAUSED", 0.8f, 45);

        // Nút bấm
        CreateMenuButton(boxObj, "RESUME", () => TogglePauseMenu(), new Vector2(0.5f, 0.5f), true, new Vector2(300, 50), 22);
        CreateMenuButton(boxObj, "QUIT", () => LeaveGame(), new Vector2(0.5f, 0.3f), true, new Vector2(300, 50), 22);

        pauseMenuPanel.SetActive(false);
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
        if (backgroundImage != null) { bgImg.sprite = backgroundImage; bgImg.color = Color.white; } else { bgImg.color = new Color(0.08f, 0.08f, 0.08f, 1f); }
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
        GameObject btnContainer = new GameObject("DiffContainer"); btnContainer.transform.SetParent(newGamePanel.transform, false);
        RectTransform btnRect = btnContainer.AddComponent<RectTransform>(); btnRect.anchorMin = new Vector2(0.4f, 0.3f); btnRect.anchorMax = new Vector2(0.6f, 0.7f); btnRect.offsetMin = Vector2.zero; btnRect.offsetMax = Vector2.zero;
        VerticalLayoutGroup vlg = btnContainer.AddComponent<VerticalLayoutGroup>(); vlg.spacing = 20;
        CreateMenuButton(btnContainer, "EASY", () => { SetDifficulty(0); pendingRoomName = "Solo_Easy_" + Random.Range(1000, 9999); OpenPanel(characterSelectPanel.GetComponent<CanvasGroup>()); }); 
        CreateMenuButton(btnContainer, "MEDIUM", () => { SetDifficulty(1); pendingRoomName = "Solo_Normal_" + Random.Range(1000, 9999); OpenPanel(characterSelectPanel.GetComponent<CanvasGroup>()); }); 
        CreateMenuButton(btnContainer, "HARD", () => { SetDifficulty(2); pendingRoomName = "Solo_Hard_" + Random.Range(1000, 9999); OpenPanel(characterSelectPanel.GetComponent<CanvasGroup>()); });
        CreateMenuButton(newGamePanel, "BACK", () => OpenPanel(mainPanel.GetComponent<CanvasGroup>()), new Vector2(0.1f, 0.1f));
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
    private async void StartGameInternal(GameMode mode, string roomName)
    {
        string popupMsg = mode == GameMode.Single
            ? "INITIALIZING SOLO PROTOCOL..."
            : (mode == GameMode.Host ? "PLANNING SURVIVAL PROTOCOL..." : "SEARCHING FOR SURVIVORS...");

        ShowConnectionPopup(popupMsg);
        isConnecting = true;

        await CleanupOldRunnersAsync();

        activeRunner = Instantiate(runnerPrefab);
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
    private void CreateTitleText(GameObject parent, string text, float height = 0.9f, int fontSize = 40, TextAlignmentOptions align = TextAlignmentOptions.Center, Vector2? aMin = null, Vector2? aMax = null) { GameObject txtObj = new GameObject("Title"); txtObj.transform.SetParent(parent.transform, false); TextMeshProUGUI txt = txtObj.AddComponent<TextMeshProUGUI>(); if (gameFont != null) txt.font = gameFont; txt.text = text; txt.fontSize = fontSize; txt.fontStyle = FontStyles.Bold; txt.alignment = align; txt.color = new Color(0.8f, 0.8f, 0.8f, 1f); RectTransform rect = txtObj.GetComponent<RectTransform>(); rect.anchorMin = aMin ?? new Vector2(0, height); rect.anchorMax = aMax ?? new Vector2(1, height); rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero; }
    private void SetDifficulty(int id) { hostDifficulty = id; PlayerPrefs.SetInt("GameDifficulty", id); PlayerPrefs.Save(); for (int i = 0; i < diffTexts.Length; i++) { if (i == id) { diffTexts[i].color = Color.yellow; diffTexts[i].fontStyle = FontStyles.Bold; } else { diffTexts[i].color = Color.gray; diffTexts[i].fontStyle = FontStyles.Normal; } } }
    private void TogglePassword() { hostHasPassword = !hostHasPassword; if (hostHasPassword) { toggleText.text = "[ YES ]"; toggleText.color = Color.red; passwordInputObj.SetActive(true); } else { toggleText.text = "[ NO ]"; toggleText.color = Color.gray; passwordInputObj.SetActive(false); } }
    private void ChangeCharacter(int direction) { previewID = (previewID + direction + characterNames.Length) % characterNames.Length; charNameText.text = characterNames[previewID]; charStatsText.text = characterStats[previewID]; UpdatePreview(); }
    private void UpdatePreview() { if (previewImages == null || previewContainer == null) return; for (int i = 0; i < previewImages.Length; i++) { if (previewImages[i] != null) { bool isActive = (i == previewID); previewImages[i].SetActive(isActive); if (isActive) { previewImages[i].transform.SetParent(previewContainer, false); RectTransform rt = previewImages[i].GetComponent<RectTransform>(); if (rt != null) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero; } Image img = previewImages[i].GetComponent<Image>(); if (img != null) img.preserveAspect = true; } } } }
    private void EnableGameplayUI() { foreach (var obj in temporarilyDisabledObjects) { if (obj != null) obj.SetActive(true); } temporarilyDisabledObjects.Clear(); }
    private void OnDestroy() { EnableGameplayUI(); isMenuDestroyed = true; }
    private GameObject CreateBasePanel(string name, GameObject parent) { GameObject p = new GameObject(name); p.transform.SetParent(parent.transform, false); RectTransform r = p.AddComponent<RectTransform>(); r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero; return p; }
    private void CreateLabel(GameObject parent, string text, Vector2 anchorMin, Vector2 anchorMax) { GameObject labelObj = new GameObject("Label"); labelObj.transform.SetParent(parent.transform, false); TextMeshProUGUI labelTxt = labelObj.AddComponent<TextMeshProUGUI>(); if (gameFont != null) labelTxt.font = gameFont; labelTxt.text = text; labelTxt.color = new Color(0.8f, 0.8f, 0.8f, 1f); labelTxt.alignment = TextAlignmentOptions.Center; labelTxt.enableAutoSizing = true; labelTxt.fontSizeMin = 14; labelTxt.fontSizeMax = 20; RectTransform labelRect = labelObj.GetComponent<RectTransform>(); labelRect.anchorMin = anchorMin; labelRect.anchorMax = anchorMax; labelRect.offsetMin = Vector2.zero; labelRect.offsetMax = Vector2.zero; }
    private GameObject CreateInputField(GameObject parent, string name, string placeholderTxt, Vector2 anchorMin, Vector2 anchorMax) { GameObject inputObj = new GameObject(name); inputObj.transform.SetParent(parent.transform, false); RectTransform rect = inputObj.AddComponent<RectTransform>(); rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero; Image bg = inputObj.AddComponent<Image>(); bg.color = new Color(0.05f, 0.05f, 0.05f, 1f); TMP_InputField inputField = inputObj.AddComponent<TMP_InputField>(); inputField.targetGraphic = bg; inputField.characterLimit = 20; GameObject viewportObj = new GameObject("Viewport"); viewportObj.transform.SetParent(inputObj.transform, false); RectTransform vpRect = viewportObj.AddComponent<RectTransform>(); vpRect.anchorMin = Vector2.zero; vpRect.anchorMax = Vector2.one; vpRect.offsetMin = new Vector2(15, 0); vpRect.offsetMax = new Vector2(-15, 0); viewportObj.AddComponent<RectMask2D>(); GameObject textObj = new GameObject("Text"); textObj.transform.SetParent(viewportObj.transform, false); TextMeshProUGUI txt = textObj.AddComponent<TextMeshProUGUI>(); if (gameFont != null) txt.font = gameFont; txt.color = Color.white; txt.alignment = TextAlignmentOptions.Left; txt.enableAutoSizing = true; txt.fontSizeMin = 15; txt.fontSizeMax = 30; txt.textWrappingMode = TextWrappingModes.NoWrap; txt.overflowMode = TextOverflowModes.Truncate; RectTransform txtRect = textObj.GetComponent<RectTransform>(); txtRect.anchorMin = Vector2.zero; txtRect.anchorMax = Vector2.one; txtRect.offsetMin = Vector2.zero; txtRect.offsetMax = Vector2.zero; GameObject phObj = new GameObject("Placeholder"); phObj.transform.SetParent(viewportObj.transform, false); TextMeshProUGUI pTxt = phObj.AddComponent<TextMeshProUGUI>(); if (gameFont != null) pTxt.font = gameFont; pTxt.text = placeholderTxt; pTxt.color = Color.gray; pTxt.alignment = TextAlignmentOptions.Left; pTxt.enableAutoSizing = true; pTxt.fontSizeMin = 15; pTxt.fontSizeMax = 30; pTxt.textWrappingMode = TextWrappingModes.NoWrap; pTxt.overflowMode = TextOverflowModes.Truncate; RectTransform phRect = phObj.GetComponent<RectTransform>(); phRect.anchorMin = Vector2.zero; phRect.anchorMax = Vector2.one; phRect.offsetMin = Vector2.zero; phRect.offsetMax = Vector2.zero; inputField.textViewport = vpRect; inputField.textComponent = txt; inputField.placeholder = pTxt; return inputObj; }
    private void CreateMenuButton(GameObject parent, string text, UnityEngine.Events.UnityAction action, Vector2? customAnchor = null, bool isCenter = false, Vector2? customSize = null, float customFontSize = 35f) { GameObject btnObj = new GameObject("Btn_" + text); btnObj.transform.SetParent(parent.transform, false); RectTransform rect = btnObj.AddComponent<RectTransform>(); if (customAnchor.HasValue) { rect.anchorMin = customAnchor.Value; rect.anchorMax = customAnchor.Value; rect.pivot = isCenter ? new Vector2(0.5f, 0.5f) : new Vector2(0, 0.5f); } rect.sizeDelta = customSize.HasValue ? customSize.Value : new Vector2(300, 50); Image btnImg = btnObj.AddComponent<Image>(); btnImg.color = new Color(1, 1, 1, 0); Button btn = btnObj.AddComponent<Button>(); btn.onClick.AddListener(action); GameObject txtObj = new GameObject("Text"); txtObj.transform.SetParent(btnObj.transform, false); TextMeshProUGUI tmpText = txtObj.AddComponent<TextMeshProUGUI>(); if (gameFont != null) tmpText.font = gameFont; tmpText.text = text; tmpText.alignment = isCenter ? TextAlignmentOptions.Center : TextAlignmentOptions.Left; tmpText.color = new Color(0.7f, 0.7f, 0.7f, 1f); tmpText.textWrappingMode = TextWrappingModes.NoWrap; tmpText.enableAutoSizing = false; tmpText.fontSize = customFontSize; RectTransform txtRect = txtObj.GetComponent<RectTransform>(); txtRect.anchorMin = Vector2.zero; txtRect.anchorMax = Vector2.one; txtRect.offsetMin = Vector2.zero; txtRect.offsetMax = Vector2.zero; AutoMenuButtonEffect effect = btnObj.AddComponent<AutoMenuButtonEffect>(); effect.Setup(tmpText, isCenter); }
    private TextMeshProUGUI CreateTextBtn(GameObject parent, string text, Vector2 anchorValue, UnityEngine.Events.UnityAction action) { GameObject btnObj = new GameObject("TextBtn_" + text); btnObj.transform.SetParent(parent.transform, false); RectTransform rect = btnObj.AddComponent<RectTransform>(); rect.anchorMin = anchorValue; rect.anchorMax = anchorValue; rect.pivot = new Vector2(0.5f, 0.5f); rect.sizeDelta = new Vector2(150, 40); Image btnImg = btnObj.AddComponent<Image>(); btnImg.color = new Color(1, 1, 1, 0); Button btn = btnObj.AddComponent<Button>(); btn.onClick.AddListener(action); GameObject txtObj = new GameObject("Text"); txtObj.transform.SetParent(btnObj.transform, false); TextMeshProUGUI tmpText = txtObj.AddComponent<TextMeshProUGUI>(); if (gameFont != null) tmpText.font = gameFont; tmpText.text = text; tmpText.alignment = TextAlignmentOptions.Center; tmpText.color = Color.gray; tmpText.enableAutoSizing = true; tmpText.fontSizeMin = 14; tmpText.fontSizeMax = 20; RectTransform txtRect = txtObj.GetComponent<RectTransform>(); txtRect.anchorMin = Vector2.zero; txtRect.anchorMax = Vector2.one; txtRect.offsetMin = Vector2.zero; txtRect.offsetMax = Vector2.zero; AutoMenuButtonEffect effect = btnObj.AddComponent<AutoMenuButtonEffect>(); effect.Setup(tmpText, true); return tmpText; }

    private void GenerateOptionsPanel(GameObject canvasGO)
    {
        optionsPanel = CreateBasePanel("OptionsPanel", canvasGO);
        CanvasGroup cg = optionsPanel.AddComponent<CanvasGroup>();
        cg.alpha = 0f; cg.interactable = false; cg.blocksRaycasts = false;
        
        CreateTitleText(optionsPanel, "OPTIONS");

        GameObject settingsArea = new GameObject("Settings_Container");
        settingsArea.transform.SetParent(optionsPanel.transform, false);
        RectTransform rect = settingsArea.AddComponent<RectTransform>();
        // Làm container cao hơn để chứa 8 cài đặt thoải mái
        rect.anchorMin = new Vector2(0.2f, 0.12f);
        rect.anchorMax = new Vector2(0.8f, 0.88f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        settingsArea.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.08f, 0.95f);

        // Khởi tạo các giá trị từ PlayerPrefs
        selectedResIndex = Mathf.Clamp(PlayerPrefs.GetInt("SelectedResIndex", 3), 0, commonResolutions.Length - 1);
        selectedWindowMode = Mathf.Clamp(PlayerPrefs.GetInt("GameWindowMode", 0), 0, windowModeLabels.Length - 1);
        
        int savedFps = PlayerPrefs.GetInt("GameFPSLimit", 60);
        selectedFpsIndex = 1;
        for (int i = 0; i < fpsOptions.Length; i++)
        {
            if (fpsOptions[i] == savedFps) { selectedFpsIndex = i; break; }
        }

        // Vị trí trục Y của các dòng
        float startY = 0.88f;
        float spacingY = 0.10f;

        // 1. Độ phân giải (Resolution) - DẠNG DROPDOWN
        string[] resStrings = new string[commonResolutions.Length];
        for (int i = 0; i < commonResolutions.Length; i++)
        {
            resStrings[i] = $"{commonResolutions[i].x} x {commonResolutions[i].y}";
        }
        CreateDropdown(settingsArea, "RESOLUTION:", 
            new Vector2(0.05f, startY - 0.04f), new Vector2(0.4f, startY + 0.02f),
            new Vector2(0.45f, startY - 0.05f), new Vector2(0.95f, startY + 0.03f),
            resStrings, selectedResIndex, (idx) => {
                selectedResIndex = idx;
                PlayerPrefs.SetInt("SelectedResIndex", selectedResIndex);
                PlayerPrefs.Save();
                ApplyWindowMode(selectedWindowMode); // Áp dụng độ phân giải và chế độ hiển thị
            });

        // 2. Chế độ hiển thị (Display Mode) - DẠNG DROPDOWN
        CreateDropdown(settingsArea, "DISPLAY MODE:", 
            new Vector2(0.05f, startY - spacingY - 0.04f), new Vector2(0.4f, startY - spacingY + 0.02f),
            new Vector2(0.45f, startY - spacingY - 0.05f), new Vector2(0.95f, startY - spacingY + 0.03f),
            windowModeLabels, selectedWindowMode, (idx) => {
                ApplyWindowMode(idx);
            });

        // 3. Độ sáng (Brightness)
        CreateLabel(settingsArea, "BRIGHTNESS:", new Vector2(0.05f, startY - spacingY*2 - 0.04f), new Vector2(0.4f, startY - spacingY*2 + 0.02f));
        CreateHorizontalSelector(settingsArea, new Vector2(0.45f, startY - spacingY*2 - 0.05f), new Vector2(0.95f, startY - spacingY*2 + 0.03f),
            () => AdjustBrightness(-0.1f), () => AdjustBrightness(0.1f), out brightValText);
        UpdateBrightText();

        // 4. Khung hình (FPS Limit)
        CreateLabel(settingsArea, "FPS LIMIT:", new Vector2(0.05f, startY - spacingY*3 - 0.04f), new Vector2(0.4f, startY - spacingY*3 + 0.02f));
        CreateHorizontalSelector(settingsArea, new Vector2(0.45f, startY - spacingY*3 - 0.05f), new Vector2(0.95f, startY - spacingY*3 + 0.03f),
            () => AdjustFPS(-1), () => AdjustFPS(1), out fpsValText);
        UpdateFPSText();

        // 5. Tốc độ chuột / Độ nhạy camera nhắm bắn (Mouse Sensitivity)
        CreateLabel(settingsArea, "AIM SENSITIVITY:", new Vector2(0.05f, startY - spacingY*4 - 0.04f), new Vector2(0.4f, startY - spacingY*4 + 0.02f));
        CreateHorizontalSelector(settingsArea, new Vector2(0.45f, startY - spacingY*4 - 0.05f), new Vector2(0.95f, startY - spacingY*4 + 0.03f),
            () => AdjustSensitivity(-0.1f), () => AdjustSensitivity(0.1f), out sensValText);
        UpdateSensText();

        // 6. Âm lượng Master (Master Volume)
        CreateLabel(settingsArea, "MASTER VOLUME:", new Vector2(0.05f, startY - spacingY*5 - 0.04f), new Vector2(0.4f, startY - spacingY*5 + 0.02f));
        CreateHorizontalSelector(settingsArea, new Vector2(0.45f, startY - spacingY*5 - 0.05f), new Vector2(0.95f, startY - spacingY*5 + 0.03f),
            () => AdjustVolume(-0.1f), () => AdjustVolume(0.1f), out volValText);
        UpdateVolumeText();

        // 7. Âm lượng Nhạc nền (Music Volume)
        CreateLabel(settingsArea, "MUSIC VOLUME:", new Vector2(0.05f, startY - spacingY*6 - 0.04f), new Vector2(0.4f, startY - spacingY*6 + 0.02f));
        CreateHorizontalSelector(settingsArea, new Vector2(0.45f, startY - spacingY*6 - 0.05f), new Vector2(0.95f, startY - spacingY*6 + 0.03f),
            () => AdjustMusicVolume(-0.1f), () => AdjustMusicVolume(0.1f), out musicValText);
        UpdateMusicVolumeText();

        // 8. Âm lượng Hiệu ứng (SFX Volume)
        CreateLabel(settingsArea, "SFX VOLUME:", new Vector2(0.05f, startY - spacingY*7 - 0.04f), new Vector2(0.4f, startY - spacingY*7 + 0.02f));
        CreateHorizontalSelector(settingsArea, new Vector2(0.45f, startY - spacingY*7 - 0.05f), new Vector2(0.95f, startY - spacingY*7 + 0.03f),
            () => AdjustSFXVolume(-0.1f), () => AdjustSFXVolume(0.1f), out sfxValText);
        UpdateSFXVolumeText();

        CreateMenuButton(optionsPanel, "BACK", () => {
            OpenPanel(mainPanel.GetComponent<CanvasGroup>());
        }, new Vector2(0.1f, 0.1f));
    }

    private void CreateDropdown(GameObject parent, string title, Vector2 labelMin, Vector2 labelMax, Vector2 btnMin, Vector2 btnMax, string[] options, int currentIndex, System.Action<int> onSelect)
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
        tmpText.text = $"{options[currentIndex]}  ▼";
        tmpText.alignment = TextAlignmentOptions.Center;
        tmpText.color = Color.yellow;
        tmpText.fontSize = 22;
        RectTransform txtRect = txtObj.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = Vector2.zero;
        txtRect.offsetMax = Vector2.zero;

        Button btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(() =>
        {
            if (activeDropdownOverlay != null)
            {
                Destroy(activeDropdownOverlay);
                activeDropdownOverlay = null;
                return;
            }

            activeDropdownOverlay = new GameObject("DropdownOverlay");
            activeDropdownOverlay.transform.SetParent(parent.transform, false);
            activeDropdownOverlay.transform.SetAsLastSibling();

            RectTransform overlayRect = activeDropdownOverlay.AddComponent<RectTransform>();
            overlayRect.anchorMin = new Vector2(btnMin.x, btnMin.y - (options.Length * 0.065f));
            overlayRect.anchorMax = new Vector2(btnMax.x, btnMin.y - 0.005f);
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;

            Image overlayImg = activeDropdownOverlay.AddComponent<Image>();
            overlayImg.color = new Color(0.02f, 0.02f, 0.02f, 0.98f);

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
                
                Image itemImg = itemObj.AddComponent<Image>();
                itemImg.color = (index == currentIndex) ? new Color(0.2f, 0.05f, 0.05f, 1f) : new Color(0.05f, 0.05f, 0.05f, 1f);

                Button itemBtn = itemObj.AddComponent<Button>();
                
                GameObject itemTxtObj = new GameObject("Text");
                itemTxtObj.transform.SetParent(itemObj.transform, false);
                TextMeshProUGUI itemTmpText = itemTxtObj.AddComponent<TextMeshProUGUI>();
                if (gameFont != null) itemTmpText.font = gameFont;
                itemTmpText.text = options[index];
                itemTmpText.alignment = TextAlignmentOptions.Center;
                itemTmpText.fontSize = 20;
                itemTmpText.color = (index == currentIndex) ? Color.yellow : Color.white;
                RectTransform itemTxtRect = itemTxtObj.GetComponent<RectTransform>();
                itemTxtRect.anchorMin = Vector2.zero;
                itemTxtRect.anchorMax = Vector2.one;

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

    private void ApplyWindowMode(int modeIndex)
    {
        selectedWindowMode = modeIndex;
        PlayerPrefs.SetInt("GameWindowMode", selectedWindowMode);
        PlayerPrefs.Save();

        FullScreenMode mode = FullScreenMode.ExclusiveFullScreen;
        if (selectedWindowMode == 1) mode = FullScreenMode.FullScreenWindow;
        else if (selectedWindowMode == 2) mode = FullScreenMode.Windowed;

        Vector2Int res = commonResolutions[selectedResIndex];
        Screen.SetResolution(res.x, res.y, mode);
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
        float val = PlayerPrefs.GetFloat("GameBrightness", 1.0f);
        val = Mathf.Clamp(val + delta, 0.5f, 1.5f);
        PlayerPrefs.SetFloat("GameBrightness", val);
        PlayerPrefs.Save();

        if (GlobalSettingsManager.Instance != null)
        {
            GlobalSettingsManager.Instance.ApplyAllSettings();
        }
        UpdateBrightText();
    }

    private void UpdateBrightText()
    {
        if (brightValText != null)
        {
            float val = PlayerPrefs.GetFloat("GameBrightness", 1.0f);
            brightValText.text = Mathf.RoundToInt(val * 100f) + "%";
        }
    }

    private void AdjustFPS(int delta)
    {
        selectedFpsIndex = Mathf.Clamp(selectedFpsIndex + delta, 0, fpsOptions.Length - 1);
        PlayerPrefs.SetInt("GameFPSLimit", fpsOptions[selectedFpsIndex]);
        PlayerPrefs.Save();

        if (GlobalSettingsManager.Instance != null)
        {
            GlobalSettingsManager.Instance.ApplyAllSettings();
        }
        UpdateFPSText();
    }

    private void UpdateFPSText()
    {
        if (fpsValText != null)
        {
            fpsValText.text = fpsLabels[selectedFpsIndex];
        }
    }

    private void AdjustSensitivity(float delta)
    {
        float val = PlayerPrefs.GetFloat("MouseSensitivity", 1.0f);
        val = Mathf.Clamp(val + delta, 0.5f, 2.0f);
        PlayerPrefs.SetFloat("MouseSensitivity", val);
        PlayerPrefs.Save();

        if (GlobalSettingsManager.Instance != null)
        {
            GlobalSettingsManager.Instance.ApplyAllSettings();
        }
        UpdateSensText();
    }

    private void UpdateSensText()
    {
        if (sensValText != null)
        {
            float val = PlayerPrefs.GetFloat("MouseSensitivity", 1.0f);
            sensValText.text = val.ToString("F1") + "x";
        }
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
        float val = PlayerPrefs.GetFloat("GameMasterVolume", 1.0f);
        val = Mathf.Clamp(val + delta, 0f, 1.0f);
        PlayerPrefs.SetFloat("GameMasterVolume", val);
        PlayerPrefs.Save();

        if (GlobalSettingsManager.Instance != null)
        {
            GlobalSettingsManager.Instance.ApplyAllSettings();
        }
        UpdateVolumeText();
    }

    private void UpdateVolumeText()
    {
        if (volValText != null)
        {
            float val = PlayerPrefs.GetFloat("GameMasterVolume", 1.0f);
            volValText.text = Mathf.RoundToInt(val * 100f) + "%";
        }
    }

    private void AdjustMusicVolume(float delta)
    {
        float val = PlayerPrefs.GetFloat("GameMusicVolume", 0.5f);
        val = Mathf.Clamp(val + delta, 0f, 1.0f);
        PlayerPrefs.SetFloat("GameMusicVolume", val);
        PlayerPrefs.Save();

        UpdateAudioSettings();
        UpdateMusicVolumeText();
    }

    private void UpdateMusicVolumeText()
    {
        if (musicValText != null)
        {
            float val = PlayerPrefs.GetFloat("GameMusicVolume", 0.5f);
            musicValText.text = Mathf.RoundToInt(val * 100f) + "%";
        }
    }

    private void AdjustSFXVolume(float delta)
    {
        float val = PlayerPrefs.GetFloat("GameSFXVolume", 0.8f);
        val = Mathf.Clamp(val + delta, 0f, 1.0f);
        PlayerPrefs.SetFloat("GameSFXVolume", val);
        PlayerPrefs.Save();

        UpdateAudioSettings();
        UpdateSFXVolumeText();
    }

    private void UpdateSFXVolumeText()
    {
        if (sfxValText != null)
        {
            float val = PlayerPrefs.GetFloat("GameSFXVolume", 0.8f);
            sfxValText.text = Mathf.RoundToInt(val * 100f) + "%";
        }
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

        if (targetPanel.gameObject.name == "CharacterSelectPanel") UpdatePreview();
        isCreditsOpen = (targetPanel.gameObject.name == "CreditsPanel");
        if (isCreditsOpen && creditsContent != null) creditsContent.anchoredPosition = Vector2.zero;
    }
    private IEnumerator FadePanel(CanvasGroup panel, float targetAlpha, bool show) { if (show) { panel.gameObject.SetActive(true); panel.blocksRaycasts = true; panel.interactable = true; } else { panel.blocksRaycasts = false; panel.interactable = false; } float startAlpha = panel.alpha; float time = 0f; while (time < 0.25f) { time += Time.unscaledDeltaTime; panel.alpha = Mathf.Lerp(startAlpha, targetAlpha, time / 0.25f); yield return null; } panel.alpha = targetAlpha; if (!show) panel.gameObject.SetActive(false); }
}

public class AutoMenuButtonEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    private TextMeshProUGUI btnText;
    private Color normalColor = new Color(0.7f, 0.7f, 0.7f, 1f);
    private Color hoverColor = new Color(0.7f, 0.15f, 0.15f, 1f);
    private Vector3 originalPos;
    private bool isCentered;
    private Coroutine colorRoutine, moveRoutine;

    public void Setup(TextMeshProUGUI textComponent, bool center) { btnText = textComponent; btnText.color = normalColor; originalPos = btnText.transform.localPosition; isCentered = center; }
    public void OnPointerEnter(PointerEventData eventData) { AnimateColor(hoverColor); if (!isCentered) AnimateMove(originalPos + new Vector3(15f, 0, 0)); if (AutoMainMenuManager.Instance != null) AutoMainMenuManager.Instance.PlayHoverSFX(); }
    public void OnPointerExit(PointerEventData eventData) { AnimateColor(normalColor); if (!isCentered) AnimateMove(originalPos); }
    public void OnPointerDown(PointerEventData eventData) { if (btnText != null) btnText.transform.localScale = Vector3.one * 0.9f; if (AutoMainMenuManager.Instance != null) AutoMainMenuManager.Instance.PlayClickSFX(); }
    public void OnPointerUp(PointerEventData eventData) { if (btnText != null) btnText.transform.localScale = Vector3.one; }
    private void AnimateColor(Color target) { if (btnText == null) return; if (colorRoutine != null) StopCoroutine(colorRoutine); colorRoutine = StartCoroutine(DoColor(target, 0.15f)); }
    private void AnimateMove(Vector3 target) { if (btnText == null) return; if (moveRoutine != null) StopCoroutine(moveRoutine); moveRoutine = StartCoroutine(DoMove(target, 0.15f)); }
    private IEnumerator DoColor(Color targetColor, float duration) { Color startColor = btnText.color; float t = 0; while (t < duration) { t += Time.unscaledDeltaTime; btnText.color = Color.Lerp(startColor, targetColor, t / duration); yield return null; } btnText.color = targetColor; }
    private IEnumerator DoMove(Vector3 targetPos, float duration) { Vector3 startPos = btnText.transform.localPosition; float t = 0; while (t < duration) { t += Time.unscaledDeltaTime; float percent = t / duration; percent = percent * (2f - percent); btnText.transform.localPosition = Vector3.Lerp(startPos, targetPos, percent); yield return null; } btnText.transform.localPosition = targetPos; }
}