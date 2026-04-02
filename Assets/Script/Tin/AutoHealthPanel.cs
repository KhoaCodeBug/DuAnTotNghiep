using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using System.Text;

public class AutoHealthPanel : MonoBehaviour
{
    public static AutoHealthPanel Instance;

    private GameObject healthCanvas;
    private GameObject panelObj;

    // --- UI ELEMENTS ---
    private Text fixedHeaderText;
    private Text rightPanelText;
    private RectTransform textContentRect;

    private bool isOpen = false;

    // --- ZOMBIE INJURY LOGIC ---
    public enum InjuryType { Scratched, Laceration, Bitten }

    // 🔥 DÙNG LIST ĐỂ CHỒNG DEBUFF
    private class BodyPartData
    {
        public string Name;
        public List<InjuryType> Injuries = new List<InjuryType>(); // Chứa N vết thương
        public bool IsBandaged = false;
        public Image Img;
    }
    private Dictionary<string, BodyPartData> bodyParts = new Dictionary<string, BodyPartData>();

    private PlayerHealth localPlayerHealth;

    private float currentHP = 100f;

    // BẢNG MÀU CHUẨN
    private Color colHealthy = new Color(0.2f, 0.22f, 0.25f, 1f);
    private Color colInjured = new Color(0.65f, 0.15f, 0.15f, 1f);
    private Color colBandaged = new Color(0.7f, 0.7f, 0.7f, 1f);
    private Color colOutline = new Color(0f, 0f, 0f, 0.9f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoSpawn()
    {
        if (Instance == null)
        {
            GameObject go = new GameObject("--- AUTO HEALTH MANAGER ---");
            go.AddComponent<AutoHealthPanel>();
            DontDestroyOnLoad(go);
        }
    }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        SetupHealthUI();
    }

    void SetupHealthUI()
    {
        // 1. CANVAS
        healthCanvas = new GameObject("--- AUTO HEALTH CANVAS ---");
        Canvas canvas = healthCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 110;
        CanvasScaler scaler = healthCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        healthCanvas.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(healthCanvas);

        // 2. MAIN PANEL
        panelObj = new GameObject("HealthPanelBG");
        panelObj.transform.SetParent(healthCanvas.transform, false);
        RectTransform panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f); panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(900, 600);

        Image panelBg = panelObj.AddComponent<Image>();
        panelBg.color = new Color(0.06f, 0.07f, 0.08f, 1f);

        GameObject borderObj = new GameObject("PanelBorder");
        borderObj.transform.SetParent(panelObj.transform, false);
        RectTransform borderRect = borderObj.AddComponent<RectTransform>();
        borderRect.anchorMin = new Vector2(0, 0); borderRect.anchorMax = new Vector2(1, 1);
        borderRect.offsetMin = Vector2.zero; borderRect.offsetMax = Vector2.zero;

        Image borderImg = borderObj.AddComponent<Image>();
        borderImg.color = new Color(0, 0, 0, 1f); // 🔥 Đã fix vụ viền tàng hình
        Outline borderOutline = borderObj.AddComponent<Outline>();
        borderOutline.effectColor = Color.white;
        borderOutline.effectDistance = new Vector2(2f, -2f);

        // ==========================================
        // 🔥 TRÁI: HÌNH NỘM MÔ PHỎNG CƠ THỂ
        // ==========================================
        GameObject mannequinObj = new GameObject("MannequinContainer");
        mannequinObj.transform.SetParent(panelObj.transform, false);
        RectTransform manRect = mannequinObj.AddComponent<RectTransform>();
        manRect.anchorMin = new Vector2(0.5f, 0.5f); manRect.anchorMax = new Vector2(0.5f, 0.5f);
        manRect.anchoredPosition = new Vector2(-225, 20);
        manRect.localScale = new Vector3(1.1f, 1.1f, 1f);

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

