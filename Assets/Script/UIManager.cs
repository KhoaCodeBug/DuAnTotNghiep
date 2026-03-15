using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager instance;

    private GameObject canvasObj;
    private GameObject inventoryWindow;
    private GameObject itemContentContainer; // Nơi chứa danh sách các dòng vật phẩm

    // Font mặc định của Unity để code sinh Text không bị tàng hình
    private Font defaultFont;

    void Awake()
    {
        if (instance != null) { Destroy(gameObject); return; }
        instance = this;

        // Lấy font mặc định
        defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (defaultFont == null) defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

        GenerateCanvas();
        GenerateInventoryWindow();
    }

    void Start()
    {
        // Đăng ký lắng nghe sự kiện: Khi túi đồ thay đổi thì gọi hàm Refresh
        if (Inventory.instance != null)
        {
            Inventory.instance.onItemChangedCallback += RefreshInventoryUI;
        }
    }

    // 1. Tự động sinh Canvas
    void GenerateCanvas()
    {
        canvasObj = new GameObject("Auto_Canvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasObj.AddComponent<GraphicRaycaster>();
    }

    // 2. Tự động sinh Cửa sổ Inventory kiểu Zomboid
    void GenerateInventoryWindow()
    {
        // Tạo Panel nền (Cửa sổ)
        inventoryWindow = new GameObject("Inventory_Window");
        inventoryWindow.transform.SetParent(canvasObj.transform, false);

        Image bgImage = inventoryWindow.AddComponent<Image>();
        bgImage.color = new Color(0.1f, 0.1f, 0.1f, 0.8f); // Màu xám đen hơi trong suốt kiểu Zomboid

        RectTransform rect = inventoryWindow.GetComponent<RectTransform>();
        // Đặt ở góc phải màn hình
        rect.anchorMin = new Vector2(1, 0.5f);
        rect.anchorMax = new Vector2(1, 0.5f);
        rect.pivot = new Vector2(1, 0.5f);
        rect.anchoredPosition = new Vector2(-50, 0); // Cách lề phải 50px
        rect.sizeDelta = new Vector2(400, 600); // Kích thước cửa sổ

        // Tạo Tiêu đề cửa sổ
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(inventoryWindow.transform, false);
        Text titleText = titleObj.AddComponent<Text>();
        titleText.font = defaultFont;
        titleText.text = "INVENTORY";
        titleText.fontSize = 24;
        titleText.color = Color.white;
        titleText.alignment = TextAnchor.UpperCenter;

        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0.5f, 1);
        titleRect.sizeDelta = new Vector2(0, 50);
        titleRect.anchoredPosition = new Vector2(0, -10);

        // Tạo Vùng chứa danh sách đồ (Vertical Layout Group)
        itemContentContainer = new GameObject("Content_Container");
        itemContentContainer.transform.SetParent(inventoryWindow.transform, false);

        RectTransform contentRect = itemContentContainer.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 0);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 0.5f);
        contentRect.sizeDelta = new Vector2(-40, -80); // Thụt lề vào so với cửa sổ cha
        contentRect.anchoredPosition = new Vector2(0, -20);

        // Thêm Layout Group để các vật phẩm tự xếp dọc từ trên xuống
        VerticalLayoutGroup vLayout = itemContentContainer.AddComponent<VerticalLayoutGroup>();
        vLayout.childAlignment = TextAnchor.UpperCenter;
        vLayout.childControlHeight = false;
        vLayout.childForceExpandHeight = false;
        vLayout.spacing = 5f; // Khoảng cách giữa các dòng
    }

    // 3. Hàm cập nhật lại giao diện mỗi khi nhặt/vứt đồ
    public void RefreshInventoryUI()
    {
        // Xóa sạch các dòng UI cũ
        foreach (Transform child in itemContentContainer.transform)
        {
            Destroy(child.gameObject);
        }

        // Tạo lại dòng UI mới cho từng món đồ trong túi
        /*foreach (Item item in Inventory.instance.items)
        {
            CreateItemRow(item);
        }*/
    }

    // 4. Sinh ra 1 dòng vật phẩm (Icon + Tên)
    void CreateItemRow(Item item)
    {
        // Tạo GameObject dòng
        GameObject rowObj = new GameObject("ItemRow_" + item.name);
        rowObj.transform.SetParent(itemContentContainer.transform, false);

        // Thêm nền cho dòng (khi hover hoặc click có thể đổi màu sau)
        Image rowBg = rowObj.AddComponent<Image>();
        rowBg.color = new Color(0.2f, 0.2f, 0.2f, 1f);

        LayoutElement layoutElement = rowObj.AddComponent<LayoutElement>();
        layoutElement.minHeight = 40; // Chiều cao mỗi dòng

        // Thêm Text tên vật phẩm
        GameObject textObj = new GameObject("ItemName");
        textObj.transform.SetParent(rowObj.transform, false);
        Text nameText = textObj.AddComponent<Text>();
        nameText.font = defaultFont;
        nameText.text = item.itemName;
        nameText.fontSize = 20;
        nameText.alignment = TextAnchor.MiddleLeft;

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0, 0);
        textRect.anchorMax = new Vector2(1, 1);
        textRect.sizeDelta = new Vector2(-50, 0); // Trừ chỗ cho Icon
        textRect.anchoredPosition = new Vector2(50, 0); // Thụt vào 50px
    }
}