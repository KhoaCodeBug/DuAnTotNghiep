using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class AutoUIManager : MonoBehaviour
{
    public static AutoUIManager Instance { get; private set; }

    [Header("Cài đặt tự động")]
    public int maxSlots = 20;

    [Header("Cài đặt Chữ (Tùy chọn)")]
    public TMP_FontAsset gameFont;

    [Header("Icons Chỉ Số (Kéo hình vào đây)")]
    public Sprite iconHealth;
    public Sprite iconStamina;
    public Sprite iconHunger;
    public Sprite iconThirst;

    private Canvas mainCanvas;
    private GameObject inventoryPanel;
    private List<SlotUIElements> slotUIList = new List<SlotUIElements>();
    private List<InventorySlot> currentSlots = new List<InventorySlot>();

    // Biến cho Tooltip & Menu
    private GameObject tooltipPanel;
    private TextMeshProUGUI tooltipTitleText;
    private TextMeshProUGUI tooltipDescText;
    private GameObject contextMenuPanel;
    private int selectedSlotIndex = -1;

    // --- BIẾN CHO SURVIVAL UI ---
    private Image healthFill;
    private Image staminaFill;
    private Image hungerFill;
    private Image thirstFill;

    private PlayerHealth playerHealth;
    private PlayerStamina playerStamina;
    private PlayerSurvival playerSurvival;

    private class SlotUIElements
    {
        public Image iconImage;
        public TextMeshProUGUI amountText;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        GenerateEntireUI();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.I))
        {
            if (inventoryPanel != null) inventoryPanel.SetActive(!inventoryPanel.activeSelf);
            HideContextMenu();
        }

        if (tooltipPanel != null && tooltipPanel.activeSelf)
        {
            Vector2 mousePos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(mainCanvas.transform as RectTransform, Input.mousePosition, mainCanvas.worldCamera, out mousePos);
            tooltipPanel.transform.localPosition = mousePos + new Vector2(15, -15);
        }

        UpdateSurvivalUI();
    }

    private void GenerateEntireUI()
    {
        GameObject canvasGO = new GameObject("AutoCanvas");
        mainCanvas = canvasGO.AddComponent<Canvas>();
        mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGO.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(canvasGO);

        // 1. TẠO SURVIVAL UI
        GenerateSurvivalBars(canvasGO);

        // 2. TẠO TÚI ĐỒ (Inventory) - ĐÃ CĂN LẠI RA GIỮA
        inventoryPanel = new GameObject("InventoryPanel");
        inventoryPanel.transform.SetParent(canvasGO.transform, false);
        RectTransform panelRect = inventoryPanel.AddComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(500, 450);
        panelRect.anchorMin = new Vector2(0.5f, 0.5f); // Ép nằm chính giữa
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;

        Image panelBg = inventoryPanel.AddComponent<Image>();
        panelBg.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);
        inventoryPanel.SetActive(false);

        // --- TITLE ---
        GameObject titleObj = new GameObject("TitleText");
        titleObj.transform.SetParent(inventoryPanel.transform, false);
        TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
        if (gameFont != null) titleText.font = gameFont;
        titleText.text = "INVENTORY";
        titleText.fontSize = 24;
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = new Color(0.9f, 0.9f, 0.9f, 1f);
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 1); titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0.5f, 1);
        titleRect.anchoredPosition = new Vector2(0, -20);
        titleRect.sizeDelta = new Vector2(0, 40);

        // --- GRID ---
        GameObject gridObj = new GameObject("SlotGrid");
        gridObj.transform.SetParent(inventoryPanel.transform, false);
        RectTransform gridRect = gridObj.AddComponent<RectTransform>();
        gridRect.anchorMin = new Vector2(0, 0); gridRect.anchorMax = new Vector2(1, 1);
        gridRect.offsetMin = new Vector2(20, 20);
        gridRect.offsetMax = new Vector2(-20, -70); // ĐÃ SỬA: Trả lại -20 để lưới đồ đối xứng 100%

        GridLayoutGroup gridLayout = gridObj.AddComponent<GridLayoutGroup>();
        gridLayout.cellSize = new Vector2(75, 75);
        gridLayout.spacing = new Vector2(10, 10);
        gridLayout.childAlignment = TextAnchor.UpperCenter;

        for (int i = 0; i < maxSlots; i++)
        {
            GameObject slotObj = new GameObject("Slot_" + i);
            slotObj.transform.SetParent(gridObj.transform, false);
            Image slotBg = slotObj.AddComponent<Image>();
            slotBg.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            SlotHoverHandler hoverHandler = slotObj.AddComponent<SlotHoverHandler>();
            hoverHandler.slotIndex = i;

            GameObject iconObj = new GameObject("ItemIcon");
            iconObj.transform.SetParent(slotObj.transform, false);
            Image iconImg = iconObj.AddComponent<Image>();
            iconImg.raycastTarget = false;
            RectTransform iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.anchorMin = Vector2.zero; iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = new Vector2(10, 10); iconRect.offsetMax = new Vector2(-10, -10);
            iconObj.SetActive(false);

            GameObject textObj = new GameObject("AmountText");
            textObj.transform.SetParent(slotObj.transform, false);
            TextMeshProUGUI amountTxt = textObj.AddComponent<TextMeshProUGUI>();
            if (gameFont != null) amountTxt.font = gameFont;
            amountTxt.fontSize = 12;
            amountTxt.fontStyle = FontStyles.Bold;
            amountTxt.alignment = TextAlignmentOptions.BottomRight;
            amountTxt.color = Color.white;
            amountTxt.margin = new Vector4(0, 0, 0, 0);

            Shadow textShadow = textObj.AddComponent<Shadow>();
            textShadow.effectColor = new Color(0, 0, 0, 0.8f);
            textShadow.effectDistance = new Vector2(2, -2);

            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(0, 0); textRect.offsetMax = new Vector2(-1, 1);
            amountTxt.raycastTarget = false;
            textObj.SetActive(false);

            slotUIList.Add(new SlotUIElements { iconImage = iconImg, amountText = amountTxt });
        }

        // ================== TẠO BẢNG TOOLTIP ==================
        tooltipPanel = new GameObject("TooltipPanel");
        tooltipPanel.transform.SetParent(canvasGO.transform, false);
        tooltipPanel.transform.SetAsLastSibling();
        RectTransform ttRect = tooltipPanel.AddComponent<RectTransform>();
        ttRect.sizeDelta = new Vector2(180, 60);
        ttRect.pivot = new Vector2(0, 1);
        Image ttBg = tooltipPanel.AddComponent<Image>();
        ttBg.color = new Color(0, 0, 0, 0.95f);
        ttBg.raycastTarget = false;
        tooltipPanel.AddComponent<Outline>().effectColor = Color.white;

        GameObject ttTitleObj = new GameObject("TooltipTitle");
        ttTitleObj.transform.SetParent(tooltipPanel.transform, false);
        tooltipTitleText = ttTitleObj.AddComponent<TextMeshProUGUI>();
        tooltipTitleText.fontSize = 14; tooltipTitleText.fontStyle = FontStyles.Bold;
        tooltipTitleText.color = new Color(1f, 0.8f, 0.2f);
        RectTransform ttTitleRect = ttTitleObj.GetComponent<RectTransform>();
        ttTitleRect.anchorMin = new Vector2(0, 0.5f); ttTitleRect.anchorMax = new Vector2(1, 1);
        ttTitleRect.offsetMin = new Vector2(10, 0); ttTitleRect.offsetMax = new Vector2(-10, -5);

        GameObject ttDescObj = new GameObject("TooltipDesc");
        ttDescObj.transform.SetParent(tooltipPanel.transform, false);
        tooltipDescText = ttDescObj.AddComponent<TextMeshProUGUI>();
        tooltipDescText.fontSize = 12; tooltipDescText.color = Color.white;
        RectTransform ttDescRect = ttDescObj.GetComponent<RectTransform>();
        ttDescRect.anchorMin = new Vector2(0, 0); ttDescRect.anchorMax = new Vector2(1, 0.5f);
        ttDescRect.offsetMin = new Vector2(10, 5); ttDescRect.offsetMax = new Vector2(-10, 0);

        tooltipPanel.SetActive(false);

        // ================== TẠO BẢNG CONTEXT MENU (CHUỘT PHẢI) ==================
        contextMenuPanel = new GameObject("ContextMenu");
        contextMenuPanel.transform.SetParent(canvasGO.transform, false);
        contextMenuPanel.transform.SetAsLastSibling();
        RectTransform ctxRect = contextMenuPanel.AddComponent<RectTransform>();
        ctxRect.sizeDelta = new Vector2(120, 85); ctxRect.pivot = new Vector2(0, 1);
        contextMenuPanel.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.05f, 0.95f);
        contextMenuPanel.AddComponent<Outline>().effectColor = Color.gray;

        VerticalLayoutGroup ctxLayout = contextMenuPanel.AddComponent<VerticalLayoutGroup>();
        ctxLayout.padding = new RectOffset(5, 5, 5, 5); ctxLayout.spacing = 5;
        ctxLayout.childControlHeight = true; ctxLayout.childControlWidth = true;
        ctxLayout.childForceExpandHeight = true; ctxLayout.childForceExpandWidth = true;

        CreateContextMenuBtn("UseButton", "Use", OnUseClicked);
        CreateContextMenuBtn("DropButton", "Drop", OnDropClicked);
        contextMenuPanel.SetActive(false);
    }

    private void CreateContextMenuBtn(string objName, string text, UnityEngine.Events.UnityAction action)
    {
        GameObject btnObj = new GameObject(objName);
        btnObj.transform.SetParent(contextMenuPanel.transform, false);
        Button btn = btnObj.AddComponent<Button>();
        btnObj.AddComponent<Image>();
        ColorBlock cb = btn.colors;
        cb.normalColor = new Color(0.12f, 0.12f, 0.12f, 1f);
        cb.highlightedColor = text == "Drop" ? new Color(0.85f, 0.2f, 0.2f, 1f) : new Color(0.15f, 0.5f, 0.85f, 1f);
        cb.pressedColor = new Color(0.1f, 0.1f, 0.1f, 1f);
        cb.selectedColor = new Color(0.12f, 0.12f, 0.12f, 1f);
        cb.fadeDuration = 0.15f; btn.colors = cb;
        btn.onClick.AddListener(action);

        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(btnObj.transform, false);
        TextMeshProUGUI txt = txtObj.AddComponent<TextMeshProUGUI>();
        txt.text = text; txt.fontSize = 15; txt.fontStyle = FontStyles.Bold;
        txt.alignment = TextAlignmentOptions.Center; txt.color = Color.white;
        RectTransform txtRect = txtObj.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero; txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = Vector2.zero; txtRect.offsetMax = Vector2.zero;
    }

    // ================== HÀM VẼ GIAO DIỆN SINH TỒN ==================
    private void GenerateSurvivalBars(GameObject canvasGO)
    {
        // -------------------------------------------------------------
        // 1. CỤM MÁU VÀ THỂ LỰC (GÓC TRÊN CÙNG BÊN TRÁI)
        // -------------------------------------------------------------
        GameObject combatPanel = new GameObject("CombatStatsPanel", typeof(RectTransform));
        combatPanel.transform.SetParent(canvasGO.transform, false);
        RectTransform combatRect = combatPanel.GetComponent<RectTransform>();
        combatRect.anchorMin = new Vector2(0, 1); combatRect.anchorMax = new Vector2(0, 1);
        combatRect.pivot = new Vector2(0, 1); combatRect.anchoredPosition = new Vector2(10, 10);

        VerticalLayoutGroup combatLayout = combatPanel.AddComponent<VerticalLayoutGroup>();
        combatLayout.spacing = -50; // ĐÃ SỬA: Ép sát Stamina vào Health không hở 1 pixel nào
        combatLayout.childControlHeight = true; combatLayout.childControlWidth = true;
        combatLayout.childForceExpandHeight = false; combatLayout.childForceExpandWidth = false;

        healthFill = CreateHorizontalBar(combatPanel.transform, "Health", iconHealth, new Color(0.8f, 0.15f, 0.15f), 220, 22);
        staminaFill = CreateHorizontalBar(combatPanel.transform, "Stamina", iconStamina, new Color(0.9f, 0.7f, 0.1f), 220, 8);
        // -------------------------------------------------------------
        // 2. CỤM ĐÓI VÀ KHÁT (GÓC DƯỚI CÙNG BÊN PHẢI)
        // -------------------------------------------------------------
        GameObject survivalPanel = new GameObject("SurvivalStatsPanel", typeof(RectTransform));
        survivalPanel.transform.SetParent(canvasGO.transform, false);
        RectTransform survivalRect = survivalPanel.GetComponent<RectTransform>();
        survivalRect.anchorMin = new Vector2(1, 0);
        survivalRect.anchorMax = new Vector2(1, 0);
        survivalRect.pivot = new Vector2(1, 0);
        survivalRect.anchoredPosition = new Vector2(0, 0);

        HorizontalLayoutGroup survivalLayout = survivalPanel.AddComponent<HorizontalLayoutGroup>();
        survivalLayout.spacing = -25;
        survivalLayout.childAlignment = TextAnchor.LowerRight;
        survivalLayout.childControlHeight = true; survivalLayout.childControlWidth = true;
        survivalLayout.childForceExpandHeight = false; survivalLayout.childForceExpandWidth = false;

        // Vẽ 2 thanh thẳng đứng đều nhau tăm tắp
        hungerFill = CreateVerticalBar(survivalPanel.transform, "Hunger", iconHunger, new Color(0.2f, 0.7f, 0.2f), 15, 150);
        thirstFill = CreateVerticalBar(survivalPanel.transform, "Thirst", iconThirst, new Color(0.15f, 0.5f, 0.9f), 15, 150);
    }

    private Image CreateHorizontalBar(Transform parent, string name, Sprite icon, Color fillColor, float barWidth, float barHeight)
    {
        GameObject container = new GameObject(name + "_Container", typeof(RectTransform));
        container.transform.SetParent(parent, false);
        HorizontalLayoutGroup hLayout = container.AddComponent<HorizontalLayoutGroup>();
        hLayout.childAlignment = TextAnchor.MiddleLeft; hLayout.spacing = 8;
        hLayout.childControlHeight = false; hLayout.childControlWidth = false;

        // Vẽ Icon (Đã khóa cứng kích thước và tỷ lệ chống méo)
        GameObject iconObj = new GameObject("Icon", typeof(RectTransform));
        iconObj.transform.SetParent(container.transform, false);
        RectTransform iconRect = iconObj.GetComponent<RectTransform>();
        iconRect.sizeDelta = new Vector2(30, 30); // Icon to rõ

        LayoutElement iconLayout = iconObj.AddComponent<LayoutElement>();
        iconLayout.minWidth = 35;
        iconLayout.minHeight = 35;

        Image iconImg = iconObj.AddComponent<Image>();
        iconImg.preserveAspect = true; // BẢO VỆ TỶ LỆ ẢNH CHỐNG GIÃN
        if (icon != null) iconImg.sprite = icon; else iconImg.color = fillColor;

        // Vẽ Thanh
        GameObject bgObj = new GameObject("BarBG", typeof(RectTransform));
        bgObj.transform.SetParent(container.transform, false);
        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.sizeDelta = new Vector2(barWidth, barHeight);
        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);
        bgObj.AddComponent<Outline>().effectColor = Color.black;

        // Vẽ Fill 
        GameObject fillObj = new GameObject("Fill", typeof(RectTransform));
        fillObj.transform.SetParent(bgObj.transform, false);
        RectTransform fillRect = fillObj.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero; fillRect.offsetMax = Vector2.zero;

        Image fillImg = fillObj.AddComponent<Image>();
        Texture2D whiteTex = Texture2D.whiteTexture;
        fillImg.sprite = Sprite.Create(whiteTex, new Rect(0, 0, whiteTex.width, whiteTex.height), Vector2.zero);
        fillImg.color = fillColor; fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal; fillImg.fillAmount = 1f;

        return fillImg;
    }

    private Image CreateVerticalBar(Transform parent, string name, Sprite icon, Color fillColor, float barWidth, float barHeight)
    {
        GameObject container = new GameObject(name + "_Container", typeof(RectTransform));
        container.transform.SetParent(parent, false);
        VerticalLayoutGroup vLayout = container.AddComponent<VerticalLayoutGroup>();
        vLayout.childAlignment = TextAnchor.LowerCenter; vLayout.spacing = 6;
        vLayout.childControlHeight = false; vLayout.childControlWidth = false;

        // Vẽ Thanh
        GameObject bgObj = new GameObject("BarBG", typeof(RectTransform));
        bgObj.transform.SetParent(container.transform, false);
        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.sizeDelta = new Vector2(barWidth, barHeight);
        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);
        bgObj.AddComponent<Outline>().effectColor = Color.black;

        // Vẽ Fill 
        GameObject fillObj = new GameObject("Fill", typeof(RectTransform));
        fillObj.transform.SetParent(bgObj.transform, false);
        RectTransform fillRect = fillObj.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero; fillRect.offsetMax = Vector2.zero;

        Image fillImg = fillObj.AddComponent<Image>();
        Texture2D whiteTex = Texture2D.whiteTexture;
        fillImg.sprite = Sprite.Create(whiteTex, new Rect(0, 0, whiteTex.width, whiteTex.height), Vector2.zero);
        fillImg.color = fillColor; fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Vertical;
        fillImg.fillOrigin = (int)Image.OriginVertical.Bottom;
        fillImg.fillAmount = 1f;

        // Vẽ Icon (Đã khóa cứng kích thước và tỷ lệ chống méo)
        GameObject iconObj = new GameObject("Icon", typeof(RectTransform));
        iconObj.transform.SetParent(container.transform, false);
        RectTransform iconRect = iconObj.GetComponent<RectTransform>();
        iconRect.sizeDelta = new Vector2(35, 35);

        LayoutElement iconLayout = iconObj.AddComponent<LayoutElement>();
        iconLayout.minWidth = 35;
        iconLayout.minHeight = 35;

        Image iconImg = iconObj.AddComponent<Image>();
        iconImg.preserveAspect = true; // BẢO VỆ TỶ LỆ ẢNH CHỐNG GIÃN
        if (icon != null) iconImg.sprite = icon; else iconImg.color = fillColor;

        bgObj.transform.SetAsFirstSibling();

        return fillImg;
    }

    private void UpdateSurvivalUI()
    {
        if (playerHealth == null) playerHealth = Object.FindAnyObjectByType<PlayerHealth>();
        if (playerStamina == null) playerStamina = Object.FindAnyObjectByType<PlayerStamina>();
        if (playerSurvival == null) playerSurvival = Object.FindAnyObjectByType<PlayerSurvival>();

        if (playerHealth != null && healthFill != null) healthFill.fillAmount = playerHealth.currentHealth / playerHealth.maxHealth;
        if (playerStamina != null && staminaFill != null) staminaFill.fillAmount = playerStamina.currentStamina / playerStamina.maxStamina;
        if (playerSurvival != null)
        {
            if (hungerFill != null) hungerFill.fillAmount = playerSurvival.currentHunger / playerSurvival.maxHunger;
            if (thirstFill != null) thirstFill.fillAmount = playerSurvival.currentThirst / playerSurvival.maxThirst;
        }
    }

    // ================== CÁC HÀM CŨ (KHÔNG ĐỔI) ==================
    public void RefreshUI(List<InventorySlot> playerSlots)
    {
        currentSlots = playerSlots;
        for (int i = 0; i < maxSlots; i++)
        {
            SlotUIElements ui = slotUIList[i];
            if (i < playerSlots.Count && playerSlots[i] != null && playerSlots[i].amount > 0)
            {
                ui.iconImage.gameObject.SetActive(true); ui.iconImage.sprite = playerSlots[i].item.icon;
                if (playerSlots[i].amount > 1) { ui.amountText.gameObject.SetActive(true); ui.amountText.text = playerSlots[i].amount.ToString(); }
                else ui.amountText.gameObject.SetActive(false);
            }
            else { ui.iconImage.gameObject.SetActive(false); ui.amountText.gameObject.SetActive(false); }
        }
    }

    public void ShowTooltip(int index)
    {
        if (contextMenuPanel != null && contextMenuPanel.activeSelf) return;
        if (currentSlots != null && index < currentSlots.Count && currentSlots[index] != null && currentSlots[index].amount > 0)
        {
            tooltipTitleText.text = currentSlots[index].item.itemName;
            string categoryName = "";
            switch (currentSlots[index].item.category)
            {
                case ItemCategory.Ammunition: categoryName = "Ammunition"; break;
                case ItemCategory.Medical: categoryName = "Medical Supplies"; break;
                case ItemCategory.Consumable: categoryName = "Consumables"; break;
            }
            tooltipDescText.text = "Type: " + categoryName; tooltipPanel.SetActive(true);
        }
    }

    public void HideTooltip() { if (tooltipPanel != null) tooltipPanel.SetActive(false); }
    public void ShowContextMenu(int index)
    {
        if (currentSlots != null && index < currentSlots.Count && currentSlots[index] != null && currentSlots[index].amount > 0)
        {
            HideTooltip(); selectedSlotIndex = index;
            Vector2 mousePos; RectTransformUtility.ScreenPointToLocalPointInRectangle(mainCanvas.transform as RectTransform, Input.mousePosition, mainCanvas.worldCamera, out mousePos);
            contextMenuPanel.transform.localPosition = mousePos; contextMenuPanel.SetActive(true);
        }
    }
    public void HideContextMenu() { if (contextMenuPanel != null) contextMenuPanel.SetActive(false); selectedSlotIndex = -1; }
    private void OnUseClicked()
    {
        int indexToUse = selectedSlotIndex; HideContextMenu();
        if (indexToUse != -1)
        {
            InventorySystem inv = Object.FindAnyObjectByType<InventorySystem>();
            if (inv != null) inv.UseItem(indexToUse); else Debug.LogError("LỖI UI: Không tìm thấy InventorySystem!");
        }
    }
    private void OnDropClicked()
    {
        int indexToDrop = selectedSlotIndex; HideContextMenu();
        if (indexToDrop != -1)
        {
            InventorySystem inv = Object.FindAnyObjectByType<InventorySystem>();
            if (inv != null) inv.DropItem(indexToDrop);
        }
    }
}