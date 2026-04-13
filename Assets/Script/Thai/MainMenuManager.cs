using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;

public class AutoMainMenuManager : MonoBehaviour, INetworkRunnerCallbacks
{
    public static AutoMainMenuManager Instance { get; private set; }

    [Header("Cài đặt chung")]
    public TMP_FontAsset gameFont;
    public Sprite backgroundImage;

    // 🔥 THÊM: Cài đặt âm thanh cho Menu
    [Header("Âm thanh Menu")]
    public AudioClip menuBGM;
    [Range(0f, 1f)] public float bgmVolume = 0.5f;
    private AudioSource bgmSource;

    // 👇 THÊM: Biến chứa SFX Click và Hover 👇
    [Header("Âm thanh Nút bấm (SFX)")]
    public AudioClip hoverSound;
    public AudioClip clickSound;
    [Range(0f, 1f)] public float sfxVolume = 0.8f;
    private AudioSource sfxSource;

    [Header("Cài đặt Fusion Network")]
    public NetworkRunner runnerPrefab;
    public int mainSceneIndex = 1;

    [Header("Hình ảnh Nhân vật (Kéo thả 2 GameObject UI Image vào đây)")]
    public GameObject[] previewImages;

    private float creditsScrollPos = 0f;
    private bool isCreditsOpen = false;
    [Header("Tốc độ cuộn Credits")]
    public float creditsScrollSpeed = 30f; // Sếp tăng số này nếu muốn chữ chạy nhanh hơn

    private Canvas mainCanvas;
    private GameObject mainPanel, newGamePanel, multiplayerPanel, characterSelectPanel, optionsPanel, creditsPanel;
    private CanvasGroup currentActivePanel;

    private string pendingRoomName = "";
    private bool pendingIsHost = false;

    // Các biến cho Bảng HOST mới
    private int hostDifficulty = 1; // 0: Dễ, 1: TB, 2: Khó
    private int hostMaxPlayers = 4;
    private bool hostHasPassword = false;
    private string hostPassword = "";

    // Giao diện để đổi màu khi chọn
    private TextMeshProUGUI[] diffTexts = new TextMeshProUGUI[3];
    private TextMeshProUGUI toggleText;
    private GameObject passwordInputObj;

    // 🔥 THÊM BIẾN CHO HỆ THỐNG SCROLL DANH SÁCH PHÒNG
    private RectTransform serverListContent;
    private GameObject passPromptPanel;
    private TMP_InputField joinPassInput;

    private int previewID = 0;
    private string[] characterNames = { "Kẻ Sống Sót: Vô Danh", "Kẻ Sống Sót: Bóng Ma" };
    private string[] characterStats = {
        "<color=#ff5555>KỸ NĂNG: CƠN ĐIÊN LÂM CHUNG</color>\nBản năng sinh tồn tột độ. Khi hạ sát 5 thực thể đột biến, lượng adrenaline kích phát vượt giới hạn cơ thể. Xóa bỏ hoàn toàn độ giật và không tiêu hao đạn dược trong 10 giây.\n<color=#aaaaaa>[Thời gian hồi phục: 50s]</color>",
        "<color=#55ffff>KỸ NĂNG: BÓNG ĐÊM TĨNH LẶNG</color>\nSinh ra để lẩn khuất. Khi hạ thấp trọng tâm, nhịp tim và hơi thở đồng bộ với môi trường xung quanh. Đánh lừa hoàn toàn giác quan của lũ thây ma trong 5 giây.\n<color=#aaaaaa>[Thời gian hồi phục: 30s]</color>"
    };

    private TextMeshProUGUI charNameText;
    private TextMeshProUGUI charStatsText;
    private TMP_InputField playerNameInput;
    private RectTransform previewContainer;

    // Chứa các Gameplay UI bị ép tắt
    private List<GameObject> temporarilyDisabledObjects = new List<GameObject>();

    private NetworkRunner lobbyRunner;

    // 🔥 THÊM BIẾN CHO HỆ THỐNG SCROLL DANH SÁCH PHÒNG
    private RectTransform creditsContent; // THÊM DÒNG NÀY CHO BẢNG CREDITS

    // 🔥 BIẾN KHÓA: Chống Spam click gây lỗi mạng
    private bool isConnecting = false;

    // Cờ báo hiệu Menu đã bị hủy khi chuyển Scene
    private bool isMenuDestroyed = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        GenerateEntireMenu();

