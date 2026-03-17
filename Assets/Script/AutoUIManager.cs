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

    private Canvas mainCanvas;
    private GameObject inventoryPanel;
    private List<SlotUIElements> slotUIList = new List<SlotUIElements>();
    private List<InventorySlot> currentSlots = new List<InventorySlot>(); // Lưu lại danh sách đồ để tra cứu

    // Biến cho Tooltip
    private GameObject tooltipPanel;
    private TextMeshProUGUI tooltipTitleText;
    private TextMeshProUGUI tooltipDescText;

    // Biến cho Context Menu (Menu Chuột Phải)
    private GameObject contextMenuPanel;
    private int selectedSlotIndex = -1; // Lưu lại xem đang click chuột phải vào ô số mấy

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
        // Bật tắt túi đồ
        if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.I))
        {
            if (inventoryPanel != null)
                inventoryPanel.SetActive(!inventoryPanel.activeSelf);

            HideContextMenu();
        }

        // MA THUẬT: Cho Tooltip bay theo con trỏ chuột
        if (tooltipPanel != null && tooltipPanel.activeSelf)
        {
            Vector2 mousePos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(mainCanvas.transform as RectTransform, Input.mousePosition, mainCanvas.worldCamera, out mousePos);
            // Cộng thêm (15, -15) để bảng thông tin nằm lệch ra một chút, không bị con trỏ chuột che khuất
            tooltipPanel.transform.localPosition = mousePos + new Vector2(15, -15);
        }
    }

    private void GenerateEntireUI()
    {
        GameObject canvasGO = new GameObject("AutoCanvas");
        mainCanvas = canvasGO.AddComponent<Canvas>();
        mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGO.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(canvasGO);

        inventoryPanel = new GameObject("InventoryPanel");
        inventoryPanel.transform.SetParent(canvasGO.transform, false);
        RectTransform panelRect = inventoryPanel.AddComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(500, 450);
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
        gridRect.offsetMin = new Vector2(20, 20); gridRect.offsetMax = new Vector2(-20, -70);

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

            // GẮN ĂNG-TEN BẮT CHUỘT VÀO Ô VUÔNG
            SlotHoverHandler hoverHandler = slotObj.AddComponent<SlotHoverHandler>();
            hoverHandler.slotIndex = i;

            GameObject iconObj = new GameObject("ItemIcon");
            iconObj.transform.SetParent(slotObj.transform, false);
            Image iconImg = iconObj.AddComponent<Image>();
            iconImg.raycastTarget = false; // Tắt bắt chuột của ảnh để không chặn ăng-ten
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
            amountTxt.raycastTarget = false; // Tắt bắt chuột
            textObj.SetActive(false);

            slotUIList.Add(new SlotUIElements { iconImage = iconImg, amountText = amountTxt });
        }

        // ================== TẠO BẢNG TOOLTIP ==================
        tooltipPanel = new GameObject("TooltipPanel");
        tooltipPanel.transform.SetParent(canvasGO.transform, false);
        tooltipPanel.transform.SetAsLastSibling(); // Ép nó nằm trên cùng để không bị đồ vật che
        RectTransform ttRect = tooltipPanel.AddComponent<RectTransform>();
        ttRect.sizeDelta = new Vector2(180, 60); // Khung Tooltip nhỏ gọn
        ttRect.pivot = new Vector2(0, 1); // Góc neo mũi chuột
        Image ttBg = tooltipPanel.AddComponent<Image>();
        ttBg.color = new Color(0, 0, 0, 0.95f); // Nền đen tuyền
        ttBg.raycastTarget = false; // Rất quan trọng: Bảng này không được cản chuột

        Outline ttOutline = tooltipPanel.AddComponent<Outline>();
        ttOutline.effectColor = Color.white;
        ttOutline.effectDistance = new Vector2(1, -1); // Viền trắng mỏng

        // Chữ Tên Vật Phẩm
        GameObject ttTitleObj = new GameObject("TooltipTitle");
        ttTitleObj.transform.SetParent(tooltipPanel.transform, false);
        tooltipTitleText = ttTitleObj.AddComponent<TextMeshProUGUI>();
        // ĐÃ XÓA DÒNG NẠP FONT Ở ĐÂY -> Sẽ dùng font mặc định của Unity/TMP
        tooltipTitleText.fontSize = 14;
        tooltipTitleText.fontStyle = FontStyles.Bold;
        tooltipTitleText.color = new Color(1f, 0.8f, 0.2f); // Màu Vàng Cam
        RectTransform ttTitleRect = ttTitleObj.GetComponent<RectTransform>();
        ttTitleRect.anchorMin = new Vector2(0, 0.5f); ttTitleRect.anchorMax = new Vector2(1, 1);
        ttTitleRect.offsetMin = new Vector2(10, 0); ttTitleRect.offsetMax = new Vector2(-10, -5);

        // Chữ Mô Tả (Loại Vật Phẩm)
        GameObject ttDescObj = new GameObject("TooltipDesc");
        ttDescObj.transform.SetParent(tooltipPanel.transform, false);
        tooltipDescText = ttDescObj.AddComponent<TextMeshProUGUI>();
        // ĐÃ XÓA DÒNG NẠP FONT Ở ĐÂY -> Sẽ dùng font mặc định của Unity/TMP
        tooltipDescText.fontSize = 12;
        tooltipDescText.color = Color.white;
        RectTransform ttDescRect = ttDescObj.GetComponent<RectTransform>();
        ttDescRect.anchorMin = new Vector2(0, 0); ttDescRect.anchorMax = new Vector2(1, 0.5f);
        ttDescRect.offsetMin = new Vector2(10, 5); ttDescRect.offsetMax = new Vector2(-10, 0);

        tooltipPanel.SetActive(false);

        // ================== TẠO BẢNG CONTEXT MENU (CHUỘT PHẢI) ==================
        contextMenuPanel = new GameObject("ContextMenu");
        contextMenuPanel.transform.SetParent(canvasGO.transform, false);
        contextMenuPanel.transform.SetAsLastSibling();
        RectTransform ctxRect = contextMenuPanel.AddComponent<RectTransform>();
        ctxRect.sizeDelta = new Vector2(120, 85); // Tăng nhẹ kích thước để chứa khoảng trống
        ctxRect.pivot = new Vector2(0, 1);

        Image ctxBg = contextMenuPanel.AddComponent<Image>();
        ctxBg.color = new Color(0.05f, 0.05f, 0.05f, 0.95f); // Nền tối hơn tí cho ngầu
        contextMenuPanel.AddComponent<Outline>().effectColor = Color.gray;

        // Thiết lập Layout để các nút tách rời nhau
        VerticalLayoutGroup ctxLayout = contextMenuPanel.AddComponent<VerticalLayoutGroup>();
        ctxLayout.padding = new RectOffset(5, 5, 5, 5); // Khoảng cách viền xung quanh menu
        ctxLayout.spacing = 5;                          // KHOẢNG CÁCH GIỮA 2 NÚT (Tách rời ở đây)
        ctxLayout.childControlHeight = true;
        ctxLayout.childControlWidth = true;
        ctxLayout.childForceExpandHeight = true;
        ctxLayout.childForceExpandWidth = true;

        // --- Tạo Nút "Use" ---
        GameObject useBtnObj = new GameObject("UseButton");
        useBtnObj.transform.SetParent(contextMenuPanel.transform, false);
        Image useImg = useBtnObj.AddComponent<Image>();
        Button useBtn = useBtnObj.AddComponent<Button>();

        // HIỆU ỨNG DI CHUỘT CHO NÚT USE (Tone Xanh Dương)
        ColorBlock useCB = useBtn.colors;
        useCB.normalColor = new Color(0.12f, 0.12f, 0.12f, 1f);      // Xám đen sang trọng
        useCB.highlightedColor = new Color(0.15f, 0.5f, 0.85f, 1f);  // Xanh dương sáng (khi lướt chuột)
        useCB.pressedColor = new Color(0.1f, 0.35f, 0.6f, 1f);       // Xanh dương sậm (khi click)
        useCB.selectedColor = new Color(0.12f, 0.12f, 0.12f, 1f);
        useCB.fadeDuration = 0.15f; // MA THUẬT Ở ĐÂY: Chuyển màu mượt mà trong 0.15 giây
        useBtn.colors = useCB;
        useBtn.onClick.AddListener(OnUseClicked);

        GameObject useTextObj = new GameObject("Text");
        useTextObj.transform.SetParent(useBtnObj.transform, false);
        TextMeshProUGUI useTxt = useTextObj.AddComponent<TextMeshProUGUI>();
        useTxt.text = "Use";
        useTxt.fontSize = 15;
        useTxt.fontStyle = FontStyles.Bold; // Làm chữ đậm lên một chút
        useTxt.alignment = TextAlignmentOptions.Center;
        useTxt.color = new Color(0.9f, 0.9f, 0.9f, 1f);
        RectTransform useTxtRect = useTextObj.GetComponent<RectTransform>();
        useTxtRect.anchorMin = Vector2.zero; useTxtRect.anchorMax = Vector2.one;
        useTxtRect.offsetMin = Vector2.zero; useTxtRect.offsetMax = Vector2.zero;


        // --- Tạo Nút "Drop" ---
        GameObject dropBtnObj = new GameObject("DropButton");
        dropBtnObj.transform.SetParent(contextMenuPanel.transform, false);
        Image dropImg = dropBtnObj.AddComponent<Image>();
        Button dropBtn = dropBtnObj.AddComponent<Button>();

        // HIỆU ỨNG DI CHUỘT CHO NÚT DROP (Tone Đỏ)
        ColorBlock dropCB = dropBtn.colors;
        dropCB.normalColor = new Color(0.12f, 0.12f, 0.12f, 1f);      // Xám đen đồng bộ với Use
        dropCB.highlightedColor = new Color(0.85f, 0.2f, 0.2f, 1f);   // Đỏ rực khi di chuột vào
        dropCB.pressedColor = new Color(0.55f, 0.1f, 0.1f, 1f);       // Đỏ sậm khi click
        dropCB.selectedColor = new Color(0.12f, 0.12f, 0.12f, 1f);
        dropCB.fadeDuration = 0.15f;
        dropBtn.colors = dropCB;
        dropBtn.onClick.AddListener(OnDropClicked);

        GameObject dropTextObj = new GameObject("Text");
        dropTextObj.transform.SetParent(dropBtnObj.transform, false);
        TextMeshProUGUI dropTxt = dropTextObj.AddComponent<TextMeshProUGUI>();
        dropTxt.text = "Drop";
        dropTxt.fontSize = 15;
        dropTxt.fontStyle = FontStyles.Bold;
        dropTxt.alignment = TextAlignmentOptions.Center;
        dropTxt.color = new Color(0.9f, 0.9f, 0.9f, 1f); 
        RectTransform dropTxtRect = dropTextObj.GetComponent<RectTransform>();
        dropTxtRect.anchorMin = Vector2.zero; dropTxtRect.anchorMax = Vector2.one;
        dropTxtRect.offsetMin = Vector2.zero; dropTxtRect.offsetMax = Vector2.zero;

        contextMenuPanel.SetActive(false);
    }

    public void RefreshUI(List<InventorySlot> playerSlots)
    {
        currentSlots = playerSlots; // Cập nhật danh sách đồ hiện tại

        for (int i = 0; i < maxSlots; i++)
        {
            SlotUIElements ui = slotUIList[i];

            if (i < playerSlots.Count && playerSlots[i] != null && playerSlots[i].amount > 0)
            {
                ui.iconImage.gameObject.SetActive(true);
                ui.iconImage.sprite = playerSlots[i].item.icon;

                if (playerSlots[i].amount > 1)
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

    // ================== HÀM ĐIỀU KHIỂN TOOLTIP ==================
    public void ShowTooltip(int index)
    {
        if (contextMenuPanel != null && contextMenuPanel.activeSelf) return;

        // Kiểm tra xem ô đó có đồ thật không
        if (currentSlots != null && index < currentSlots.Count && currentSlots[index] != null && currentSlots[index].amount > 0)
        {
            // Hiển thị tên vật phẩm
            tooltipTitleText.text = currentSlots[index].item.itemName;

            string categoryName = "";
            switch (currentSlots[index].item.category)
            {
                case ItemCategory.Ammunition: categoryName = "Ammunition"; break;
                case ItemCategory.Medical: categoryName = "Medical Supplies"; break; // Dùng Medical Supplies cho chuẩn game
                case ItemCategory.Consumable: categoryName = "Consumables"; break;
            }
            tooltipDescText.text = "Type: " + categoryName;

            tooltipPanel.SetActive(true);
        }
    }

    public void HideTooltip()
    {
        if (tooltipPanel != null) tooltipPanel.SetActive(false);
    }

    // ================== HÀM ĐIỀU KHIỂN CONTEXT MENU ==================
    public void ShowContextMenu(int index)
    {
        // Chỉ hiện menu nếu ô đó có đồ
        if (currentSlots != null && index < currentSlots.Count && currentSlots[index] != null && currentSlots[index].amount > 0)
        {
            HideTooltip();

            selectedSlotIndex = index; // Nhớ lại ô đang chọn

            // Ép Menu bay đến ngay vị trí chuột
            Vector2 mousePos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(mainCanvas.transform as RectTransform, Input.mousePosition, mainCanvas.worldCamera, out mousePos);
            contextMenuPanel.transform.localPosition = mousePos;

            contextMenuPanel.SetActive(true);
        }
    }

    public void HideContextMenu()
    {
        if (contextMenuPanel != null) contextMenuPanel.SetActive(false);
        selectedSlotIndex = -1;
    }

    // ================== SỰ KIỆN KHI BẤM NÚT ==================
    private void OnUseClicked()
    {
        Debug.Log("Người chơi bấm USE vật phẩm ở ô số: " + selectedSlotIndex);
        HideContextMenu();
        // Lát nữa mình sẽ móc nối sang InventorySystem để trừ đồ ở đây
    }

    private void OnDropClicked()
    {
        Debug.Log("Người chơi bấm DROP vật phẩm ở ô số: " + selectedSlotIndex);
        HideContextMenu();
        // Lát nữa mình sẽ móc nối sang InventorySystem để vứt đồ ở đây
    }
}