        // ==========================================
        // 🔥 PHẢI: KHUNG TEXT & DANH SÁCH CUỘN
        // ==========================================
        GameObject rightContainer = new GameObject("RightContainer");
        rightContainer.transform.SetParent(panelObj.transform, false);
        RectTransform rightRect = rightContainer.AddComponent<RectTransform>();
        rightRect.anchorMin = new Vector2(0.5f, 0.5f); rightRect.anchorMax = new Vector2(0.5f, 0.5f);
        rightRect.anchoredPosition = new Vector2(200, 0);
        rightRect.sizeDelta = new Vector2(400, 500);

        fixedHeaderText = CreateText("FixedHeader", rightContainer.transform, Vector2.zero, new Vector2(400, 100), "", 22, FontStyle.Bold, Color.white, TextAnchor.UpperLeft);
        RectTransform headerRect = fixedHeaderText.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0, 1); headerRect.anchorMax = new Vector2(0, 1);
        headerRect.pivot = new Vector2(0.5f, 1);
        headerRect.anchoredPosition = new Vector2(200, 0);
        fixedHeaderText.lineSpacing = 1.3f;

        GameObject scrollViewObj = new GameObject("Scroll View");
        scrollViewObj.transform.SetParent(rightContainer.transform, false);
        RectTransform scrollRectTransform = scrollViewObj.AddComponent<RectTransform>();
        scrollRectTransform.anchorMin = new Vector2(0, 0); scrollRectTransform.anchorMax = new Vector2(0, 0);
        scrollRectTransform.pivot = new Vector2(0.5f, 1);
        scrollRectTransform.sizeDelta = new Vector2(400, 390);
        scrollRectTransform.anchoredPosition = new Vector2(200, 400);

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
        textContentRect.sizeDelta = new Vector2(400, 0);

        ContentSizeFitter fitter = contentObj.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        rightPanelText = contentObj.AddComponent<Text>();
        rightPanelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        rightPanelText.fontSize = 20;
        rightPanelText.fontStyle = FontStyle.Bold;
        rightPanelText.color = Color.white;
        rightPanelText.alignment = TextAnchor.UpperLeft;
        rightPanelText.lineSpacing = 1.3f;

        scrollRect.viewport = viewportRect;
        scrollRect.content = textContentRect;

        // 🔥 Cập nhật lại Hướng dẫn (bỏ phần Left click)
        Text instructionText = CreateText("Instruction", panelObj.transform, new Vector2(0, -270), new Vector2(800, 30),
            "Right click: Apply/Remove Bandage | Scroll: View details", 16, FontStyle.Normal, new Color(0.5f, 0.5f, 0.5f), TextAnchor.MiddleCenter);
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

        // 🔥 XÓA DÒNG LEFT CLICK TEST TẠI ĐÂY
        // Button btn = partObj.AddComponent<Button>();
        // btn.onClick.AddListener(() => TakeRandomZombieAttack(""));

        EventTrigger trigger = partObj.AddComponent<EventTrigger>();
        EventTrigger.Entry rightClickEntry = new EventTrigger.Entry();
        rightClickEntry.eventID = EventTriggerType.PointerClick;
        rightClickEntry.callback.AddListener((data) => {
            PointerEventData pointerData = (PointerEventData)data;
            if (pointerData.button == PointerEventData.InputButton.Right)
            {
                OnBodyPartRightClicked(partName);
            }
        });
        trigger.triggers.Add(rightClickEntry);

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

    private void FindLocalPlayerHealth()
    {
        if (localPlayerHealth != null) return;

        PlayerHealth[] allPlayers = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);
        foreach (var p in allPlayers)
        {
            if (p.HasInputAuthority)
            {
                localPlayerHealth = p;
                break;
            }
        }
    }

    public void TakeRandomZombieAttack(string forcedTarget = "")
    {
        FindLocalPlayerHealth();

        string targetPart = forcedTarget;

        if (string.IsNullOrEmpty(targetPart))
        {
            string[] attackableParts = new string[] {
                "Upper Torso", "Lower Torso",
                "Left Upper Arm", "Left Forearm", "Left Hand",
                "Right Upper Arm", "Right Forearm", "Right Hand",
                "Left Thigh", "Left Calf", "Left Foot",
                "Right Thigh", "Right Calf", "Right Foot"
            };

            float hitRoll = Random.Range(0f, 100f);
            if (hitRoll <= 5f) targetPart = "Neck";
            else targetPart = attackableParts[Random.Range(0, attackableParts.Length)];
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
        part.Injuries.Add(injuryResult);

        if (localPlayerHealth != null)
        {
            localPlayerHealth.TakeDamage(10f, false);
        }

        EvaluateGlobalBleeding();
        UpdateAllUI();
    }

    private void OnBodyPartRightClicked(string partName)
    {
        FindLocalPlayerHealth();
        BodyPartData part = bodyParts[partName];

        if (partName == "Neck" && part.Injuries.Contains(InjuryType.Bitten))
        {
            Debug.Log("Án tử: Không thể băng bó vết cắn ở yết hầu!");
            return;
        }

        if (part.Injuries.Count > 0 && !part.IsBandaged)
        {
            part.IsBandaged = true;
        }
        else if (part.IsBandaged)
        {
            part.IsBandaged = false;
            part.Injuries.Clear();
        }

        EvaluateGlobalBleeding();
        UpdateAllUI();
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

        if (hasUnbandagedWounds)
        {
            localPlayerHealth.SetGlobalBleeding(true);
        }
        else
        {
            localPlayerHealth.SetGlobalBleeding(false);
        }
    }

    private void UpdateAllUI()
    {
        FindLocalPlayerHealth();

        float displayHP = 100f;
        bool isBleedingReal = false;
        bool isPainReal = false;

        if (localPlayerHealth != null)
        {
            displayHP = localPlayerHealth.currentHealth;
            isBleedingReal = localPlayerHealth.isBleeding;
            isPainReal = localPlayerHealth.isInPain;
        }

        StringBuilder headerText = new StringBuilder();
        StringBuilder listText = new StringBuilder();

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

        // ĐÃ SỬA LỖI currentHealth THÀNH currentHP TẠI ĐÂY
        currentHP = Mathf.Clamp(displayHP, 0, 100f);

        headerText.AppendLine("Overall Body Status");
        string overallStatus = "";
        string statusColor = "<color=#ffaaaa>";

        if (currentHP >= 100f) { overallStatus = "OK"; statusColor = "<color=white>"; }
        else if (currentHP >= 90f) overallStatus = "Slight Damage";
        else if (currentHP >= 80f) overallStatus = "Minor Damage";
        else if (currentHP >= 60f) overallStatus = "Moderate Damage";
        else if (currentHP >= 50f) overallStatus = "Severe Damage";
        else if (currentHP >= 40f) overallStatus = "Very Severe Damage";
        else if (currentHP >= 20f) overallStatus = "Critical Damage";
        else if (currentHP >= 10f) overallStatus = "Highly Critical Damage";
        else if (currentHP > 0f) overallStatus = "Terminal Damage";
        else overallStatus = "Deceased";

        headerText.AppendLine($"{statusColor}{overallStatus}</color>");

        if (isPainReal) headerText.AppendLine("Pain");
        if (isBleedingReal) headerText.AppendLine("<color=red>Bleeding</color>");

        fixedHeaderText.text = headerText.ToString();

        foreach (var part in injuredParts)
        {
            listText.AppendLine($"<color=white>{part.Name}</color>");

            if (part.IsBandaged)
            {
                listText.AppendLine("<color=#4ade80>  - Bandaged</color>");
            }
            else
            {
                foreach (var inj in part.Injuries)
                {
                    listText.AppendLine($"<color=#ff4444>  - {inj.ToString()}</color>");
                }
            }
        }

        rightPanelText.text = listText.ToString();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.H))
        {
            isOpen = !isOpen;
            panelObj.SetActive(isOpen);

            if (isOpen)
            {
                UpdateAllUI();
            }
        }

        if (isOpen && localPlayerHealth != null)
        {
            UpdateAllUI();
        }
    }

    // ==========================================
    // 🔥 HÀM NÀY ĐỂ BÊN PLAYERHEALTH LẤY DEBUFF VÀ HIỂN THỊ
    // ==========================================
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
}