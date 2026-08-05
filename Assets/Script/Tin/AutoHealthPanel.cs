using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using System.Text;
using System.Collections;

public class AutoHealthPanel : MonoBehaviour
{
    private static AutoHealthPanel instance;
    public static AutoHealthPanel Instance
    {
        get
        {
            if (instance == null || instance.gameObject == null)
            {
                instance = FindFirstObjectByType<AutoHealthPanel>();
                if (instance == null)
                {
                    GameObject go = new GameObject("--- AUTO HEALTH MANAGER ---");
                    instance = go.AddComponent<AutoHealthPanel>();
                    DontDestroyOnLoad(go);
                }
            }
            return instance;
        }
        private set
        {
            instance = value;
        }
    }

    private GameObject healthCanvas;
    private GameObject panelObj;

    // --- UI ELEMENTS ---
    private Text fixedHeaderText;
    private RectTransform textContentRect;
    
    // --- Graphical Health Bar ---
    private Image healthBarFill;
    private Text healthBarText;

    private bool isOpen = false;
    public bool IsOpen => isOpen;

    // --- ZOMBIE INJURY LOGIC ---
    public enum InjuryType { Scratched, Laceration, Bitten }

    private class BodyPartData
    {
        public string Name;
        public List<InjuryType> Injuries = new List<InjuryType>();
        public bool IsBandaged = false;
        public Image Img;
    }
    private Dictionary<string, BodyPartData> bodyParts = new Dictionary<string, BodyPartData>();

    private PlayerHealth localPlayerHealth;
    private InventorySystem localInventory;

    private Color colHealthy = new Color(0.2f, 0.22f, 0.25f, 1f);
    private Color colInjured = new Color(0.65f, 0.15f, 0.15f, 1f);
    private Color colBandaged = new Color(0.7f, 0.7f, 0.7f, 1f);
    private Color colOutline = new Color(0f, 0f, 0f, 0.9f);

    private GameObject contextMenuPanel;
    private string selectedPartNameForContext;

    private bool isHealing = false;
    public bool IsHealing => isHealing;
    private float currentDisplayedHealth = 100f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoSpawn()
    {
        // Kích hoạt qua cơ chế Lazy initialization
        var trigger = Instance;
    }

    void Awake()
    {
        if (instance == null) instance = this;
        else { Destroy(gameObject); return; }

        SetupHealthUI();
    }

    void SetupHealthUI()
    {
        healthCanvas = new GameObject("--- AUTO HEALTH CANVAS ---");
        Canvas canvas = healthCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 110;
        CanvasScaler scaler = healthCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        healthCanvas.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(healthCanvas);

        panelObj = new GameObject("HealthPanelBG");
        panelObj.transform.SetParent(healthCanvas.transform, false);
        RectTransform panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f); panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(620, 530);
        panelRect.anchoredPosition = Vector2.zero;

        Image panelBg = panelObj.AddComponent<Image>();
        panelBg.color = new Color(0.06f, 0.07f, 0.08f, 1f);

        GameObject borderObj = new GameObject("PanelBorder");
        borderObj.transform.SetParent(panelObj.transform, false);
        RectTransform borderRect = borderObj.AddComponent<RectTransform>();
        borderRect.anchorMin = new Vector2(0, 0); borderRect.anchorMax = new Vector2(1, 1);
        borderRect.offsetMin = Vector2.zero; borderRect.offsetMax = Vector2.zero;

        Image borderImg = borderObj.AddComponent<Image>();
        borderImg.color = new Color(0, 0, 0, 1f);
        Outline borderOutline = borderObj.AddComponent<Outline>();
        borderOutline.effectColor = Color.white;
        borderOutline.effectDistance = new Vector2(2f, -2f);

        GameObject mannequinObj = new GameObject("MannequinContainer");
        mannequinObj.transform.SetParent(panelObj.transform, false);
        RectTransform manRect = mannequinObj.AddComponent<RectTransform>();
        manRect.anchorMin = new Vector2(0.5f, 0.5f); manRect.anchorMax = new Vector2(0.5f, 0.5f);
        manRect.anchoredPosition = new Vector2(-170, 0);
        manRect.localScale = new Vector3(1f, 1f, 1f);

