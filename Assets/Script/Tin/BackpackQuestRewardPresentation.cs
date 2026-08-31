using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Local-only presentation for the two quest backpack milestones. It borrows
/// the scan/reveal language of the map presentation, but owns a separate
/// canvas and never mutates map state or map rewards.
/// </summary>
public sealed class BackpackQuestRewardPresentation : MonoBehaviour
{
    private static BackpackQuestRewardPresentation instance;
    private static Sprite solidSprite;

    private Canvas rewardCanvas;
    private CanvasGroup rootGroup;
    private GameObject root;
    private RectTransform scanPulse;
    private RectTransform scanCore;
    private RectTransform scanSweep;
    private RectTransform rewardCard;
    private Image scanPulseImage;
    private Image scanCoreImage;
    private Image scanSweepImage;
    private Image cardAccentImage;
    private Image iconImage;
    private Outline cardOutline;
    private TextMeshProUGUI scanLabel;
    private TextMeshProUGUI tierLabel;
    private TextMeshProUGUI titleLabel;
    private TextMeshProUGUI bodyLabel;
    private TextMeshProUGUI capacityLabel;
    private Coroutine presentationRoutine;
    private bool ownsAutoCanvasSuppression;

    // This is an informational reward reveal, not a gameplay modal. The
    // multiplayer simulation and local E interactions remain usable while
    // the owner reads the notification.
    public static bool BlocksGameplayInput => false;

    public static void Show(int level, ItemData backpack)
    {
        if (!Application.isPlaying || !BackpackQuestRewardRules.IsRewardLevel(level)) return;

        BackpackQuestRewardPresentation presenter = GetOrCreate();
        presenter.ShowInternal(level, backpack);
    }

    private static BackpackQuestRewardPresentation GetOrCreate()
    {
        if (instance != null) return instance;

        GameObject presenterObject = new GameObject("Backpack Quest Reward Presentation");
        DontDestroyOnLoad(presenterObject);
        instance = presenterObject.AddComponent<BackpackQuestRewardPresentation>();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (presentationRoutine != null) StopCoroutine(presentationRoutine);
        RestoreAutoCanvasSuppression();
        if (instance == this) instance = null;
    }

    private void ShowInternal(int level, ItemData backpack)
    {
        EnsureCanvas();
        ApplyRewardStyle(level);

        string titleKey = level == BackpackQuestRewardRules.HospitalBackpackLevel
            ? "backpack.quest.level4.title"
            : "backpack.quest.level5.title";
        string bodyKey = level == BackpackQuestRewardRules.HospitalBackpackLevel
            ? "backpack.quest.level4.body"
            : "backpack.quest.level5.body";

        scanLabel.text = GameLocalization.Get("backpack.quest.scan");
        tierLabel.text = GameLocalization.Get(level == BackpackQuestRewardRules.HospitalBackpackLevel
            ? "backpack.quest.level4.tier"
            : "backpack.quest.level5.tier");
        titleLabel.text = GameLocalization.Get(titleKey);
        bodyLabel.text = GameLocalization.Get(bodyKey);
        capacityLabel.text = string.Format(GameLocalization.Get("backpack.quest.capacity"),
            BackpackCapacityRules.GetBackpackSlots(level), BackpackCapacityRules.MaxBackpackSlots);
        iconImage.sprite = backpack != null ? backpack.icon : null;
        iconImage.enabled = iconImage.sprite != null;

        if (presentationRoutine != null) StopCoroutine(presentationRoutine);
        // Keep the normal gameplay canvas/input path alive. Unlike opening the
        // map, receiving a personal backpack reward must not interrupt a
        // teammate's interaction or make the owner miss an immediate E prompt.
        ownsAutoCanvasSuppression = false;

        root.SetActive(true);
        rootGroup.alpha = 0f;
        scanPulse.localScale = Vector3.one * 0.62f;
        scanCore.localScale = Vector3.one * 0.65f;
        scanCore.localRotation = Quaternion.Euler(0f, 0f, 45f);
        scanSweep.anchoredPosition = new Vector2(0f, -100f);
        rewardCard.localScale = Vector3.one * 0.90f;
        presentationRoutine = StartCoroutine(RevealRoutine());
    }

