using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Fusion;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class HotbarHUDManager : MonoBehaviour
{
    private static HotbarHUDManager instance;
    public static HotbarHUDManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<HotbarHUDManager>();
                if (instance == null)
                {
                    GameObject go = new GameObject("--- AUTO HOTBAR HUD ---");
                    instance = go.AddComponent<HotbarHUDManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return instance;
        }
    }

    private GameObject hudCanvas;
    private GameObject hotbarPanel;
    
    // UI Elements
    private List<Image> slotBackgrounds = new List<Image>();
    private List<Image> slotIcons = new List<Image>();
    private List<TextMeshProUGUI> slotAmounts = new List<TextMeshProUGUI>();
    private List<Image> slotBatteryFills = new List<Image>();
    private RectTransform selectionHighlight;
    private TextMeshProUGUI itemNameText;

    // State
    public int selectedSlotIndex = 0; // 0 to 8
    private int lastSelectedSlotIndex = -1;
    private float itemNameTimer = 0f;
    private InventorySystem localInventory;
    private PlayerSurvival localSurvival;
    private bool hudShouldBeVisible = true;
    
    // Config
    private int hotbarSize = 5;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoSpawn()
    {
        var trigger = Instance;
    }

    void Awake()
    {
        if (instance == null) instance = this;
        else if (instance != this) { Destroy(gameObject); return; }

        DontDestroyOnLoad(transform.root.gameObject);
        GenerateHUD();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void GenerateHUD()
    {
        ClearHUDReferences();

        hudCanvas = new GameObject("HotbarCanvas");
        hudCanvas.transform.SetParent(transform, false);
        Canvas canvas = hudCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 120; // Cao hơn Canvas kho đồ (100) để kéo thả Hotbar mượt mà ngay cả khi mở Inventory!
        CanvasScaler scaler = hudCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        hudCanvas.AddComponent<GraphicRaycaster>();

        // --- HOTBAR PANEL (Dành cho 5 ô size 80x80: 5*80 + 4*8 spacing + 16 padding = 448px width, 80 + 16 = 96px height) ---
        hotbarPanel = new GameObject("HotbarPanel");
        hotbarPanel.transform.SetParent(hudCanvas.transform, false);
        RectTransform hbRt = hotbarPanel.AddComponent<RectTransform>();
        hbRt.anchorMin = new Vector2(0.5f, 0f); hbRt.anchorMax = new Vector2(0.5f, 0f);
        hbRt.pivot = new Vector2(0.5f, 0f);
        hbRt.anchoredPosition = new Vector2(0, 15);
        hbRt.sizeDelta = new Vector2(448, 96);

        Image hbBg = hotbarPanel.AddComponent<Image>();
        hbBg.color = new Color(0, 0, 0, 0.6f);

        // --- ITEM NAME TEXT (Minecraft style) ---
        GameObject nameObj = new GameObject("ItemNameText");
        nameObj.transform.SetParent(hudCanvas.transform, false);
        itemNameText = nameObj.AddComponent<TextMeshProUGUI>();
        
        TMP_FontAsset defaultFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (defaultFont == null)
        {
            TMP_FontAsset[] allFonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            if (allFonts.Length > 0) defaultFont = allFonts[0];
        }
        if (defaultFont != null) itemNameText.font = defaultFont;
        
        itemNameText.fontSize = 20;
        itemNameText.fontStyle = FontStyles.Normal;
        itemNameText.color = Color.white;
        itemNameText.alignment = TextAlignmentOptions.Center;
        itemNameText.textWrappingMode = TextWrappingModes.NoWrap;

        RectTransform nameRt = nameObj.GetComponent<RectTransform>();
        nameRt.anchorMin = new Vector2(0.5f, 0f); nameRt.anchorMax = new Vector2(0.5f, 0f);
        nameRt.pivot = new Vector2(0.5f, 0f);
        nameRt.anchoredPosition = new Vector2(0, 120); // Nằm ở độ cao 120 (phía trên panel 96px)
        nameRt.sizeDelta = new Vector2(448, 35);
        itemNameText.gameObject.SetActive(false); // Ẩn mặc định

        // --- HOTBAR LAYOUT ---
        HorizontalLayoutGroup layout = hotbarPanel.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.spacing = 8;
        layout.childControlHeight = true; layout.childControlWidth = true;

        for (int i = 0; i < hotbarSize; i++)
        {
            int slotIndex = i; // Local copy for closure
            GameObject slotObj = new GameObject($"HotbarSlot_{i}");
            slotObj.transform.SetParent(hotbarPanel.transform, false);

            UISlotDragHandler drag = slotObj.AddComponent<UISlotDragHandler>();
            drag.slotIndex = i;
            drag.slotLocation = UISlotDragHandler.Location.Hotbar;

            Image bg = slotObj.AddComponent<Image>();
            bg.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            slotBackgrounds.Add(bg);
            
            // Add click listener
            Button btn = slotObj.AddComponent<Button>();
            btn.onClick.AddListener(() => { selectedSlotIndex = slotIndex; });

            GameObject iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(slotObj.transform, false);
            RectTransform iconRt = iconObj.AddComponent<RectTransform>();
            iconRt.anchorMin = Vector2.zero; iconRt.anchorMax = Vector2.one;
            iconRt.offsetMin = new Vector2(6, 6); iconRt.offsetMax = new Vector2(-6, -6);
            Image icon = iconObj.AddComponent<Image>();
            icon.preserveAspect = true;
            iconObj.SetActive(false);
            slotIcons.Add(icon);

            GameObject batteryObj = new GameObject("BatteryFill");
            batteryObj.transform.SetParent(slotObj.transform, false);
            RectTransform batteryRt = batteryObj.AddComponent<RectTransform>();
            batteryRt.anchorMin = new Vector2(0f, 0f); batteryRt.anchorMax = new Vector2(1f, 0f);
            batteryRt.pivot = new Vector2(0f, 0f);
            batteryRt.anchoredPosition = new Vector2(5f, 5f);
            batteryRt.sizeDelta = new Vector2(-10f, 5f);
            Image battery = batteryObj.AddComponent<Image>();
            battery.type = Image.Type.Filled;
            battery.fillMethod = Image.FillMethod.Horizontal;
            battery.fillOrigin = 0;
            battery.color = new Color(0.35f, 0.95f, 0.45f, 0.95f);
            battery.raycastTarget = false;
            batteryObj.SetActive(false);
            slotBatteryFills.Add(battery);

            GameObject txtObj = new GameObject("Amount");
            txtObj.transform.SetParent(slotObj.transform, false);
            RectTransform txtRt = txtObj.AddComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero; txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = Vector2.zero; txtRt.offsetMax = new Vector2(-4, -4);
            TextMeshProUGUI txt = txtObj.AddComponent<TextMeshProUGUI>();
            txt.alignment = TextAlignmentOptions.BottomRight;
            txt.fontSize = 18;
            txt.fontStyle = FontStyles.Bold;
            txt.color = Color.white;
            txt.textWrappingMode = TextWrappingModes.NoWrap;
            txtObj.SetActive(false);
            slotAmounts.Add(txt);
        }

        // --- SELECTION HIGHLIGHT (Khung viền 4 cạnh thuần túy 100% trong suốt bên trong) ---
        GameObject hlObj = new GameObject("HighlightFrame");
        hlObj.transform.SetParent(hotbarPanel.transform, false);
        selectionHighlight = hlObj.AddComponent<RectTransform>();

        CreateBorderLine(selectionHighlight, "TopBorder", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1f), new Vector2(0, 3f));
        CreateBorderLine(selectionHighlight, "BottomBorder", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0f), new Vector2(0, 3f));
        CreateBorderLine(selectionHighlight, "LeftBorder", new Vector2(0, 0), new Vector2(0, 1), new Vector2(0f, 0.5f), new Vector2(3f, 0));
        CreateBorderLine(selectionHighlight, "RightBorder", new Vector2(1, 0), new Vector2(1, 1), new Vector2(1f, 0.5f), new Vector2(3f, 0));

        hudCanvas.SetActive(hudShouldBeVisible);
    }

    private void ClearHUDReferences()
    {
        slotBackgrounds.Clear();
        slotIcons.Clear();
        slotAmounts.Clear();
        slotBatteryFills.Clear();
        hotbarPanel = null;
        selectionHighlight = null;
        itemNameText = null;
    }

    private bool IsHUDComplete()
    {
        if (hudCanvas == null || hotbarPanel == null || selectionHighlight == null || itemNameText == null)
            return false;

        if (slotBackgrounds.Count != hotbarSize || slotIcons.Count != hotbarSize ||
            slotAmounts.Count != hotbarSize || slotBatteryFills.Count != hotbarSize)
            return false;

        for (int i = 0; i < hotbarSize; i++)
        {
            if (slotBackgrounds[i] == null || slotIcons[i] == null ||
                slotAmounts[i] == null || slotBatteryFills[i] == null)
                return false;
        }

        return true;
    }

    private void EnsureHUDIntegrity()
    {
        if (!IsHUDComplete())
        {
            if (hudCanvas != null) Destroy(hudCanvas);
            GenerateHUD();
            return;
        }

        // Recover from an accidental/stale deactivation while respecting an
        // intentional hide requested by the spectator UI.
        if (hudCanvas.activeSelf != hudShouldBeVisible)
        {
            hudCanvas.SetActive(hudShouldBeVisible);
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureHUDIntegrity();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (instance == this) instance = null;
    }

    private void CreateBorderLine(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size)
    {
        GameObject line = new GameObject(name);
        line.transform.SetParent(parent, false);
        RectTransform rt = line.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.pivot = pivot; rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
        Image img = line.AddComponent<Image>();
        img.color = Color.white;
        img.raycastTarget = false;
    }

    private void FindLocalPlayerCache()
    {
        if (localInventory != null &&
            (localInventory.Object == null || !localInventory.Object.IsValid || !localInventory.HasInputAuthority))
        {
            localInventory = null;
            localSurvival = null;
        }

        if (localInventory == null || localSurvival == null)
        {
            foreach (var ph in FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None))
            {
                if (ph.HasInputAuthority)
                {
                    localSurvival = ph.GetComponent<PlayerSurvival>();
                    localInventory = ph.GetComponent<InventorySystem>();
                    return;
                }
            }
        }
    }

    private void Update()
    {
        EnsureHUDIntegrity();
        FindLocalPlayerCache();
        HandleInput();
        UpdateUI();
    }

    private void HandleInput()
    {
        if (VehicleRepairSkillCheckUI.BlocksGameplayInput) return;

        bool isTyping = AutoChatManager.Instance != null && AutoChatManager.Instance.IsTyping();
        bool isMenuOpen = AutoUIManager.Instance != null && AutoUIManager.Instance.IsAnyMenuOpen();

        if (isTyping || isMenuOpen) return;

        // Shift+1..4 is reserved for multiplayer vehicle seat switching.
        bool seatModifierHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        for (int i = 0; i < hotbarSize && !seatModifierHeld; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i)) selectedSlotIndex = i;
        }

        // Lăn chuột: Đã tắt theo yêu cầu vì trùng lặp với Zoom Camera
        /*
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll > 0f)
        {
            selectedSlotIndex = (selectedSlotIndex - 1 + hotbarSize) % hotbarSize;
        }
        else if (scroll < 0f)
        {
            selectedSlotIndex = (selectedSlotIndex + 1) % hotbarSize;
        }
        */

        // Chuột trái: CHỈ dùng nhanh Nhu yếu phẩm ĐỒ ĂN / NƯỚC UỐNG (Không dùng Bandage, Y tế hay Đạn!)
        if (Input.GetMouseButtonDown(0) && EventSystem.current != null && !EventSystem.current.IsPointerOverGameObject())
        {
            FlashlightController flashlight = localInventory != null ? localInventory.GetComponent<FlashlightController>() : null;
            if (flashlight != null && flashlight.TryToggleFromHotbar(selectedSlotIndex)) return;
            if (localInventory != null && selectedSlotIndex < localInventory.slots.Count)
            {
                var slot = localInventory.slots[selectedSlotIndex];
                if (slot != null && slot.item != null && slot.amount > 0)
                {
                    ItemData item = slot.item;
                    // CHỈ Cho phép sử dụng Đồ Ăn & Nước Uống
                    bool isFoodOrWater = (item.hungerRestore > 0 || item.thirstRestore > 0) &&
                                         item.category != ItemCategory.Medical &&
                                         item.category != ItemCategory.Ammunition;

                    if (isFoodOrWater && AutoUIManager.Instance != null && !AutoUIManager.Instance.isDoingAction)
                    {
                        AutoUIManager.Instance.StartItemUseFromHotbar(selectedSlotIndex, item);
                    }
                }
            }
        }
    }

    private void UpdateUI()
    {
        // Update Slots
        if (localInventory != null)
        {
            FlashlightController flashlight = localInventory.GetComponent<FlashlightController>();
            for (int i = 0; i < hotbarSize; i++)
            {
                if (i < localInventory.slots.Count && localInventory.slots[i].item != null && localInventory.slots[i].amount > 0)
                {
                    slotIcons[i].gameObject.SetActive(true);
                    slotIcons[i].sprite = localInventory.slots[i].item.icon;
                    if (localInventory.slots[i].amount > 1)
                    {
                        slotAmounts[i].gameObject.SetActive(true);
                        slotAmounts[i].text = localInventory.slots[i].amount.ToString();
                    }
                    else slotAmounts[i].gameObject.SetActive(false);

                    bool isFlashlight = localInventory.slots[i].item.name == FlashlightController.ItemId ||
                                       localInventory.slots[i].item.itemName == FlashlightController.ItemId;
                    slotBatteryFills[i].gameObject.SetActive(isFlashlight && flashlight != null);
                    if (isFlashlight && flashlight != null)
                    {
                        float battery01 = flashlight.DisplayBattery01;
                        slotBatteryFills[i].fillAmount = battery01;
                        slotBatteryFills[i].color = battery01 > 0.25f
                            ? new Color(0.35f, 0.95f, 0.45f, 0.95f)
                            : new Color(1f, 0.35f, 0.2f, 0.95f);
                    }
                }
                else
                {
                    slotIcons[i].gameObject.SetActive(false);
                    slotAmounts[i].gameObject.SetActive(false);
                    slotBatteryFills[i].gameObject.SetActive(false);
                }
            }
        }

        // Move Highlight
        if (slotBackgrounds.Count > selectedSlotIndex)
        {
            selectionHighlight.SetParent(slotBackgrounds[selectedSlotIndex].transform, false);
            selectionHighlight.anchorMin = Vector2.zero; selectionHighlight.anchorMax = Vector2.one;
            selectionHighlight.offsetMin = Vector2.zero; selectionHighlight.offsetMax = Vector2.zero;
            selectionHighlight.transform.SetAsFirstSibling(); // Đưa Highlight xuống dưới Icon để Icon đè lên trên nổi bật!
        }

        // Cập nhật tên Item hiển thị như Minecraft (hiện 2s rồi biến mất)
        if (selectedSlotIndex != lastSelectedSlotIndex)
        {
            lastSelectedSlotIndex = selectedSlotIndex;
            itemNameTimer = 0.5f; // Hiện tên trong 0.5 giây khi chuyển slot
        }

        if (itemNameText != null)
        {
            if (itemNameTimer > 0f)
            {
                itemNameTimer -= Time.deltaTime;
                if (localInventory != null && selectedSlotIndex >= 0 && selectedSlotIndex < localInventory.slots.Count)
                {
                    var slot = localInventory.slots[selectedSlotIndex];
                    if (slot != null && slot.item != null && slot.amount > 0)
                    {
                        itemNameText.text = GameLocalization.TranslateLiteral(slot.item.itemName);
                        itemNameText.gameObject.SetActive(true);
                    }
                    else
                    {
                        itemNameText.gameObject.SetActive(false);
                    }
                }
                else
                {
                    itemNameText.gameObject.SetActive(false);
                }
            }
            else
            {
                itemNameText.gameObject.SetActive(false);
            }
        }
    }

    public ItemData GetSelectedWeapon()
    {
        if (localInventory == null) return null;
        if (selectedSlotIndex < localInventory.slots.Count)
        {
            ItemData item = localInventory.slots[selectedSlotIndex].item;
            if (item != null && item.category == ItemCategory.Weapon)
            {
                return item;
            }
        }
        return null;
    }

    public bool HasGunEquipped()
    {
        return GetSelectedWeapon() != null;
    }

    public void SetHUDVisible(bool visible)
    {
        hudShouldBeVisible = visible;
        EnsureHUDIntegrity();
        if (hudCanvas != null)
        {
            hudCanvas.SetActive(visible);
        }
    }
}