        CreateBodyPart("Head", mannequinObj.transform, new Vector2(0, 160), new Vector2(50, 50), new Vector2(0.5f, 0.5f), 0);
        CreateBodyPart("Neck", mannequinObj.transform, new Vector2(0, 120), new Vector2(20, 25), new Vector2(0.5f, 0.5f), 0);
        CreateBodyPart("Upper Torso", mannequinObj.transform, new Vector2(0, 75), new Vector2(80, 60), new Vector2(0.5f, 0.5f), 0);
        CreateBodyPart("Lower Torso", mannequinObj.transform, new Vector2(0, 13), new Vector2(70, 60), new Vector2(0.5f, 0.5f), 0);
        CreateBodyPart("Left Thigh", mannequinObj.transform, new Vector2(-18, -19), new Vector2(30, 65), new Vector2(0.5f, 1f), 0);
        CreateBodyPart("Left Calf", mannequinObj.transform, new Vector2(-18, -85), new Vector2(26, 65), new Vector2(0.5f, 1f), 0);
        CreateBodyPart("Left Foot", mannequinObj.transform, new Vector2(-18, -152), new Vector2(30, 18), new Vector2(0.5f, 1f), 0);
        CreateBodyPart("Right Thigh", mannequinObj.transform, new Vector2(18, -19), new Vector2(30, 65), new Vector2(0.5f, 1f), 0);
        CreateBodyPart("Right Calf", mannequinObj.transform, new Vector2(18, -85), new Vector2(26, 65), new Vector2(0.5f, 1f), 0);
        CreateBodyPart("Right Foot", mannequinObj.transform, new Vector2(18, -152), new Vector2(30, 18), new Vector2(0.5f, 1f), 0);
        CreateBodyPart("Left Upper Arm", mannequinObj.transform, new Vector2(-44, 105), new Vector2(24, 60), new Vector2(0.5f, 1f), -30);
        CreateBodyPart("Left Forearm", mannequinObj.transform, new Vector2(-74, 53), new Vector2(20, 60), new Vector2(0.5f, 1f), -30);
        CreateBodyPart("Left Hand", mannequinObj.transform, new Vector2(-104, 1), new Vector2(20, 20), new Vector2(0.5f, 1f), -30);
        CreateBodyPart("Right Upper Arm", mannequinObj.transform, new Vector2(44, 105), new Vector2(24, 60), new Vector2(0.5f, 1f), 30);
        CreateBodyPart("Right Forearm", mannequinObj.transform, new Vector2(74, 53), new Vector2(20, 60), new Vector2(0.5f, 1f), 30);
        CreateBodyPart("Right Hand", mannequinObj.transform, new Vector2(104, 1), new Vector2(20, 20), new Vector2(0.5f, 1f), 30);

        GameObject rightContainer = new GameObject("RightContainer");
        rightContainer.transform.SetParent(panelObj.transform, false);
        RectTransform rightRect = rightContainer.AddComponent<RectTransform>();
        rightRect.anchorMin = new Vector2(0.5f, 0.5f); rightRect.anchorMax = new Vector2(0.5f, 0.5f);
        rightRect.anchoredPosition = new Vector2(165, 30);
        rightRect.sizeDelta = new Vector2(280, 380);