    private IEnumerator RevealRoutine()
    {
        float elapsed = 0f;
        while (elapsed < 0.42f)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / 0.42f);
            rootGroup.alpha = Mathf.SmoothStep(0f, 1f, t);
            scanPulse.localScale = Vector3.one * Mathf.Lerp(0.62f, 1.08f, t);
            scanCore.localScale = Vector3.one * Mathf.Lerp(0.65f, 1.15f, t);
            rewardCard.localScale = Vector3.one * Mathf.Lerp(0.90f, 1f, t);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < 1.10f)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / 1.10f);
            scanPulse.localScale = Vector3.one * Mathf.Lerp(1.08f, 1f, t);
            scanCore.localScale = Vector3.one * Mathf.Lerp(1.15f, 1f, t);
            scanCore.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(45f, 135f, t));
            scanSweep.anchoredPosition = new Vector2(0f, Mathf.Lerp(-100f, 100f, t));
            yield return null;
        }

        yield return WaitUnscaled(1.55f);

        elapsed = 0f;
        while (elapsed < 0.45f)
        {
            elapsed += Time.unscaledDeltaTime;
            rootGroup.alpha = 1f - Mathf.Clamp01(elapsed / 0.45f);
            yield return null;
        }

        FinishPresentation();
    }

    private static IEnumerator WaitUnscaled(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private void FinishPresentation()
    {
        if (presentationRoutine != null)
        {
            StopCoroutine(presentationRoutine);
            presentationRoutine = null;
        }

        if (root != null) root.SetActive(false);
        RestoreAutoCanvasSuppression();
    }

    private void RestoreAutoCanvasSuppression()
    {
        if (!ownsAutoCanvasSuppression) return;
        ownsAutoCanvasSuppression = false;
        AutoUIManager.Instance?.SetQuestOverlayOpen(false);
    }

    private void EnsureCanvas()
    {
        if (rewardCanvas != null) return;

        GameObject canvasObject = new GameObject("Backpack Quest Reward Canvas");
        canvasObject.transform.SetParent(transform, false);
        rewardCanvas = canvasObject.AddComponent<Canvas>();
        rewardCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // The main menu canvas is authored at order 999; keep the reward
        // reveal above it in QA/tutorial scenes and above gameplay HUDs.
        rewardCanvas.sortingOrder = 2000;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        root = CreateRect("Reward Root", canvasObject.transform,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero).gameObject;
        rootGroup = root.AddComponent<CanvasGroup>();
        rootGroup.blocksRaycasts = false;
        rootGroup.interactable = false;

        CreateImage("Dimmer", root.transform, new Color(0.006f, 0.008f, 0.014f, 0.94f),
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        scanPulse = CreateRect("Scan Pulse", root.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 55f), new Vector2(310f, 310f));
        scanPulseImage = scanPulse.gameObject.AddComponent<Image>();
        scanPulseImage.sprite = GetSolidSprite();
        scanPulseImage.color = new Color(0.95f, 0.58f, 0.16f, 0.06f);
        scanPulseImage.raycastTarget = false;
        Outline pulseOutline = scanPulse.gameObject.AddComponent<Outline>();
        pulseOutline.effectColor = new Color(1f, 0.64f, 0.20f, 0.72f);
        pulseOutline.effectDistance = new Vector2(2f, 2f);

        RectTransform pulseInner = CreateRect("Scan Pulse Inner", scanPulse,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(232f, 232f));
        Image pulseInnerImage = pulseInner.gameObject.AddComponent<Image>();
        pulseInnerImage.sprite = GetSolidSprite();
        pulseInnerImage.color = new Color(0.95f, 0.58f, 0.16f, 0.025f);
        pulseInnerImage.raycastTarget = false;
        Outline pulseInnerOutline = pulseInner.gameObject.AddComponent<Outline>();
        pulseInnerOutline.effectColor = new Color(0.22f, 0.86f, 0.78f, 0.42f);
        pulseInnerOutline.effectDistance = new Vector2(1f, 1f);

        scanSweep = CreateRect("Scan Sweep", scanPulse,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -100f), new Vector2(240f, 2f));
        scanSweepImage = scanSweep.gameObject.AddComponent<Image>();
        scanSweepImage.sprite = GetSolidSprite();
        scanSweepImage.color = new Color(0.55f, 0.95f, 0.88f, 0.85f);
        scanSweepImage.raycastTarget = false;

        CreateImage("Scan Cross Horizontal", root.transform, new Color(0.32f, 0.78f, 0.74f, 0.22f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 55f), new Vector2(520f, 1f));
        CreateImage("Scan Cross Vertical", root.transform, new Color(0.32f, 0.78f, 0.74f, 0.22f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 55f), new Vector2(1f, 520f));

        scanCore = CreateRect("Scan Core", root.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 55f), new Vector2(76f, 76f));
        scanCore.localRotation = Quaternion.Euler(0f, 0f, 45f);
        scanCoreImage = scanCore.gameObject.AddComponent<Image>();
        scanCoreImage.sprite = GetSolidSprite();
        scanCoreImage.color = new Color(0.95f, 0.58f, 0.16f, 0.86f);
        scanCoreImage.raycastTarget = false;
        Outline coreOutline = scanCore.gameObject.AddComponent<Outline>();
        coreOutline.effectColor = new Color(0.55f, 0.95f, 0.88f, 0.90f);
        coreOutline.effectDistance = new Vector2(2f, 2f);

        rewardCard = CreateRect("Reward Card", root.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -188f), new Vector2(760f, 282f));
        Image cardImage = rewardCard.gameObject.AddComponent<Image>();
        cardImage.sprite = GetSolidSprite();
        cardImage.color = new Color(0.025f, 0.035f, 0.042f, 0.98f);
        cardImage.raycastTarget = false;
        cardOutline = rewardCard.gameObject.AddComponent<Outline>();
        cardOutline.effectColor = new Color(0.95f, 0.58f, 0.16f, 0.85f);
        cardOutline.effectDistance = new Vector2(2f, 2f);

        cardAccentImage = CreateImage("Card Accent", rewardCard, new Color(0.95f, 0.58f, 0.16f, 0.95f),
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -4f), new Vector2(0f, 5f));

        RectTransform iconFrame = CreateRect("Icon Frame", rewardCard,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(112f, 0f), new Vector2(174f, 174f));
        Image frameImage = iconFrame.gameObject.AddComponent<Image>();
        frameImage.sprite = GetSolidSprite();
        frameImage.color = new Color(0.04f, 0.07f, 0.075f, 1f);
        frameImage.raycastTarget = false;
        Outline frameOutline = iconFrame.gameObject.AddComponent<Outline>();
        frameOutline.effectColor = new Color(0.32f, 0.78f, 0.74f, 0.80f);
        frameOutline.effectDistance = new Vector2(2f, 2f);

        RectTransform iconRect = CreateRect("Backpack Icon", iconFrame,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-22f, -22f));
        iconImage = iconRect.gameObject.AddComponent<Image>();
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;

        scanLabel = CreateText("Scan Label", root.transform, 14f, FontStyles.Bold,
            new Color(0.55f, 0.95f, 0.88f, 0.95f), TextAlignmentOptions.Center,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 270f), new Vector2(760f, 36f));
        tierLabel = CreateText("Tier Label", rewardCard, 13f, FontStyles.Bold,
            new Color(0.55f, 0.95f, 0.88f, 0.95f), TextAlignmentOptions.Left,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(230f, -36f), new Vector2(-260f, 28f));
        titleLabel = CreateText("Title", rewardCard, 28f, FontStyles.Bold,
            Color.white, TextAlignmentOptions.Left,
            new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(230f, 42f), new Vector2(-260f, 50f));
        bodyLabel = CreateText("Body", rewardCard, 16f, FontStyles.Normal,
            new Color(0.76f, 0.84f, 0.82f, 1f), TextAlignmentOptions.Left,
            new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(230f, -8f), new Vector2(-260f, 42f));
        capacityLabel = CreateText("Capacity", rewardCard, 18f, FontStyles.Bold,
            new Color(0.95f, 0.72f, 0.30f, 1f), TextAlignmentOptions.Left,
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f),
            new Vector2(230f, 34f), new Vector2(-260f, 32f));

        root.SetActive(false);
    }

    private void ApplyRewardStyle(int level)
    {
        bool hospitalReward = level == BackpackQuestRewardRules.HospitalBackpackLevel;
        Color accent = hospitalReward
            ? new Color(0.98f, 0.64f, 0.20f, 1f)
            : new Color(0.95f, 0.30f, 0.16f, 1f);
        Color scan = hospitalReward
            ? new Color(0.42f, 0.93f, 0.80f, 1f)
            : new Color(0.35f, 0.82f, 0.98f, 1f);

        scanPulseImage.color = new Color(accent.r, accent.g, accent.b, 0.06f);
        scanCoreImage.color = new Color(accent.r, accent.g, accent.b, 0.86f);
        scanSweepImage.color = new Color(scan.r, scan.g, scan.b, 0.85f);
        cardAccentImage.color = new Color(accent.r, accent.g, accent.b, 0.95f);
        cardOutline.effectColor = new Color(accent.r, accent.g, accent.b, 0.85f);
        scanLabel.color = new Color(scan.r, scan.g, scan.b, 0.95f);
        tierLabel.color = new Color(scan.r, scan.g, scan.b, 0.95f);
        capacityLabel.color = new Color(accent.r, accent.g, accent.b, 1f);
    }

    private static RectTransform CreateRect(string objectName, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
    {
        GameObject child = new GameObject(objectName, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static Image CreateImage(string objectName, Transform parent, Color color,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
    {
        RectTransform rect = CreateRect(objectName, parent, anchorMin, anchorMax, pivot, position, size);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = GetSolidSprite();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI CreateText(string objectName, Transform parent, float fontSize,
        FontStyles style, Color color, TextAlignmentOptions alignment,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
    {
        RectTransform rect = CreateRect(objectName, parent, anchorMin, anchorMax, pivot, position, size);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        TMP_FontAsset font = AutoUIManager.Instance != null ? AutoUIManager.Instance.gameFont : null;
        if (font == null) font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (font != null) text.font = font;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    private static Sprite GetSolidSprite()
    {
        if (solidSprite == null)
        {
            solidSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f), 1f);
            solidSprite.name = "BackpackQuestReward_SolidSprite";
            solidSprite.hideFlags = HideFlags.DontSave;
        }

        return solidSprite;
    }
}
