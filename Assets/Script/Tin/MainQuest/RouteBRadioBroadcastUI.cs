using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Compact, non-blocking Route B radio/subtitle presentation. Recorded clips are
/// optional; missing radio clips use a procedural static bed so story flow remains complete.
/// </summary>
public sealed class RouteBRadioBroadcastUI : MonoBehaviour
{
    private static RouteBRadioBroadcastUI instance;

    private Canvas canvas;
    private TMP_Text eyebrowText;
    private TMP_Text titleText;
    private TMP_Text bodyText;
    private TMP_Text skipText;
    private AudioSource audioSource;
    private AudioClip radioStaticClip;
    private Coroutine sequenceRoutine;
    private Action sequenceCompleted;
    private bool skipRequested;
    private bool waitForInteractionKeyRelease;

    public static bool IsVisible => instance != null && instance.canvas != null && instance.canvas.enabled;

    public static void ShowOpeningSequence(Action onCompleted)
    {
        RouteBRadioBroadcastUI ui = EnsureInstance();
        ui.BeginOpeningSequence(onCompleted);
    }

    public static void ShowCue(RouteBAudioCueId cueId)
    {
        RouteBRadioBroadcastUI ui = EnsureInstance();
        // Never let a later milestone interrupt the opening broadcast: its
        // completion callback is what opens the non-locking route choice.
        if (IsVisible && ui.sequenceCompleted != null)
            return;
        ui.BeginSingleCue(RouteBAudioContent.Get(cueId));
    }

    public static void SkipIfOpen()
    {
        if (instance != null && IsVisible) instance.skipRequested = true;
    }

    public static void CloseIfOpen()
    {
        if (instance != null) instance.FinishSequence(false);
    }

    private static RouteBRadioBroadcastUI EnsureInstance()
    {
        if (instance != null) return instance;
        GameObject host = new GameObject("Route B Radio Broadcast UI");
        instance = host.AddComponent<RouteBRadioBroadcastUI>();
        instance.Build();
        return instance;
    }