        fixedHeaderText = CreateText("FixedHeader", rightContainer.transform, Vector2.zero, new Vector2(250, 60), "", 18, FontStyle.Normal, Color.white, TextAnchor.UpperLeft);
        RectTransform headerRect = fixedHeaderText.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0, 1); headerRect.anchorMax = new Vector2(0, 1);
        headerRect.pivot = new Vector2(0, 1);
        headerRect.anchoredPosition = new Vector2(10, -5); // Đặt đúng PosX = 10, PosY = -5 như ảnh Inspector
        fixedHeaderText.lineSpacing = 1.1f;

        // Create Overall Health Bar (Dạng Slider có viền khung)
        GameObject hbBgObj = new GameObject("HealthBarBG");
        hbBgObj.transform.SetParent(panelObj.transform, false); 
        RectTransform hbBgRect = hbBgObj.AddComponent<RectTransform>();
        hbBgRect.anchorMin = new Vector2(0.5f, 0.5f); hbBgRect.anchorMax = new Vector2(0.5f, 0.5f);
        hbBgRect.pivot = new Vector2(0.5f, 0.5f);
        hbBgRect.sizeDelta = new Vector2(30, 320); // Làm thanh máu dài ra một chút
        hbBgRect.anchoredPosition = new Vector2(0, 0); // Đặt đúng chính giữa màn hình
        
        Image hbBgImg = hbBgObj.AddComponent<Image>();
        hbBgImg.color = new Color(0.1f, 0.1f, 0.1f, 1f); // Nền xám đen
        
        // Thêm viền khung
        Outline hbOutline = hbBgObj.AddComponent<Outline>();
        hbOutline.effectColor = new Color(0.8f, 0.8f, 0.8f, 1f); // Viền xám sáng
        hbOutline.effectDistance = new Vector2(2f, -2f);

        GameObject hbFillObj = new GameObject("HealthBarFill");
        hbFillObj.transform.SetParent(hbBgObj.transform, false);
        RectTransform hbFillRect = hbFillObj.AddComponent<RectTransform>();
        hbFillRect.anchorMin = Vector2.zero; hbFillRect.anchorMax = Vector2.one;
        // Khoảng cách từ fill tới viền khung
        hbFillRect.offsetMin = new Vector2(3, 3); hbFillRect.offsetMax = new Vector2(-3, -3);
        
        healthBarFill = hbFillObj.AddComponent<Image>();
        healthBarFill.type = Image.Type.Filled;
        healthBarFill.fillMethod = Image.FillMethod.Vertical; // Máu dựt dọc

        healthBarFill.fillAmount = 1f;
        healthBarFill.color = Color.green;

        GameObject hbTxtObj = new GameObject("HealthBarText");
        hbTxtObj.transform.SetParent(hbBgObj.transform, false);
        RectTransform hbTxtRect = hbTxtObj.AddComponent<RectTransform>();
        hbTxtRect.anchorMin = Vector2.zero; hbTxtRect.anchorMax = Vector2.one;
        hbTxtRect.offsetMin = hbTxtRect.offsetMax = Vector2.zero;
        
        healthBarText = hbTxtObj.AddComponent<Text>();
        healthBarText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        healthBarText.fontSize = 14;
        healthBarText.fontStyle = FontStyle.Bold;
        healthBarText.color = Color.white;
        healthBarText.alignment = TextAnchor.MiddleCenter;
        healthBarText.text = ""; // Xóa chữ HP ngang
        hbTxtObj.SetActive(false); // Ẩn luôn vì giao diện Zomboid không có chữ HP ở đây


        GameObject scrollViewObj = new GameObject("Scroll View");
        scrollViewObj.transform.SetParent(rightContainer.transform, false);
        RectTransform scrollRectTransform = scrollViewObj.AddComponent<RectTransform>();
        scrollRectTransform.anchorMin = new Vector2(0, 1); scrollRectTransform.anchorMax = new Vector2(0, 1);
        scrollRectTransform.pivot = new Vector2(0, 1);
        scrollRectTransform.sizeDelta = new Vector2(250, 300); // Rộng 250, Cao 300
        scrollRectTransform.anchoredPosition = new Vector2(10, -75); // Nằm ngay bên dưới tiêu đề Status (cao lên đúng vị trí hình 2)

        ScrollRect scrollRect = scrollViewObj.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.scrollSensitivity = 25f;

        GameObject viewportObj = new GameObject("Viewport");
        viewportObj.transform.SetParent(scrollViewObj.transform, false);
        RectTransform viewportRect = viewportObj.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero; viewportRect.anchorMax = Vector2.one;
        viewportRect.sizeDelta = Vector2.zero;
        viewportRect.offsetMin = Vector2.zero; viewportRect.offsetMax = Vector2.zero;

        Image viewportBg = viewportObj.AddComponent<Image>();
        viewportBg.color = new Color(0, 0, 0, 0.01f);
        viewportObj.AddComponent<Mask>().showMaskGraphic = false;

        GameObject contentObj = new GameObject("Content");
        contentObj.transform.SetParent(viewportObj.transform, false);
        textContentRect = contentObj.AddComponent<RectTransform>();
        textContentRect.anchorMin = new Vector2(0, 1); textContentRect.anchorMax = new Vector2(0, 1);
        textContentRect.pivot = new Vector2(0, 1);
        textContentRect.anchoredPosition = Vector2.zero;
        textContentRect.sizeDelta = new Vector2(250, 0);

        VerticalLayoutGroup contentLayout = contentObj.AddComponent<VerticalLayoutGroup>();
        contentLayout.childControlHeight = true;
        contentLayout.childControlWidth = true;
        contentLayout.childForceExpandHeight = false;
        contentLayout.childForceExpandWidth = true;
        contentLayout.spacing = 5;

        ContentSizeFitter fitter = contentObj.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewportRect;
        scrollRect.content = textContentRect;

        contextMenuPanel = new GameObject("ContextMenuPanel");
        contextMenuPanel.transform.SetParent(panelObj.transform, false);

        Canvas ctxCanvas = contextMenuPanel.AddComponent<Canvas>();
        ctxCanvas.overrideSorting = true;
        ctxCanvas.sortingOrder = 150;
        contextMenuPanel.AddComponent<GraphicRaycaster>();

        RectTransform ctxRect = contextMenuPanel.GetComponent<RectTransform>();
        ctxRect.anchorMin = new Vector2(1f, 0);
        ctxRect.anchorMax = new Vector2(1f, 0);
        ctxRect.pivot = new Vector2(1f, 1f);
        ctxRect.sizeDelta = new Vector2(200, 50);
        ctxRect.anchoredPosition = new Vector2(0, 15);

        Image ctxBg = contextMenuPanel.AddComponent<Image>();
        ctxBg.color = new Color(0.12f, 0.12f, 0.14f, 0.98f);

        Outline ctxOutline = contextMenuPanel.AddComponent<Outline>();
        ctxOutline.effectColor = new Color(0.6f, 0.6f, 0.6f, 1f);
        ctxOutline.effectDistance = new Vector2(1.5f, -1.5f);

        GameObject ctxContent = new GameObject("CtxContent");
        ctxContent.transform.SetParent(contextMenuPanel.transform, false);

        RectTransform ctxContentRect = ctxContent.AddComponent<RectTransform>();
        ctxContentRect.anchorMin = Vector2.zero; ctxContentRect.anchorMax = Vector2.one;
        ctxContentRect.offsetMin = new Vector2(4, 4); ctxContentRect.offsetMax = new Vector2(-4, -4);

        VerticalLayoutGroup ctxLayout = ctxContent.AddComponent<VerticalLayoutGroup>();
        ctxLayout.spacing = 2;
        ctxLayout.childControlHeight = true;
        ctxLayout.childForceExpandHeight = true;

        contextMenuPanel.SetActive(false);

        Text instructionText = CreateText("Instruction", panelObj.transform, new Vector2(0, -270), new Vector2(800, 30),
            "Right click on injuries: Apply/Remove Bandage | Scroll: View details", 16, FontStyle.Normal, new Color(0.5f, 0.5f, 0.5f), TextAnchor.MiddleCenter);
        instructionText.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);

        panelObj.SetActive(false);
    }

    private void CreateBodyPart(string partName, Transform parent, Vector2 position, Vector2 size, Vector2 pivot, float rotation)
    {
        GameObject partObj = new GameObject("Part_" + partName);
        partObj.transform.SetParent(parent, false);

        RectTransform rect = partObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localEulerAngles = new Vector3(0, 0, rotation);

        Image img = partObj.AddComponent<Image>();
        img.color = colHealthy;

        Outline outline = partObj.AddComponent<Outline>();
        outline.effectColor = colOutline;
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        BodyPartData data = new BodyPartData { Name = partName, Img = img };
        bodyParts.Add(partName, data);
    }

    private Text CreateText(string name, Transform parent, Vector2 pos, Vector2 size, string text, int fontSize, FontStyle style, Color color, TextAnchor align)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;

        Text txt = go.AddComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = fontSize;
        txt.fontStyle = style;
        txt.color = color;
        txt.alignment = align;
        txt.text = text;
        return txt;
    }

    private void FindLocalPlayerCache()
    {
        if (PlayerHealth.LocalHealthInstance != null)
        {
            localPlayerHealth = PlayerHealth.LocalHealthInstance;
            localInventory = localPlayerHealth.GetComponent<InventorySystem>();
        }
        else
        {
            // Fallback: Tìm PlayerHealth có InputAuthority
            foreach (var ph in FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None))
            {
                if (ph != null && ph.Object != null && ph.HasInputAuthority)
                {
                    PlayerHealth.LocalHealthInstance = ph;
                    localPlayerHealth = ph;
                    localInventory = ph.GetComponent<InventorySystem>();
                    Debug.Log("[HEALTH] ✅ Đã tìm thấy LocalHealthInstance qua fallback");
                    return;
                }
            }
            localPlayerHealth = null;
            localInventory = null;
        }
    }

    public void TakeRandomZombieAttack(string forcedTarget = "")
    {
        FindLocalPlayerCache();

        string targetPart = forcedTarget;

        if (string.IsNullOrEmpty(targetPart))
        {
            List<string> healthyParts = new List<string>();

            foreach (var kvp in bodyParts)
            {
                if (kvp.Key == "Neck") continue;
                if (kvp.Value.Injuries.Count == 0)
                {
                    healthyParts.Add(kvp.Key);
                }
            }

            if (healthyParts.Count == 0)
            {
                healthyParts.AddRange(bodyParts.Keys);
                healthyParts.Remove("Neck");
            }

            float hitRoll = Random.Range(0f, 100f);

            if (hitRoll <= 5f) targetPart = "Neck";
            else targetPart = healthyParts[Random.Range(0, healthyParts.Count)];
        }

        InjuryType injuryResult;
        float injuryRoll = Random.Range(0f, 100f);

        if (targetPart == "Neck")
        {
            injuryResult = InjuryType.Bitten;
        }
        else
        {
            if (injuryRoll <= 5f) injuryResult = InjuryType.Bitten;
            else if (injuryRoll <= 52.5f) injuryResult = InjuryType.Laceration;
            else injuryResult = InjuryType.Scratched;
        }

        BodyPartData part = bodyParts[targetPart];
        part.IsBandaged = false;

        if (!part.Injuries.Contains(injuryResult))
        {
            part.Injuries.Add(injuryResult);
        }

        if (localPlayerHealth != null)
        {
            if (injuryResult == InjuryType.Bitten)
            {
                localPlayerHealth.SetBitten(); // Kích hoạt 100% hóa Zombie bên logic
            }
        }

        EvaluateGlobalBleeding();

        if (isOpen)
        {
            UpdateAllUI();
        }
    }

    private void EvaluateGlobalBleeding()
    {
        if (localPlayerHealth == null) return;

        bool hasUnbandagedWounds = false;

        foreach (var p in bodyParts.Values)
        {
            if (p.Injuries.Count > 0 && !p.IsBandaged)
            {
                hasUnbandagedWounds = true;
                break;
            }
        }

        // Báo cho PlayerHealth biết để nó chạy tụt máu từ từ (bleedDamagePerSecond)
        localPlayerHealth.SetGlobalBleeding(hasUnbandagedWounds);
    }

    private void UpdateAllUI()
    {
        FindLocalPlayerCache();

        float displayHP = 100f;
        bool isPainReal = false;

        float maxHP = 100f;
        if (localPlayerHealth != null)
        {
            displayHP = localPlayerHealth.CurrentHealthSafe;
            maxHP = localPlayerHealth.maxHealth;
            isPainReal = (localPlayerHealth.Object != null && localPlayerHealth.Object.IsValid) ? localPlayerHealth.isInPain : false;
        }

        // Bỏ cập nhật ngang lập tức, thay bằng hiệu ứng Lerp ở hàm Update()
        // Chỉ lưu lại text nếu có
        if (healthBarText != null && healthBarText.gameObject.activeSelf)
        {
            healthBarText.text = $"HP: {Mathf.RoundToInt(displayHP)}/{Mathf.RoundToInt(maxHP)}";
        }

        StringBuilder headerText = new StringBuilder();
        List<BodyPartData> injuredParts = new List<BodyPartData>();

        foreach (var part in bodyParts.Values)
        {
            if (part.Injuries.Count > 0)
            {
                injuredParts.Add(part);
            }

            if (part.Injuries.Count == 0) part.Img.color = colHealthy;
            else if (part.IsBandaged) part.Img.color = colBandaged;
            else part.Img.color = colInjured;
        }

        // 🔥 LOGIC CHUẨN: CHỈ HIỆN MỨC ĐỘ MÁU VÀ MỨC ĐỘ ĐAU
        headerText.AppendLine("Overall Body Status");
        string overallStatus = "";
        string statusColor = "<color=#ffaaaa>";

        if (displayHP >= 100f) { overallStatus = "OK"; statusColor = "<color=white>"; }
        else if (displayHP >= 90f) overallStatus = "Slight Damage";
        else if (displayHP >= 80f) overallStatus = "Minor Damage";
        else if (displayHP >= 60f) { overallStatus = "Moderate Damage"; statusColor = "<color=#ff6666>"; }
        else if (displayHP >= 50f) { overallStatus = "Severe Damage"; statusColor = "<color=#ff4444>"; }
        else if (displayHP >= 40f) { overallStatus = "Very Severe Damage"; statusColor = "<color=#ff2222>"; }
        else if (displayHP >= 20f) { overallStatus = "Critical Damage"; statusColor = "<color=red>"; }
        else if (displayHP >= 10f) { overallStatus = "Highly Critical Damage"; statusColor = "<color=red>"; }
        else if (displayHP > 0f) { overallStatus = "Terminal Damage"; statusColor = "<color=red>"; }
        else { overallStatus = "Deceased"; statusColor = "<color=black>"; }

        headerText.AppendLine($"{statusColor}{overallStatus}</color>");

        // Nếu đau thì hiện chữ Pain màu xám nhạt dưới dòng máu (Giống ảnh)
        if (isPainReal) headerText.AppendLine("<color=#cccccc>Minor Pain</color>");

        fixedHeaderText.text = headerText.ToString();

        HideContextMenu();

        for (int i = textContentRect.childCount - 1; i >= 0; i--)
        {
            Destroy(textContentRect.GetChild(i).gameObject);
        }

        foreach (var part in injuredParts)
        {
            CreateInjuryEntry(part);
        }
    }

    private void CreateInjuryEntry(BodyPartData part)
    {
        GameObject entryObj = new GameObject("Entry_" + part.Name);
        entryObj.transform.SetParent(textContentRect, false);

        RectTransform rect = entryObj.AddComponent<RectTransform>();
        LayoutElement layout = entryObj.AddComponent<LayoutElement>();
        layout.minHeight = 45;

        Image bg = entryObj.AddComponent<Image>();
        bg.color = Color.clear; // Xóa hoàn toàn khung màu nền (trong suốt 100%)

        EventTrigger trigger = entryObj.AddComponent<EventTrigger>();
        EventTrigger.Entry rightClickEntry = new EventTrigger.Entry();
        rightClickEntry.eventID = EventTriggerType.PointerClick;
        rightClickEntry.callback.AddListener((data) => {
            PointerEventData pointerData = (PointerEventData)data;
            if (pointerData.button == PointerEventData.InputButton.Right && !isHealing)
            {
                ShowContextMenu(part, entryObj.transform);
            }
        });
        trigger.triggers.Add(rightClickEntry);

        List<string> lines = new List<string>();
        lines.Add($"<color=white>{part.Name}</color>");

        // 🔥 BĂNG BÓ: Đổi thành chữ xanh, MẤT chữ Bleeding đỏ
        if (part.IsBandaged)
        {
            lines.Add("<color=#4ade80>  - Bandaged</color>");

            // Nếu bị cắn thì dù băng lại, nó vẫn là vết cắn (nhưng ngưng chảy máu)
            if (part.Injuries.Contains(InjuryType.Bitten))
            {
                lines.Add("<color=#ff4444>  - Bitten</color>");
            }
        }
        else
        {
            foreach (var inj in part.Injuries)
            {
                lines.Add($"<color=#ff4444>  - {inj.ToString()}</color>");
            }

            // Nếu chưa băng bó, thêm dòng Bleeding y chang ảnh
            if (part.Injuries.Count > 0)
            {
                lines.Add("<color=#ff4444>  - Bleeding</color>");
            }
        }

        string finalText = string.Join("\n", lines);

        Text txt = CreateText("Text", entryObj.transform, Vector2.zero, new Vector2(250, 35), finalText, 18, FontStyle.Normal, Color.white, TextAnchor.UpperLeft);
        txt.lineSpacing = 1.1f;

        RectTransform txtRect = txt.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero; txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = Vector2.zero; txtRect.offsetMax = Vector2.zero;

        Canvas.ForceUpdateCanvases();
        layout.minHeight = txt.preferredHeight + 10;
    }

    void Update()
    {
        // 🔥 ĐĂNG KÝ NGƯỜI CHƠI ĐỘNG NẾU CHƯA TÌM THẤY
        if (localPlayerHealth == null)
        {
            FindLocalPlayerCache();
            if (localPlayerHealth != null && isOpen)
            {
                UpdateAllUI();
            }
        }
        
        // Hiệu ứng máu giật mượt mà (Lerp)
        if (localPlayerHealth != null && localPlayerHealth.Object != null && localPlayerHealth.Object.IsValid && healthBarFill != null)
        {
            float targetHealth = localPlayerHealth.currentHealth;
            if (Mathf.Abs(currentDisplayedHealth - targetHealth) > 0.1f)
            {
                // Tốc độ giật máu: Nếu mất máu đột ngột -> giật nhanh, hồi máu -> lên chậm
                float lerpSpeed = (targetHealth < currentDisplayedHealth) ? 8f : 2f; 
                currentDisplayedHealth = Mathf.Lerp(currentDisplayedHealth, targetHealth, Time.deltaTime * lerpSpeed);
                
                float maxHP = localPlayerHealth.maxHealth;
                float pct = maxHP > 0 ? currentDisplayedHealth / maxHP : 0f;
                healthBarFill.fillAmount = pct;
                healthBarFill.color = Color.Lerp(colInjured, Color.white, pct); // Theo Hình 2 thì máu từ Trắng ngả xuống Đỏ
            }
        }
        if (Input.GetMouseButtonDown(0) && EventSystem.current != null && !EventSystem.current.IsPointerOverGameObject())
        {
            HideContextMenu();
        }

        if (isOpen && localPlayerHealth != null)
        {
            UpdateHeaderTextOnly();
        }
    }

    public void SetOpenState(bool state)
    {
        if (isHealing) return;
        if (isOpen == state) return;

        isOpen = state;
        if (panelObj != null) panelObj.SetActive(isOpen);
        HideContextMenu();

        if (isOpen)
        {
            UpdateAllUI();
        }
    }

    private void TogglePanel()
    {
        isOpen = !isOpen;
        panelObj.SetActive(isOpen);
        HideContextMenu();

        if (isOpen)
        {
            UpdateAllUI();
        }
    }

    private void UpdateHeaderTextOnly()
    {
        if (localPlayerHealth == null) return;

        float displayHP = localPlayerHealth.CurrentHealthSafe;

        StringBuilder headerText = new StringBuilder();
        headerText.AppendLine("Overall Body Status");
        string overallStatus = "";
        string statusColor = "<color=#ffaaaa>";

        if (displayHP >= 100f) { overallStatus = "OK"; statusColor = "<color=white>"; }
        else if (displayHP >= 90f) overallStatus = "Slight Damage";
        else if (displayHP >= 80f) overallStatus = "Minor Damage";
        else if (displayHP >= 60f) { overallStatus = "Moderate Damage"; statusColor = "<color=#ff6666>"; }
        else if (displayHP >= 50f) { overallStatus = "Severe Damage"; statusColor = "<color=#ff4444>"; }
        else if (displayHP >= 40f) { overallStatus = "Very Severe Damage"; statusColor = "<color=#ff2222>"; }
        else if (displayHP >= 20f) { overallStatus = "Critical Damage"; statusColor = "<color=red>"; }
        else if (displayHP >= 10f) { overallStatus = "Highly Critical Damage"; statusColor = "<color=red>"; }
        else if (displayHP > 0f) { overallStatus = "Terminal Damage"; statusColor = "<color=red>"; }
        else { overallStatus = "Deceased"; statusColor = "<color=black>"; }

        headerText.AppendLine($"{statusColor}{overallStatus}</color>");

        if (localPlayerHealth.isInPain) headerText.AppendLine("<color=#cccccc>Minor Pain</color>");

        fixedHeaderText.text = headerText.ToString();
    }

    private void ShowContextMenu(BodyPartData part, Transform entryTransform)
    {
        FindLocalPlayerCache();
        selectedPartNameForContext = part.Name;

        Transform ctxContent = contextMenuPanel.transform.Find("CtxContent");

        if (ctxContent != null)
        {
            for (int i = ctxContent.childCount - 1; i >= 0; i--)
            {
                Destroy(ctxContent.GetChild(i).gameObject);
            }
        }

        contextMenuPanel.SetActive(true);
        contextMenuPanel.transform.SetParent(entryTransform, false);

        RectTransform ctxRect = contextMenuPanel.GetComponent<RectTransform>();
        ctxRect.anchorMin = new Vector2(1f, 0f);
        ctxRect.anchorMax = new Vector2(1f, 0f);
        ctxRect.pivot = new Vector2(1f, 1f);
        ctxRect.anchoredPosition = new Vector2(0, 15);

        if (part.IsBandaged)
        {
            CreateContextMenuButton("Remove Bandage", () => StartCoroutine(HealActionRoutine(part, "Remove")), ctxContent);
        }
        else if (part.Injuries.Count > 0)
        {
            bool hasBandage = false;
            ItemData bandageData = null;

            if (localInventory != null)
            {
                foreach (var slot in localInventory.slots)
                {
                    if (slot.item != null)
                    {
                        string itemNameLower = slot.item.itemName.ToLower();
                        if (itemNameLower.Contains("bandage") || itemNameLower.Contains("băng"))
                        {
                            hasBandage = true;
                            bandageData = slot.item;
                            break;
                        }
                    }
                }
            }

            if (hasBandage)
            {
                CreateContextMenuButton("Apply Bandage", () => StartCoroutine(HealActionRoutine(part, "Apply", bandageData)), ctxContent);
            }
            else
            {
                CreateContextMenuButton("<color=gray>No Bandages</color>", null, ctxContent);
            }
        }
    }

    private void HideContextMenu()
    {
        if (contextMenuPanel != null && contextMenuPanel.activeSelf)
        {
            contextMenuPanel.SetActive(false);
            if (panelObj != null)
            {
                contextMenuPanel.transform.SetParent(panelObj.transform, false);
            }
        }
    }

    private void CreateContextMenuButton(string label, UnityEngine.Events.UnityAction action, Transform parentContent)
    {
        GameObject btnObj = new GameObject("CtxBtn_" + label);
        btnObj.transform.SetParent(parentContent, false);

        RectTransform rect = btnObj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(200, 50);

        Image img = btnObj.AddComponent<Image>();
        img.color = new Color(0.2f, 0.2f, 0.22f, 1f);

        Button btn = btnObj.AddComponent<Button>();
        if (action != null)
        {
            btn.onClick.AddListener(action);
            btn.onClick.AddListener(HideContextMenu);

            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(0.6f, 0.6f, 0.6f, 1f);
            cb.pressedColor = new Color(0.4f, 0.4f, 0.4f, 1f);
            cb.selectedColor = Color.white;
            btn.colors = cb;
        }
        else
        {
            btn.interactable = false;
        }

        Text txt = CreateText("Txt", btnObj.transform, Vector2.zero, new Vector2(180, 50), label, 18, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        RectTransform tRect = txt.GetComponent<RectTransform>();
        tRect.anchorMin = Vector2.zero; tRect.anchorMax = Vector2.one;
        tRect.offsetMin = new Vector2(0, 0);
        tRect.offsetMax = Vector2.zero;
    }

    private IEnumerator HealActionRoutine(BodyPartData part, string actionType, ItemData itemUsed = null)
    {
        isHealing = true;
        HideContextMenu();

        isOpen = false;
        panelObj.SetActive(false);

        float duration = 2.5f;
        if (itemUsed != null && itemUsed.useTime > 0)
        {
            duration = itemUsed.useTime;
        }

        // 👇 THÊM ĐOẠN NÀY: Xác định chữ sẽ hiển thị dựa vào hành động
        string actionText = (actionType == "Apply") ? "Applying Bandage..." : "Removing Bandage...";

        if (AutoUIManager.Instance != null)
        {
            // Truyền actionText vào
            AutoUIManager.Instance.ShowReloadUI(0, duration, actionText);
        }

        float timer = 0;

        while (timer < duration)
        {
            timer += Time.deltaTime;

            if (AutoUIManager.Instance != null)
            {
                // Truyền actionText vào
                AutoUIManager.Instance.ShowReloadUI(timer, duration, actionText);
            }

            yield return null;
        }

        if (actionType == "Apply" && itemUsed != null)
        {
            localInventory.ConsumeItem(itemUsed, 1);
            part.IsBandaged = true;
        }
        else if (actionType == "Remove")
        {
            part.IsBandaged = false;

            bool hasBitten = part.Injuries.Contains(InjuryType.Bitten);
            part.Injuries.Clear();
            if (hasBitten) part.Injuries.Add(InjuryType.Bitten);
        }

        EvaluateGlobalBleeding();
        EndHealAction();

        TogglePanel();
    }

    private void EndHealAction()
    {
        isHealing = false;
        if (AutoUIManager.Instance != null)
        {
            AutoUIManager.Instance.HideReloadUI();
        }
    }

    public List<InjuryType> GetActiveGlobalInjuries()
    {
        List<InjuryType> uniqueInjuries = new List<InjuryType>();

        foreach (var part in bodyParts.Values)
        {
            if (!part.IsBandaged)
            {
                foreach (var inj in part.Injuries)
                {
                    if (!uniqueInjuries.Contains(inj))
                    {
                        uniqueInjuries.Add(inj);
                    }
                }
            }
        }
        return uniqueInjuries;
    }

    public void ResetAllInjuries()
    {
        FindLocalPlayerCache(); // 🔥 Cập nhật cache người chơi trước khi xử lý
        foreach (var part in bodyParts.Values)
        {
            part.Injuries.Clear();
            part.IsBandaged = false;
            if (part.Img != null)
            {
                part.Img.color = colHealthy;
            }
        }
        EvaluateGlobalBleeding();
        UpdateAllUI();
    }

    private void OnDestroy()
    {
        if (healthCanvas != null)
        {
            Destroy(healthCanvas);
        }
        if (instance == this) instance = null;
    }
}