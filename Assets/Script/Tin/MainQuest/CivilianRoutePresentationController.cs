using System.Collections;
using System.Collections.Generic;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Local presentation for Route A. The authoritative manager owns quest state;
/// this component only sequences the reward banner, map reveal and final outro.
/// </summary>
[DisallowMultipleComponent]
public sealed class CivilianRoutePresentationController : MonoBehaviour
{
    private const float BannerFadeInSeconds = 0.42f;
    private const float BannerHoldSeconds = 2.35f;
    private const float BannerFadeOutSeconds = 0.38f;
    private const float OutroDriveSeconds = 5.2f;

    public static bool BlocksGameplayInput { get; private set; }

    private MainQuestManager manager;
    private Coroutine presentationRoutine;
    private Canvas canvas;
    private CanvasGroup rootGroup;
    private RectTransform bannerPanel;
    private TMP_Text eyebrowText;
    private TMP_Text titleText;
    private TMP_Text bodyText;
    private Image accentLine;
    private Image fadeImage;
    private Image topBar;
    private Image bottomBar;
    private TMP_Text outroTitle;
    private TMP_Text outroSubtitle;
    private readonly List<(Renderer renderer, bool enabled, bool forceOff)> hiddenVehicleRenderers = new();
    private GameObject outroVehicleVisual;
    private Camera gameplayCamera;
    private PZ_CameraController isoCamera;
    private bool isoCameraWasEnabled;
    private VehicleEngineAudioController suspendedEngineAudio;
    private AudioSource outroEngineAudio;
    private CivilianOutroRoadLooper outroRoadLooper;

    public static CivilianRoutePresentationController Attach(MainQuestManager target)
    {
        if (target == null) return null;
        CivilianRoutePresentationController result = target.GetComponent<CivilianRoutePresentationController>();
        if (result == null) result = target.gameObject.AddComponent<CivilianRoutePresentationController>();
        result.manager = target;
        return result;
    }

    public void PlayCarReadySequence(Vector2 checkpoint, Vector2 cityExit)
    {
        if (!isActiveAndEnabled) return;
        if (presentationRoutine != null) StopCoroutine(presentationRoutine);
        presentationRoutine = StartCoroutine(CarReadyRoutine(checkpoint, cityExit));
    }

    public void PlayOutro(NetworkObject repairedVehicle, Vector2 outroEnd, float survivalSeconds)
    {
        if (!isActiveAndEnabled)
        {
            VictorySummaryUI.ShowForCurrentMatch(survivalSeconds, EscapeEndingRoute.CivilianCar);
            return;
        }
        if (presentationRoutine != null) StopCoroutine(presentationRoutine);
        presentationRoutine = StartCoroutine(OutroRoutine(repairedVehicle, outroEnd, survivalSeconds));
    }

    private IEnumerator CarReadyRoutine(Vector2 checkpoint, Vector2 cityExit)
    {
        BlocksGameplayInput = true;
        ArrivalCarInspectionUI.ActiveInstance?.Close();
        EnsureUI();
        ConfigureCanvasForBanner();
        AutoUIManager.Instance?.SetQuestOverlayOpen(true);

        QuestFlowUIPrototype flow = QuestFlowUIPrototype.Instance;
        PreMilitaryQuestRuntimeBridge.Instance?.ConfigureCivilianRouteMap(
            checkpoint, cityExit, MainQuestManager.CivilianRouteStage.CarReady);
        flow?.SetCivilianCityMapUnlocked(true);

        eyebrowText.text = "TUYẾN THOÁT HIỂM A  //  PHƯƠNG TIỆN SẴN SÀNG";
        titleText.text = "XE ĐÃ HOẠT ĐỘNG";
        bodyText.text = "Đã xác định một tuyến đường có thể rời khỏi thành phố";
        rootGroup.alpha = 0f;
        bannerPanel.localScale = Vector3.one * 0.9f;
        accentLine.rectTransform.localScale = new Vector3(0f, 1f, 1f);

        float elapsed = 0f;
        while (elapsed < BannerFadeInSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Smooth01(elapsed / BannerFadeInSeconds);
            rootGroup.alpha = t;
            bannerPanel.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, t);
            accentLine.rectTransform.localScale = new Vector3(t, 1f, 1f);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < BannerHoldSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float pulse = 0.78f + Mathf.Sin(elapsed * Mathf.PI * 2.3f) * 0.22f;
            Color color = accentLine.color;
            color.a = pulse;
            accentLine.color = color;
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < BannerFadeOutSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            rootGroup.alpha = 1f - Smooth01(elapsed / BannerFadeOutSeconds);
            yield return null;
        }

