using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Fusion;
using UnityEngine.EventSystems;

/// <summary>
/// Professional AAA Dev Cheat Console & Debug Suite.
/// Includes Fullscreen Raycast Blocking, Category Tabs (Cheats, Backpack, Items)
/// to prevent accidental item spawns or world interaction clicks.
/// </summary>
public class DevCheatManager : MonoBehaviour
{
    // ============================
    // SINGLETON
    // ============================
    private static DevCheatManager instance;
    public static DevCheatManager Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("--- DEV CHEAT MANAGER ---");
                instance = go.AddComponent<DevCheatManager>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }

    // ============================
    // CHEAT STATES
    // ============================
    public bool isGodMode = false;
    private bool isMenuOpen = false;

    public bool IsMenuOpen => isMenuOpen;

    // ============================
    // UI REFERENCES
    // ============================
    private GameObject cheatCanvasGO;
    private GameObject cheatRootGO;
    private GameObject mainPanelGO;

    private GameObject cheatsTabContent;
    private GameObject backpackTabContent;
    private GameObject itemsTabContent;
    // Keep the ScrollRect roots as well as their contents.  Toggling only the
    // content leaves an invisible full-screen GraphicRaycaster target on top of
    // the selected tab, which makes clicks land on the wrong UI.
    private GameObject cheatsTabRoot;
    private GameObject backpackTabRoot;
    private GameObject itemsTabRoot;

    private Image tabBtnCheatsImg;
    private Image tabBtnBackpackImg;
    private Image tabBtnItemsImg;

    private TextMeshProUGUI statusText;
    private TextMeshProUGUI godModeBtnText;
    private Image godModeBtnImage;

    private Dictionary<int, Button> capacityButtons = new Dictionary<int, Button>();
    private Dictionary<int, TextMeshProUGUI> capacityButtonTexts = new Dictionary<int, TextMeshProUGUI>();
    private Dictionary<int, Image> capacityButtonImages = new Dictionary<int, Image>();

    private TMP_FontAsset tmpFont;

    // ============================
    // PLAYER CACHE
    // ============================
    private List<ItemData> cachedItems = new List<ItemData>();
    private PlayerHealth cachedHealth;
    private InventorySystem cachedInventory;

    // ============================
    // AUTO INIT
    // ============================
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoInit()
    {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
        // A developer console must never exist in a release build.
        return;
#else
        var _ = Instance;
#endif
    }

    private void Awake()
    {
        if (instance == null) instance = this;
        else { Destroy(gameObject); return; }

        tmpFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        LoadItems();
        BuildUI();
    }

    private void LoadItems()
    {
        cachedItems.Clear();
        ItemData[] loaded = Resources.LoadAll<ItemData>("Items");
        if (loaded != null)
        {
            foreach (var item in loaded)
            {
                if (item != null && item.category != ItemCategory.Backpack)
                {
                    cachedItems.Add(item);
                }
            }

            // Ưu tiên Vũ Khí (Weapon) lên đầu, sau đó xếp theo Category
            cachedItems.Sort((a, b) =>
            {
                if (a.category == ItemCategory.Weapon && b.category != ItemCategory.Weapon) return -1;
                if (a.category != ItemCategory.Weapon && b.category == ItemCategory.Weapon) return 1;
                return a.category.CompareTo(b.category);
            });
        }
    }

    // ============================
    // UPDATE LOOP
    // ============================
    private void Update()
    {
        if (RouteBRadioBroadcastUI.BlocksLocalGameplayInput ||
            VehicleRepairSkillCheckUI.BlocksGameplayInput) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // The cheat is a modal UI.  Consume Escape before other menus see it.
        if (isMenuOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            HideCheatMenu();
            AutoMainMenuManager.EscapeConsumedThisFrame = true;
            return;
        }

        // Phím P mở/đóng menu cheat
        if (Input.GetKeyDown(KeyCode.P))
        {
            if (AutoChatManager.Instance != null && AutoChatManager.Instance.IsTyping()) return;

            if (!CanUseDevCheats())
            {
                Debug.LogWarning("[CHEAT] Host/server authority is required to open the developer console.");
                return;
            }

            ToggleMenu();
        }

        if (Input.GetKeyDown(KeyCode.F6))
            RunRouteBCheat(AdvanceRouteBStory);
        else if (Input.GetKeyDown(KeyCode.F7))
            RunRouteBCheat(CompleteRouteBClues);
        else if (Input.GetKeyDown(KeyCode.F10))
            RunRouteBCheat(AdvanceRouteBBase);
        else if (Input.GetKeyDown(KeyCode.F11))
            RunRouteBCheat(ReplayCurrentRouteBAudio);
        else if (Input.GetKeyDown(KeyCode.F12))
            RunRouteBCheat(TeleportToCurrentRouteBObjective);
#endif

        // God Mode Update Loop
        if (isGodMode)
        {
            CachePlayer();
            if (cachedHealth != null && cachedHealth.Object != null && cachedHealth.Object.IsValid)
            {
                cachedHealth.currentHealth = cachedHealth.maxHealth;
                cachedHealth.isBleeding = false;
                cachedHealth.isInPain = false;
            }
        }

        // Giữ trỏ chuột hiển thị khi mở Dev Menu
        if (isMenuOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            UpdateStatusDisplay();
        }
    }

    public void ToggleMenu()
    {
        if (!CanUseDevCheats())
        {
            Debug.LogWarning("[CHEAT] Rejected ToggleMenu from a non-host client.");
            return;
        }

        isMenuOpen = !isMenuOpen;
        cheatRootGO.SetActive(isMenuOpen);

        if (isMenuOpen)
        {
            CachePlayer();
            if (AutoTabManager.Instance != null) AutoTabManager.Instance.ShowTabs(false);
            if (AutoUIManager.Instance != null) AutoUIManager.Instance.ForceHideInventoryOnly();
            UpdateStatusDisplay();
            UpdateCapacityButtonStyles();
            SelectTab("BACKPACK"); // Mặc định mở Tab Balo khi vào cheat để test
        }
    }

    public void HideCheatMenu()
    {
        if (isMenuOpen)
        {
            isMenuOpen = false;
            if (cheatRootGO != null) cheatRootGO.SetActive(false);
            AutoMainMenuManager.EscapeConsumedThisFrame = true;
        }
    }

    // ============================
    // DÒ TÌM PLAYER LOCAL CHÍNH XÁC
    // ============================
    private void CachePlayer()
    {
        cachedHealth = null;
        cachedInventory = null;

        if (PlayerMovement.LocalPlayerInstance != null)
        {
            cachedHealth = PlayerMovement.LocalPlayerInstance.GetComponent<PlayerHealth>();
            cachedInventory = PlayerMovement.LocalPlayerInstance.GetComponent<InventorySystem>();
            if (cachedInventory != null) return;
        }

        foreach (var h in FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None))
        {
            // Never fall back to a remote player just because this machine is
            // the state authority for that player.
            if (h != null && h.Object != null && h.Object.IsValid && h.HasInputAuthority)
            {
                cachedHealth = h;
                cachedInventory = h.GetComponent<InventorySystem>();
                return;
            }
        }
    }

    // ============================
    // DỰNG TOÀN BỘ GIAO DIỆN UI AAA
    // ============================
    private void BuildUI()
    {
        // --- Canvas ---
        cheatCanvasGO = new GameObject("CheatCanvas");
        DontDestroyOnLoad(cheatCanvasGO);
        Canvas cv = cheatCanvasGO.AddComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = 9999; // Nằm trên cùng tuyệt đối
        CanvasScaler cs = cheatCanvasGO.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920, 1080);
        cs.matchWidthOrHeight = 0.5f;
        cheatCanvasGO.AddComponent<GraphicRaycaster>();

        // --- Root Panel Container ---
        cheatRootGO = MakeRect("CheatRoot", cheatCanvasGO.transform,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);

        // --- FULLSCREEN OVERLAY (Chặn 100% click xuyên xuống thế giới game & các UI khác) ---
        GameObject overlayGO = MakeRect("DimOverlay", cheatRootGO.transform,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);
        Image overlayImg = overlayGO.AddComponent<Image>();
        overlayImg.color = new Color(0f, 0f, 0f, 0.45f);
        overlayImg.raycastTarget = true; // CHẶN CLICK XUYÊN THẤU TOÀN MÀN HÌNH

        // --- Main Panel (Khung chính 580x720) ---
        mainPanelGO = MakeRect("MainPanel", cheatRootGO.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(580, 720));
        Image panelBG = mainPanelGO.AddComponent<Image>();
        panelBG.color = new Color32(15, 20, 28, 255);
        panelBG.raycastTarget = true;

        Outline panelOutline = mainPanelGO.AddComponent<Outline>();
        panelOutline.effectColor = new Color32(56, 189, 248, 220); // Cyan Neon
        panelOutline.effectDistance = new Vector2(2, -2);

        // --- Header ---
        GameObject header = MakeRect("Header", mainPanelGO.transform,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
            new Vector2(0, -6), new Vector2(0, 42));
        Image headerBG = header.AddComponent<Image>();
        headerBG.color = new Color32(23, 32, 45, 255);
        MakeTMP(header, "DEVELOPER CHEAT CONSOLE", 20, FontStyles.Bold,
            new Color32(56, 189, 248, 255), TextAlignmentOptions.Center);

        // --- Status Bar ---
        GameObject statusBar = MakeRect("StatusBar", mainPanelGO.transform,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
            new Vector2(0, -50), new Vector2(0, 32));
        Image statusBG = statusBar.AddComponent<Image>();
        statusBG.color = new Color32(12, 16, 22, 255);
        statusText = MakeTMP(statusBar, "Status: Ready", 12, FontStyles.Bold,
            new Color32(226, 232, 240, 255), TextAlignmentOptions.Center);

        // --- Category Tabs Header (Phân chia Tab rõ ràng, tránh bấm nhầm nút Spawn đồ) ---
        GameObject tabHeaderGO = MakeRect("TabHeader", mainPanelGO.transform,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
            new Vector2(0, -84), new Vector2(-24, 38));

        TextMeshProUGUI t1Txt, t2Txt, t3Txt;
        MakeButton(tabHeaderGO.transform, "⚡ CHEATS", new Color32(30, 41, 59, 255), () => SelectTab("CHEATS"),
            new Vector2(0.00f, 0f), new Vector2(0.32f, 1f), out t1Txt, out tabBtnCheatsImg);

        MakeButton(tabHeaderGO.transform, "🎒 BACKPACK TEST", new Color32(30, 41, 59, 255), () => SelectTab("BACKPACK"),
            new Vector2(0.34f, 0f), new Vector2(0.66f, 1f), out t2Txt, out tabBtnBackpackImg);

        MakeButton(tabHeaderGO.transform, "📋 SPAWN ITEMS", new Color32(30, 41, 59, 255), () => SelectTab("ITEMS"),
            new Vector2(0.68f, 0f), new Vector2(1.00f, 1f), out t3Txt, out tabBtnItemsImg);

        // --- Content Area Container ---
        GameObject contentArea = MakeRect("ContentArea", mainPanelGO.transform,
            new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);
        RectTransform caRT = contentArea.GetComponent<RectTransform>();
        caRT.offsetMin = new Vector2(12, 12);
        caRT.offsetMax = new Vector2(-12, -126);

        // Dựng 3 Tab riêng biệt
        cheatsTabRoot = CreateScrollTab("TabContent_Cheats", contentArea.transform, out cheatsTabContent);
        backpackTabRoot = CreateScrollTab("TabContent_Backpack", contentArea.transform, out backpackTabContent);
        itemsTabRoot = CreateScrollTab("TabContent_Items", contentArea.transform, out itemsTabContent);

        // --- POPULATE TAB 1: PLAYER CHEATS ---
        AddSectionHeader(cheatsTabContent.transform, "⚡ PLAYER QUICK CHEATS");
        AddGodModeRow(cheatsTabContent.transform);
        AddActionRow(cheatsTabContent.transform, "❤️ RESTORE 100% HP & STATUS", "HEAL NOW", new Color32(16, 185, 129, 255), () => {
            CachePlayer();
            if (cachedHealth != null && cachedHealth.Object != null && cachedHealth.Object.IsValid)
            {
                cachedHealth.currentHealth = cachedHealth.maxHealth;
                cachedHealth.isBleeding = false;
                cachedHealth.isInPain = false;
                Debug.Log("[CHEAT] ❤️ Health & status fully restored!");
            }
        });
        AddActionRow(cheatsTabContent.transform, "📦 GIVE +100 ALL AMMO TYPES", "ADD AMMO", new Color32(245, 158, 11, 255), () => {
            CachePlayer();
            if (cachedInventory != null)
            {
                ItemData a7 = Resources.Load<ItemData>("Items/Ammo762");
                ItemData a12 = Resources.Load<ItemData>("Items/Ammo12Gauge");
                if (a7 != null) cachedInventory.AddItem(a7, 100);
                if (a12 != null) cachedInventory.AddItem(a12, 100);
                Debug.Log("[CHEAT] 📦 Granted +100 7.62mm & 12 Gauge Ammo!");
            }
        });

        AddSectionHeader(cheatsTabContent.transform, "📻 ROUTE B FLOW TEST — NO LOOT CONTAINERS");
        AddActionRow(cheatsTabContent.transform, "NEXT STORY STEP  [F6]", "ADVANCE", new Color32(124, 58, 237, 255),
            () => RunRouteBCheat(AdvanceRouteBStory));
        AddActionRow(cheatsTabContent.transform, "COMPLETE RESIDENTIAL CLUES  [F7]", "3 / 3", new Color32(217, 119, 6, 255),
            () => RunRouteBCheat(CompleteRouteBClues));
        AddActionRow(cheatsTabContent.transform, "NEXT MILITARY-BASE STEP  [F10]", "ADVANCE", new Color32(5, 150, 105, 255),
            () => RunRouteBCheat(AdvanceRouteBBase));
        AddActionRow(cheatsTabContent.transform, "REPLAY CURRENT STORY AUDIO  [F11]", "REPLAY", new Color32(2, 132, 199, 255),
            () => RunRouteBCheat(ReplayCurrentRouteBAudio));
        AddActionRow(cheatsTabContent.transform, "TELEPORT TO CURRENT NON-LOOT OBJECTIVE  [F12]", "TELEPORT", new Color32(14, 116, 144, 255),
            () => RunRouteBCheat(TeleportToCurrentRouteBObjective));

        // --- POPULATE TAB 2: FIXED INVENTORY CAPACITY TEST ---
        AddSectionHeader(backpackTabContent.transform, "🎒 FIXED INVENTORY CAPACITY");
        AddCapacityRow(backpackTabContent.transform, InventorySystem.FixedTotalSlots,
            "20 Slots Total (5 Hotbar + 15 Storage) — No Backpack Levels");

        // --- POPULATE TAB 3: ITEM SPAWNER ---
        AddSectionHeader(itemsTabContent.transform, "📋 ITEM SPAWNER");
        foreach (ItemData item in cachedItems)
        {
            if (item == null) continue;
            AddItemRow(itemsTabContent.transform, item);
        }

        // Ẩn mặc định
        cheatRootGO.SetActive(false);
    }

    private void SelectTab(string tabName)
    {
        // Disable the complete ScrollRect, not just its Content.  Each root
        // owns an Image/Viewport which otherwise continues to intercept
        // pointer events while visually empty.
        cheatsTabRoot.SetActive(tabName == "CHEATS");
        backpackTabRoot.SetActive(tabName == "BACKPACK");
        itemsTabRoot.SetActive(tabName == "ITEMS");

        tabBtnCheatsImg.color = (tabName == "CHEATS") ? new Color32(56, 189, 248, 255) : new Color32(30, 41, 59, 255);
        tabBtnBackpackImg.color = (tabName == "BACKPACK") ? new Color32(56, 189, 248, 255) : new Color32(30, 41, 59, 255);
        tabBtnItemsImg.color = (tabName == "ITEMS") ? new Color32(56, 189, 248, 255) : new Color32(30, 41, 59, 255);
    }

    private GameObject CreateScrollTab(string name, Transform parent, out GameObject contentGO)
    {
        GameObject scrollGO = MakeRect(name, parent,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);
        Image scrollBG = scrollGO.AddComponent<Image>();
        scrollBG.color = new Color32(8, 12, 16, 230);
        scrollBG.raycastTarget = true;

        ScrollRect sr = scrollGO.AddComponent<ScrollRect>();
        sr.horizontal = false;
        sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.scrollSensitivity = 35f;

        GameObject vpGO = MakeRect("Viewport", scrollGO.transform,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);
        Image vpImg = vpGO.AddComponent<Image>();
        vpImg.color = Color.white;
        Mask vpMask = vpGO.AddComponent<Mask>();
        vpMask.showMaskGraphic = false;
        sr.viewport = vpGO.GetComponent<RectTransform>();

        contentGO = MakeRect("Content", vpGO.transform,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
            Vector2.zero, Vector2.zero);

        VerticalLayoutGroup vlg = contentGO.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 6;
        vlg.padding = new RectOffset(6, 6, 6, 6);
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        ContentSizeFitter csf = contentGO.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        sr.content = contentGO.GetComponent<RectTransform>();

        return scrollGO;
    }

    // ============================
    // UI BUILDER HELPER METHODS
    // ============================

    private void AddSectionHeader(Transform contentParent, string title)
    {
        GameObject row = MakeRect("Header_" + title, contentParent,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(0, 32));
        LayoutElement le = row.AddComponent<LayoutElement>();
        le.preferredHeight = 32;
        le.minHeight = 32;

        Image bg = row.AddComponent<Image>();
        bg.color = new Color32(30, 41, 59, 255);

        MakeTMP(row, title, 13, FontStyles.Bold,
            new Color32(56, 189, 248, 255), TextAlignmentOptions.Left, 12f);
    }

    private void AddGodModeRow(Transform contentParent)
    {
        GameObject row = MakeRect("Row_GodMode", contentParent,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(0, 44));
        LayoutElement le = row.AddComponent<LayoutElement>();
        le.preferredHeight = 44;
        le.minHeight = 44;

        Image bg = row.AddComponent<Image>();
        bg.color = new Color32(21, 29, 40, 255);

        GameObject labelGO = MakeRect("Label", row.transform,
            new Vector2(0, 0), new Vector2(0.68f, 1), new Vector2(0, 0.5f),
            Vector2.zero, Vector2.zero);
        labelGO.GetComponent<RectTransform>().offsetMin = new Vector2(14, 0);
        MakeTMP(labelGO, "🛡️ GOD MODE (INVINCIBILITY)", 14, FontStyles.Bold,
            new Color32(241, 245, 249, 255), TextAlignmentOptions.Left);

        Button btn = MakeButton(row.transform, "TOGGLE", new Color32(239, 68, 68, 255), () => {
            if (!CanUseDevCheats())
            {
                Debug.LogWarning("[CHEAT] Rejected God Mode command from a non-host client.");
                return;
            }

            isGodMode = !isGodMode;
            UpdateGodModeButtonStyle();
            Debug.Log(isGodMode ? "[CHEAT] 🛡️ God Mode ENABLED" : "[CHEAT] 🛡️ God Mode DISABLED");
        }, new Vector2(0.70f, 0.12f), new Vector2(0.97f, 0.88f), out godModeBtnText, out godModeBtnImage);

        UpdateGodModeButtonStyle();
    }

    private void UpdateGodModeButtonStyle()
    {
        if (godModeBtnText != null && godModeBtnImage != null)
        {
            if (isGodMode)
            {
                godModeBtnImage.color = new Color32(16, 185, 129, 255);
                godModeBtnText.text = "ACTIVE";
            }
            else
            {
                godModeBtnImage.color = new Color32(239, 68, 68, 255);
                godModeBtnText.text = "OFF";
            }
        }
    }

    private void AddActionRow(Transform contentParent, string label, string btnText, Color32 btnColor, UnityEngine.Events.UnityAction onClick)
    {
        GameObject row = MakeRect("Row_" + label, contentParent,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(0, 44));
        LayoutElement le = row.AddComponent<LayoutElement>();
        le.preferredHeight = 44;
        le.minHeight = 44;

        Image bg = row.AddComponent<Image>();
        bg.color = new Color32(21, 29, 40, 255);

        GameObject labelGO = MakeRect("Label", row.transform,
            new Vector2(0, 0), new Vector2(0.68f, 1), new Vector2(0, 0.5f),
            Vector2.zero, Vector2.zero);
        labelGO.GetComponent<RectTransform>().offsetMin = new Vector2(14, 0);
        MakeTMP(labelGO, label, 14, FontStyles.Bold,
            new Color32(241, 245, 249, 255), TextAlignmentOptions.Left);

        TextMeshProUGUI tmpText;
        Image tmpImg;
        MakeButton(row.transform, btnText, btnColor, () => {
            if (!CanUseDevCheats())
            {
                Debug.LogWarning("[CHEAT] Rejected action command from a non-host client.");
                return;
            }

            onClick?.Invoke();
        },
            new Vector2(0.70f, 0.12f), new Vector2(0.97f, 0.88f), out tmpText, out tmpImg);
    }

    private void AddCapacityRow(Transform contentParent, int capacitySlots, string label)
    {
        int slots = capacitySlots;
        GameObject row = MakeRect("CapRow_" + slots, contentParent,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(0, 44));
        LayoutElement le = row.AddComponent<LayoutElement>();
        le.preferredHeight = 44;
        le.minHeight = 44;

        Image bg = row.AddComponent<Image>();
        bg.color = new Color32(21, 29, 40, 255);

        GameObject labelGO = MakeRect("Label", row.transform,
            new Vector2(0, 0), new Vector2(0.68f, 1), new Vector2(0, 0.5f),
            Vector2.zero, Vector2.zero);
        labelGO.GetComponent<RectTransform>().offsetMin = new Vector2(14, 0);
        MakeTMP(labelGO, label, 13, FontStyles.Bold,
            new Color32(226, 232, 240, 255), TextAlignmentOptions.Left);

        TextMeshProUGUI btnTxt;
        Image btnImg;
        Button btn = MakeButton(row.transform, "APPLY", new Color32(30, 140, 140, 255), () => {
            ApplyBackpackCapacity(slots);
        }, new Vector2(0.70f, 0.12f), new Vector2(0.97f, 0.88f), out btnTxt, out btnImg);

        capacityButtons[slots] = btn;
        capacityButtonTexts[slots] = btnTxt;
        capacityButtonImages[slots] = btnImg;
    }

    private void ApplyBackpackCapacity(int targetSlots)
    {
        if (!CanUseDevCheats())
        {
            Debug.LogWarning("[CHEAT] Rejected backpack-capacity command from a non-host client.");
            return;
        }

        CachePlayer();
        if (cachedInventory == null)
        {
            Debug.LogWarning("[CHEAT] Cannot set backpack capacity: the host local inventory was not found.");
            return;
        }

        cachedInventory.SetMaxSlots(targetSlots);
        if (AutoUIManager.Instance != null)
        {
            AutoUIManager.Instance.RefreshUI(cachedInventory.slots, cachedInventory.maxSlots);
        }
        Debug.Log($"[CHEAT] 🎒 Capacity reset to fixed {InventorySystem.FixedTotalSlots} slots.");
        UpdateCapacityButtonStyles();
        UpdateStatusDisplay();
    }

    private void UpdateCapacityButtonStyles()
    {
        int currentCapacity = (cachedInventory != null)
            ? cachedInventory.maxSlots
            : InventorySystem.FixedTotalSlots;

        foreach (var kvp in capacityButtons)
        {
            int slotSize = kvp.Key;
            TextMeshProUGUI txt = capacityButtonTexts[slotSize];
            Image img = capacityButtonImages[slotSize];

            if (txt == null || img == null) continue;

            if (slotSize == currentCapacity)
            {
                img.color = new Color32(16, 185, 129, 255);
                txt.text = "[ACTIVE]";
            }
            else
            {
                img.color = new Color32(30, 140, 140, 255);
                txt.text = "APPLY";
            }
        }
    }

    private void AddItemRow(Transform contentParent, ItemData item)
    {
        ItemData capturedItem = item;

        GameObject row = MakeRect("Item_" + item.itemName, contentParent,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(0, 46));
        LayoutElement le = row.AddComponent<LayoutElement>();
        le.preferredHeight = 46;
        le.minHeight = 46;

        Image bg = row.AddComponent<Image>();
        bg.color = new Color32(18, 25, 35, 255);

        // Icon
        GameObject iconGO = MakeRect("Icon", row.transform,
            new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f),
            new Vector2(10, 0), new Vector2(34, 34));
        Image iconImg = iconGO.AddComponent<Image>();
        iconImg.sprite = item.icon;
        iconImg.preserveAspect = true;

        // Name & Category
        GameObject nameGO = MakeRect("Name", row.transform,
            new Vector2(0, 0), new Vector2(0.58f, 1), new Vector2(0, 0.5f),
            Vector2.zero, Vector2.zero);
        nameGO.GetComponent<RectTransform>().offsetMin = new Vector2(52, 0);
        string catTag = GetCatTag(item.category);
        MakeTMP(nameGO, $"{item.itemName} <size=10><color=#38bdf8>{catTag}</color></size>",
            13, FontStyles.Bold, Color.white, TextAlignmentOptions.Left);

        // Button +1
        TextMeshProUGUI b1Txt; Image b1Img;
        MakeButton(row.transform, "+1", new Color32(37, 99, 235, 255), () => {
            SpawnItem(capturedItem, 1);
        }, new Vector2(0.60f, 0.12f), new Vector2(0.77f, 0.88f), out b1Txt, out b1Img);

        // Button +Stack
        int stack = capturedItem.isStackable ? capturedItem.maxStack : 1;
        TextMeshProUGUI bStkTxt; Image bStkImg;
        MakeButton(row.transform, $"+{stack}", new Color32(16, 185, 129, 255), () => {
            SpawnItem(capturedItem, stack);
        }, new Vector2(0.79f, 0.12f), new Vector2(0.97f, 0.88f), out bStkTxt, out bStkImg);
    }

    private void SpawnItem(ItemData item, int amount)
    {
        if (!CanUseDevCheats())
        {
            Debug.LogWarning("[CHEAT] Rejected item-spawn command from a non-host client.");
            return;
        }

        CachePlayer();
        if (cachedInventory == null) return;

        if (item.category == ItemCategory.Backpack)
        {
            Debug.LogWarning($"[CHEAT] Backpack item '{item.itemName}' is disabled; inventory stays at " +
                             $"{InventorySystem.FixedTotalSlots} slots.");
            UpdateCapacityButtonStyles();
            UpdateStatusDisplay();
            return;
        }

        cachedInventory.AddItem(item, amount);
        Debug.Log($"[CHEAT] 🎁 Spawned +{amount} {item.itemName}");
        UpdateStatusDisplay();
    }

    private string GetCatTag(ItemCategory cat)
    {
        switch (cat)
        {
            case ItemCategory.Weapon: return "[Weapon]";
            case ItemCategory.Ammunition: return "[Ammo]";
            case ItemCategory.Medical: return "[Medical]";
            case ItemCategory.Consumable: return "[Consumable]";
            case ItemCategory.Backpack: return "[Backpack]";
            default: return "[Item]";
        }
    }

    private void UpdateStatusDisplay()
    {
        if (statusText == null) return;
        CachePlayer();

        string playerState = (cachedHealth != null && cachedHealth.Object != null && cachedHealth.Object.IsValid)
            ? "<color=#22c55e>Connected</color>" : "<color=#ef4444>Disconnected</color>";
        int capacity = (cachedInventory != null) ? cachedInventory.maxSlots : 15;
        int storage = Mathf.Max(0, capacity - 5);
        string godState = isGodMode ? "<color=#22c55e>ON</color>" : "<color=#94a3b8>OFF</color>";

        statusText.text = $"Player: {playerState}  ·  Capacity: <color=#38bdf8>{capacity} Slots</color> (5 Hotbar + {storage} Storage)  ·  GodMode: {godState}";
    }

    private void RunRouteBCheat(System.Action action)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (AutoChatManager.Instance != null && AutoChatManager.Instance.IsTyping()) return;
        if (!CanUseDevCheats())
        {
            Debug.LogWarning("[CHEAT] Route B flow controls require Solo/Host authority.");
            return;
        }

        // Route audio and choice panels own the local modal layer. Closing the
        // cheat first prevents its fullscreen raycast blocker from covering them.
        HideCheatMenu();
        action?.Invoke();
