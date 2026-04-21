using Fusion;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AutoUIManager : MonoBehaviour
{
    public static AutoUIManager Instance { get; private set; }

    #region Cài Đặt Chung
    [Header("Cài đặt")]
    public int maxSlots = 20;
    private int containerSlots = 9; // Tủ đồ 3x3
    public TMP_FontAsset gameFont;
    public Sprite iconAmmo;
    public TextMeshProUGUI clockText { get; private set; }
    private Canvas mainCanvas;
    #endregion

    #region Biến UI - Inventory & Action
    private GameObject inventoryPanel, tooltipPanel, contextMenuPanel, actionBarPanel, ammoContainer;
    private TextMeshProUGUI tooltipTitleText, tooltipDescText, actionBarText, ammoText;
    private Image actionBarFill;
    private List<SlotUIElements> slotUIList = new List<SlotUIElements>();
    private List<InventorySlot> currentSlots = new List<InventorySlot>();
    private int selectedSlotIndex = -1;
    private float invToggleCooldown = 0f;
    public bool isDoingAction;
    #endregion

    #region Biến UI - Tủ Đồ
    private GameObject containerPanel;
    private List<SlotUIElements> containerSlotUIList = new List<SlotUIElements>();
    private LootContainer currentOpenContainer;
    #endregion

    #region Biến Nhân Vật
    private GameObject localPlayer;
    #endregion

    #region Biến UI - Giao Dịch
    private GameObject tradeRequestPanel;
    private PlayerRef pendingTradeSender;
    private PlayerRef pendingTradeTarget;

    private GameObject tradeWindowPanel;
    private Image myOfferIcon, partnerOfferIcon;
    private TextMeshProUGUI myOfferAmountTxt, partnerOfferAmountTxt, partnerStatusTxt;
    private Button btnReady, btnConfirm, btnPickItem;
    private TextMeshProUGUI btnReadyTxt;
    #endregion

    private class SlotUIElements
    {
        public Image iconImage;
        public TextMeshProUGUI amountText;
        public Button slotButton;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        GenerateEntireUI();
        CreateAmmoUI();
    }

    private void OnDestroy()
    {
        // 1. Phá hủy cái Canvas tổng (Chứa Balo, Bảng Trade, Đồng Hồ...)
        if (mainCanvas != null)
        {
            Destroy(mainCanvas.gameObject);
        }

        // 2. Phá hủy bảng Đạn (Nếu nó nằm ngoài Canvas tổng)
        if (ammoContainer != null)
        {
            Destroy(ammoContainer);
        }
    }

    private void Update()
    {
        if (AutoChatManager.Instance != null && AutoChatManager.Instance.IsTyping()) return;

        HandleInput();
        UpdateTradeWindowRealtime();
    }

    // 🔥 HÀM BẢO VỆ MULTIPLAYER: Đảm bảo luôn lấy đúng nhân vật của máy mình
    private void EnsureLocalPlayer()
    {
        if (localPlayer != null && localPlayer.GetComponent<NetworkObject>().HasInputAuthority) return;

        var players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        foreach (var pMove in players)
        {
            if (pMove.Object != null && pMove.HasInputAuthority)
            {
                localPlayer = pMove.gameObject;
                return;
            }
        }
    }

    private void HandleInput()
    {
        // 🔥 KIỂM TRA TRADE (CÓ BẢO VỆ MẠNG)
        EnsureLocalPlayer();
        PlayerTrade pt = localPlayer != null ? localPlayer.GetComponent<PlayerTrade>() : null;
        bool isTrading = false;

        // CHỐT CHẶN: Phải chắc chắn nhân vật đã được Fusion setup xong hoàn toàn mới đọc biến mạng
        if (pt != null && pt.Object != null && pt.Object.IsValid)
        {
            isTrading = pt.IsTrading;
        }

        if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.I))
        {
            if (Time.time < invToggleCooldown) return;

            bool isHealthHealing = AutoHealthPanel.Instance != null && AutoHealthPanel.Instance.IsHealing;
            if (isDoingAction || isHealthHealing) return;

            bool isHealthOpen = AutoHealthPanel.Instance != null && AutoHealthPanel.Instance.IsOpen;
            if (!inventoryPanel.activeSelf && isHealthOpen) return;

            invToggleCooldown = Time.time + 0.2f;

            // 🔥 FIX: Đang mở Balo để chọn đồ trade -> Bấm Tab/I -> Quay về bảng Trade
            if (isTrading && inventoryPanel != null && inventoryPanel.activeSelf)
            {
                inventoryPanel.SetActive(false);
                if (tradeWindowPanel != null) tradeWindowPanel.SetActive(true);
                HideContextMenu();
                HideTooltip();
                return; // Dừng tại đây, không chạy code đóng mở Balo thông thường ở dưới nữa
            }

            // Code đóng/mở Balo bình thường
            if (inventoryPanel != null)
            {
                bool newState = !inventoryPanel.activeSelf;
                inventoryPanel.SetActive(newState);

                if (ammoContainer != null) ammoContainer.SetActive(!newState);

                if (!newState)
                    CloseContainerUI();
                else
                    UpdatePanelsLayout();
            }
            HideContextMenu();
            HideTooltip();
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (Time.time < invToggleCooldown) return;

            // 🔥 FIX: Đang mở Balo để chọn đồ trade -> Bấm ESC -> Quay về bảng Trade
            if (isTrading && inventoryPanel != null && inventoryPanel.activeSelf)
            {
                invToggleCooldown = Time.time + 0.2f;
                inventoryPanel.SetActive(false);
                if (tradeWindowPanel != null) tradeWindowPanel.SetActive(true);
                HideContextMenu();
                HideTooltip();
                return; // Dừng tại đây
            }

            // Nút ESC thông thường (Đóng Balo / Tủ đồ)
            if ((inventoryPanel != null && inventoryPanel.activeSelf) || (containerPanel != null && containerPanel.activeSelf))
            {
                invToggleCooldown = Time.time + 0.2f;
                if (inventoryPanel != null) inventoryPanel.SetActive(false);

                if (ammoContainer != null) ammoContainer.SetActive(true);

                CloseContainerUI();
                HideContextMenu();
                HideTooltip();
            }

            // Đang ở bảng Trade (không mở balo) -> Bấm ESC -> Hủy Trade hoàn toàn
            if (tradeWindowPanel != null && tradeWindowPanel.activeSelf && localPlayer != null)
            {
                localPlayer.GetComponent<PlayerTrade>().CancelTrade();
            }
        }

        if (Input.GetMouseButtonDown(1))
        {
            if (contextMenuPanel != null && contextMenuPanel.activeSelf) HideContextMenu();
        }

        if (tooltipPanel != null && tooltipPanel.activeSelf)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                mainCanvas.transform as RectTransform, Input.mousePosition, mainCanvas.worldCamera, out Vector2 mousePos);
            tooltipPanel.transform.localPosition = mousePos + new Vector2(15, -15);
        }
    }

    private void GenerateEntireUI()
    {
        if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            DontDestroyOnLoad(eventSystem);
        }

        GameObject canvasGO = new GameObject("AutoCanvas");
        mainCanvas = canvasGO.AddComponent<Canvas>();
        mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

        canvasGO.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(canvasGO);

        GenerateInventoryUI(canvasGO);
        GenerateContainerUI(canvasGO);
        GenerateActionBar(canvasGO);
        GenerateTradeRequestUI(canvasGO);
        GenerateTradeWindowUI(canvasGO);
        GenerateClockUI(canvasGO);
    }

    private void UpdatePanelsLayout()
    {
        if (inventoryPanel != null)
        {
            RectTransform rect = inventoryPanel.GetComponent<RectTransform>();

            if (containerPanel != null && containerPanel.activeSelf)
            {
                rect.anchoredPosition = new Vector2(-150, 0); // Giữ khoảng cách sếp đã chỉnh
            }
            else
            {
                rect.anchoredPosition = Vector2.zero;
            }
        }
    }

    #region GIAO DIỆN TÚI ĐỒ (INVENTORY)
    private void GenerateInventoryUI(GameObject canvasGO)
    {
        inventoryPanel = new GameObject("InventoryPanel");
        inventoryPanel.transform.SetParent(canvasGO.transform, false);

        RectTransform panelRect = inventoryPanel.AddComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(500, 450);
        panelRect.localScale = new Vector3(0.85f, 0.85f, 1f);

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
        titleRect.pivot = new Vector2(0.5f, 1); titleRect.anchoredPosition = new Vector2(0, -20);
        titleRect.sizeDelta = new Vector2(0, 40);

        GameObject gridObj = new GameObject("SlotGrid");
        gridObj.transform.SetParent(inventoryPanel.transform, false);
        RectTransform gridRect = gridObj.AddComponent<RectTransform>();
        gridRect.anchorMin = new Vector2(0, 0); gridRect.anchorMax = new Vector2(1, 1);
        gridRect.offsetMin = new Vector2(20, 35);
        gridRect.offsetMax = new Vector2(-20, -70);

        GridLayoutGroup gridLayout = gridObj.AddComponent<GridLayoutGroup>();
        gridLayout.cellSize = new Vector2(75, 75); gridLayout.spacing = new Vector2(10, 10);
        gridLayout.childAlignment = TextAnchor.UpperCenter;

        for (int i = 0; i < maxSlots; i++)
        {
            int slotIndex = i;
            GameObject slotObj = new GameObject("Slot_" + i);
            slotObj.transform.SetParent(gridObj.transform, false);

            UISlotDragHandler drag = slotObj.AddComponent<UISlotDragHandler>();
            drag.slotIndex = i;
            drag.isFromInventory = true;

            Image slotBg = slotObj.AddComponent<Image>();
            slotBg.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            SlotHoverHandler hoverHandler = slotObj.AddComponent<SlotHoverHandler>();
            hoverHandler.slotIndex = i;

            Button btn = slotObj.AddComponent<Button>();
            btn.onClick.AddListener(() => OnInventorySlotClicked(slotIndex));

            GameObject iconObj = new GameObject("ItemIcon");
            iconObj.transform.SetParent(slotObj.transform, false);
            Image iconImg = iconObj.AddComponent<Image>();
            iconImg.raycastTarget = false; iconImg.preserveAspect = true;
            RectTransform iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.anchorMin = Vector2.zero; iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = new Vector2(10, 10); iconRect.offsetMax = new Vector2(-10, -10);
            iconObj.SetActive(false);

            GameObject textObj = new GameObject("AmountText");
            textObj.transform.SetParent(slotObj.transform, false);
            TextMeshProUGUI amountTxt = textObj.AddComponent<TextMeshProUGUI>();
            if (gameFont != null) amountTxt.font = gameFont;
            amountTxt.fontSize = 12; amountTxt.fontStyle = FontStyles.Bold;
            amountTxt.alignment = TextAlignmentOptions.BottomRight; amountTxt.color = Color.white;
            textObj.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.8f);
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(0, 0); textRect.offsetMax = new Vector2(-1, 1);
            textObj.SetActive(false);

            slotUIList.Add(new SlotUIElements { iconImage = iconImg, amountText = amountTxt, slotButton = btn });
        }

        CreateTooltipAndContextMenu(canvasGO);
    }

    private void CreateTooltipAndContextMenu(GameObject canvasGO)
    {
        tooltipPanel = new GameObject("TooltipPanel");
        tooltipPanel.transform.SetParent(canvasGO.transform, false);
        tooltipPanel.transform.SetAsLastSibling();

        RectTransform ttRect = tooltipPanel.AddComponent<RectTransform>();
        ttRect.sizeDelta = new Vector2(180, 60);
        ttRect.pivot = new Vector2(0, 1);

        tooltipPanel.AddComponent<Image>().color = new Color(0, 0, 0, 0.95f);
        tooltipPanel.AddComponent<Outline>().effectColor = Color.white;

        GameObject ttTitleObj = new GameObject("TooltipTitle");
        ttTitleObj.transform.SetParent(tooltipPanel.transform, false);

        tooltipTitleText = ttTitleObj.AddComponent<TextMeshProUGUI>();
        if (gameFont != null) tooltipTitleText.font = gameFont;
        tooltipTitleText.fontSize = 14;
        tooltipTitleText.fontStyle = FontStyles.Bold;
        tooltipTitleText.color = new Color(1f, 0.8f, 0.2f);

        RectTransform ttTitleRect = ttTitleObj.GetComponent<RectTransform>();
        ttTitleRect.anchorMin = new Vector2(0, 0.5f);
        ttTitleRect.anchorMax = new Vector2(1, 1);
        ttTitleRect.offsetMin = new Vector2(10, 0);
        ttTitleRect.offsetMax = new Vector2(-10, -5);

        GameObject ttDescObj = new GameObject("TooltipDesc");
        ttDescObj.transform.SetParent(tooltipPanel.transform, false);

        tooltipDescText = ttDescObj.AddComponent<TextMeshProUGUI>();
        if (gameFont != null) tooltipDescText.font = gameFont;
        tooltipDescText.fontSize = 12;
        tooltipDescText.color = Color.white;

        RectTransform ttDescRect = ttDescObj.GetComponent<RectTransform>();
        ttDescRect.anchorMin = new Vector2(0, 0);
        ttDescRect.anchorMax = new Vector2(1, 0.5f);
        ttDescRect.offsetMin = new Vector2(10, 5);
        ttDescRect.offsetMax = new Vector2(-10, 0);

        tooltipPanel.SetActive(false);

        contextMenuPanel = new GameObject("ContextMenu");
        contextMenuPanel.transform.SetParent(canvasGO.transform, false);
        contextMenuPanel.transform.SetAsLastSibling();

        RectTransform ctxRect = contextMenuPanel.AddComponent<RectTransform>();
        ctxRect.sizeDelta = new Vector2(120, 120);
        ctxRect.pivot = new Vector2(0, 1);

        contextMenuPanel.AddComponent<Image>().color = new Color(0, 0, 0, 0.95f);
        contextMenuPanel.AddComponent<Outline>().effectColor = Color.white;

        VerticalLayoutGroup ctxLayout = contextMenuPanel.AddComponent<VerticalLayoutGroup>();
        ctxLayout.padding = new RectOffset(5, 5, 5, 5);
        ctxLayout.spacing = 5;
        ctxLayout.childControlHeight = true;
        ctxLayout.childControlWidth = true;
        ctxLayout.childForceExpandHeight = true;
        ctxLayout.childForceExpandWidth = true;

        CreateContextMenuBtn("UseButton", "Use / Offer", OnUseClicked);
        CreateContextMenuBtn("StoreButton", "Cất vào Tủ", OnStoreClicked);
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
        if (gameFont != null) txt.font = gameFont;
        txt.text = text;
        txt.fontSize = 15;
        txt.fontStyle = FontStyles.Bold;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = new Color(0.15f, 0.15f, 0.15f, 1f);

        RectTransform txtRect = txtObj.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = Vector2.zero;
        txtRect.offsetMax = Vector2.zero;
    }
    #endregion

    #region VẼ GIAO DIỆN TỦ ĐỒ
    private void GenerateContainerUI(GameObject canvasGO)
    {
        containerPanel = new GameObject("ContainerPanel");
        containerPanel.transform.SetParent(canvasGO.transform, false);

        RectTransform panelRect = containerPanel.AddComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(300, 360);
        panelRect.localScale = new Vector3(0.85f, 0.85f, 1f);

        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = new Vector2(250, 0);

        Image panelBg = containerPanel.AddComponent<Image>();
        panelBg.color = new Color(0.15f, 0.15f, 0.2f, 0.95f);
        containerPanel.AddComponent<Outline>().effectColor = new Color(0.3f, 0.6f, 0.8f);
        containerPanel.SetActive(false);

        GameObject titleObj = new GameObject("TitleText");
        titleObj.transform.SetParent(containerPanel.transform, false);
        TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
        if (gameFont != null) titleText.font = gameFont;
        titleText.text = "LOOT CONTAINER";
        titleText.fontSize = 24;
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = new Color(0.5f, 0.8f, 1f, 1f);

        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 1); titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0.5f, 1); titleRect.anchoredPosition = new Vector2(0, -20);
        titleRect.sizeDelta = new Vector2(0, 40);

        GameObject gridObj = new GameObject("ContainerSlotGrid");
        gridObj.transform.SetParent(containerPanel.transform, false);
        RectTransform gridRect = gridObj.AddComponent<RectTransform>();
        gridRect.anchorMin = new Vector2(0, 0); gridRect.anchorMax = new Vector2(1, 1);
        gridRect.offsetMin = new Vector2(20, 20); gridRect.offsetMax = new Vector2(-20, -70);

        GridLayoutGroup gridLayout = gridObj.AddComponent<GridLayoutGroup>();
        gridLayout.cellSize = new Vector2(75, 75);
        gridLayout.spacing = new Vector2(10, 10);
        gridLayout.childAlignment = TextAnchor.UpperCenter;

        for (int i = 0; i < containerSlots; i++)
        {
            int slotIndex = i;
            GameObject slotObj = new GameObject("ContSlot_" + i);
            slotObj.transform.SetParent(gridObj.transform, false);

            UISlotDragHandler drag = slotObj.AddComponent<UISlotDragHandler>();
            drag.slotIndex = i;
            drag.isFromInventory = false;

            Image slotBg = slotObj.AddComponent<Image>();
            slotBg.color = new Color(0.2f, 0.25f, 0.3f, 1f);

            Button btn = slotObj.AddComponent<Button>();
            btn.onClick.AddListener(() => OnContainerSlotClicked(slotIndex));

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
            amountTxt.alignment = TextAlignmentOptions.BottomRight; amountTxt.color = Color.white;
            textObj.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.8f);
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(0, 0); textRect.offsetMax = new Vector2(-1, 1);
            textObj.SetActive(false);

            containerSlotUIList.Add(new SlotUIElements { iconImage = iconImg, amountText = amountTxt, slotButton = btn });
        }
    }
    #endregion

    #region LOGIC XỬ LÝ TỦ ĐỒ VÀ DRAG & DROP
    public void OpenContainerUI(LootContainer container)
    {
        currentOpenContainer = container;
        containerPanel.SetActive(true);

        if (inventoryPanel != null && !inventoryPanel.activeSelf)
        {
            inventoryPanel.SetActive(true);
        }

        if (ammoContainer != null) ammoContainer.SetActive(false);

        UpdatePanelsLayout();
        RefreshContainerUI(container);
    }

    public void CloseContainerUI()
    {
        currentOpenContainer = null;
        if (containerPanel != null) containerPanel.SetActive(false);

        if (ammoContainer != null && (inventoryPanel == null || !inventoryPanel.activeSelf))
            ammoContainer.SetActive(true);

        UpdatePanelsLayout();
    }

    public bool IsContainerOpen(LootContainer containerToCheck)
    {
        return containerPanel.activeSelf && currentOpenContainer == containerToCheck;
    }

    public void RefreshContainerUI(LootContainer container)
    {
        if (currentOpenContainer != container) return;

        List<InventorySlot> cSlots = container.itemsInContainer;

        for (int i = 0; i < containerSlots; i++)
        {
            SlotUIElements ui = containerSlotUIList[i];
            if (i < cSlots.Count && cSlots[i] != null && cSlots[i].amount > 0)
            {
                ui.iconImage.gameObject.SetActive(true);
                ui.iconImage.sprite = cSlots[i].item.icon;
                ui.slotButton.interactable = true;

                if (cSlots[i].amount > 1)
                {
                    ui.amountText.gameObject.SetActive(true);
                    ui.amountText.text = cSlots[i].amount.ToString();
                }
                else ui.amountText.gameObject.SetActive(false);
            }
            else
            {
                ui.iconImage.gameObject.SetActive(false);
                ui.amountText.gameObject.SetActive(false);
                ui.slotButton.interactable = false;
            }
        }
    }

    private void OnContainerSlotClicked(int index)
    {
        if (currentOpenContainer == null) return;

        EnsureLocalPlayer();
        if (localPlayer == null) return;

        PlayerRef myNetworkID = localPlayer.GetComponent<NetworkObject>().InputAuthority;
        string itemName = currentOpenContainer.itemsInContainer[index].item.itemName;

        currentOpenContainer.RPC_RequestTakeItem(index, itemName, myNetworkID);
    }

    // 🔥 HÀM KÉO THẢ ĐÃ CÓ BẢO VỆ MULTIPLAYER
    public bool HasItemAt(int index, bool isInv)
    {
        if (isInv) return currentSlots != null && index < currentSlots.Count && currentSlots[index].amount > 0;
        return currentOpenContainer != null && index < currentOpenContainer.itemsInContainer.Count && currentOpenContainer.itemsInContainer[index].amount > 0;
    }

    public void DragItemToContainer(int invIdx)
    {
        if (currentOpenContainer == null || currentSlots == null || invIdx >= currentSlots.Count) return;

        EnsureLocalPlayer();
        if (localPlayer == null) return;

        InventorySlot slot = currentSlots[invIdx];
        if (slot.item == null) return;

        currentOpenContainer.RPC_StoreItem(slot.item.itemName, slot.amount);
        localPlayer.GetComponent<InventorySystem>().ConsumeItem(slot.item, slot.amount);
    }

    public void DragItemToInventory(int contIdx)
    {
        if (currentOpenContainer == null || contIdx >= currentOpenContainer.itemsInContainer.Count) return;

        EnsureLocalPlayer();
        if (localPlayer == null) return;

        InventorySlot slot = currentOpenContainer.itemsInContainer[contIdx];
        if (slot.item == null) return;

        string name = slot.item.itemName;
        currentOpenContainer.RPC_RequestTakeItem(contIdx, name, localPlayer.GetComponent<NetworkObject>().InputAuthority);
    }
    #endregion

    #region Action Bar 
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
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(2, 2);
        fillRect.offsetMax = new Vector2(-2, -2);

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
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = Vector2.zero;
        txtRect.offsetMax = Vector2.zero;
        txtObj.AddComponent<Shadow>().effectColor = Color.black;

        actionBarPanel.SetActive(false);
    }
    #endregion

    #region VẼ GIAO DIỆN BẢNG GIAO DỊCH
    private void GenerateTradeWindowUI(GameObject canvasGO)
    {
        tradeWindowPanel = new GameObject("TradeWindowPanel");
        tradeWindowPanel.transform.SetParent(canvasGO.transform, false);

        RectTransform panelRect = tradeWindowPanel.AddComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(600, 420);
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;

        Image panelBg = tradeWindowPanel.AddComponent<Image>();
        panelBg.color = new Color(0.12f, 0.12f, 0.15f, 0.98f);
        tradeWindowPanel.AddComponent<Outline>().effectColor = new Color(0.8f, 0.6f, 0.1f);

        TMP_FontAsset activeFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (activeFont == null) activeFont = gameFont;

        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(tradeWindowPanel.transform, false);

        TextMeshProUGUI titleTxt = titleObj.AddComponent<TextMeshProUGUI>();
        titleTxt.font = activeFont;
        titleTxt.text = "BÀN GIAO DỊCH";
        titleTxt.fontSize = 26;
        titleTxt.fontStyle = FontStyles.Bold;
        titleTxt.alignment = TextAlignmentOptions.Center;
        titleTxt.color = new Color(1f, 0.8f, 0.2f);

        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0.5f, 1);
        titleRect.anchoredPosition = new Vector2(0, -15);
        titleRect.sizeDelta = new Vector2(0, 40);

        GameObject myArea = CreateTradeArea(tradeWindowPanel.transform, "ĐỒ CỦA BẠN", new Vector2(0.25f, 0.6f), activeFont, out myOfferIcon, out myOfferAmountTxt);

        btnPickItem = CreateButton(tradeWindowPanel.transform, "BtnPick", "CHỌN ĐỒ", new Vector2(0.25f, 0), new Color(0.2f, 0.5f, 0.8f), OpenInventoryForTrade, activeFont);
        RectTransform pickRect = btnPickItem.GetComponent<RectTransform>();
        pickRect.pivot = new Vector2(0.5f, 0); pickRect.anchoredPosition = new Vector2(0, 140); pickRect.sizeDelta = new Vector2(150, 40);

        btnReady = CreateButton(tradeWindowPanel.transform, "BtnReady", "KHÓA LẠI", new Vector2(0.25f, 0), new Color(0.8f, 0.5f, 0.1f), () => {
            EnsureLocalPlayer();
            if (localPlayer != null) localPlayer.GetComponent<PlayerTrade>().RPC_ToggleReady();
        }, activeFont);
        RectTransform readyRect = btnReady.GetComponent<RectTransform>();
        readyRect.pivot = new Vector2(0.5f, 0); readyRect.anchoredPosition = new Vector2(0, 90); readyRect.sizeDelta = new Vector2(150, 40);
        btnReadyTxt = btnReady.GetComponentInChildren<TextMeshProUGUI>();

        GameObject partnerArea = CreateTradeArea(tradeWindowPanel.transform, "ĐỒ ĐỐI TÁC", new Vector2(0.75f, 0.6f), activeFont, out partnerOfferIcon, out partnerOfferAmountTxt);

        GameObject pStatObj = new GameObject("PartnerStatus");
        pStatObj.transform.SetParent(tradeWindowPanel.transform, false);

        partnerStatusTxt = pStatObj.AddComponent<TextMeshProUGUI>();
        partnerStatusTxt.font = activeFont;
        partnerStatusTxt.text = "Đang chọn...";
        partnerStatusTxt.fontSize = 18;
        partnerStatusTxt.fontStyle = FontStyles.Bold;
        partnerStatusTxt.alignment = TextAlignmentOptions.Center;
        partnerStatusTxt.color = Color.gray;

        RectTransform pStatRect = pStatObj.GetComponent<RectTransform>();
        pStatRect.anchorMin = new Vector2(0.75f, 0); pStatRect.anchorMax = new Vector2(0.75f, 0);
        pStatRect.pivot = new Vector2(0.5f, 0); pStatRect.anchoredPosition = new Vector2(0, 90);
        pStatRect.sizeDelta = new Vector2(200, 45);

        btnConfirm = CreateButton(tradeWindowPanel.transform, "BtnConfirm", "XÁC NHẬN TRADE", new Vector2(0.5f, 0), new Color(0.2f, 0.7f, 0.2f), () => {
            EnsureLocalPlayer();
            if (localPlayer != null) localPlayer.GetComponent<PlayerTrade>().RPC_ConfirmTrade();
        }, activeFont);
        RectTransform confirmRect = btnConfirm.GetComponent<RectTransform>();
        confirmRect.pivot = new Vector2(1, 0); confirmRect.anchoredPosition = new Vector2(-10, 20); confirmRect.sizeDelta = new Vector2(180, 50);

        Button btnCancel = CreateButton(tradeWindowPanel.transform, "BtnCancel", "HỦY BỎ", new Vector2(0.5f, 0), new Color(0.8f, 0.2f, 0.2f), () => {
            EnsureLocalPlayer();
            if (localPlayer != null) localPlayer.GetComponent<PlayerTrade>().CancelTrade();
        }, activeFont);
        RectTransform cancelRect = btnCancel.GetComponent<RectTransform>();
        cancelRect.pivot = new Vector2(0, 0); cancelRect.anchoredPosition = new Vector2(10, 20); cancelRect.sizeDelta = new Vector2(150, 50);

        tradeWindowPanel.SetActive(false);
    }

    private GameObject CreateTradeArea(Transform parent, string title, Vector2 anchor, TMP_FontAsset font, out Image icon, out TextMeshProUGUI amountTxt)
    {
        GameObject area = new GameObject(title + "_Area");
        area.transform.SetParent(parent, false);

        RectTransform aRect = area.AddComponent<RectTransform>();
        aRect.anchorMin = anchor; aRect.anchorMax = anchor;
        aRect.pivot = new Vector2(0.5f, 0.5f); aRect.anchoredPosition = Vector2.zero;
        aRect.sizeDelta = new Vector2(200, 180);

        GameObject tObj = new GameObject("Title");
        tObj.transform.SetParent(area.transform, false);

        TextMeshProUGUI tTxt = tObj.AddComponent<TextMeshProUGUI>();
        tTxt.font = font; tTxt.text = title; tTxt.fontSize = 20;
        tTxt.fontStyle = FontStyles.Bold; tTxt.alignment = TextAlignmentOptions.Center; tTxt.color = Color.white;

        RectTransform tRect = tObj.GetComponent<RectTransform>();
        tRect.anchorMin = new Vector2(0.5f, 1); tRect.anchorMax = new Vector2(0.5f, 1);
        tRect.pivot = new Vector2(0.5f, 1); tRect.anchoredPosition = new Vector2(0, 0); tRect.sizeDelta = new Vector2(200, 40);

        GameObject slotObj = new GameObject("SlotBG");
        slotObj.transform.SetParent(area.transform, false);

        Image sBg = slotObj.AddComponent<Image>();
        sBg.color = new Color(0.15f, 0.15f, 0.15f, 1f);

        RectTransform sRect = slotObj.GetComponent<RectTransform>();
        sRect.anchorMin = new Vector2(0.5f, 0.4f); sRect.anchorMax = new Vector2(0.5f, 0.4f);
        sRect.pivot = new Vector2(0.5f, 0.5f); sRect.anchoredPosition = Vector2.zero; sRect.sizeDelta = new Vector2(100, 100);
        slotObj.AddComponent<Outline>().effectColor = Color.gray;

        GameObject iObj = new GameObject("Icon");
        iObj.transform.SetParent(slotObj.transform, false);

        icon = iObj.AddComponent<Image>();
        icon.preserveAspect = true;

        RectTransform iRect = iObj.GetComponent<RectTransform>();
        iRect.anchorMin = Vector2.zero; iRect.anchorMax = Vector2.one;
        iRect.offsetMin = new Vector2(10, 10); iRect.offsetMax = new Vector2(-10, -10);
        icon.gameObject.SetActive(false);

        GameObject amObj = new GameObject("Amount");
        amObj.transform.SetParent(slotObj.transform, false);

        amountTxt = amObj.AddComponent<TextMeshProUGUI>();
        amountTxt.font = font; amountTxt.fontSize = 18; amountTxt.fontStyle = FontStyles.Bold;
        amountTxt.alignment = TextAlignmentOptions.BottomRight; amountTxt.color = Color.white;
        amObj.AddComponent<Shadow>().effectColor = Color.black;

        RectTransform amRect = amObj.GetComponent<RectTransform>();
        amRect.anchorMin = Vector2.zero; amRect.anchorMax = Vector2.one;
        amRect.offsetMin = new Vector2(0, 0); amRect.offsetMax = new Vector2(-5, 5);
        amountTxt.gameObject.SetActive(false);

        return area;
    }

    private Button CreateButton(Transform parent, string name, string text, Vector2 anchor, Color color, UnityEngine.Events.UnityAction action, TMP_FontAsset font)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);

        RectTransform rect = btnObj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(150, 40);
        rect.anchorMin = anchor; rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f); rect.anchoredPosition = Vector2.zero;

        Image img = btnObj.AddComponent<Image>();
        img.color = color;

        Button btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(action);

        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(btnObj.transform, false);

        TextMeshProUGUI txt = txtObj.AddComponent<TextMeshProUGUI>();
        txt.font = font; txt.text = text; txt.fontSize = 18;
        txt.fontStyle = FontStyles.Bold; txt.alignment = TextAlignmentOptions.Center; txt.color = Color.white;

        RectTransform tRect = txtObj.GetComponent<RectTransform>();
        tRect.anchorMin = Vector2.zero; tRect.anchorMax = Vector2.one;
        tRect.offsetMin = tRect.offsetMax = Vector2.zero;

        return btn;
    }

    public void ShowTradeWindow()
    {
        if (ammoContainer != null) ammoContainer.SetActive(false);
        if (inventoryPanel != null) inventoryPanel.SetActive(false);

        tradeWindowPanel.SetActive(true);
    }

    private void OpenInventoryForTrade()
    {
        tradeWindowPanel.SetActive(false);
        if (inventoryPanel != null)
        {
            inventoryPanel.SetActive(true);
            inventoryPanel.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
        }
    }

    public void HideTradeWindow()
    {
        tradeWindowPanel.SetActive(false);
        if (inventoryPanel != null) inventoryPanel.SetActive(false);

        if (ammoContainer != null) ammoContainer.SetActive(true);
    }

    private void UpdateTradeWindowRealtime()
    {
        if (tradeWindowPanel == null || !tradeWindowPanel.activeSelf) return;

        EnsureLocalPlayer();
        if (localPlayer == null) return;

        PlayerTrade myTrade = localPlayer.GetComponent<PlayerTrade>();
        if (myTrade == null || !myTrade.IsTrading) return;

        PlayerTrade partnerTrade = myTrade.GetPlayerTrade(myTrade.TradePartner);
        if (partnerTrade == null) return;

        UpdateTradeSlotUI(myOfferIcon, myOfferAmountTxt, myTrade.OfferItemName.ToString(), myTrade.OfferAmount);

        btnPickItem.gameObject.SetActive(!myTrade.IsReady);

        btnReadyTxt.text = myTrade.IsReady ? "MỞ KHÓA" : "KHÓA LẠI";
        btnReady.GetComponent<Image>().color = myTrade.IsReady ? new Color(0.8f, 0.2f, 0.2f) : new Color(0.8f, 0.5f, 0.1f);

        UpdateTradeSlotUI(partnerOfferIcon, partnerOfferAmountTxt, partnerTrade.OfferItemName.ToString(), partnerTrade.OfferAmount);

        if (partnerTrade.IsConfirmed)
        {
            partnerStatusTxt.text = "ĐÃ CHỐT KÈO!"; partnerStatusTxt.color = Color.green;
        }
        else if (partnerTrade.IsReady)
        {
            partnerStatusTxt.text = "ĐÃ KHÓA!"; partnerStatusTxt.color = Color.yellow;
        }
        else
        {
            partnerStatusTxt.text = "Đang chọn..."; partnerStatusTxt.color = Color.gray;
        }

        btnConfirm.interactable = myTrade.IsReady && partnerTrade.IsReady;
        btnConfirm.GetComponent<Image>().color = btnConfirm.interactable ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.3f, 0.3f, 0.3f);
    }

    private void UpdateTradeSlotUI(Image icon, TextMeshProUGUI amountTxt, string itemName, int amount)
    {
        if (string.IsNullOrEmpty(itemName) || amount <= 0)
        {
            icon.gameObject.SetActive(false);
            amountTxt.gameObject.SetActive(false);
        }
        else
        {
            ItemData data = Resources.Load<ItemData>("Items/" + itemName);
            if (data != null)
            {
                icon.sprite = data.icon;
                icon.gameObject.SetActive(true);

                if (amount > 1) { amountTxt.text = amount.ToString(); amountTxt.gameObject.SetActive(true); }
                else { amountTxt.gameObject.SetActive(false); }
            }
        }
    }
    #endregion

    #region Hành động khi click chuột
    private void OnInventorySlotClicked(int index)
    {
        if (currentSlots == null || index < 0 || index >= currentSlots.Count || currentSlots[index].amount <= 0) return;

        if ((Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) && currentOpenContainer != null)
        {
            ItemData itemToStore = currentSlots[index].item;
            int amountToStore = currentSlots[index].amount;

            EnsureLocalPlayer();
            if (localPlayer != null)
            {
                localPlayer.GetComponent<InventorySystem>().ConsumeItem(itemToStore, amountToStore);
                currentOpenContainer.RPC_StoreItem(itemToStore.name, amountToStore);

                HideContextMenu();
            }
        }
        else
        {
            ShowContextMenu(index);
        }
    }

    private void OnUseClicked()
    {
        int index = selectedSlotIndex;
        HideContextMenu();

        if (index != -1)
        {
            EnsureLocalPlayer();
            if (localPlayer == null) return;

            PlayerTrade pt = localPlayer.GetComponent<PlayerTrade>();
            ItemData itemToUse = currentSlots[index].item;

            if (pt != null && pt.IsTrading)
            {
                if (!pt.IsReady)
                {
                    pt.RPC_SetOffer(itemToUse.name, currentSlots[index].amount);
                    if (inventoryPanel != null) inventoryPanel.SetActive(false);
                    tradeWindowPanel.SetActive(true);
                }
                return;
            }

            if (isDoingAction) return;
            if (itemToUse.category == ItemCategory.Ammunition) return;

            if (itemToUse.category == ItemCategory.Medical)
            {
                PlayerHealth health = localPlayer.GetComponent<PlayerHealth>();
                if (health != null)
                {
                    string itemNameLower = itemToUse.itemName.ToLower();

                    if (itemNameLower.Contains("bandage") || itemNameLower.Contains("băng"))
                    {
                        if (!health.isBleeding)
                        {
                            Debug.Log("Không bị chảy máu, không cần dùng băng gạc!");
                            return;
                        }
                    }
                    else if (itemNameLower.Contains("painkiller") || itemNameLower.Contains("thuốc") || itemNameLower.Contains("đau"))
                    {
                        if (!health.isInPain)
                        {
                            Debug.Log("Không bị đau, không cần dùng thuốc!");
                            return;
                        }
                    }
                    else
                    {
                        if (health.currentHealth >= health.maxHealth)
                        {
                            Debug.Log("Máu đã đầy, không cần dùng!");
                            return;
                        }
                    }
                }
            }

            if (itemToUse.useTime > 0)
                StartCoroutine(ActionTimerRoutine(index, itemToUse));
            else
            {
                localPlayer.GetComponent<InventorySystem>().UseItem(index);
                ApplyMedicalCure(itemToUse.itemName);
            }
        }
    }

    private void OnStoreClicked()
    {
        int index = selectedSlotIndex;
        HideContextMenu();

        if (currentOpenContainer == null) return;

        EnsureLocalPlayer();
        if (index != -1 && localPlayer != null)
        {
            ItemData itemToStore = currentSlots[index].item;
            int amountToStore = currentSlots[index].amount;

            localPlayer.GetComponent<InventorySystem>().ConsumeItem(itemToStore, amountToStore);
            currentOpenContainer.RPC_StoreItem(itemToStore.name, amountToStore);
        }
    }

    private void OnDropClicked()
    {
        int indexToDrop = selectedSlotIndex;
        HideContextMenu();

        EnsureLocalPlayer();
        if (indexToDrop != -1 && localPlayer != null)
        {
            localPlayer.GetComponent<InventorySystem>().DropItem(indexToDrop);
        }
    }

    private void ApplyMedicalCure(string itemName)
    {
        EnsureLocalPlayer();
        if (localPlayer == null) return;

        PlayerHealth health = localPlayer.GetComponent<PlayerHealth>();
        if (health == null) return;

        string nameLower = itemName.ToLower();
        if (nameLower.Contains("bandage") || nameLower.Contains("băng"))
        {
            Debug.Log("Đã quấn băng gạc!");
        }
        else if (nameLower.Contains("painkiller") || nameLower.Contains("thuốc") || nameLower.Contains("đau"))
        {
            health.UsePainkiller();
            Debug.Log("Đã dùng thuốc giảm đau!");
        }
    }
    #endregion

    #region GIAO DIỆN LỜI MỜI TRADE
    private void GenerateTradeRequestUI(GameObject canvasGO)
    {
        tradeRequestPanel = new GameObject("TradeRequestPanel");
        tradeRequestPanel.transform.SetParent(canvasGO.transform, false);

        RectTransform panelRect = tradeRequestPanel.AddComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(400, 200);
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;

        Image panelBg = tradeRequestPanel.AddComponent<Image>();
        panelBg.color = new Color(0.12f, 0.12f, 0.12f, 0.98f);
        tradeRequestPanel.AddComponent<Outline>().effectColor = new Color(0.9f, 0.7f, 0.1f);

        TMP_FontAsset activeFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (activeFont == null) activeFont = gameFont;

        GameObject txtObj = new GameObject("RequestText");
        txtObj.transform.SetParent(tradeRequestPanel.transform, false);

        TextMeshProUGUI txt = txtObj.AddComponent<TextMeshProUGUI>();
        txt.font = activeFont;
        txt.text = "GIAO DỊCH ĐANG TỚI!\nMột người chơi khác muốn trao đổi đồ với bạn.";
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = Color.white;
        txt.enableAutoSizing = true;
        txt.fontSizeMin = 14; txt.fontSizeMax = 22;

        RectTransform txtRect = txtObj.GetComponent<RectTransform>();
        txtRect.anchorMin = new Vector2(0.05f, 0.5f); txtRect.anchorMax = new Vector2(0.95f, 0.95f);
        txtRect.offsetMin = Vector2.zero; txtRect.offsetMax = Vector2.zero;

        CreateTradeBtn("BtnYes", "ĐỒNG Ý", new Color(0.2f, 0.6f, 0.2f), new Vector2(0.25f, 0.3f), activeFont, OnAcceptTradeClicked);
        CreateTradeBtn("BtnNo", "TỪ CHỐI", new Color(0.8f, 0.2f, 0.2f), new Vector2(0.75f, 0.3f), activeFont, OnDeclineTradeClicked);

        tradeRequestPanel.SetActive(false);
    }

    private void CreateTradeBtn(string name, string text, Color color, Vector2 anchor, TMP_FontAsset font, UnityEngine.Events.UnityAction action)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(tradeRequestPanel.transform, false);

        RectTransform rect = btnObj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(130, 45);
        rect.anchorMin = anchor; rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f); rect.anchoredPosition = Vector2.zero;

        Image img = btnObj.AddComponent<Image>();
        img.color = color;

        Button btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(action);

        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(btnObj.transform, false);

        TextMeshProUGUI txt = txtObj.AddComponent<TextMeshProUGUI>();
        txt.font = font; txt.text = text; txt.fontSize = 18;
        txt.alignment = TextAlignmentOptions.Center; txt.color = Color.white; txt.fontStyle = FontStyles.Bold;

        RectTransform tRect = txtObj.GetComponent<RectTransform>();
        tRect.anchorMin = Vector2.zero; tRect.anchorMax = Vector2.one;
        tRect.offsetMin = tRect.offsetMax = Vector2.zero;
    }

    public void ShowTradeRequestPopup(PlayerRef sender, PlayerRef target)
    {
        pendingTradeSender = sender;
        pendingTradeTarget = target;

        if (inventoryPanel != null) inventoryPanel.SetActive(false);
        if (ammoContainer != null) ammoContainer.SetActive(false);
        if (actionBarPanel != null) actionBarPanel.SetActive(false);

        HideContextMenu();
        HideTooltip();
        tradeRequestPanel.SetActive(true);
    }

    private void RestoreHUD()
    {
        if (ammoContainer != null) ammoContainer.SetActive(true);
    }

    private void OnAcceptTradeClicked()
    {
        tradeRequestPanel.SetActive(false);
        RestoreHUD();

        EnsureLocalPlayer();
        if (localPlayer != null)
        {
            PlayerTrade pt = localPlayer.GetComponent<PlayerTrade>();
            if (pt != null) pt.AcceptTradeRequest(pendingTradeSender);
        }
    }

    private void OnDeclineTradeClicked()
    {
        tradeRequestPanel.SetActive(false);
        RestoreHUD();

        EnsureLocalPlayer();
        if (localPlayer != null)
        {
            PlayerTrade pt = localPlayer.GetComponent<PlayerTrade>();
            if (pt != null) pt.DeclineTradeRequest(pendingTradeSender);
        }
    }
    #endregion

    #region Action Routine
    private IEnumerator ActionTimerRoutine(int slotIndex, ItemData itemToUse)
    {
        isDoingAction = true;
        actionBarPanel.SetActive(true);
        actionBarFill.fillAmount = 0f;

        if (inventoryPanel != null) inventoryPanel.SetActive(false);
        CloseContainerUI();
        yield return new WaitForSeconds(0.1f);

        float timer = 0f;
        float duration = itemToUse.useTime;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            actionBarFill.fillAmount = timer / duration;
            actionBarText.text = $"Using {itemToUse.itemName}... {(duration - timer):F1}s";

            if (Input.GetKey(KeyCode.LeftShift) || Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.1f || Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.1f)
            {
                actionBarPanel.SetActive(false);
                isDoingAction = false;
                yield break;
            }
            yield return null;
        }

        actionBarPanel.SetActive(false);
        isDoingAction = false;

        EnsureLocalPlayer();
        if (localPlayer != null)
        {
            localPlayer.GetComponent<InventorySystem>().UseItem(slotIndex);
            ApplyMedicalCure(itemToUse.itemName);
        }
    }
    #endregion

    #region Quản Lý Context Menu & Tooltip
    public void RefreshUI(List<InventorySlot> playerSlots)
    {
        currentSlots = playerSlots;

        for (int i = 0; i < maxSlots; i++)
        {
            SlotUIElements ui = slotUIList[i];
            if (i < playerSlots.Count && playerSlots[i] != null && playerSlots[i].amount > 0)
            {
                ui.iconImage.gameObject.SetActive(true);
                ui.iconImage.sprite = playerSlots[i].item.icon;

                if (playerSlots[i].amount >= 1)
                {
                    ui.amountText.gameObject.SetActive(true);
                    ui.amountText.text = playerSlots[i].amount.ToString();
                }
                else
                {
                    ui.amountText.gameObject.SetActive(false);
                }
            }
            else
            {
                ui.iconImage.gameObject.SetActive(false);
                ui.amountText.gameObject.SetActive(false);
            }
        }
    }

    public void ShowTooltip(int index)
    {
        if (contextMenuPanel != null && contextMenuPanel.activeSelf) return;

        if (currentSlots != null && index < currentSlots.Count && currentSlots[index] != null && currentSlots[index].amount > 0)
        {
            tooltipTitleText.text = currentSlots[index].item.itemName;
            string cat = currentSlots[index].item.category == ItemCategory.Ammunition ? "Ammunition" : currentSlots[index].item.category == ItemCategory.Medical ? "Medical Supplies" : "Consumables";
            tooltipDescText.text = "Type: " + cat;
            tooltipPanel.SetActive(true);
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
            HideTooltip();
            selectedSlotIndex = index;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                mainCanvas.transform as RectTransform, Input.mousePosition, mainCanvas.worldCamera, out Vector2 mousePos);

            contextMenuPanel.transform.localPosition = mousePos;
            contextMenuPanel.SetActive(true);
        }
    }

    public void HideContextMenu()
    {
        if (contextMenuPanel != null) contextMenuPanel.SetActive(false);
        selectedSlotIndex = -1;
    }

    public bool IsInventoryOpen()
    {
        return inventoryPanel != null && inventoryPanel.activeSelf;
    }
    #endregion

    #region Giao Diện Đạn Dược & Thay Đạn (Ammo & Reload)
    public void CreateAmmoUI()
    {
        if (mainCanvas == null) mainCanvas = FindAnyObjectByType<Canvas>();
        if (mainCanvas == null) return;

        ammoContainer = new GameObject("AmmoDisplayContainer");
        ammoContainer.transform.SetParent(mainCanvas.transform, false);

        RectTransform containerRt = ammoContainer.AddComponent<RectTransform>();
        containerRt.anchorMin = new Vector2(0, 0); containerRt.anchorMax = new Vector2(0, 0);
        containerRt.pivot = new Vector2(0, 0); containerRt.anchoredPosition = new Vector2(10, 15); containerRt.sizeDelta = new Vector2(250, 25);

        GameObject iconObj = new GameObject("AmmoIcon");
        iconObj.transform.SetParent(ammoContainer.transform, false);

        Image ammoImage = iconObj.AddComponent<Image>();
        if (iconAmmo != null) ammoImage.sprite = iconAmmo;

        RectTransform iconRt = iconObj.GetComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0, 0.5f); iconRt.anchorMax = new Vector2(0, 0.5f);
        iconRt.pivot = new Vector2(0, 0.5f); iconRt.anchoredPosition = new Vector2(15, 0); iconRt.sizeDelta = new Vector2(15, 30);

        GameObject textObj = new GameObject("AmmoText");
        textObj.transform.SetParent(ammoContainer.transform, false);

        ammoText = textObj.AddComponent<TextMeshProUGUI>();
        TMP_FontAsset defaultFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (defaultFont != null) ammoText.font = defaultFont; else if (gameFont != null) ammoText.font = gameFont;

        ammoText.fontSize = 20; ammoText.color = Color.white; ammoText.alignment = TextAlignmentOptions.MidlineLeft;

        RectTransform textRt = textObj.GetComponent<RectTransform>();
        textRt.anchorMin = new Vector2(0, 0.5f); textRt.anchorMax = new Vector2(0, 0.5f);
        textRt.pivot = new Vector2(0, 0.5f); textRt.anchoredPosition = new Vector2(50, 0); textRt.sizeDelta = new Vector2(200, 25);
        ammoText.text = "-- / --";
    }

    public void UpdateAmmoUI(int current, int reserve)
    {
        if (ammoText != null) ammoText.text = $"{current} / {reserve}";
    }

    public void ShowReloadUI(float currentTimer, float maxDuration)
    {
        if (ammoContainer != null) ammoContainer.SetActive(false);
        if (actionBarPanel != null && !actionBarPanel.activeSelf) actionBarPanel.SetActive(true);
        if (actionBarFill != null) actionBarFill.fillAmount = currentTimer / maxDuration;
        if (actionBarText != null) actionBarText.text = $"Reloading... {(maxDuration - currentTimer):F1}s";
    }

    public void HideReloadUI()
    {
        if (actionBarPanel != null) actionBarPanel.SetActive(false);
        if (ammoContainer != null) ammoContainer.SetActive(true);
    }
    #endregion

    #region 🔥 MỚI: GIAO DIỆN ĐỒNG HỒ (DAY/NIGHT CYCLE)
    private void GenerateClockUI(GameObject canvasGO)
    {
        GameObject clockPanel = new GameObject("ClockPanel", typeof(RectTransform));
        clockPanel.transform.SetParent(canvasGO.transform, false);

        RectTransform panelRect = clockPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1, 1);
        panelRect.anchorMax = new Vector2(1, 1);
        panelRect.pivot = new Vector2(1, 1);
        panelRect.anchoredPosition = new Vector2(-1, -1);
        panelRect.sizeDelta = new Vector2(50, 20);

        Image bg = clockPanel.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.1f, 0.15f, 0.9f);
        clockPanel.AddComponent<Outline>().effectColor = new Color(0.5f, 0.5f, 0.5f);

        GameObject textObj = new GameObject("TimeText", typeof(RectTransform));
        textObj.transform.SetParent(clockPanel.transform, false);

        clockText = textObj.AddComponent<TextMeshProUGUI>();

        if (gameFont != null)
        {
            clockText.font = gameFont;
        }

        clockText.text = "12:00";
        clockText.fontSize = 15;
        clockText.fontStyle = FontStyles.Bold;
        clockText.alignment = TextAlignmentOptions.Center;
        clockText.color = new Color(0.9f, 0.9f, 0.9f, 1f);

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
    }
    // =========================================================
    // 🔥 CẦU DAO TỔNG: DÙNG ĐỂ KHÓA CHÂN TAY KHI MỞ UI
    // =========================================================
    public bool IsAnyMenuOpen()
    {
        bool isInv = inventoryPanel != null && inventoryPanel.activeSelf;
        bool isLoot = containerPanel != null && containerPanel.activeSelf;
        bool isTrade = tradeWindowPanel != null && tradeWindowPanel.activeSelf;

        return isInv || isLoot || isTrade;
    }
    #endregion
}