        // 🔥 BẬT NHẠC NỀN KHI MENU CHẠY
        if (menuBGM != null)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.clip = menuBGM;
            bgmSource.loop = true;
            bgmSource.volume = bgmVolume;
            bgmSource.Play();
        }

        // 👇 TẠO BỘ PHÁT SFX CHO NÚT BẤM 👇
        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
    }

    // 👇 HÀM ĐỂ CÁC NÚT GỌI KHI BỊ CHUỘT TƯƠNG TÁC 👇
    public void PlayHoverSFX()
    {
        if (hoverSound != null && sfxSource != null)
        {
            sfxSource.PlayOneShot(hoverSound, sfxVolume);
        }
    }

    public void PlayClickSFX()
    {
        if (clickSound != null && sfxSource != null)
        {
            sfxSource.PlayOneShot(clickSound, sfxVolume);
        }
    }

    // 🔥 TRUY SÁT VÀ TẮT GỌN CÁC UI GAMEPLAY NẰM TRONG DONTDESTROYONLOAD
    private IEnumerator Start()
    {
        // Chờ 1 nhịp để đám Health/Chat của sếp kịp chạy lệnh sinh ra
        yield return new WaitForEndOfFrame();

        string[] targetNames = {
            "AutoChatCanvas",
            "--- AUTO CHAT MANAGER ---",
            "--- AUTO HEALTH CANVAS ---",
            "--- AUTO HEALTH MANAGER ---",
            "HealthPanel"
        };

        foreach (string target in targetNames)
        {
            GameObject foundObj = GameObject.Find(target);
            if (foundObj != null && foundObj.activeSelf)
            {
                foundObj.SetActive(false); // Ép tắt ngay lập tức
                temporarilyDisabledObjects.Add(foundObj);
            }
        }
    }

    private void Update()
    {
        // Xử lý EventSystem cũ của sếp
        if (EventSystem.current != null)
        {
            GameObject selectedObj = EventSystem.current.currentSelectedGameObject;
            if (selectedObj != null && selectedObj.GetComponent<TMP_InputField>() == null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        // 🔥 LOGIC MÚA CHỮ CREDITS
        if (isCreditsOpen && creditsContent != null) // Đổi từ serverListContent sang creditsContent
        {
            // Content sẽ trôi lên trên
            creditsContent.anchoredPosition += Vector2.up * creditsScrollSpeed * Time.unscaledDeltaTime;

            // Nếu trôi hết thì reset lại từ đầu (Vòng lặp vô tận)
            if (creditsContent.anchoredPosition.y > creditsContent.sizeDelta.y + 500f)
            {
                creditsContent.anchoredPosition = new Vector2(0, 0);
            }
        }
    }

    private void GenerateEntireMenu()
    {
        if (FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("EventSystem");
            EventSystem es = eventSystem.AddComponent<EventSystem>();
            es.sendNavigationEvents = false;
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        GameObject canvasGO = new GameObject("AutoMenuCanvas");
        mainCanvas = canvasGO.AddComponent<Canvas>();
        mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // 🔥 QUYỀN LỰC TỐI CAO: Ép Menu này đè bẹp mọi Canvas khác
        mainCanvas.sortingOrder = 999;

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
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        GenerateMainPanel(canvasGO);
        GenerateNewGamePanel(canvasGO);
        GenerateMultiplayerPanel_NEW(canvasGO); // Bảng Multi xịn
        GenerateCharacterSelectPanel(canvasGO);
        GenerateOptionsPanel(canvasGO);
        GenerateCreditsPanel(canvasGO);

        OpenPanel(mainPanel.GetComponent<CanvasGroup>());
    }

    #region TẠO CÁC PANEL CƠ BẢN
    private void GenerateMainPanel(GameObject canvasGO)
    {
        mainPanel = CreateBasePanel("MainPanel", canvasGO);
        CanvasGroup cg = mainPanel.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;

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
        titleRect.anchorMin = new Vector2(0.1f, 0.7f);
        titleRect.anchorMax = new Vector2(0.5f, 0.95f);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;

        GameObject btnContainer = new GameObject("ButtonContainer");
        btnContainer.transform.SetParent(mainPanel.transform, false);
        RectTransform btnRect = btnContainer.AddComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.1f, 0.1f);
        btnRect.anchorMax = new Vector2(0.3f, 0.6f);
        btnRect.offsetMin = Vector2.zero;
        btnRect.offsetMax = Vector2.zero;

        VerticalLayoutGroup vlg = btnContainer.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 15;
        vlg.childAlignment = TextAnchor.MiddleLeft;
        vlg.childControlHeight = false;
        vlg.childControlWidth = true;

        CreateMenuButton(btnContainer, "SOLO", () => OpenPanel(newGamePanel.GetComponent<CanvasGroup>()));
        CreateMenuButton(btnContainer, "MULTIPLAYER", () => OpenPanel(multiplayerPanel.GetComponent<CanvasGroup>()));
        CreateMenuButton(btnContainer, "TUTORIAL", () => Debug.Log("Chuyển Scene Tutorial..."));
        CreateMenuButton(btnContainer, "OPTIONS", () => OpenPanel(optionsPanel.GetComponent<CanvasGroup>()));
        CreateMenuButton(btnContainer, "CREDITS", () => OpenPanel(creditsPanel.GetComponent<CanvasGroup>()));
        CreateMenuButton(btnContainer, "QUIT", () => Application.Quit());
    }

    private void GenerateNewGamePanel(GameObject canvasGO)
    {
        newGamePanel = CreateBasePanel("NewGamePanel", canvasGO);
        CanvasGroup cg = newGamePanel.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;

        CreateTitleText(newGamePanel, "SELECT DIFFICULTY");

        GameObject btnContainer = new GameObject("DiffContainer");
        btnContainer.transform.SetParent(newGamePanel.transform, false);
        RectTransform btnRect = btnContainer.AddComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.4f, 0.3f);
        btnRect.anchorMax = new Vector2(0.6f, 0.7f);
        btnRect.offsetMin = Vector2.zero;
        btnRect.offsetMax = Vector2.zero;

        VerticalLayoutGroup vlg = btnContainer.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 20;

        CreateMenuButton(btnContainer, "BUILDER (EASY)", () => Debug.Log("Start Easy"));
        CreateMenuButton(btnContainer, "SURVIVOR (NORMAL)", () => Debug.Log("Start Normal"));
        CreateMenuButton(btnContainer, "APOCALYPSE (HARD)", () => Debug.Log("Start Hard"));

        CreateMenuButton(newGamePanel, "BACK", () => OpenPanel(mainPanel.GetComponent<CanvasGroup>()), new Vector2(0.1f, 0.1f));
    }
    #endregion

    // ==========================================
    // 🔥 BẢNG MULTIPLAYER 
    // ==========================================
    private void GenerateMultiplayerPanel_NEW(GameObject canvasGO)
    {
        multiplayerPanel = CreateBasePanel("MultiplayerPanel", canvasGO);
        CanvasGroup cg = multiplayerPanel.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;

        // --- KHU VỰC TAB HOST / JOIN ---
        GameObject hostArea = new GameObject("Host_Container");
        hostArea.transform.SetParent(multiplayerPanel.transform, false);
        RectTransform hostRect = hostArea.AddComponent<RectTransform>();
        hostRect.anchorMin = new Vector2(0.15f, 0.15f);
        hostRect.anchorMax = new Vector2(0.85f, 0.8f);
        hostRect.offsetMin = Vector2.zero;
        hostRect.offsetMax = Vector2.zero;
        hostArea.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.08f, 0.95f);

        GameObject joinArea = new GameObject("Join_Container");
        joinArea.transform.SetParent(multiplayerPanel.transform, false);
        RectTransform joinRect = joinArea.AddComponent<RectTransform>();
        joinRect.anchorMin = new Vector2(0.15f, 0.15f);
        joinRect.anchorMax = new Vector2(0.85f, 0.8f);
        joinRect.offsetMin = Vector2.zero;
        joinRect.offsetMax = Vector2.zero;
        joinArea.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.08f, 0.95f);
        joinArea.SetActive(false);

        // 2 Nút Tab trên cùng (Tọa độ của sếp)
        CreateMenuButton(multiplayerPanel, "TẠO CĂN CỨ", () =>
        {
            hostArea.SetActive(true);
            joinArea.SetActive(false);
        }, new Vector2(0.3f, 0.85f), true, new Vector2(350, 50));

        CreateMenuButton(multiplayerPanel, "TÌM CĂN CỨ", () =>
        {
            hostArea.SetActive(false);
            joinArea.SetActive(true);
            ConnectToLobby();
        }, new Vector2(0.7f, 0.85f), true, new Vector2(350, 50));

        // ------------------------------------------
        // 🔥 XÂY DỰNG KHU VỰC HOST (CREATE) - TỌA ĐỘ CỦA SẾP
        // ------------------------------------------
        CreateTitleText(hostArea, "THIẾT LẬP CĂN CỨ", 0.9f);

        CreateLabel(hostArea, "TÊN CĂN CỨ:", new Vector2(0.1f, 0.7f), new Vector2(0.3f, 0.75f));
        GameObject roomInputObj = CreateInputField(hostArea, "HostRoomName", "VD: Trại Tị Nạn...", new Vector2(0.35f, 0.68f), new Vector2(0.9f, 0.77f));
        TMP_InputField roomInput = roomInputObj.GetComponent<TMP_InputField>();

        CreateLabel(hostArea, "ĐỘ KHÓ:", new Vector2(0.1f, 0.55f), new Vector2(0.3f, 0.6f));
        // Lệnh này xài đúng 1 tọa độ Anchor giống như hàm CreateTextBtn của sếp
        diffTexts[0] = CreateTextBtn(hostArea, "DỄ", new Vector2(0.4f, 0.575f), () => SetDifficulty(0));
        diffTexts[1] = CreateTextBtn(hostArea, "BÌNH THƯỜNG", new Vector2(0.6f, 0.575f), () => SetDifficulty(1));
        diffTexts[2] = CreateTextBtn(hostArea, "ĐỊA NGỤC", new Vector2(0.8f, 0.575f), () => SetDifficulty(2));
        SetDifficulty(1);

        CreateLabel(hostArea, "SỐ NGƯỜI TỐI ĐA:", new Vector2(0.1f, 0.4f), new Vector2(0.3f, 0.45f));
        GameObject playerInputObj = CreateInputField(hostArea, "MaxPlayers", "4", new Vector2(0.35f, 0.38f), new Vector2(0.45f, 0.47f));
        TMP_InputField playerInput = playerInputObj.GetComponent<TMP_InputField>();
        playerInput.contentType = TMP_InputField.ContentType.IntegerNumber;

        CreateLabel(hostArea, "MẬT KHẨU:", new Vector2(0.1f, 0.25f), new Vector2(0.3f, 0.3f));
        toggleText = CreateTextBtn(hostArea, "[ KHÔNG ]", new Vector2(0.4f, 0.275f), TogglePassword);

        passwordInputObj = CreateInputField(hostArea, "HostPassword", "Nhập Pass...", new Vector2(0.55f, 0.23f), new Vector2(0.9f, 0.32f));
        passwordInputObj.GetComponent<TMP_InputField>().contentType = TMP_InputField.ContentType.Password;
        passwordInputObj.SetActive(false);

        CreateMenuButton(hostArea, "TIẾP TỤC (CHỌN NHÂN VẬT)", () =>
        {
            pendingRoomName = string.IsNullOrEmpty(roomInput.text) ? "Camp_" + Random.Range(100, 999) : roomInput.text;
            hostMaxPlayers = string.IsNullOrEmpty(playerInput.text) ? 4 : int.Parse(playerInput.text);

            if (hostHasPassword)
            {
                hostPassword = passwordInputObj.GetComponent<TMP_InputField>().text;
            }

            pendingIsHost = true;
            OpenPanel(characterSelectPanel.GetComponent<CanvasGroup>());
        }, new Vector2(0.5f, 0.08f), true, new Vector2(500, 60), 25f);

        // ------------------------------------------
        // 🔥 XÂY DỰNG KHU VỰC JOIN TÍCH HỢP SCROLL
        // ------------------------------------------
        CreateTitleText(joinArea, "DANH SÁCH CĂN CỨ", 0.9f);

        GameObject scrollObj = new GameObject("Scroll View");
        scrollObj.transform.SetParent(joinArea.transform, false);
        RectTransform scrollRectT = scrollObj.AddComponent<RectTransform>();
        // Lấy tọa độ Y hệt như cái listBG cũ của sếp
        scrollRectT.anchorMin = new Vector2(0.1f, 0.2f);
        scrollRectT.anchorMax = new Vector2(0.9f, 0.75f);
        scrollRectT.offsetMin = Vector2.zero;
        scrollRectT.offsetMax = Vector2.zero;

        ScrollRect scrollRect = scrollObj.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.scrollSensitivity = 20f;

        GameObject viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollObj.transform, false);
        RectTransform vpRect = viewport.AddComponent<RectTransform>();
        vpRect.anchorMin = Vector2.zero;
        vpRect.anchorMax = Vector2.one;
        vpRect.offsetMin = Vector2.zero;
        vpRect.offsetMax = Vector2.zero;
        viewport.AddComponent<Image>().color = new Color(0, 0, 0, 0.5f);
        viewport.AddComponent<RectMask2D>();

        GameObject content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        serverListContent = content.AddComponent<RectTransform>();
        serverListContent.anchorMin = new Vector2(0, 1);
        serverListContent.anchorMax = new Vector2(1, 1);
        serverListContent.pivot = new Vector2(0.5f, 1);
        serverListContent.offsetMin = Vector2.zero;
        serverListContent.offsetMax = Vector2.zero;
        serverListContent.sizeDelta = new Vector2(0, 0);

        VerticalLayoutGroup vlgList = content.AddComponent<VerticalLayoutGroup>();
        vlgList.childAlignment = TextAnchor.UpperCenter;
        vlgList.childControlHeight = false;
        vlgList.childControlWidth = true;
        vlgList.childForceExpandHeight = false;
        vlgList.spacing = 10;
        vlgList.padding = new RectOffset(10, 10, 10, 10);

        ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = vpRect;
        scrollRect.content = serverListContent;

        // Bảng hỏi Pass
        passPromptPanel = new GameObject("PasswordPrompt");
        passPromptPanel.transform.SetParent(joinArea.transform, false);
        RectTransform promptRect = passPromptPanel.AddComponent<RectTransform>();
        promptRect.anchorMin = new Vector2(0.2f, 0.3f);
        promptRect.anchorMax = new Vector2(0.8f, 0.7f);
        promptRect.offsetMin = Vector2.zero;
        promptRect.offsetMax = Vector2.zero;
        passPromptPanel.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 1f);
        passPromptPanel.SetActive(false);

        CreateLabel(passPromptPanel, "CĂN CỨ BỊ KHÓA, NHẬP MẬT KHẨU:", new Vector2(0, 0.7f), new Vector2(1, 0.9f));
        GameObject joinPassInputObj = CreateInputField(passPromptPanel, "JoinPass", "...", new Vector2(0.2f, 0.4f), new Vector2(0.8f, 0.6f));
        joinPassInput = joinPassInputObj.GetComponent<TMP_InputField>();
        joinPassInput.contentType = TMP_InputField.ContentType.Password;

        CreateMenuButton(passPromptPanel, "ĐÓNG", () =>
        {
            passPromptPanel.SetActive(false);
        }, new Vector2(0.2f, 0.15f), true, new Vector2(150, 40), 20f);

        CreateMenuButton(passPromptPanel, "XÁC NHẬN", () =>
        {
            passPromptPanel.SetActive(false);
            pendingIsHost = false;
            OpenPanel(characterSelectPanel.GetComponent<CanvasGroup>());
        }, new Vector2(0.8f, 0.15f), true, new Vector2(150, 40), 20f);

        // Nút Load Danh Sách (Để chung form với CreateMenuButton)
        CreateMenuButton(joinArea, "LÀM MỚI DANH SÁCH", () =>
        {
            Debug.Log("Gọi lệnh Refresh Fusion...");
            ConnectToLobby();
        }, new Vector2(0.5f, 0.08f), true, new Vector2(300, 50), 20f);

        CreateMenuButton(multiplayerPanel, "BACK", () => OpenPanel(mainPanel.GetComponent<CanvasGroup>()), new Vector2(0.1f, 0.05f));
    }

    // ==========================================
    // 🔥 HÀM ĐỔ DỮ LIỆU ĐỘNG VÀO SCROLL VIEW
    // ==========================================
    public void UpdateServerListUI(List<SessionInfo> sessionList)
    {
        if (serverListContent == null) return;

        foreach (Transform child in serverListContent)
        {
            Destroy(child.gameObject);
        }

        foreach (SessionInfo session in sessionList)
        {
            string roomName = session.Name;
            int currentPlayers = session.PlayerCount;
            int maxPlayers = session.MaxPlayers;
            bool isLocked = false;
            string diff = "Bình thường";

            CreateDynamicServerItem(roomName, diff, currentPlayers, maxPlayers, isLocked, () =>
            {
                pendingRoomName = roomName;
                if (isLocked)
                {
                    passPromptPanel.SetActive(true);
                }
                else
                {
                    pendingIsHost = false;
                    OpenPanel(characterSelectPanel.GetComponent<CanvasGroup>());
                }
            });
        }
    }

    private void CreateDynamicServerItem(string roomName, string diff, int currentPlayers, int maxPlayers, bool isLocked, UnityEngine.Events.UnityAction action)
    {
        GameObject btnObj = new GameObject("Room_" + roomName);
        btnObj.transform.SetParent(serverListContent, false);

        LayoutElement le = btnObj.AddComponent<LayoutElement>();
        le.minHeight = 50f;

        Image btnImg = btnObj.AddComponent<Image>();
        btnImg.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

        Button btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(action);

        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(btnObj.transform, false);
        TextMeshProUGUI tmpText = txtObj.AddComponent<TextMeshProUGUI>();

        if (gameFont != null) tmpText.font = gameFont;

        string lockText = isLocked ? "<color=red>[LOCKED]</color>" : "<color=green>[OPEN]</color>";
        tmpText.text = $"{lockText} Tên: {roomName} | Độ Khó: {diff} | Người Chơi: {currentPlayers}/{maxPlayers}";
        tmpText.alignment = TextAlignmentOptions.Center;
        tmpText.color = Color.white;

        tmpText.enableAutoSizing = true;
        tmpText.fontSizeMin = 14;
        tmpText.fontSizeMax = 22;

        RectTransform txtRect = txtObj.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = Vector2.zero;
        txtRect.offsetMax = Vector2.zero;

        // 🔥 THÊM HIỆU ỨNG HOVER CHO DANH SÁCH PHÒNG
        AutoMenuButtonEffect effect = btnObj.AddComponent<AutoMenuButtonEffect>();
        effect.Setup(tmpText, true);
    }

    private void SetDifficulty(int id)
    {
        hostDifficulty = id;
        for (int i = 0; i < diffTexts.Length; i++)
        {
            if (i == id)
            {
                diffTexts[i].color = Color.yellow;
                diffTexts[i].fontStyle = FontStyles.Bold;
            }
            else
            {
                diffTexts[i].color = Color.gray;
                diffTexts[i].fontStyle = FontStyles.Normal;
            }
        }
    }

    private void TogglePassword()
    {
        hostHasPassword = !hostHasPassword;
        if (hostHasPassword)
        {
            toggleText.text = "[ CÓ ]";
            toggleText.color = Color.red;
            passwordInputObj.SetActive(true);
        }
        else
        {
            toggleText.text = "[ KHÔNG ]";
            toggleText.color = Color.gray;
            passwordInputObj.SetActive(false);
        }
    }

    // ==========================================
    // 🔥 BẢNG CHỌN NHÂN VẬT
    // ==========================================
    private void GenerateCharacterSelectPanel(GameObject canvasGO)
    {
        characterSelectPanel = CreateBasePanel("CharacterSelectPanel", canvasGO);
        CanvasGroup cg = characterSelectPanel.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;

        CreateTitleText(characterSelectPanel, "CUSTOMIZE SURVIVOR");

        GameObject customArea = new GameObject("CustomArea");
        customArea.transform.SetParent(characterSelectPanel.transform, false);

        RectTransform customRect = customArea.AddComponent<RectTransform>();
        customRect.anchorMin = new Vector2(0.2f, 0.1f);
        customRect.anchorMax = new Vector2(0.8f, 0.85f);
        customRect.offsetMin = Vector2.zero;
        customRect.offsetMax = Vector2.zero;

        customArea.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.05f, 0.9f);

        CreateMenuButton(customArea, "<", () =>
        {
            ChangeCharacter(-1);
        }, new Vector2(0.1f, 0.92f), true, new Vector2(60, 60));

        CreateMenuButton(customArea, ">", () =>
        {
            ChangeCharacter(1);
        }, new Vector2(0.9f, 0.92f), true, new Vector2(60, 60));

        GameObject nameObj = new GameObject("CharNameText");
        nameObj.transform.SetParent(customArea.transform, false);
        charNameText = nameObj.AddComponent<TextMeshProUGUI>();

        if (gameFont != null)
        {
            charNameText.font = gameFont;
        }

        charNameText.text = characterNames[0];
        charNameText.fontSize = 30;
        charNameText.fontStyle = FontStyles.Bold;
        charNameText.color = Color.yellow;
        charNameText.alignment = TextAlignmentOptions.Center;
        charNameText.enableAutoSizing = true;
        charNameText.fontSizeMin = 20;
        charNameText.fontSizeMax = 40;

        RectTransform nameRect = nameObj.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0.2f, 0.85f);
        nameRect.anchorMax = new Vector2(0.8f, 1f);
        nameRect.offsetMin = Vector2.zero;
        nameRect.offsetMax = Vector2.zero;

        GameObject previewBox = new GameObject("PreviewContainer");
        previewBox.transform.SetParent(customArea.transform, false);
        previewContainer = previewBox.AddComponent<RectTransform>();
        previewContainer.anchorMin = new Vector2(0.3f, 0.55f);
        previewContainer.anchorMax = new Vector2(0.7f, 0.85f);
        previewContainer.offsetMin = Vector2.zero;
        previewContainer.offsetMax = Vector2.zero;

        GameObject statsObj = new GameObject("CharStatsText");
        statsObj.transform.SetParent(customArea.transform, false);
        charStatsText = statsObj.AddComponent<TextMeshProUGUI>();

        if (gameFont != null)
        {
            charStatsText.font = gameFont;
        }

        charStatsText.text = characterStats[0];
        charStatsText.fontSize = 25;
        charStatsText.alignment = TextAlignmentOptions.Top;
        charStatsText.richText = true;
        charStatsText.enableAutoSizing = true;
        charStatsText.fontSizeMin = 14;
        charStatsText.fontSizeMax = 30;

        RectTransform statsRect = statsObj.GetComponent<RectTransform>();
        statsRect.anchorMin = new Vector2(0.1f, 0.35f);
        statsRect.anchorMax = new Vector2(0.9f, 0.52f);
        statsRect.offsetMin = Vector2.zero;
        statsRect.offsetMax = Vector2.zero;

        GameObject labelObj = new GameObject("InputLabel");
        labelObj.transform.SetParent(customArea.transform, false);
        TextMeshProUGUI labelTxt = labelObj.AddComponent<TextMeshProUGUI>();

        if (gameFont != null)
        {
            labelTxt.font = gameFont;
        }

        labelTxt.text = "ĐỊNH DANH KẺ SỐNG SÓT";
        labelTxt.color = new Color(0.8f, 0.8f, 0.8f, 1f);
        labelTxt.alignment = TextAlignmentOptions.Center;
        labelTxt.enableAutoSizing = true;
        labelTxt.fontSizeMin = 14;
        labelTxt.fontSizeMax = 25;

        RectTransform labelRect = labelObj.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.2f, 0.26f);
        labelRect.anchorMax = new Vector2(0.8f, 0.32f);
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        GameObject inputObj = CreateInputField(customArea, "PlayerNameInput", "Nhập tên...", new Vector2(0.3f, 0.15f), new Vector2(0.7f, 0.25f));
        playerNameInput = inputObj.GetComponent<TMP_InputField>();
        playerNameInput.text = PlayerPrefs.GetString("MyPlayerName", "Survivor_" + Random.Range(100, 999));

        CreateMenuButton(customArea, "TIẾN VÀO VÙNG ĐẤT CHẾT", () =>
        {
            if (isConnecting) return;
            isConnecting = true;

            PlayerPrefs.SetString("MyPlayerName", playerNameInput.text);
            PlayerPrefs.SetInt("SelectedCharacterID", previewID);
            PlayerPrefs.Save();

            if (pendingIsHost)
            {
                StartHostGame(pendingRoomName);
            }
            else
            {
                StartClientGame(pendingRoomName);
            }

        }, new Vector2(0.5f, 0.1f), true, new Vector2(450, 70), 25f);

        CreateMenuButton(characterSelectPanel, "BACK", () =>
        {
            isConnecting = false;
            OpenPanel(multiplayerPanel.GetComponent<CanvasGroup>());
        }, new Vector2(0.1f, 0.1f), false, new Vector2(300, 50));
    }

    private void ChangeCharacter(int direction)
    {
        previewID = (previewID + direction + characterNames.Length) % characterNames.Length;
        charNameText.text = characterNames[previewID];
        charStatsText.text = characterStats[previewID];
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        if (previewImages == null || previewContainer == null)
        {
            return;
        }

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

                    Image img = previewImages[i].GetComponent<Image>();
                    if (img != null)
                    {
                        img.preserveAspect = true;
                    }
                }
            }
        }
    }

    #region OPTIONS & CREDITS
    private void GenerateOptionsPanel(GameObject canvasGO)
    {
        optionsPanel = CreateBasePanel("OptionsPanel", canvasGO);
        CanvasGroup cg = optionsPanel.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;

        CreateTitleText(optionsPanel, "OPTIONS");
        CreateMenuButton(optionsPanel, "BACK", () =>
        {
            OpenPanel(mainPanel.GetComponent<CanvasGroup>());
        }, new Vector2(0.1f, 0.1f));
    }

    private void GenerateCreditsPanel(GameObject canvasGO)
    {
        creditsPanel = CreateBasePanel("CreditsPanel", canvasGO);
        CanvasGroup cg = creditsPanel.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;

        CreateTitleText(creditsPanel, "SURVIVAL TEAM", 0.9f);

        // --- KHU VỰC CUỘN CHỮ (SCROLL VIEW TÀNG HÌNH) ---
        GameObject scrollObj = new GameObject("Credits_Scroll");
        scrollObj.transform.SetParent(creditsPanel.transform, false);
        RectTransform scrollRectT = scrollObj.AddComponent<RectTransform>();
        scrollRectT.anchorMin = new Vector2(0.15f, 0.2f);
        scrollRectT.anchorMax = new Vector2(0.85f, 0.8f);
        scrollRectT.offsetMin = Vector2.zero;
        scrollRectT.offsetMax = Vector2.zero;

        ScrollRect sr = scrollObj.AddComponent<ScrollRect>();
        sr.horizontal = false;
        sr.vertical = true;
        sr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

        // Viewport
        GameObject vp = new GameObject("Viewport");
        vp.transform.SetParent(scrollObj.transform, false);
        RectTransform vpRT = vp.AddComponent<RectTransform>();
        vpRT.anchorMin = Vector2.zero; vpRT.anchorMax = Vector2.one;
        vpRT.offsetMin = Vector2.zero; vpRT.offsetMax = Vector2.zero;
        vp.AddComponent<RectMask2D>();

        // Content (Nơi chứa danh sách múa)
        GameObject content = new GameObject("Content");
        content.transform.SetParent(vp.transform, false);
        RectTransform contentRT = content.AddComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0, 1);
        contentRT.anchorMax = new Vector2(1, 1);
        contentRT.pivot = new Vector2(0.5f, 1);
        contentRT.offsetMin = Vector2.zero;
        contentRT.offsetMax = Vector2.zero;

        VerticalLayoutGroup vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.spacing = 40;
        vlg.padding = new RectOffset(0, 0, 400, 400); // Chừa trống để chữ trôi từ dưới lên

        ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        sr.content = contentRT;
        creditsContent = contentRT; // Mượn biến này để xử lý cuộn trong Update

        // --- ĐỔ DANH SÁCH NHÂN SỰ ---
        CreateCreditLine(content, "LEAD PROGRAMMER", "TRẦN NGỌC ĐĂNG KHOA", Color.cyan);
        CreateCreditLine(content, "SYSTEM & PLAYER UI", "NGUYỄN TRÍ TÍN", Color.yellow);
        CreateCreditLine(content, "WORLD ARCHITECT (MAP)", "YÊN NHI", Color.white);
        CreateCreditLine(content, "LEAD AI & ZOMBIE BOSS", "HOÀNG THÁI", Color.red);
        CreateCreditLine(content, "VEHICLE MECHANICS", "VĂN HẬU", Color.green);
        CreateCreditLine(content, "TECHNICAL ARTIST (LOS FOW)", "ĐĂNG KHOA", Color.white);

        CreateCreditLine(content, "POWERED BY", "UNITY 6.0 / PHOTON FUSION", new Color(0.7f, 0.7f, 0.7f));
        CreateCreditLine(content, "AUDIO DESIGN", "BGM: PROJECT ZOMBOID\nSFX: KENNEY / FREESOUND", new Color(0.7f, 0.7f, 0.7f));

        CreateCreditLine(content, "SPECIAL THANKS", "TO ALL SURVIVORS WHO TESTED THIS GAME", Color.white);

        CreateMenuButton(creditsPanel, "BACK", () =>
        {
            isCreditsOpen = false;
            OpenPanel(mainPanel.GetComponent<CanvasGroup>());
        }, new Vector2(0.1f, 0.1f));
    }

    private void CreateCreditLine(GameObject parent, string role, string name, Color nameColor)
    {
        GameObject lineObj = new GameObject("CreditLine");
        lineObj.transform.SetParent(parent.transform, false);
        TextMeshProUGUI txt = lineObj.AddComponent<TextMeshProUGUI>();
        if (gameFont != null) txt.font = gameFont;
        txt.text = $"<size=20><color=#aaaaaa>{role}</color></size>\n<size=32><color=#{ColorUtility.ToHtmlStringRGB(nameColor)}>{name}</color></size>";
        txt.alignment = TextAlignmentOptions.Center;
    }
    #endregion

    #region HÀM HỖ TRỢ CƠ BẢN
    private GameObject CreateBasePanel(string name, GameObject parent)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent.transform, false);

        RectTransform rect = panel.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        return panel;
    }

    private void CreateTitleText(GameObject parent, string text, float height = 0.9f)
    {
        GameObject txtObj = new GameObject("Title");
        txtObj.transform.SetParent(parent.transform, false);
        TextMeshProUGUI txt = txtObj.AddComponent<TextMeshProUGUI>();

        if (gameFont != null)
        {
            txt.font = gameFont;
        }

        txt.text = text;
        txt.fontSize = 40;
        txt.fontStyle = FontStyles.Bold;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = new Color(0.8f, 0.8f, 0.8f, 1f);

        RectTransform rect = txtObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, height);
        rect.anchorMax = new Vector2(1, height);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void CreateLabel(GameObject parent, string text, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(parent.transform, false);
        TextMeshProUGUI labelTxt = labelObj.AddComponent<TextMeshProUGUI>();

        if (gameFont != null)
        {
            labelTxt.font = gameFont;
        }

        labelTxt.text = text;
        labelTxt.color = new Color(0.8f, 0.8f, 0.8f, 1f);
        labelTxt.alignment = TextAlignmentOptions.Right;
        labelTxt.enableAutoSizing = true;
        labelTxt.fontSizeMin = 14;
        labelTxt.fontSizeMax = 20;

        RectTransform labelRect = labelObj.GetComponent<RectTransform>();
        labelRect.anchorMin = anchorMin;
        labelRect.anchorMax = anchorMax;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
    }

    private GameObject CreateInputField(GameObject parent, string name, string placeholderTxt, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject inputObj = new GameObject(name);
        inputObj.transform.SetParent(parent.transform, false);

        RectTransform rect = inputObj.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image bg = inputObj.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.05f, 0.05f, 1f);

        TMP_InputField inputField = inputObj.AddComponent<TMP_InputField>();
        inputField.targetGraphic = bg;
        inputField.characterLimit = 20;

        GameObject viewportObj = new GameObject("Viewport");
        viewportObj.transform.SetParent(inputObj.transform, false);

        RectTransform vpRect = viewportObj.AddComponent<RectTransform>();
        vpRect.anchorMin = Vector2.zero;
        vpRect.anchorMax = Vector2.one;
        vpRect.offsetMin = new Vector2(15, 0);
        vpRect.offsetMax = new Vector2(-15, 0);

        viewportObj.AddComponent<RectMask2D>();

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(viewportObj.transform, false);
        TextMeshProUGUI txt = textObj.AddComponent<TextMeshProUGUI>();

        if (gameFont != null)
        {
            txt.font = gameFont;
        }

        txt.color = Color.white;
        txt.alignment = TextAlignmentOptions.Left;
        txt.enableAutoSizing = true;
        txt.fontSizeMin = 15;
        txt.fontSizeMax = 30;
        txt.textWrappingMode = TextWrappingModes.NoWrap;
        txt.overflowMode = TextOverflowModes.Truncate;

        RectTransform txtRect = textObj.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = Vector2.zero;
        txtRect.offsetMax = Vector2.zero;

        GameObject phObj = new GameObject("Placeholder");
        phObj.transform.SetParent(viewportObj.transform, false);
        TextMeshProUGUI pTxt = phObj.AddComponent<TextMeshProUGUI>();

        if (gameFont != null)
        {
            pTxt.font = gameFont;
        }

        pTxt.text = placeholderTxt;
        pTxt.color = Color.gray;
        pTxt.alignment = TextAlignmentOptions.Left;
        pTxt.enableAutoSizing = true;
        pTxt.fontSizeMin = 15;
        pTxt.fontSizeMax = 30;
        pTxt.textWrappingMode = TextWrappingModes.NoWrap;
        pTxt.overflowMode = TextOverflowModes.Truncate;

        RectTransform phRect = phObj.GetComponent<RectTransform>();
        phRect.anchorMin = Vector2.zero;
        phRect.anchorMax = Vector2.one;
        phRect.offsetMin = Vector2.zero;
        phRect.offsetMax = Vector2.zero;

        inputField.textViewport = vpRect;
        inputField.textComponent = txt;
        inputField.placeholder = pTxt;

        return inputObj;
    }

    private void CreateMenuButton(GameObject parent, string text, UnityEngine.Events.UnityAction action, Vector2? customAnchor = null, bool isCenter = false, Vector2? customSize = null, float customFontSize = 35f)
    {
        GameObject btnObj = new GameObject("Btn_" + text);
        btnObj.transform.SetParent(parent.transform, false);

        RectTransform rect = btnObj.AddComponent<RectTransform>();

        if (customAnchor.HasValue)
        {
            rect.anchorMin = customAnchor.Value;
            rect.anchorMax = customAnchor.Value;
            rect.pivot = isCenter ? new Vector2(0.5f, 0.5f) : new Vector2(0, 0.5f);
        }

        rect.sizeDelta = customSize.HasValue ? customSize.Value : new Vector2(300, 50);

        Image btnImg = btnObj.AddComponent<Image>();
        btnImg.color = new Color(1, 1, 1, 0);

        Button btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(action);

        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(btnObj.transform, false);
        TextMeshProUGUI tmpText = txtObj.AddComponent<TextMeshProUGUI>();

        if (gameFont != null)
        {
            tmpText.font = gameFont;
        }

        tmpText.text = text;
        tmpText.alignment = isCenter ? TextAlignmentOptions.Center : TextAlignmentOptions.Left;
        tmpText.color = new Color(0.7f, 0.7f, 0.7f, 1f);
        tmpText.textWrappingMode = TextWrappingModes.NoWrap;
        tmpText.enableAutoSizing = true;
        tmpText.fontSizeMin = 16;
        tmpText.fontSizeMax = customFontSize;

        RectTransform txtRect = txtObj.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = Vector2.zero;
        txtRect.offsetMax = Vector2.zero;

        AutoMenuButtonEffect effect = btnObj.AddComponent<AutoMenuButtonEffect>();
        effect.Setup(tmpText, isCenter);
    }

    private TextMeshProUGUI CreateTextBtn(GameObject parent, string text, Vector2 anchorValue, UnityEngine.Events.UnityAction action)
    {
        GameObject btnObj = new GameObject("TextBtn_" + text);
        btnObj.transform.SetParent(parent.transform, false);

        RectTransform rect = btnObj.AddComponent<RectTransform>();
        rect.anchorMin = anchorValue;
        rect.anchorMax = anchorValue;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(150, 40);

        Image btnImg = btnObj.AddComponent<Image>();
        btnImg.color = new Color(1, 1, 1, 0);

        Button btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(action);

        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(btnObj.transform, false);
        TextMeshProUGUI tmpText = txtObj.AddComponent<TextMeshProUGUI>();

        if (gameFont != null)
        {
            tmpText.font = gameFont;
        }

        tmpText.text = text;
        tmpText.alignment = TextAlignmentOptions.Center;
        tmpText.color = Color.gray;
        tmpText.enableAutoSizing = true;
        tmpText.fontSizeMin = 14;
        tmpText.fontSizeMax = 20;

        RectTransform txtRect = txtObj.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = Vector2.zero;
        txtRect.offsetMax = Vector2.zero;

        // 🔥 THÊM HIỆU ỨNG HOVER
        AutoMenuButtonEffect effect = btnObj.AddComponent<AutoMenuButtonEffect>();
        effect.Setup(tmpText, true);

        return tmpText;
    }

    private void OnDestroy()
    {
        // Khi Scene tải xong, Menu cũ bị xóa, tự động bật cờ này lên
        isMenuDestroyed = true;
    }
    #endregion

    #region QUẢN LÝ CHUYỂN TRANG
    private void OpenPanel(CanvasGroup targetPanel)
    {
        if (currentActivePanel == targetPanel) return;
        if (currentActivePanel != null) StartCoroutine(FadePanel(currentActivePanel, 0f, false));
        currentActivePanel = targetPanel;
        StartCoroutine(FadePanel(currentActivePanel, 1f, true));

        if (targetPanel.gameObject.name == "CharacterSelectPanel") UpdatePreview();

        // 🔥 KIỂM TRA NẾU MỞ CREDITS THÌ RESET VÀ CHẠY
        isCreditsOpen = (targetPanel.gameObject.name == "CreditsPanel");
        if (isCreditsOpen && creditsContent != null) // Đổi ở đây nữa
        {
            creditsContent.anchoredPosition = Vector2.zero;
        }
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

        while (time < 0.25f)
        {
            time += Time.unscaledDeltaTime;
            panel.alpha = Mathf.Lerp(startAlpha, targetAlpha, time / 0.25f);
            yield return null;
        }

        panel.alpha = targetAlpha;

        if (!show)
        {
            panel.gameObject.SetActive(false);
        }
    }
    #endregion

    #region FUSION MULTIPLAYER
    private async void StartHostGame(string roomName)
    {
        CleanupOldRunners();

        NetworkRunner runner = Instantiate(runnerPrefab);

        NetworkSceneManagerDefault sceneManager = runner.GetComponent<NetworkSceneManagerDefault>();
        if (sceneManager == null)
        {
            sceneManager = runner.gameObject.AddComponent<NetworkSceneManagerDefault>();
        }

        var result = await runner.StartGame(new StartGameArgs()
        {
            GameMode = GameMode.Host,
            SessionName = roomName,
            SceneManager = sceneManager
        });

        if (result.Ok)
        {
            // 🔥 TẮT NHẠC NỀN KHI VÀO GAME
            if (bgmSource != null) bgmSource.Stop();

            EnableGameplayUI();
            gameObject.SetActive(false);
            await runner.LoadScene(SceneRef.FromIndex(mainSceneIndex));
        }
        else
        {
            Debug.LogError("Lỗi Host: " + result.ShutdownReason);
            isConnecting = false;
        }
    }

    private async void StartClientGame(string roomName)
    {
        CleanupOldRunners();

        NetworkRunner runner = Instantiate(runnerPrefab);

        NetworkSceneManagerDefault sceneManager = runner.GetComponent<NetworkSceneManagerDefault>();
        if (sceneManager == null)
        {
            sceneManager = runner.gameObject.AddComponent<NetworkSceneManagerDefault>();
        }

        var result = await runner.StartGame(new StartGameArgs()
        {
            GameMode = GameMode.Client,
            SessionName = roomName,
            SceneManager = sceneManager
        });

        if (isMenuDestroyed) return;

        if (result.Ok)
        {
            // 🔥 TẮT NHẠC NỀN KHI VÀO GAME
            if (bgmSource != null) bgmSource.Stop();

            EnableGameplayUI();
            gameObject.SetActive(false);
        }
        else
        {
            Debug.LogError("Lỗi Client: " + result.ShutdownReason);
            isConnecting = false;
        }
    }

    private void CleanupOldRunners()
    {
        NetworkRunner[] oldRunners = FindObjectsByType<NetworkRunner>(FindObjectsSortMode.None);
        foreach (var r in oldRunners)
        {
            Destroy(r.gameObject);
        }
    }

    private void EnableGameplayUI()
    {
        foreach (var obj in temporarilyDisabledObjects)
        {
            if (obj != null)
            {
                obj.SetActive(true);
            }
        }
        temporarilyDisabledObjects.Clear();
    }

    // --- HÀM BẢO FUSION ĐI DÒ PHÒNG ---
    // --- HÀM BẢO FUSION ĐI DÒ PHÒNG ---
    private async void ConnectToLobby()
    {
        // 🔥 1. CHỐNG SPAM CLICK: Nếu đang dò sóng hoặc đang kết nối rồi thì bỏ qua
        if (lobbyRunner != null && lobbyRunner.IsCloudReady)
        {
            Debug.Log("Đã kết nối Sảnh, đang chờ Fusion cập nhật danh sách...");
            return;
        }

        if (lobbyRunner == null)
        {
            lobbyRunner = gameObject.AddComponent<NetworkRunner>();
        }

        // 🔥 2. QUAN TRỌNG NHẤT: Báo cho Fusion biết script này sẽ "lắng nghe" sự kiện
        lobbyRunner.AddCallbacks(this);

        Debug.Log("Đang kết nối vào Sảnh chờ (Lobby) để dò phòng...");

        // Bắt đầu tham gia vào Sảnh (Lobby)
        var result = await lobbyRunner.JoinSessionLobby(SessionLobby.ClientServer);

        if (result.Ok)
        {
            Debug.Log("Đã vào Sảnh! Fusion sẽ tự động gọi hàm OnSessionListUpdated nếu có căn cứ mới.");
        }
        else
        {
            Debug.LogError("Lỗi dò sóng: " + result.ShutdownReason);
            // 🔥 Xóa cái Runner bị lỗi đi để lần sau sếp bấm lại nó làm lại từ đầu cho sạch
            Destroy(lobbyRunner);
            lobbyRunner = null;
        }
    }

    // --- HÀM ĂNG-TEN HỨNG DỮ LIỆU TỪ FUSION ---
    // (Bắt buộc phải có vì sếp đã khai báo INetworkRunnerCallbacks ở trên)
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        Debug.Log($"Phát hiện {sessionList.Count} căn cứ đang hoạt động!");
        // Gọi hàm đổ UI siêu xịn của sếp ra
        UpdateServerListUI(sessionList);
    }

    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        // Xóa lệnh quăng lỗi đi, thay bằng dòng Log để sếp biết tại sao nó tắt
        Debug.LogWarning($"[Fusion] Đã đóng kết nối mạng. Lý do: {shutdownReason}");

        // Mở khóa cho phép bấm nút lại (đề phòng kẹt UI)
        isConnecting = false;
    }

    void INetworkRunnerCallbacks.OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }

    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }

    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, System.ArraySegment<byte> data) { }

    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }

    public void OnInput(NetworkRunner runner, NetworkInput input) { }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }

    void INetworkRunnerCallbacks.OnConnectedToServer(NetworkRunner runner) { }

    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }

    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }

    public void OnSceneLoadDone(NetworkRunner runner) { }

    public void OnSceneLoadStart(NetworkRunner runner) { }

    #endregion
}