        canvas.enabled = false;
        rootGroup.blocksRaycasts = false;
        AutoUIManager.Instance?.SetQuestOverlayOpen(false);
        BlocksGameplayInput = false;
        presentationRoutine = null;

        float waitUntil = Time.unscaledTime + 5f;
        flow = QuestFlowUIPrototype.Instance;
        while (flow == null && Time.unscaledTime < waitUntil)
        {
            yield return null;
            flow = QuestFlowUIPrototype.Instance;
        }
        if (flow != null)
        {
            PreMilitaryQuestRuntimeBridge.Instance?.ConfigureCivilianRouteMap(
                checkpoint, cityExit, MainQuestManager.CivilianRouteStage.CarReady);
            flow.SetCivilianCityMapUnlocked(true);
            flow.OpenCivilianRouteMapReveal();
        }
        else
            Debug.LogWarning("[ROUTE A] Quest map UI was unavailable after the car-ready banner.", this);
    }

    private IEnumerator OutroRoutine(NetworkObject repairedVehicle, Vector2 outroEnd, float survivalSeconds)
    {
        BlocksGameplayInput = true;
        VictorySummaryUI.CloseBlockingGameplayUI();
        EnsureUI();
        ConfigureCanvasForOutro();
        PrepareOutroVehicle(repairedVehicle);
        bool useRoadLoop = PrepareOutroRoadLoop(outroEnd);

        float elapsed = 0f;
        const float settleSeconds = 0.55f;
        while (elapsed < settleSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Smooth01(elapsed / settleSeconds);
            SetLetterboxAlpha(t);
            yield return null;
        }

        Vector3 startPosition = outroVehicleVisual != null
            ? outroVehicleVisual.transform.position
            : repairedVehicle != null ? repairedVehicle.transform.position : Vector3.zero;
        Vector3 endPosition = new Vector3(outroEnd.x, outroEnd.y, startPosition.z);
        Vector3 cameraOffset = gameplayCamera != null
            ? gameplayCamera.transform.position - startPosition
            : new Vector3(0f, 0f, -10f);
        float startZoom = gameplayCamera != null ? gameplayCamera.orthographicSize : 5f;
        float targetZoom = Mathf.Max(startZoom, 6.2f);

        elapsed = 0f;
        while (elapsed < OutroDriveSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / OutroDriveSeconds);
            float eased = Smooth01(t);
            Vector3 vehiclePosition = useRoadLoop && outroVehicleVisual != null
                ? outroVehicleVisual.transform.position
                : Vector3.Lerp(startPosition, endPosition, eased);
            if (!useRoadLoop && outroVehicleVisual != null)
                outroVehicleVisual.transform.position = vehiclePosition;
            if (gameplayCamera != null)
            {
                Vector3 desired = vehiclePosition + cameraOffset;
                desired.z = gameplayCamera.transform.position.z;
                gameplayCamera.transform.position = Vector3.Lerp(
                    gameplayCamera.transform.position, desired, 1f - Mathf.Exp(-5f * Time.unscaledDeltaTime));
                gameplayCamera.orthographicSize = Mathf.Lerp(startZoom, targetZoom, Smooth01(t));
            }

            float fadeT = Mathf.InverseLerp(0.72f, 1f, t);
            SetFadeAlpha(Smooth01(fadeT));
            yield return null;
        }

        SetFadeAlpha(1f);
        if (outroRoadLooper != null) outroRoadLooper.StopLoop();
        outroTitle.text = "ĐÃ RỜI KHỎI THÀNH PHỐ";
        outroSubtitle.text = "Một chặng đường mới đang chờ phía trước...";
        elapsed = 0f;
        const float titleSeconds = 2.15f;
        while (elapsed < titleSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float alpha = Mathf.Clamp01(elapsed / 0.55f) *
                          Mathf.Clamp01((titleSeconds - elapsed) / 0.45f);
            SetTextAlpha(outroTitle, alpha);
            SetTextAlpha(outroSubtitle, alpha * 0.88f);
            yield return null;
        }

        canvas.enabled = false;
        rootGroup.blocksRaycasts = false;
        presentationRoutine = null;
        VictorySummaryUI.ShowForCurrentMatch(survivalSeconds, EscapeEndingRoute.CivilianCar);
    }

    private void EnsureUI()
    {
        if (canvas != null) return;
        GameObject root = new GameObject("Civilian Route Presentation",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster),
            typeof(CanvasGroup));
        root.transform.SetParent(transform, false);
        canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 4900;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        rootGroup = root.GetComponent<CanvasGroup>();

        fadeImage = Image("Fade", root.transform, Color.black);
        Stretch(fadeImage.rectTransform);
        fadeImage.raycastTarget = true;

        topBar = Image("Top Cinematic Bar", root.transform, Color.black);
        SetAnchored(topBar.rectTransform, new Vector2(0.5f, 1f), new Vector2(1920f, 120f),
            new Vector2(0f, -60f));
        bottomBar = Image("Bottom Cinematic Bar", root.transform, Color.black);
        SetAnchored(bottomBar.rectTransform, new Vector2(0.5f, 0f), new Vector2(1920f, 120f),
            new Vector2(0f, 60f));

        Image bannerBackdrop = Image("Car Ready Banner", root.transform,
            new Color(0.025f, 0.08f, 0.065f, 0.97f));
        bannerPanel = bannerBackdrop.rectTransform;
        SetAnchored(bannerPanel, new Vector2(0.5f, 0.5f), new Vector2(780f, 230f), Vector2.zero);
        Outline outline = bannerBackdrop.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.28f, 0.88f, 0.7f, 0.75f);
        outline.effectDistance = new Vector2(2f, -2f);

        eyebrowText = Text("Eyebrow", bannerPanel, string.Empty, 18f,
            new Color(0.58f, 0.9f, 0.78f), FontStyles.Bold);
        SetAnchored(eyebrowText.rectTransform, new Vector2(0.5f, 1f), new Vector2(710f, 34f),
            new Vector2(0f, -40f));
        titleText = Text("Title", bannerPanel, string.Empty, 44f,
            new Color(0.35f, 1f, 0.62f), FontStyles.Bold);
        SetAnchored(titleText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(710f, 62f),
            new Vector2(0f, 13f));
        bodyText = Text("Body", bannerPanel, string.Empty, 21f,
            new Color(0.86f, 0.93f, 0.88f), FontStyles.Normal);
        SetAnchored(bodyText.rectTransform, new Vector2(0.5f, 0f), new Vector2(700f, 42f),
            new Vector2(0f, 43f));
        accentLine = Image("Accent Sweep", bannerPanel, new Color(0.28f, 1f, 0.7f, 1f));
        SetAnchored(accentLine.rectTransform, new Vector2(0.5f, 0f), new Vector2(700f, 4f),
            new Vector2(0f, 15f));

        outroTitle = Text("Outro Title", root.transform, string.Empty, 46f, Color.white, FontStyles.Bold);
        SetAnchored(outroTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(1000f, 70f),
            new Vector2(0f, 26f));
        outroSubtitle = Text("Outro Subtitle", root.transform, string.Empty, 21f,
            new Color(0.76f, 0.84f, 0.79f), FontStyles.Normal);
        SetAnchored(outroSubtitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(900f, 42f),
            new Vector2(0f, -38f));
        canvas.enabled = false;
    }

    private void ConfigureCanvasForBanner()
    {
        canvas.enabled = true;
        rootGroup.alpha = 1f;
        rootGroup.blocksRaycasts = true;
        bannerPanel.gameObject.SetActive(true);
        fadeImage.gameObject.SetActive(true);
        fadeImage.color = new Color(0f, 0f, 0f, 0.58f);
        topBar.gameObject.SetActive(false);
        bottomBar.gameObject.SetActive(false);
        outroTitle.gameObject.SetActive(false);
        outroSubtitle.gameObject.SetActive(false);
    }

    private void ConfigureCanvasForOutro()
    {
        canvas.enabled = true;
        rootGroup.alpha = 1f;
        rootGroup.blocksRaycasts = true;
        bannerPanel.gameObject.SetActive(false);
        fadeImage.gameObject.SetActive(true);
        SetFadeAlpha(0f);
        topBar.gameObject.SetActive(true);
        bottomBar.gameObject.SetActive(true);
        SetLetterboxAlpha(0f);
        outroTitle.gameObject.SetActive(true);
        outroSubtitle.gameObject.SetActive(true);
        SetTextAlpha(outroTitle, 0f);
        SetTextAlpha(outroSubtitle, 0f);
    }

    private void PrepareOutroVehicle(NetworkObject repairedVehicle)
    {
        RestoreOutroObjects();
        if (repairedVehicle == null || !repairedVehicle.IsValid) return;

        hiddenVehicleRenderers.Clear();
        Renderer[] renderers = repairedVehicle.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null) continue;
            hiddenVehicleRenderers.Add((renderer, renderer.enabled, renderer.forceRenderingOff));
        }

        outroVehicleVisual = CloneVehicleVisual(repairedVehicle.transform);
        for (int i = 0; i < hiddenVehicleRenderers.Count; i++)
        {
            Renderer renderer = hiddenVehicleRenderers[i].renderer;
            renderer.forceRenderingOff = true;
            renderer.enabled = false;
        }

        gameplayCamera = Camera.main;
        if (gameplayCamera != null)
        {
            isoCamera = gameplayCamera.GetComponent<PZ_CameraController>();
            if (isoCamera != null)
            {
                isoCameraWasEnabled = isoCamera.enabled;
                isoCamera.enabled = false;
            }
        }

        VehicleEngineAudioController engine = repairedVehicle.GetComponent<VehicleEngineAudioController>();
        AudioClip outroClip = Resources.Load<AudioClip>("Intro/VehicleAudio/CarAcce2");
        int copiedSample = 0;
        AudioSource[] existingSources = repairedVehicle.GetComponentsInChildren<AudioSource>(true);
        for (int i = 0; i < existingSources.Length; i++)
        {
            AudioSource source = existingSources[i];
            if (source == null || !source.isPlaying || source.clip == null) continue;
            if (!source.gameObject.name.Contains("Engine Driving")) continue;
            outroClip = source.clip;
            copiedSample = source.timeSamples;
            break;
        }
        if (engine != null)
        {
            suspendedEngineAudio = engine;
            engine.enabled = false;
        }
        if (outroClip != null)
        {
            outroEngineAudio = gameObject.AddComponent<AudioSource>();
            outroEngineAudio.playOnAwake = false;
            outroEngineAudio.loop = true;
            outroEngineAudio.spatialBlend = 0f;
            outroEngineAudio.clip = outroClip;
            outroEngineAudio.volume = 0.72f * Mathf.Clamp01(PlayerPrefs.GetFloat("GameSFXVolume", 0.8f));
            if (copiedSample > 0 && copiedSample < outroClip.samples) outroEngineAudio.timeSamples = copiedSample;
            outroEngineAudio.Play();
        }
    }

    private bool PrepareOutroRoadLoop(Vector2 outroEnd)
    {
        if (manager == null || outroVehicleVisual == null) return false;
        outroRoadLooper = GetComponent<CivilianOutroRoadLooper>();
        if (outroRoadLooper == null) outroRoadLooper = gameObject.AddComponent<CivilianOutroRoadLooper>();
        bool prepared = outroRoadLooper.Prepare(manager.CivilianCheckpointPosition,
            manager.CivilianCityExitPosition, outroEnd, outroVehicleVisual.transform);
        if (!prepared) return false;
        outroRoadLooper.BeginLoop();
        return outroRoadLooper.IsLooping;
    }

    private static GameObject CloneVehicleVisual(Transform source)
    {
        GameObject root = new GameObject("Civilian Outro Vehicle Visual");
        root.transform.position = source.position;
        root.transform.rotation = source.rotation;
        root.transform.localScale = source.lossyScale;
        CloneSpriteNodes(source, root.transform, source, true);
        return root;
    }

    private static void CloneSpriteNodes(Transform source, Transform parent, Transform sourceRoot, bool rootNode)
    {
        Transform target = parent;
        if (!rootNode)
        {
            GameObject child = new GameObject(source.name);
            target = child.transform;
            target.SetParent(parent, false);
            target.localPosition = source.localPosition;
            target.localRotation = source.localRotation;
            target.localScale = source.localScale;
        }

        SpriteRenderer original = source.GetComponent<SpriteRenderer>();
        if (original != null)
        {
            SpriteRenderer copy = target.gameObject.AddComponent<SpriteRenderer>();
            copy.sprite = original.sprite;
            copy.color = original.color;
            copy.flipX = original.flipX;
            copy.flipY = original.flipY;
            copy.drawMode = original.drawMode;
            copy.size = original.size;
            copy.maskInteraction = original.maskInteraction;
            copy.sortingLayerID = original.sortingLayerID;
            copy.sortingOrder = original.sortingOrder + 50;
            copy.sharedMaterial = original.sharedMaterial;
            copy.enabled = original.enabled;
        }
        for (int i = 0; i < source.childCount; i++)
            CloneSpriteNodes(source.GetChild(i), target, sourceRoot, false);
    }

    private void RestoreOutroObjects()
    {
        if (outroRoadLooper != null) outroRoadLooper.StopLoop();
        for (int i = 0; i < hiddenVehicleRenderers.Count; i++)
        {
            var state = hiddenVehicleRenderers[i];
            if (state.renderer == null) continue;
            state.renderer.forceRenderingOff = state.forceOff;
            state.renderer.enabled = state.enabled;
        }
        hiddenVehicleRenderers.Clear();
        if (outroVehicleVisual != null) Destroy(outroVehicleVisual);
        outroVehicleVisual = null;
        if (isoCamera != null) isoCamera.enabled = isoCameraWasEnabled;
        isoCamera = null;
        gameplayCamera = null;
        if (suspendedEngineAudio != null) suspendedEngineAudio.enabled = true;
        suspendedEngineAudio = null;
        if (outroEngineAudio != null) Destroy(outroEngineAudio);
        outroEngineAudio = null;
    }

    private void SetFadeAlpha(float alpha)
    {
        Color color = fadeImage.color;
        color.a = Mathf.Clamp01(alpha);
        fadeImage.color = color;
    }

    private void SetLetterboxAlpha(float alpha)
    {
        Color color = Color.black;
        color.a = Mathf.Clamp01(alpha);
        topBar.color = color;
        bottomBar.color = color;
    }

    private static void SetTextAlpha(TMP_Text text, float alpha)
    {
        Color color = text.color;
        color.a = Mathf.Clamp01(alpha);
        text.color = color;
    }

    private static float Smooth01(float value)
    {
        float t = Mathf.Clamp01(value);
        return t * t * (3f - 2f * t);
    }

    private static Image Image(string name, Transform parent, Color color)
    {
        GameObject child = new GameObject(name, typeof(RectTransform), typeof(Image));
        child.transform.SetParent(parent, false);
        Image image = child.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static TMP_Text Text(string name, Transform parent, string value, float size,
        Color color, FontStyles style)
    {
        GameObject child = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        child.transform.SetParent(parent, false);
        TMP_Text text = child.GetComponent<TMP_Text>();
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.fontStyle = style;
        text.alignment = TextAlignmentOptions.Center;
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetAnchored(RectTransform rect, Vector2 anchor, Vector2 size, Vector2 position)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    private void OnDisable()
    {
        BlocksGameplayInput = false;
        RestoreOutroObjects();
    }

    private void OnDestroy()
    {
        BlocksGameplayInput = false;
        RestoreOutroObjects();
    }
}
