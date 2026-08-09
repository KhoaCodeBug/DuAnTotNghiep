using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD độ ồn cục bộ. UI chỉ thể hiện tiếng người chơi tự tạo; voice từ người khác chỉ tạo flash cyan.
/// </summary>
public class AutoNoiseMeter : MonoBehaviour
{
    private static AutoNoiseMeter instance;
    public static AutoNoiseMeter Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("--- AUTO NOISE METER ---");
                instance = go.AddComponent<AutoNoiseMeter>();
                DontDestroyOnLoad(go);
            }

            return instance;
        }
    }

    private const int SegmentCount = 18;
    private readonly Image[] segments = new Image[SegmentCount];
    private Image panel;
    private Image border;
    private Text title;
    private Text sourceLabel;

    private float movementNoise;
    private float transientNoise;
    private float displayedNoise;
    private float pulseTimer;
    private float heardVoiceTimer;
    private float sourceTimer;
    private string sourceName = "YÊN LẶNG";
    private bool tutorialHighlight;

    public static void SetMovementNoise(bool isMoving, bool isRunning, bool isCrouching)
    {
        Instance.SetMovement(isMoving, isRunning, isCrouching);
    }

    public static void ReportTransientNoise(float intensity, string source)
    {
        Instance.AddTransientNoise(intensity, source);
    }

    public static void ReportHeardVoice(float proximity)
    {
        Instance.FlashHeardVoice(proximity);
    }

    public static void SetHUDVisible(bool visible)
    {
        if (instance != null && instance.canvasObject != null)
        {
            instance.canvasObject.SetActive(visible);
        }
    }

    public static void SetTutorialHighlight(bool highlighted)
    {
        if (instance == null && !highlighted) return;
        Instance.tutorialHighlight = highlighted;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        BuildUi();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private GameObject canvasObject;

    private void BuildUi()
    {
        canvasObject = new GameObject("--- NOISE METER CANVAS ---");
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 105;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject panelObject = new GameObject("NoiseMeterPanel");
        panelObject.transform.SetParent(canvasObject.transform, false);
        RectTransform panelRect = panelObject.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.zero;
        panelRect.pivot = Vector2.zero;
        panelRect.anchoredPosition = new Vector2(38f, 36f);
        panelRect.sizeDelta = new Vector2(350f, 82f);

        panel = panelObject.AddComponent<Image>();
        panel.color = new Color(0.025f, 0.035f, 0.045f, 0.9f);
        Outline panelOutline = panelObject.AddComponent<Outline>();
        panelOutline.effectColor = new Color(0f, 0f, 0f, 0.95f);
        panelOutline.effectDistance = new Vector2(2f, -2f);

        GameObject borderObject = new GameObject("NoiseMeterGlow");
        borderObject.transform.SetParent(panelObject.transform, false);
        RectTransform borderRect = borderObject.AddComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.offsetMin = new Vector2(2f, 2f);
        borderRect.offsetMax = new Vector2(-2f, -2f);
        border = borderObject.AddComponent<Image>();
        border.color = new Color(0.2f, 0.85f, 0.42f, 0.2f);

        title = CreateText("Title", panelObject.transform, "ĐỘ ỒN", 15, FontStyle.Bold, new Color(0.8f, 0.9f, 0.85f, 1f));
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(0f, 1f);
        titleRect.pivot = new Vector2(0f, 1f);
        titleRect.anchoredPosition = new Vector2(14f, -9f);
        titleRect.sizeDelta = new Vector2(150f, 22f);

        sourceLabel = CreateText("Source", panelObject.transform, "YÊN LẶNG", 12, FontStyle.Normal, new Color(0.55f, 0.65f, 0.62f, 1f));
        sourceLabel.alignment = TextAnchor.MiddleRight;
        RectTransform sourceRect = sourceLabel.rectTransform;
        sourceRect.anchorMin = new Vector2(1f, 1f);
        sourceRect.anchorMax = new Vector2(1f, 1f);
        sourceRect.pivot = new Vector2(1f, 1f);
        sourceRect.anchoredPosition = new Vector2(-14f, -9f);
        sourceRect.sizeDelta = new Vector2(175f, 22f);

        GameObject barObject = new GameObject("NoiseSegments");
        barObject.transform.SetParent(panelObject.transform, false);
        RectTransform barRect = barObject.AddComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0f, 0f);
        barRect.anchorMax = new Vector2(1f, 0f);
        barRect.pivot = new Vector2(0.5f, 0f);
        barRect.anchoredPosition = new Vector2(0f, 13f);
        barRect.sizeDelta = new Vector2(-28f, 32f);

        for (int i = 0; i < SegmentCount; i++)
        {
            GameObject segmentObject = new GameObject("Segment_" + i);
            segmentObject.transform.SetParent(barObject.transform, false);
            RectTransform segmentRect = segmentObject.AddComponent<RectTransform>();
            float width = 1f / SegmentCount;
            segmentRect.anchorMin = new Vector2(i * width, 0f);
            segmentRect.anchorMax = new Vector2((i + 1) * width, 1f);
            segmentRect.offsetMin = new Vector2(2f, 0f);
            segmentRect.offsetMax = new Vector2(-2f, 0f);
            segments[i] = segmentObject.AddComponent<Image>();
        }
    }

    private Text CreateText(string name, Transform parent, string value, int size, FontStyle style, Color color)
    {
        GameObject textObject = new GameObject(name);
        textObject.transform.SetParent(parent, false);
        Text text = textObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.alignment = TextAnchor.MiddleLeft;
        return text;
    }

    private void SetMovement(bool isMoving, bool isRunning, bool isCrouching)
    {
        movementNoise = !isMoving || isCrouching ? 0f : (isRunning ? 0.48f : 0.24f);
        if (movementNoise > 0f)
        {
            sourceName = isRunning ? "CHẠY" : "BƯỚC CHÂN";
            sourceTimer = 0.15f;
            pulseTimer = Mathf.Max(pulseTimer, 0.06f);
        }
    }

    private void AddTransientNoise(float intensity, string source)
    {
        transientNoise = Mathf.Max(transientNoise, Mathf.Clamp01(intensity));
        sourceName = source;
        sourceTimer = 0.65f;
        pulseTimer = Mathf.Max(pulseTimer, 0.22f);
    }

    private void FlashHeardVoice(float proximity)
    {
        if (proximity <= 0f) return;
        heardVoiceTimer = Mathf.Max(heardVoiceTimer, 0.14f + Mathf.Clamp01(proximity) * 0.12f);
    }

    private void Update()
    {
        transientNoise = Mathf.MoveTowards(transientNoise, 0f, Time.unscaledDeltaTime * 0.9f);
        pulseTimer = Mathf.Max(0f, pulseTimer - Time.unscaledDeltaTime);
        heardVoiceTimer = Mathf.Max(0f, heardVoiceTimer - Time.unscaledDeltaTime);
        sourceTimer = Mathf.Max(0f, sourceTimer - Time.unscaledDeltaTime);

        float targetNoise = Mathf.Max(movementNoise, transientNoise);
        float speed = targetNoise > displayedNoise ? 5.5f : 1.3f;
        displayedNoise = Mathf.MoveTowards(displayedNoise, targetNoise, Time.unscaledDeltaTime * speed);
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        int activeSegments = Mathf.CeilToInt(displayedNoise * SegmentCount);
        for (int i = 0; i < SegmentCount; i++)
        {
            Color color = GetSegmentColor(i / (float)(SegmentCount - 1));
            bool active = i < activeSegments;
            float pulse = pulseTimer > 0f ? 0.18f : 0f;
            color.a = active ? Mathf.Clamp01(0.82f + pulse) : 0.12f;
            segments[i].color = color;
        }

        Color levelColor = GetSegmentColor(Mathf.Clamp01(displayedNoise));
        bool heardVoice = heardVoiceTimer > 0f;
        float tutorialPulse = tutorialHighlight ? 0.5f + Mathf.PingPong(Time.unscaledTime * 2.4f, 0.5f) : 0f;
        border.color = tutorialHighlight
            ? new Color(1f, 0.82f, 0.12f, tutorialPulse)
            : heardVoice
            ? new Color(0.15f, 0.85f, 1f, 0.65f)
            : new Color(levelColor.r, levelColor.g, levelColor.b, 0.18f + (pulseTimer > 0f ? 0.3f : 0f));
        panel.color = heardVoice
            ? new Color(0.025f, 0.08f, 0.1f, 0.94f)
            : new Color(0.025f, 0.035f, 0.045f, 0.9f);

        sourceLabel.text = heardVoice ? "VOICE LÂN CẬN" : (sourceTimer > 0f ? sourceName : "YÊN LẶNG");
        sourceLabel.color = heardVoice ? new Color(0.4f, 0.9f, 1f, 1f) : levelColor;
    }

    private Color GetSegmentColor(float t)
    {
        if (t < 0.5f) return Color.Lerp(new Color(0.1f, 0.95f, 0.42f), new Color(1f, 0.84f, 0.08f), t * 2f);
        return Color.Lerp(new Color(1f, 0.84f, 0.08f), new Color(1f, 0.16f, 0.1f), (t - 0.5f) * 2f);
    }
}