public class AutoMenuButtonEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    private TextMeshProUGUI btnText;
    private Color normalColor = new Color(0.7f, 0.7f, 0.7f, 1f);
    private Color hoverColor = new Color(0.7f, 0.15f, 0.15f, 1f);
    private Vector3 originalPos;
    private bool isCentered;
    private Coroutine colorRoutine, moveRoutine;

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
        if (!isCentered)
        {
            AnimateMove(originalPos + new Vector3(15f, 0, 0));
        }

        // 👇 THÊM LỆNH GỌI TIẾNG HOVER
        if (AutoMainMenuManager.Instance != null)
        {
            AutoMainMenuManager.Instance.PlayHoverSFX();
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        AnimateColor(normalColor);
        if (!isCentered)
        {
            AnimateMove(originalPos);
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (btnText != null)
        {
            btnText.transform.localScale = Vector3.one * 0.9f;
        }

        // 👇 THÊM LỆNH GỌI TIẾNG CLICK
        if (AutoMainMenuManager.Instance != null)
        {
            AutoMainMenuManager.Instance.PlayClickSFX();
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (btnText != null)
        {
            btnText.transform.localScale = Vector3.one;
        }
    }

    private void AnimateColor(Color target)
    {
        if (btnText == null) return;

        if (colorRoutine != null)
        {
            StopCoroutine(colorRoutine);
        }
        colorRoutine = StartCoroutine(DoColor(target, 0.15f));
    }

    private void AnimateMove(Vector3 target)
    {
        if (btnText == null) return;

        if (moveRoutine != null)
        {
            StopCoroutine(moveRoutine);
        }
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
            percent = percent * (2f - percent); // Ease out
            btnText.transform.localPosition = Vector3.Lerp(startPos, targetPos, percent);
            yield return null;
        }

        btnText.transform.localPosition = targetPos;
    }
}