#endif
    }

    private static void AdvanceRouteBStory()
    {
        MainQuestManager manager = MainQuestManager.Instance;
        if (manager == null || !manager.IsNetworkReady)
        {
            Debug.LogWarning("[CHEAT] MainQuestManager is not ready. Start Main from MainMenu first.");
            return;
        }
        manager.DebugAdvanceRouteB();
    }

    private static void CompleteRouteBClues()
    {
        MainQuestManager manager = MainQuestManager.Instance;
        if (manager == null || !manager.IsNetworkReady)
        {
            Debug.LogWarning("[CHEAT] MainQuestManager is not ready. Start Main from MainMenu first.");
            return;
        }
        manager.DebugCompleteClueSearch();
    }

    private static void AdvanceRouteBBase()
    {
        MilitaryBaseQuestManager manager = MilitaryBaseQuestManager.Instance;
        if (manager == null || !manager.IsNetworkReady)
        {
            Debug.LogWarning("[CHEAT] MilitaryBaseQuestManager is not ready. Start Main from MainMenu first.");
            return;
        }
        manager.DebugAdvanceMilitaryRoute();
    }

    private static void ReplayCurrentRouteBAudio()
    {
        MainQuestManager main = MainQuestManager.Instance;
        if (main == null || !main.IsNetworkReady)
        {
            Debug.LogWarning("[CHEAT] Route B is not ready.");
            return;
        }

        RouteBAudioCueId cue;
        switch (main.CurrentStage)
        {
            case MainQuestManager.QuestStage.NotStarted:
                cue = RouteBAudioCueId.OpeningEmergencyBroadcast;
                break;
            case MainQuestManager.QuestStage.SearchNeighborhood:
                cue = main.RouteClueCount switch
                {
                    0 => RouteBAudioCueId.PlayerRouteReaction,
                    1 => RouteBAudioCueId.FirstSupplyDocument,
                    _ => RouteBAudioCueId.SecondEvacuationDocument
                };
                break;
            case MainQuestManager.QuestStage.LocateOffice:
                cue = RouteBAudioCueId.ThirdCoordinationDocument;
                break;
            case MainQuestManager.QuestStage.FindCityMap:
                cue = main.CurrentOfficeInvestigationStep switch
                {
                    <= 0 => RouteBAudioCueId.OfficeLocated,
                    1 => RouteBAudioCueId.DispatchDeskLog,
                    _ => RouteBAudioCueId.OfficeRadioRecording
                };
                break;
            default:
                MilitaryBaseQuestManager military = MilitaryBaseQuestManager.Instance;
                if (military == null || !military.IsNetworkReady)
                {
                    cue = RouteBAudioCueId.MilitaryRouteRevealed;
                    break;
                }
                cue = military.CurrentPhase switch
                {
                    MilitaryBaseQuestManager.Phase.NotReached => RouteBAudioCueId.MilitaryRouteRevealed,
                    MilitaryBaseQuestManager.Phase.Investigating => RouteBAudioCueId.MilitaryBaseApproach,
                    MilitaryBaseQuestManager.Phase.SiegeAndRepair when military.IsGeneratorActive =>
                        RouteBAudioCueId.GeneratorOnline,
                    MilitaryBaseQuestManager.Phase.SiegeAndRepair => RouteBAudioCueId.SiegeStarted,
                    MilitaryBaseQuestManager.Phase.ReadyToEscape => RouteBAudioCueId.EscapeVehicleReady,
                    MilitaryBaseQuestManager.Phase.Escaped => RouteBAudioCueId.MilitaryEvacuationComplete,
                    _ => RouteBAudioCueId.AlarmPointOfNoReturn
                };
                break;
        }

        RouteBRadioBroadcastUI.ShowCue(cue);
        Debug.Log($"[QUEST TEST] F11: replay {cue}.");
    }

    private static void TeleportToCurrentRouteBObjective()
    {
        MainQuestManager manager = MainQuestManager.Instance;
        if (manager == null || !manager.IsNetworkReady)
        {
            Debug.LogWarning("[CHEAT] Route B is not ready. Start Main from MainMenu first.");
            return;
        }
        manager.DebugTeleportToCurrentObjective();
    }

    // ============================
    // LOW-LEVEL UI UTILITIES
    // ============================

    private GameObject MakeRect(string name, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 sizeDelta)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;
        return go;
    }

    private TextMeshProUGUI MakeTMP(GameObject parent, string text,
        float fontSize, FontStyles style, Color color,
        TextAlignmentOptions align, float leftPad = 0f)
    {
        GameObject textGO;
        TextMeshProUGUI tmp = parent.GetComponent<TextMeshProUGUI>();
        if (tmp == null)
        {
            textGO = MakeRect("Text", parent.transform,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            textGO.GetComponent<RectTransform>().offsetMin = new Vector2(leftPad, 0);
            textGO.GetComponent<RectTransform>().offsetMax = Vector2.zero;
            tmp = textGO.AddComponent<TextMeshProUGUI>();
        }
        if (tmpFont != null) tmp.font = tmpFont;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = align;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        return tmp;
    }

    private Button MakeButton(Transform parent, string label, Color32 bgColor,
        UnityEngine.Events.UnityAction onClick,
        Vector2 anchorMin, Vector2 anchorMax,
        out TextMeshProUGUI outText, out Image outImage)
    {
        GameObject btnGO = MakeRect("Btn_" + label, parent,
            anchorMin, anchorMax, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);
        btnGO.GetComponent<RectTransform>().offsetMin = Vector2.zero;
        btnGO.GetComponent<RectTransform>().offsetMax = Vector2.zero;

        Image btnBG = btnGO.AddComponent<Image>();
        btnBG.color = bgColor;
        btnBG.raycastTarget = true;
        outImage = btnBG;

        Button btn = btnGO.AddComponent<Button>();
        // Persistent states such as [ACTIVE] are controlled explicitly by the
        // cheat UI.  Unity's ColorTint was overwriting those colours on hover
        // and press, making a successful command look inactive.
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(() => {
            onClick?.Invoke();
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        });

        ColorBlock cb = btn.colors;
        cb.highlightedColor = new Color(1f, 1f, 1f, 0.2f);
        cb.pressedColor = new Color(0f, 0f, 0f, 0.4f);
        btn.colors = cb;

        outText = MakeTMP(btnGO, label, 12, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);

        return btn;
    }

    private bool CanUseDevCheats()
    {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
        return false;
#else
        NetworkRunner runner = FindFirstObjectByType<NetworkRunner>();

        // No runner means an offline development test, where this machine is
        // the only authority.  In Fusion, IsServer is true for Host and Server
        // modes and false for regular clients.
        return runner == null || runner.IsServer;
#endif
    }
}
