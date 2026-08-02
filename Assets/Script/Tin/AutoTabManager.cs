using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class AutoTabManager : MonoBehaviour
{
    public static AutoTabManager Instance { get; private set; }

    private GameObject tabCanvasObj;
    private GameObject tabContainer;
    private Button btnInventory;
    private Button btnHealth;
    private Text txtInventory;
    private Text txtHealth;

    private Color activeColor = new Color(0.1f, 0.85f, 0.1f, 1f); // Xanh lá nổi bật giống hình
    private Color inactiveColor = new Color(0.15f, 0.15f, 0.15f, 1f); // Xám đen

    public enum TabType { Inventory, Health }
    public TabType CurrentTab { get; private set; } = TabType.Inventory;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoSpawn()
    {
        if (Instance == null)
        {
            var go = new GameObject("--- AUTO TAB MANAGER ---");
            Instance = go.AddComponent<AutoTabManager>();
            DontDestroyOnLoad(go);
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
        CreateUI();
    }

    private void Update()
    {
        if (tabCanvasObj != null && tabCanvasObj.activeSelf)
        {
            bool isInvActive = AutoUIManager.Instance != null && AutoUIManager.Instance.IsInventoryOpen();
            bool isHealthActive = AutoHealthPanel.Instance != null && AutoHealthPanel.Instance.IsOpen;

            if (!isInvActive && !isHealthActive)
            {
                tabCanvasObj.SetActive(false);
            }
        }
    }

    private void CreateUI()
    {
        tabCanvasObj = new GameObject("TabCanvas");
        tabCanvasObj.transform.SetParent(this.transform, false);
        Canvas canvas = tabCanvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 150; // Luôn nằm trên cùng

        CanvasScaler scaler = tabCanvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        tabCanvasObj.AddComponent<GraphicRaycaster>();

        tabContainer = new GameObject("TabContainer");
        tabContainer.transform.SetParent(tabCanvasObj.transform, false);
        RectTransform containerRt = tabContainer.AddComponent<RectTransform>();
        containerRt.anchorMin = new Vector2(0.5f, 0.5f);
        containerRt.anchorMax = new Vector2(0.5f, 0.5f);
        containerRt.pivot = new Vector2(0.5f, 0.5f);
        // Kích thước chuẩn của bảng là 620x530. Mép trên ở y = 265.
        // Nút Tab cao 45, nếu sát thì tâm là 287.5. Nhích lên 1 chút (thêm 10px) -> 297.5
        containerRt.anchoredPosition = new Vector2(0, 297.5f);
        containerRt.sizeDelta = new Vector2(620, 45); // Chiều rộng bằng chiều rộng chuẩn 620

        HorizontalLayoutGroup layout = tabContainer.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 0; // Sát nhau y như hình
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;

        btnInventory = CreateTabButton("INVENTORY", tabContainer.transform, out txtInventory);
        btnHealth = CreateTabButton("HEALTH STATUS", tabContainer.transform, out txtHealth);

        btnInventory.onClick.AddListener(() => SwitchTab(TabType.Inventory));
        btnHealth.onClick.AddListener(() => SwitchTab(TabType.Health));

        tabCanvasObj.SetActive(false);
    }

    private Button CreateTabButton(string label, Transform parent, out Text textComponent)
    {
        GameObject btnObj = new GameObject("TabBtn_" + label);
        btnObj.transform.SetParent(parent, false);
        RectTransform rt = btnObj.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(310, 45); // Chia đôi chiều rộng 620 của TabContainer

        Image img = btnObj.AddComponent<Image>();
        img.color = inactiveColor;
        Outline outline = btnObj.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2, -2);
        
        Button btn = btnObj.AddComponent<Button>();

        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(btnObj.transform, false);
        RectTransform txtRt = txtObj.AddComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero; txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = Vector2.zero; txtRt.offsetMax = Vector2.zero;

        textComponent = txtObj.AddComponent<Text>();
        textComponent.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        textComponent.text = label;
        textComponent.fontSize = 18;
        textComponent.fontStyle = FontStyle.BoldAndItalic;
        textComponent.lineSpacing = 1.0f;
        textComponent.color = Color.white;
        textComponent.alignment = TextAnchor.MiddleCenter;

        return btn;
    }

    public void ShowTabs(bool show)
    {
        if (tabCanvasObj != null)
        {
            tabCanvasObj.SetActive(show);
        }
        
        if (show)
        {
            // Cập nhật giao diện khi mở lên lại
            SwitchTab(CurrentTab);
        }
    }

    public void SwitchTab(TabType newTab)
    {
        CurrentTab = newTab;

        if (CurrentTab == TabType.Inventory)
        {
            btnInventory.GetComponent<Image>().color = activeColor;
            btnHealth.GetComponent<Image>().color = inactiveColor;

            if (AutoUIManager.Instance != null) AutoUIManager.Instance.ForceShowInventoryOnly();
            if (AutoHealthPanel.Instance != null) AutoHealthPanel.Instance.SetOpenState(false);
        }
        else
        {
            btnHealth.GetComponent<Image>().color = activeColor;
            btnInventory.GetComponent<Image>().color = inactiveColor;

            if (AutoUIManager.Instance != null) AutoUIManager.Instance.ForceHideInventoryOnly();
            if (AutoHealthPanel.Instance != null) AutoHealthPanel.Instance.SetOpenState(true);
        }
    }
}
