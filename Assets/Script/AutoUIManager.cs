using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

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

    private GameObject tooltipPanel;
    private TextMeshProUGUI tooltipTitleText;
    private TextMeshProUGUI tooltipDescText;
    private GameObject contextMenuPanel;
    private int selectedSlotIndex = -1;

    private Image healthFill;
    private Image staminaFill;
    private Image hungerFill;
    private Image thirstFill;

    private PlayerHealth playerHealth;
    private PlayerStamina playerStamina;
    private PlayerSurvival playerSurvival;

    private GameObject actionBarPanel;
    private Image actionBarFill;
    private TextMeshProUGUI actionBarText;
    private bool isDoingAction = false;

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
        // 1. Phím Tab hoặc I để Bật/Tắt túi đồ
        if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.I))
        {
            if (inventoryPanel != null) inventoryPanel.SetActive(!inventoryPanel.activeSelf);
            HideContextMenu();
            HideTooltip();
        }

        // 🔥 2. MỚI: Phím ESC để TẮT túi đồ
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (inventoryPanel != null && inventoryPanel.activeSelf)
            {
                inventoryPanel.SetActive(false);
                HideContextMenu();
                HideTooltip();
            }
        }

        // 🔥 3. MỚI: Bấm Chuột Phải để TẮT cái Context Menu (Use/Drop)
        if (Input.GetMouseButtonDown(1))
        {
            if (contextMenuPanel != null && contextMenuPanel.activeSelf)
            {
                HideContextMenu();
            }
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

        GenerateSurvivalBars(canvasGO);
        GenerateInventoryUI(canvasGO);
        GenerateActionBar(canvasGO);
    }

    private void GenerateInventoryUI(GameObject canvasGO)
    {
        inventoryPanel = new GameObject("InventoryPanel");
        inventoryPanel.transform.SetParent(canvasGO.transform, false);
        RectTransform panelRect = inventoryPanel.AddComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(500, 450);
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;

        Image panelBg = inventoryPanel.AddComponent<Image>();
        panelBg.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);
        inventoryPanel.SetActive(false);

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

        GameObject gridObj = new GameObject("SlotGrid");
        gridObj.transform.SetParent(inventoryPanel.transform, false);
        RectTransform gridRect = gridObj.AddComponent<RectTransform>();
        gridRect.anchorMin = new Vector2(0, 0); gridRect.anchorMax = new Vector2(1, 1);
        gridRect.offsetMin = new Vector2(20, 20);
        gridRect.offsetMax = new Vector2(-20, -70);

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
            iconImg.preserveAspect = true;

            RectTransform iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.anchorMin = Vector2.zero; iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = new Vector2(10, 10); iconRect.offsetMax = new Vector2(-10, -10);
            iconObj.SetActive(false);

            GameObject textObj = new GameObject("AmountText");
            textObj.transform.SetParent(slotObj.transform, false);
            TextMeshProUGUI amountTxt = textObj.AddComponent<TextMeshProUGUI>();
            if (gameFont != null) amountTxt.font = gameFont;
            amountTxt.fontSize = 12; amountTxt.fontStyle = FontStyles.Bold;
            amountTxt.alignment = TextAlignmentOptions.BottomRight;
            amountTxt.color = Color.white;
            amountTxt.margin = new Vector4(0, 0, 0, 0);

            textObj.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.8f);
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(0, 0); textRect.offsetMax = new Vector2(-1, 1);
            amountTxt.raycastTarget = false;
            textObj.SetActive(false);

            slotUIList.Add(new SlotUIElements { iconImage = iconImg, amountText = amountTxt });
        }

        tooltipPanel = new GameObject("TooltipPanel");
        tooltipPanel.transform.SetParent(canvasGO.transform, false);
        tooltipPanel.transform.SetAsLastSibling();
        RectTransform ttRect = tooltipPanel.AddComponent<RectTransform>();
        ttRect.sizeDelta = new Vector2(180, 60); ttRect.pivot = new Vector2(0, 1);
        Image ttBg = tooltipPanel.AddComponent<Image>();
        ttBg.color = new Color(0, 0, 0, 0.95f); ttBg.raycastTarget = false;
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

        contextMenuPanel = new GameObject("ContextMenu");
        contextMenuPanel.transform.SetParent(canvasGO.transform, false);
        contextMenuPanel.transform.SetAsLastSibling();
        RectTransform ctxRect = contextMenuPanel.AddComponent<RectTransform>();
        ctxRect.sizeDelta = new Vector2(120, 85); ctxRect.pivot = new Vector2(0, 1);

        // 🔥 ĐÃ SỬA: Đổi màu nền và viền y hệt Tooltip (Đen trong suốt, Viền Trắng)
        contextMenuPanel.AddComponent<Image>().color = new Color(0, 0, 0, 0.95f);
        contextMenuPanel.AddComponent<Outline>().effectColor = Color.white;

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

        Image btnImage = btnObj.AddComponent<Image>();
        btnImage.color = Color.white;

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = btnImage;

        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = text == "Drop" ? new Color(0.85f, 0.2f, 0.2f, 1f) : new Color(0.15f, 0.5f, 0.85f, 1f);
        cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        cb.selectedColor = Color.white;
        cb.fadeDuration = 0.15f;
        btn.colors = cb;
        btn.onClick.AddListener(action);

        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(btnObj.transform, false);
        TextMeshProUGUI txt = txtObj.AddComponent<TextMeshProUGUI>();
        txt.text = text;
        txt.fontSize = 15;
        txt.fontStyle = FontStyles.Bold;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = new Color(0.15f, 0.15f, 0.15f, 1f);

        RectTransform txtRect = txtObj.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero; txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = Vector2.zero; txtRect.offsetMax = Vector2.zero;
    }

    private void OnUseClicked()
    {
        int indexToUse = selectedSlotIndex;
        HideContextMenu();

        if (indexToUse != -1)
        {
            if (isDoingAction)
            {
                Debug.Log("Đang bận sử dụng món khác!");
                return;
            }

            ItemData itemToUse = currentSlots[indexToUse].item;

            if (itemToUse.category == ItemCategory.Ammunition)
            {
                Debug.Log("Không thể dùng đạn từ đây! Hãy cầm súng và nhấn R để nạp.");
                return;
            }

            if (itemToUse.category == ItemCategory.Medical)
            {
                if (playerHealth == null) playerHealth = Object.FindAnyObjectByType<PlayerHealth>();

                if (playerHealth != null && playerHealth.currentHealth >= playerHealth.maxHealth)
                {
                    Debug.Log("Máu đang đầy, không cần dùng đồ y tế!");
                    return;
                }
            }

            if (itemToUse.useTime > 0)
            {
                StartCoroutine(ActionTimerRoutine(indexToUse, itemToUse));
            }
            else
            {
                InventorySystem inv = Object.FindAnyObjectByType<InventorySystem>();
                if (inv != null) inv.UseItem(indexToUse);
            }
        }
    }

    private void GenerateActionBar(GameObject canvasGO)
    {
        actionBarPanel = new GameObject("ActionBarPanel");
        actionBarPanel.transform.SetParent(canvasGO.transform, false);
        RectTransform actRect = actionBarPanel.AddComponent<RectTransform>();

        actRect.anchorMin = new Vector2(0.5f, 0.2f);
        actRect.anchorMax = new Vector2(0.5f, 0.2f);
        actRect.pivot = new Vector2(0.5f, 0.5f);
        actRect.sizeDelta = new Vector2(250, 25);
        actRect.anchoredPosition = Vector2.zero;

        Image bg = actionBarPanel.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
        actionBarPanel.AddComponent<Outline>().effectColor = Color.black;

        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(actionBarPanel.transform, false);
        RectTransform fillRect = fillObj.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(2, 2); fillRect.offsetMax = new Vector2(-2, -2);

        actionBarFill = fillObj.AddComponent<Image>();
        actionBarFill.color = new Color(0.2f, 0.8f, 0.3f, 1f);
        actionBarFill.type = Image.Type.Filled;
        actionBarFill.fillMethod = Image.FillMethod.Horizontal;
        actionBarFill.fillAmount = 0f;

        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(actionBarPanel.transform, false);
        actionBarText = txtObj.AddComponent<TextMeshProUGUI>();
        if (gameFont != null) actionBarText.font = gameFont;
        actionBarText.fontSize = 14;
        actionBarText.fontStyle = FontStyles.Bold;
        actionBarText.alignment = TextAlignmentOptions.Center;
        actionBarText.color = Color.white;
        RectTransform txtRect = txtObj.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero; txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = Vector2.zero; txtRect.offsetMax = Vector2.zero;
        actionBarText.AddComponent<Shadow>().effectColor = Color.black;

        actionBarPanel.SetActive(false);
    }

    private IEnumerator ActionTimerRoutine(int slotIndex, ItemData itemToUse)
    {
        isDoingAction = true;
        actionBarPanel.SetActive(true);
        actionBarFill.fillAmount = 0f;

        if (inventoryPanel != null) inventoryPanel.SetActive(false);

        PlayerMovement pm = Object.FindAnyObjectByType<PlayerMovement>();
        if (pm != null) pm.isUsingItem = true;

        float timer = 0f;
        float duration = itemToUse.useTime;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            actionBarFill.fillAmount = timer / duration;

            float timeLeft = duration - timer;
            actionBarText.text = $"Using {itemToUse.itemName}... {timeLeft:F1}s";

            if (Input.GetKey(KeyCode.LeftShift) || Input.GetMouseButton(1) || Input.GetMouseButton(0))
            {
                actionBarPanel.SetActive(false);
                isDoingAction = false;
                if (pm != null) pm.isUsingItem = false;

                Debug.Log("Đã hủy dùng đồ!");
                yield break;
            }

            yield return null;
        }

        actionBarPanel.SetActive(false);
        isDoingAction = false;
        if (pm != null) pm.isUsingItem = false;

        InventorySystem inv = Object.FindAnyObjectByType<InventorySystem>();
        if (inv != null) inv.UseItem(slotIndex);
    }

    private void GenerateSurvivalBars(GameObject canvasGO)
    {
        GameObject combatPanel = new GameObject("CombatStatsPanel", typeof(RectTransform));
        combatPanel.transform.SetParent(canvasGO.transform, false);
        RectTransform combatRect = combatPanel.GetComponent<RectTransform>();
        combatRect.anchorMin = new Vector2(0, 1); combatRect.anchorMax = new Vector2(0, 1);
        combatRect.pivot = new Vector2(0, 1); combatRect.anchoredPosition = new Vector2(10, 10);

        VerticalLayoutGroup combatLayout = combatPanel.AddComponent<VerticalLayoutGroup>();
        combatLayout.spacing = -50;
        combatLayout.childControlHeight = true; combatLayout.childControlWidth = true;
        combatLayout.childForceExpandHeight = false; combatLayout.childForceExpandWidth = false;

        healthFill = CreateHorizontalBar(combatPanel.transform, "Health", iconHealth, new Color(0.8f, 0.15f, 0.15f), 220, 22);
        staminaFill = CreateHorizontalBar(combatPanel.transform, "Stamina", iconStamina, new Color(0.9f, 0.7f, 0.1f), 220, 8);

        GameObject survivalPanel = new GameObject("SurvivalStatsPanel", typeof(RectTransform));
        survivalPanel.transform.SetParent(canvasGO.transform, false);
        RectTransform survivalRect = survivalPanel.GetComponent<RectTransform>();
        survivalRect.anchorMin = new Vector2(1, 0); survivalRect.anchorMax = new Vector2(1, 0);
        survivalRect.pivot = new Vector2(1, 0); survivalRect.anchoredPosition = new Vector2(0, 0);

        HorizontalLayoutGroup survivalLayout = survivalPanel.AddComponent<HorizontalLayoutGroup>();
        survivalLayout.spacing = -25;
        survivalLayout.childAlignment = TextAnchor.LowerRight;
        survivalLayout.childControlHeight = true; survivalLayout.childControlWidth = true;
        survivalLayout.childForceExpandHeight = false; survivalLayout.childForceExpandWidth = false;

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

        GameObject iconObj = new GameObject("Icon", typeof(RectTransform));
        iconObj.transform.SetParent(container.transform, false);
        RectTransform iconRect = iconObj.GetComponent<RectTransform>();
        iconRect.sizeDelta = new Vector2(30, 30);

        LayoutElement iconLayout = iconObj.AddComponent<LayoutElement>();
        iconLayout.minWidth = 35; iconLayout.minHeight = 35;

        Image iconImg = iconObj.AddComponent<Image>();
        iconImg.preserveAspect = true;
        if (icon != null) iconImg.sprite = icon; else iconImg.color = fillColor;

        GameObject bgObj = new GameObject("BarBG", typeof(RectTransform));
        bgObj.transform.SetParent(container.transform, false);
        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.sizeDelta = new Vector2(barWidth, barHeight);
        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);
        bgObj.AddComponent<Outline>().effectColor = Color.black;

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

        GameObject bgObj = new GameObject("BarBG", typeof(RectTransform));
        bgObj.transform.SetParent(container.transform, false);
        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.sizeDelta = new Vector2(barWidth, barHeight);
        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);
        bgObj.AddComponent<Outline>().effectColor = Color.black;

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

        GameObject iconObj = new GameObject("Icon", typeof(RectTransform));
        iconObj.transform.SetParent(container.transform, false);
        RectTransform iconRect = iconObj.GetComponent<RectTransform>();
        iconRect.sizeDelta = new Vector2(35, 35);

        LayoutElement iconLayout = iconObj.AddComponent<LayoutElement>();
        iconLayout.minWidth = 35; iconLayout.minHeight = 35;

        Image iconImg = iconObj.AddComponent<Image>();
        iconImg.preserveAspect = true;
        if (icon != null) iconImg.sprite = icon; else iconImg.color = fillColor;

        bgObj.transform.SetAsFirstSibling();

        return fillImg;
    }

    private void UpdateSurvivalUI()
    {
        if (playerHealth == null) playerHealth = Object.FindAnyObjectByType<PlayerHealth>();
        if (playerStamina == null) playerStamina = Object.FindAnyObjectByType<PlayerStamina>();
        if (playerSurvival == null) playerSurvival = Object.FindAnyObjectByType<PlayerSurvival>();

        if (playerHealth != null && healthFill != null)
            UpdateHorizontalBar(healthFill, playerHealth.currentHealth, playerHealth.maxHealth, 220f);

        if (playerStamina != null && staminaFill != null)
            UpdateHorizontalBar(staminaFill, playerStamina.currentStamina, playerStamina.maxStamina, 220f);

        if (playerSurvival != null)
        {
            if (hungerFill != null) hungerFill.fillAmount = Mathf.Clamp01(playerSurvival.currentHunger / playerSurvival.maxHunger);
            if (thirstFill != null) thirstFill.fillAmount = Mathf.Clamp01(playerSurvival.currentThirst / playerSurvival.maxThirst);
        }
    }

    private void UpdateHorizontalBar(Image fillImg, float currentVal, float maxVal, float baseWidth)
    {
        if (maxVal <= 0) maxVal = 1f;
        float ratio = currentVal / maxVal;

        RectTransform bgRect = fillImg.transform.parent.GetComponent<RectTransform>();
        LayoutElement bgLayout = bgRect.GetComponent<LayoutElement>();
        if (bgLayout == null) bgLayout = bgRect.gameObject.AddComponent<LayoutElement>();

        if (ratio > 1f)
        {
            bgLayout.preferredWidth = baseWidth * ratio;
            fillImg.fillAmount = 1f;
        }
        else
        {
            bgLayout.preferredWidth = baseWidth;
            fillImg.fillAmount = ratio;
        }
    }

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

    public void HideTooltip() 
    { 
        if (tooltipPanel != null) tooltipPanel.SetActive(false); 
    }

    public void ShowContextMenu(int index)
    {
        if (currentSlots != null && index < currentSlots.Count && currentSlots[index] != null && currentSlots[index].amount > 0)
        {
            HideTooltip(); selectedSlotIndex = index;
            Vector2 mousePos; RectTransformUtility.ScreenPointToLocalPointInRectangle(mainCanvas.transform as RectTransform, Input.mousePosition, mainCanvas.worldCamera, out mousePos);
            contextMenuPanel.transform.localPosition = mousePos; contextMenuPanel.SetActive(true);
        }
    }

    public void HideContextMenu() 
    { 
        if (contextMenuPanel != null) contextMenuPanel.SetActive(false); 
        selectedSlotIndex = -1; 
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

    // 🔥 MỚI: Hàm này để các script khác hỏi xem Túi đồ có đang mở không
    public bool IsInventoryOpen()
    {
        return inventoryPanel != null && inventoryPanel.activeSelf;
    }
}