    private void Awake()
    {
        if (instance == null) instance = this;
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }
        if (canvas == null) Build();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Update()
    {
        if (!IsVisible) return;
        if (waitForInteractionKeyRelease)
        {
            if (!Input.GetKey(KeyCode.E)) waitForInteractionKeyRelease = false;
            return;
        }

        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Escape))
            skipRequested = true;
    }

    private void BeginOpeningSequence(Action onCompleted)
    {
        if (sequenceRoutine != null) StopCoroutine(sequenceRoutine);
        audioSource.Stop();
        sequenceCompleted = onCompleted;
        skipRequested = false;
        waitForInteractionKeyRelease = Input.GetKey(KeyCode.E);
        canvas.enabled = true;
        sequenceRoutine = StartCoroutine(PlayOpeningSequence());
    }

    private void BeginSingleCue(RouteBAudioCue cue)
    {
        if (sequenceRoutine != null) StopCoroutine(sequenceRoutine);
        audioSource.Stop();
        sequenceCompleted = null;
        skipRequested = false;
        waitForInteractionKeyRelease = Input.GetKey(KeyCode.E);
        canvas.enabled = true;
        sequenceRoutine = StartCoroutine(PlaySingleCue(cue));
    }

    private IEnumerator PlaySingleCue(RouteBAudioCue cue)
    {
        RenderCue(cue, 0, 1);
        float duration = PlayCueAudio(cue);
        float elapsed = 0f;
        while (!skipRequested && elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        sequenceRoutine = null;
        FinishSequence(true);
    }

    private IEnumerator PlayOpeningSequence()
    {
        var cues = RouteBAudioContent.OpeningSequence;
        for (int i = 0; i < cues.Count; i++)
        {
            RouteBAudioCue cue = cues[i];
            RenderCue(cue, i, cues.Count);
            float duration = PlayCueAudio(cue);
            float elapsed = 0f;
            while (!skipRequested && elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            audioSource.Stop();
            if (skipRequested) break;

            float gap = 0f;
            while (gap < 0.3f)
            {
                gap += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        sequenceRoutine = null;
        FinishSequence(true);
    }

    private void RenderCue(RouteBAudioCue cue, int index, int count)
    {
        eyebrowText.text = cue.Speaker + "  //  TUYẾN THOÁT HIỂM B";
        eyebrowText.color = cue.IsRadioTransmission
            ? new Color(1f, 0.67f, 0.14f)
            : new Color(0.28f, 0.88f, 0.7f);
        titleText.text = cue.Title;
        bodyText.text = GameLocalization.IsVietnamese ? cue.Vietnamese : cue.English;
        skipText.text = $"{index + 1}/{count}    [E] BỎ QUA";
    }

    private float PlayCueAudio(RouteBAudioCue cue)
    {
        AudioClip clip = Resources.Load<AudioClip>(cue.AudioResourcePath);
        audioSource.Stop();
        audioSource.clip = clip != null ? clip : cue.IsRadioTransmission ? radioStaticClip : null;
        audioSource.loop = clip == null && cue.IsRadioTransmission;
        audioSource.volume = PlayerPrefs.GetFloat("GameSFXVolume", 0.8f) *
                             (clip == null ? 0.2f : 0.78f);
        if (audioSource.clip != null) audioSource.Play();
        return clip != null ? Mathf.Max(cue.FallbackDuration, clip.length + 0.2f) : cue.FallbackDuration;
    }

    private void FinishSequence(bool invokeCallback)
    {
        if (sequenceRoutine != null)
        {
            StopCoroutine(sequenceRoutine);
            sequenceRoutine = null;
        }
        audioSource?.Stop();
        if (canvas != null) canvas.enabled = false;
        Action callback = sequenceCompleted;
        sequenceCompleted = null;
        skipRequested = false;
        if (invokeCallback) callback?.Invoke();
    }

    private void Build()
    {
        if (canvas != null) return;
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 4350;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        GameObject panel = new GameObject("Route B Radio Panel", typeof(RectTransform), typeof(Image), typeof(Outline));
        panel.transform.SetParent(transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 1f);
        panelRect.anchorMax = new Vector2(0.5f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.sizeDelta = new Vector2(820f, 190f);
        panelRect.anchoredPosition = new Vector2(0f, -48f);
        panel.GetComponent<Image>().color = new Color(0.025f, 0.04f, 0.037f, 0.96f);
        panel.GetComponent<Outline>().effectColor = new Color(0.38f, 0.44f, 0.41f, 0.95f);

        eyebrowText = Text(panelRect, "Radio Speaker", string.Empty, 12f, FontStyles.Bold,
            new Vector2(0f, 1f), new Vector2(670f, 24f), new Vector2(28f, -22f), TextAlignmentOptions.Left);
        titleText = Text(panelRect, "Radio Title", string.Empty, 23f, FontStyles.Bold,
            new Vector2(0f, 1f), new Vector2(670f, 34f), new Vector2(28f, -51f), TextAlignmentOptions.Left);
        bodyText = Text(panelRect, "Radio Subtitle", string.Empty, 15f, FontStyles.Normal,
            new Vector2(0f, 1f), new Vector2(760f, 76f), new Vector2(28f, -91f), TextAlignmentOptions.TopLeft);
        bodyText.color = new Color(0.82f, 0.86f, 0.84f);
        skipText = Text(panelRect, "Radio Skip", string.Empty, 11f, FontStyles.Bold,
            new Vector2(1f, 0f), new Vector2(170f, 22f), new Vector2(-24f, 16f), TextAlignmentOptions.Right);
        skipText.color = new Color(0.62f, 0.69f, 0.67f);

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.ignoreListenerPause = true;
        radioStaticClip = CreateRadioStaticClip();
        canvas.enabled = false;
    }

    private static TextMeshProUGUI Text(Transform parent, string name, string value, float size,
        FontStyles style, Vector2 anchor, Vector2 dimensions, Vector2 position, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.sizeDelta = dimensions;
        rect.anchoredPosition = position;
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = GameLocalization.GetRuntimeFont(TMP_Settings.defaultFontAsset);
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }

    private static AudioClip CreateRadioStaticClip()
    {
        const int sampleRate = 22050;
        const int sampleCount = sampleRate * 2;
        float[] samples = new float[sampleCount];
        System.Random random = new System.Random(7319);
        float previous = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            float white = (float)(random.NextDouble() * 2.0 - 1.0);
            previous = Mathf.Lerp(previous, white, 0.32f);
            samples[i] = previous * 0.16f;
        }
        AudioClip clip = AudioClip.Create("Route B Radio Static", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
