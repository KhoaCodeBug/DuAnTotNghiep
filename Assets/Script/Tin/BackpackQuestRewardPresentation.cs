using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Combination B + A presentation for quest backpack milestones:
/// - Effect B: Purely visual tactical scan & backpack reveal (no text, light dimmer).
/// - Notification A: Compact HUD toast appearing after Effect B completes,
///   showing before -> after storage capacity (30 -> 40 or 40 -> 50).
/// </summary>
[ExecuteAlways]
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
    private RectTransform iconFrame;
    private Image scanPulseImage;
    private Image scanCoreImage;
    private Image scanSweepImage;
    private Image iconImage;
    private Outline iconFrameOutline;

    // Notification A HUD elements
    private GameObject notificationHud;
    private CanvasGroup notificationGroup;
    private RectTransform notificationRect;
    private Image notificationBg;
    private Outline notificationOutline;
    private Image notificationAccentTop;
    private Image notificationIconImage;
    private TextMeshProUGUI notificationTitleLabel;
    private TextMeshProUGUI notificationBodyLabel;
    private Coroutine notificationRoutine;

    private Coroutine presentationRoutine;
    private bool isPresentationActive;
    private bool isNotificationActive;
    private int currentPresentedLevel = 4;
    private int currentPreviousBackpackLevel = -1;
    private ItemData currentRewardBackpack;
    private string lastNotificationTitle = string.Empty;
    private string lastNotificationBody = string.Empty;
    private System.Action onPresentationCompleted;

    public static bool BlocksGameplayInput => false;
    public static bool IsVisible =>
        instance != null
        && instance.gameObject != null
        && instance.gameObject.activeInHierarchy
        && instance.isPresentationActive
        && instance.root != null
        && instance.root.activeInHierarchy;

    public static bool IsNotificationVisible =>
        instance != null
        && instance.gameObject != null
        && instance.gameObject.activeInHierarchy
        && instance.isNotificationActive
        && instance.notificationHud != null
        && instance.notificationHud.activeInHierarchy;

    public static string LastNotificationTitle => instance != null ? instance.lastNotificationTitle : string.Empty;
    public static string LastNotificationBody => instance != null ? instance.lastNotificationBody : string.Empty;

    public static void Show(int level, ItemData backpack, System.Action onCompleted = null)
    {
        ShowWithPreviousLevel(level, backpack, -1, onCompleted);
    }

    public static void ShowWithPreviousLevel(int level, ItemData backpack, int previousLevel, System.Action onCompleted = null)
    {
        if (!BackpackQuestRewardRules.IsRewardLevel(level))
        {
            onCompleted?.Invoke();
            return;
        }

        BackpackQuestRewardPresentation presenter = GetOrCreate();
        presenter.currentPreviousBackpackLevel = previousLevel;
        presenter.ShowInternal(level, backpack, onCompleted);
    }

    public static void ShowUpgradeNotification(int level)
    {
        if (!BackpackQuestRewardRules.IsRewardLevel(level)) return;
        BackpackQuestRewardPresentation presenter = GetOrCreate();
        presenter.ShowUpgradeNotificationInternal(level);
    }

    public static void DismissNotification()
    {
        if (instance != null) instance.DismissNotificationInternal();
    }

    private static int completedEffectBLevel = -1;
    public static event System.Action OnNotificationDismissed;
    private static System.Action onPostNotificationAction;

    public static void RegisterPostNotificationAction(System.Action action)
    {
        if (action == null) return;
        if (!IsNotificationVisible && !IsVisible)
        {
            action.Invoke();
            return;
        }
        onPostNotificationAction += action;
    }

    public static void ResetForTests()
    {
        completedEffectBLevel = -1;
        onPostNotificationAction = null;
        OnNotificationDismissed = null;
        if (instance != null)
        {
            instance.currentRewardBackpack = null;
            instance.currentPreviousBackpackLevel = -1;
            instance.isPresentationActive = false;
            instance.isNotificationActive = false;
            if (instance.presentationRoutine != null) instance.StopCoroutine(instance.presentationRoutine);
            if (instance.notificationRoutine != null) instance.StopCoroutine(instance.notificationRoutine);
            if (instance.gameObject != null)
            {
                if (Application.isPlaying)
                    Destroy(instance.gameObject);
                else
                    DestroyImmediate(instance.gameObject);
            }
            instance = null;
        }
        QuestFlowUIPrototype.ResetInstanceForTests();
        if (AutoChatManager.ExistingInstance != null)
        {
            AutoChatManager.ExistingInstance.SetSuppressedByReward(false);
        }
    }

    private static BackpackQuestRewardPresentation GetOrCreate()
    {
        if (instance != null) return instance;

        GameObject presenterObject = new GameObject("Backpack Quest Reward Presentation");
        if (Application.isPlaying) DontDestroyOnLoad(presenterObject);
        instance = presenterObject.AddComponent<BackpackQuestRewardPresentation>();
        return instance;
    }

    public static void PurgeStalePreviewObjects()
    {
        var all = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < all.Length; i++)
        {
            GameObject go = all[i];
            if (go != null && go.name.Contains("Design Preview"))
            {
                if (Application.isPlaying)
                    Destroy(go);
                else
                    DestroyImmediate(go);
            }
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            if (Application.isPlaying)
                Destroy(gameObject);
            else
                DestroyImmediate(gameObject);
        }

        instance = this;
        if (Application.isPlaying) DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        isPresentationActive = false;
        isNotificationActive = false;
        currentRewardBackpack = null;
        currentPreviousBackpackLevel = -1;
        if (presentationRoutine != null) StopCoroutine(presentationRoutine);
        if (notificationRoutine != null) StopCoroutine(notificationRoutine);
        if (instance == this) instance = null;
        if (AutoChatManager.ExistingInstance != null)
        {
            AutoChatManager.ExistingInstance.SetSuppressedByReward(false);
        }
    }

    private void ShowInternal(int level, ItemData backpack, System.Action onCompleted)
    {
        PurgeStalePreviewObjects();
        completedEffectBLevel = -1;
        QuestFlowUIPrototype flow = QuestFlowUIPrototype.Instance;
        if (flow != null)
        {
            flow.CloseAllQuestOverlays();
        }
        instance = this;
        currentPresentedLevel = level;
        if (currentPreviousBackpackLevel < 0)
        {
            PlayerMovement localPlayer = PlayerMovement.LocalPlayerInstance;
            InventorySystem inv = localPlayer != null ? localPlayer.GetComponent<InventorySystem>() : null;
            if (inv != null && inv.CurrentBackpackLevel >= 0 && inv.CurrentBackpackLevel < level)
            {
                currentPreviousBackpackLevel = inv.CurrentBackpackLevel;
            }
            else
            {
                currentPreviousBackpackLevel = level == BackpackQuestRewardRules.RadioBackpackLevel ? 4 : 3;
            }
        }
        currentRewardBackpack = backpack != null ? backpack : BackpackItemCatalog.GetOrCreate(level);
        onPresentationCompleted = onCompleted;
        EnsureCanvas();
        ApplyRewardStyle(level);

        // Populate backpack icon
        iconImage.sprite = currentRewardBackpack != null ? currentRewardBackpack.icon : null;
        if (iconImage.sprite == null)
        {
            ItemData catalogBackpack = BackpackItemCatalog.GetOrCreate(level);
            if (catalogBackpack != null) iconImage.sprite = catalogBackpack.icon;
        }
        iconImage.enabled = iconImage.sprite != null;

        // Dismiss any stale notification
        DismissNotificationInternal();

        // Suppress AutoChat panel during Effect B
        if (AutoChatManager.ExistingInstance != null)
        {
            AutoChatManager.ExistingInstance.SetSuppressedByReward(true);
        }

        if (presentationRoutine != null) StopCoroutine(presentationRoutine);
        isPresentationActive = true;

        root.SetActive(true);
        rootGroup.alpha = 0f;
        scanPulse.localScale = Vector3.one * 0.62f;
        scanCore.localScale = Vector3.one * 0.65f;
        scanCore.localRotation = Quaternion.Euler(0f, 0f, 45f);
        scanSweep.anchoredPosition = new Vector2(0f, -90f);
        iconFrame.localScale = Vector3.one * 0.85f;
        if (Application.isPlaying)
        {
            presentationRoutine = StartCoroutine(RevealRoutine());
        }
    }

    private IEnumerator RevealRoutine()
    {
        float elapsed = 0f;
        while (elapsed < 0.35f)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / 0.35f);
            rootGroup.alpha = Mathf.SmoothStep(0f, 1f, t);
            scanPulse.localScale = Vector3.one * Mathf.Lerp(0.62f, 1.06f, t);
            scanCore.localScale = Vector3.one * Mathf.Lerp(0.65f, 1.12f, t);
            iconFrame.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, t);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < 1.0f)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / 1.0f);
            scanPulse.localScale = Vector3.one * Mathf.Lerp(1.06f, 1f, t);
            scanCore.localScale = Vector3.one * Mathf.Lerp(1.12f, 1f, t);
            scanCore.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(45f, 135f, t));
            scanSweep.anchoredPosition = new Vector2(0f, Mathf.Lerp(-90f, 90f, t));
            yield return null;
        }

        yield return WaitUnscaled(0.9f);

        elapsed = 0f;
        while (elapsed < 0.35f)
        {
            elapsed += Time.unscaledDeltaTime;
            rootGroup.alpha = 1f - Mathf.Clamp01(elapsed / 0.35f);
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

        isPresentationActive = false;
        completedEffectBLevel = currentPresentedLevel;
        if (root != null) root.SetActive(false);

        // Restore AutoChat suppression after Effect B finishes
        if (AutoChatManager.ExistingInstance != null)
        {
            AutoChatManager.ExistingInstance.SetSuppressedByReward(false);
        }

        // Notification A appears strictly after Effect B completes
        ShowUpgradeNotificationInternal(currentPresentedLevel);

        System.Action callback = onPresentationCompleted;
        onPresentationCompleted = null;
        callback?.Invoke();
    }

    private void ShowUpgradeNotificationInternal(int level)
    {
        if (isPresentationActive)
        {
            // Notification A is impossible in production before Effect B completion.
            return;
        }

        if (completedEffectBLevel != level)
        {
            // Reject cold calls or calls without a completed Effect B state/token.
            return;
        }
        completedEffectBLevel = -1;

        instance = this;
        EnsureCanvas();
        currentPresentedLevel = level;

        if (currentRewardBackpack == null || currentRewardBackpack.backpackLevel != level)
        {
            currentRewardBackpack = BackpackItemCatalog.GetOrCreate(level);
        }

        string displayName = BackpackItemCatalog.GetLocalizedDisplayName(currentRewardBackpack);
        if (string.IsNullOrEmpty(displayName))
        {
            displayName = BackpackItemCatalog.GetDisplayName(level);
        }

        int prevLevel = currentPreviousBackpackLevel >= 0
            ? currentPreviousBackpackLevel
            : (level == BackpackQuestRewardRules.RadioBackpackLevel ? 4 : 3);

        int prevSlots = BackpackCapacityRules.GetBackpackSlots(prevLevel);
        int newSlots = currentRewardBackpack != null
            ? BackpackCapacityRules.GetStorageSlots(currentRewardBackpack)
            : BackpackCapacityRules.GetBackpackSlots(level);
        int delta = Mathf.Max(0, newSlots - prevSlots);

        string reason = level == BackpackQuestRewardRules.HospitalBackpackLevel
            ? GameLocalization.Get("backpack.notification.reason.level4", "Hospital milestone completed")
            : GameLocalization.Get("backpack.notification.reason.level5", "Radio restoration milestone completed");

        string capFormat = GameLocalization.Get("backpack.notification.capacity_transition", "STORAGE {0} → {1} (+{2} SLOTS)");
        string capacity = string.Format(capFormat, prevSlots, newSlots, delta);

        string titleFormat = GameLocalization.Get("backpack.notification.title_transition", "BACKPACK LEVEL {0} → LEVEL {1}");
        lastNotificationTitle = string.Format(titleFormat, prevLevel, level);

        string bodyFormat = GameLocalization.Get("backpack.notification.body_format", "{0}\n{1}  •  {2}");
        lastNotificationBody = string.Format(bodyFormat, displayName, reason, capacity);

        currentPreviousBackpackLevel = -1;

        notificationTitleLabel.text = lastNotificationTitle;
        notificationBodyLabel.text = lastNotificationBody;

        Sprite rewardIcon = currentRewardBackpack != null ? currentRewardBackpack.icon : null;
        if (rewardIcon == null)
        {
            ItemData catalogBackpack = BackpackItemCatalog.GetOrCreate(level);
            if (catalogBackpack != null) rewardIcon = catalogBackpack.icon;
        }
        notificationIconImage.sprite = rewardIcon;
        notificationIconImage.enabled = notificationIconImage.sprite != null;

        bool hospitalReward = level == BackpackQuestRewardRules.HospitalBackpackLevel;
        Color accent = hospitalReward
            ? new Color(0.98f, 0.64f, 0.20f, 1f)
            : new Color(0.95f, 0.38f, 0.20f, 1f);

        notificationOutline.effectColor = new Color(accent.r, accent.g, accent.b, 0.85f);
        notificationAccentTop.color = new Color(accent.r, accent.g, accent.b, 0.95f);
        notificationTitleLabel.color = new Color(accent.r, accent.g, accent.b, 1f);

        isNotificationActive = true;
        if (notificationHud != null) notificationHud.SetActive(true);
        if (notificationGroup != null) notificationGroup.alpha = 1f;

        if (notificationRoutine != null) StopCoroutine(notificationRoutine);
        if (Application.isPlaying)
        {
            notificationRoutine = StartCoroutine(NotificationRoutine());
        }
    }

    private IEnumerator NotificationRoutine()
    {
        Vector2 targetPos = new Vector2(0f, -GameplayHudLayout.CanonicalToastTargetY1080p);
        Vector2 startPos = new Vector2(0f, -GameplayHudLayout.CanonicalToastStartY1080p);
        notificationRect.anchoredPosition = startPos;
        notificationGroup.alpha = 0f;

        // Slide down and fade in
        float elapsed = 0f;
        const float inDuration = 0.22f;
        while (elapsed < inDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / inDuration);
            float eased = t * (2f - t);
            notificationGroup.alpha = eased;
            notificationRect.anchoredPosition = Vector2.Lerp(startPos, targetPos, eased);
            yield return null;
        }

        notificationGroup.alpha = 1f;
        notificationRect.anchoredPosition = targetPos;

        // Hold
        yield return WaitUnscaled(2.8f);

        // Fade out
        elapsed = 0f;
        const float outDuration = 0.35f;
        while (elapsed < outDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / outDuration);
            notificationGroup.alpha = 1f - t;
            yield return null;
        }

        DismissNotificationInternal();
    }

    private void DismissNotificationInternal()
    {
        if (notificationRoutine != null)
        {
            StopCoroutine(notificationRoutine);
            notificationRoutine = null;
        }

        isNotificationActive = false;
        if (notificationHud != null) notificationHud.SetActive(false);

        System.Action postAction = onPostNotificationAction;
        onPostNotificationAction = null;
        postAction?.Invoke();
        OnNotificationDismissed?.Invoke();
    }

    private void EnsureCanvas()
    {
        if (rewardCanvas != null) return;

        GameObject canvasObject = new GameObject("Backpack Quest Reward Canvas");
        canvasObject.transform.SetParent(transform, false);
        rewardCanvas = canvasObject.AddComponent<Canvas>();
        rewardCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        rewardCanvas.sortingOrder = 2000;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        // --- EFFECT B: PURE VISUAL SCAN & REVEAL ROOT ---
        root = CreateRect("Reward Root", canvasObject.transform,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero).gameObject;
        rootGroup = root.AddComponent<CanvasGroup>();
        rootGroup.blocksRaycasts = false;
        rootGroup.interactable = false;

        // Dimmer: light enough (alpha 0.48) to keep gameplay visible underneath
        CreateImage("Dimmer", root.transform, new Color(0.006f, 0.008f, 0.014f, 0.48f),
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        // Center Tactical Radar & Crosshairs
        scanPulse = CreateRect("Scan Pulse", root.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(280f, 280f));
        scanPulseImage = scanPulse.gameObject.AddComponent<Image>();
        scanPulseImage.sprite = GetSolidSprite();
        scanPulseImage.color = new Color(0.95f, 0.58f, 0.16f, 0.06f);
        scanPulseImage.raycastTarget = false;
        Outline pulseOutline = scanPulse.gameObject.AddComponent<Outline>();
        pulseOutline.effectColor = new Color(1f, 0.64f, 0.20f, 0.72f);
        pulseOutline.effectDistance = new Vector2(2f, 2f);

        RectTransform pulseInner = CreateRect("Scan Pulse Inner", scanPulse,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(210f, 210f));
        Image pulseInnerImage = pulseInner.gameObject.AddComponent<Image>();
        pulseInnerImage.sprite = GetSolidSprite();
        pulseInnerImage.color = new Color(0.95f, 0.58f, 0.16f, 0.025f);
        pulseInnerImage.raycastTarget = false;
        Outline pulseInnerOutline = pulseInner.gameObject.AddComponent<Outline>();
        pulseInnerOutline.effectColor = new Color(0.22f, 0.86f, 0.78f, 0.42f);
        pulseInnerOutline.effectDistance = new Vector2(1f, 1f);

        scanSweep = CreateRect("Scan Sweep", scanPulse,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -90f), new Vector2(220f, 2f));
        scanSweepImage = scanSweep.gameObject.AddComponent<Image>();
        scanSweepImage.sprite = GetSolidSprite();
        scanSweepImage.color = new Color(0.55f, 0.95f, 0.88f, 0.85f);
        scanSweepImage.raycastTarget = false;

        CreateImage("Scan Cross Horizontal", root.transform, new Color(0.32f, 0.78f, 0.74f, 0.24f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(440f, 1f));
        CreateImage("Scan Cross Vertical", root.transform, new Color(0.32f, 0.78f, 0.74f, 0.24f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(1f, 440f));

        scanCore = CreateRect("Scan Core", root.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(64f, 64f));
        scanCore.localRotation = Quaternion.Euler(0f, 0f, 45f);
        scanCoreImage = scanCore.gameObject.AddComponent<Image>();
        scanCoreImage.sprite = GetSolidSprite();
        scanCoreImage.color = new Color(0.95f, 0.58f, 0.16f, 0.86f);
        scanCoreImage.raycastTarget = false;
        Outline coreOutline = scanCore.gameObject.AddComponent<Outline>();
        coreOutline.effectColor = new Color(0.55f, 0.95f, 0.88f, 0.90f);
        coreOutline.effectDistance = new Vector2(2f, 2f);

        // Center Backpack Icon Frame (Pure visual focus of Effect B)
        iconFrame = CreateRect("Center Icon Frame", root.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(160f, 160f));
        Image frameImage = iconFrame.gameObject.AddComponent<Image>();
        frameImage.sprite = GetSolidSprite();
        frameImage.color = new Color(0.02f, 0.035f, 0.045f, 0.82f);
        frameImage.raycastTarget = false;
        iconFrameOutline = iconFrame.gameObject.AddComponent<Outline>();
        iconFrameOutline.effectColor = new Color(0.32f, 0.78f, 0.74f, 0.85f);
        iconFrameOutline.effectDistance = new Vector2(2f, 2f);

        RectTransform iconRect = CreateRect("Backpack Icon", iconFrame,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-16f, -16f));
        iconImage = iconRect.gameObject.AddComponent<Image>();
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;

        root.SetActive(false);

        // --- NOTIFICATION A: HUD TOAST ---
        notificationRect = CreateRect("Notification HUD", canvasObject.transform,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -GameplayHudLayout.CanonicalToastTargetY1080p),
            new Vector2(GameplayHudLayout.CanonicalToastWidth1080p, GameplayHudLayout.CanonicalToastHeight1080p));
        notificationHud = notificationRect.gameObject;
        notificationGroup = notificationHud.AddComponent<CanvasGroup>();
        notificationGroup.blocksRaycasts = false;
        notificationGroup.interactable = false;

        notificationBg = notificationHud.AddComponent<Image>();
        notificationBg.sprite = GetSolidSprite();
        notificationBg.color = new Color(0.025f, 0.038f, 0.048f, 0.94f);
        notificationBg.raycastTarget = false;

        notificationOutline = notificationHud.AddComponent<Outline>();
        notificationOutline.effectColor = new Color(0.95f, 0.58f, 0.16f, 0.85f);
        notificationOutline.effectDistance = new Vector2(1.5f, 1.5f);

        notificationAccentTop = CreateImage("Notification Accent Top", notificationHud.transform,
            new Color(0.95f, 0.58f, 0.16f, 0.95f),
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -1f), new Vector2(0f, 3f));

        RectTransform notifIconFrame = CreateRect("Notif Icon Frame", notificationHud.transform,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(14f, 0f), new Vector2(56f, 56f));
        Image notifFrameImg = notifIconFrame.gameObject.AddComponent<Image>();
        notifFrameImg.sprite = GetSolidSprite();
        notifFrameImg.color = new Color(0.04f, 0.07f, 0.08f, 1f);
        notifFrameImg.raycastTarget = false;

        RectTransform notifIconRect = CreateRect("Notif Icon", notifIconFrame,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-6f, -6f));
        notificationIconImage = notifIconRect.gameObject.AddComponent<Image>();
        notificationIconImage.preserveAspect = true;
        notificationIconImage.raycastTarget = false;

        notificationTitleLabel = CreateText("Notification Title", notificationHud.transform, 12f, FontStyles.Bold,
            new Color(0.98f, 0.64f, 0.20f, 1f), TextAlignmentOptions.Left,
            new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(80f, 22f), new Vector2(-92f, 20f));

        notificationBodyLabel = CreateText("Notification Body", notificationHud.transform, 12.5f, FontStyles.Bold,
            Color.white, TextAlignmentOptions.Left,
            new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(80f, -12f), new Vector2(-92f, 44f));

        notificationHud.SetActive(false);
    }

    private void ApplyRewardStyle(int level)
    {
        bool hospitalReward = level == BackpackQuestRewardRules.HospitalBackpackLevel;
        Color accent = hospitalReward
            ? new Color(0.98f, 0.64f, 0.20f, 1f)
            : new Color(0.95f, 0.38f, 0.20f, 1f);
        Color scan = hospitalReward
            ? new Color(0.42f, 0.93f, 0.80f, 1f)
            : new Color(0.35f, 0.82f, 0.98f, 1f);

        scanPulseImage.color = new Color(accent.r, accent.g, accent.b, 0.06f);
        scanCoreImage.color = new Color(accent.r, accent.g, accent.b, 0.86f);
        scanSweepImage.color = new Color(scan.r, scan.g, scan.b, 0.85f);
        iconFrameOutline.effectColor = new Color(scan.r, scan.g, scan.b, 0.85f);
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
        text.textWrappingMode = TextWrappingModes.Normal;
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
