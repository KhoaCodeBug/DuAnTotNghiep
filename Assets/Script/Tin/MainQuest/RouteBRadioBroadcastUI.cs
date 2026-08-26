using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Local Route B dialogue presentation. It never pauses the shared simulation:
/// only this client's UI, audio mix and local network input are temporarily locked.
/// Missing radio clips use a procedural static bed so story flow remains complete.
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
    private readonly Queue<PendingCue> pendingCues = new Queue<PendingCue>();
    private readonly Dictionary<Canvas, bool> suppressedCanvases = new Dictionary<Canvas, bool>();
    private bool localPresentationActive;
    private float listenerVolumeBeforeDialogue = 1f;
    private float nextCanvasSuppressionAt;

    private const float DialogueDuckMultiplier = 0.18f;
    private const float CanvasSuppressionInterval = 0.2f;

    private readonly struct PendingCue
    {
        public PendingCue(RouteBAudioCue cue, Action callback)
        {
            Cue = cue;
            Callback = callback;
        }

        public RouteBAudioCue Cue { get; }
        public Action Callback { get; }
    }

    public static bool IsVisible => instance != null && instance.canvas != null && instance.canvas.enabled;
    public static bool BlocksLocalGameplayInput => IsVisible;

    public static void ShowOpeningSequence(Action onCompleted)
    {
        RouteBRadioBroadcastUI ui = EnsureInstance();
        ui.BeginSequence(RouteBAudioContent.OpeningSequence, onCompleted);
    }

    public static void ShowHospitalRecording(Action onCompleted = null)
    {
        RouteBRadioBroadcastUI ui = EnsureInstance();
        // This is an authoritative story milestone. Replace an incidental queued
        // cue so the completion callback can never be lost behind stale UI.
        if (IsVisible) ui.FinishSequence(false);
        ui.BeginSequence(RouteBAudioContent.HospitalRecordingSequence, onCompleted);
    }

    public static void ShowCue(RouteBAudioCueId cueId)
    {
        ShowCue(cueId, null);
    }

    public static void ShowCue(RouteBAudioCueId cueId, Action onCompleted)
    {
        RouteBRadioBroadcastUI ui = EnsureInstance();
        // Never let a later milestone interrupt a cue that owns a story-flow
        // callback. Ordinary milestone cues are queued so all 15 recordings
        // remain audible even when two network events arrive close together.
        if (IsVisible && ui.sequenceCompleted != null)
            return;
        if (IsVisible)
        {
            ui.pendingCues.Enqueue(new PendingCue(RouteBAudioContent.Get(cueId), onCompleted));
            return;
        }
        ui.BeginSingleCue(RouteBAudioContent.Get(cueId), onCompleted);
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
        EndLocalPresentation();
        if (instance == this) instance = null;
    }

    private void Update()
    {
        if (!IsVisible) return;

        // AudioListener is process-local, so ducking here never changes the
        // server or another player's mix. The dialogue source ignores listener
        // volume and therefore stays clear above the reduced game audio.
        if (localPresentationActive)
        {
            AudioListener.volume = listenerVolumeBeforeDialogue * DialogueDuckMultiplier;
            if (Time.unscaledTime >= nextCanvasSuppressionAt)
            {
                nextCanvasSuppressionAt = Time.unscaledTime + CanvasSuppressionInterval;
                SuppressForeignCanvases();
            }
        }

        if (waitForInteractionKeyRelease)
        {
            if (!Input.GetKey(KeyCode.E)) waitForInteractionKeyRelease = false;
            return;
        }

        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Escape))
            skipRequested = true;
    }

    private void BeginSequence(IReadOnlyList<RouteBAudioCue> cues, Action onCompleted)
    {
        if (sequenceRoutine != null) StopCoroutine(sequenceRoutine);
        audioSource.Stop();
        sequenceCompleted = onCompleted;
        skipRequested = false;
        waitForInteractionKeyRelease = Input.GetKey(KeyCode.E);
        BeginLocalPresentation();
        canvas.enabled = true;
        sequenceRoutine = StartCoroutine(PlaySequence(cues));
    }

    private void BeginSingleCue(RouteBAudioCue cue, Action onCompleted)
    {
        if (sequenceRoutine != null) StopCoroutine(sequenceRoutine);
        audioSource.Stop();
        sequenceCompleted = onCompleted;
        skipRequested = false;
        waitForInteractionKeyRelease = Input.GetKey(KeyCode.E);
        BeginLocalPresentation();
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

    private IEnumerator PlaySequence(IReadOnlyList<RouteBAudioCue> cues)
    {
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
        eyebrowText.text = RouteBAudioContent.GetLocalizedSpeaker(cue, GameLocalization.IsVietnamese);
        eyebrowText.color = cue.IsRadioTransmission
            ? new Color(1f, 0.67f, 0.14f)
            : new Color(0.28f, 0.88f, 0.7f);
        titleText.text = RouteBAudioContent.GetLocalizedTitle(cue, GameLocalization.IsVietnamese);
        bodyText.text = GameLocalization.IsVietnamese ? cue.Vietnamese : cue.English;
        skipText.text = $"{index + 1}/{count}    " +
                        (GameLocalization.IsVietnamese ? "[E] BỎ QUA" : "[E] SKIP");
    }

    private float PlayCueAudio(RouteBAudioCue cue)
    {
        AudioClip clip = Resources.Load<AudioClip>(cue.AudioResourcePath);
        audioSource.Stop();
        audioSource.clip = clip != null ? clip : cue.IsRadioTransmission ? radioStaticClip : null;
        audioSource.loop = clip == null && cue.IsRadioTransmission;
        float masterVolume = PlayerPrefs.GetFloat("GameMasterVolume", 1f);
        audioSource.volume = masterVolume * PlayerPrefs.GetFloat("GameSFXVolume", 0.8f) *
                             (clip == null ? 0.24f : 0.92f);
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
        if (!invokeCallback)
        {
            pendingCues.Clear();
            EndLocalPresentation();
            return;
        }

        if (pendingCues.Count > 0 && !EscapeRouteDecisionUI.IsVisible)
        {
            callback?.Invoke();
            PendingCue next = pendingCues.Dequeue();
            BeginSingleCue(next.Cue, next.Callback);
            return;
        }

        EndLocalPresentation();
        // Restore the previous Canvas states before a story callback opens the
        // route-choice UI. Otherwise a previously-created disabled choice canvas
        // would be restored to disabled immediately after ShowPreMilitaryChoice.
        callback?.Invoke();
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
        panelRect.anchorMin = new Vector2(0.5f, 0f);
        panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.sizeDelta = new Vector2(1120f, 228f);
        panelRect.anchoredPosition = new Vector2(0f, 128f);
        panel.GetComponent<Image>().color = new Color(0.025f, 0.04f, 0.037f, 0.96f);
        panel.GetComponent<Outline>().effectColor = new Color(0.38f, 0.44f, 0.41f, 0.95f);

        eyebrowText = Text(panelRect, "Radio Speaker", string.Empty, 12f, FontStyles.Bold,
            new Vector2(0f, 1f), new Vector2(930f, 24f), new Vector2(28f, -20f), TextAlignmentOptions.Left);
        titleText = Text(panelRect, "Radio Title", string.Empty, 23f, FontStyles.Bold,
            new Vector2(0f, 1f), new Vector2(930f, 34f), new Vector2(28f, -47f), TextAlignmentOptions.Left);
        bodyText = Text(panelRect, "Radio Subtitle", string.Empty, 15f, FontStyles.Normal,
            new Vector2(0f, 1f), new Vector2(1040f, 116f), new Vector2(28f, -84f), TextAlignmentOptions.TopLeft);
        bodyText.color = new Color(0.82f, 0.86f, 0.84f);
        skipText = Text(panelRect, "Radio Skip", string.Empty, 11f, FontStyles.Bold,
            new Vector2(1f, 0f), new Vector2(170f, 22f), new Vector2(-24f, 16f), TextAlignmentOptions.Right);
        skipText.color = new Color(0.62f, 0.69f, 0.67f);

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.ignoreListenerPause = true;
        audioSource.ignoreListenerVolume = true;
        radioStaticClip = CreateRadioStaticClip();
        canvas.enabled = false;
    }

    private void BeginLocalPresentation()
    {
        if (localPresentationActive) return;
        localPresentationActive = true;
        QuestUIDialogueState.SetActive(true);
        listenerVolumeBeforeDialogue = AudioListener.volume;
        AudioListener.volume = listenerVolumeBeforeDialogue * DialogueDuckMultiplier;
        nextCanvasSuppressionAt = 0f;
        SuppressForeignCanvases();
    }

    private void SuppressForeignCanvases()
    {
        Canvas[] canvases = Resources.FindObjectsOfTypeAll<Canvas>();
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas candidate = canvases[i];
            if (candidate == null || candidate == canvas || !candidate.gameObject.scene.IsValid()) continue;
            // Fog is part of the rendered world, not an interactive gameplay
            // HUD. Keep it active under dialogue and the following route choice.
            if (candidate.gameObject.name == "Local Fog Vision Overlay") continue;
            if (!suppressedCanvases.ContainsKey(candidate))
                suppressedCanvases.Add(candidate, candidate.enabled);
            candidate.enabled = false;
        }
    }

    private void EndLocalPresentation()
    {
        if (!localPresentationActive) return;
        localPresentationActive = false;
        QuestUIDialogueState.SetActive(false);
        AudioListener.volume = listenerVolumeBeforeDialogue;
        foreach (KeyValuePair<Canvas, bool> entry in suppressedCanvases)
            if (entry.Key != null) entry.Key.enabled = entry.Value;
        suppressedCanvases.Clear();
        // AutoCanvas is shared by several modal presenters. Its snapshot can
        // legitimately be stale when a cinematic changes modal ownership while
        // this radio sequence is open, so let AutoUIManager restore from its
        // current logical state instead of preserving that stale value.
        AutoUIManager.Instance?.ReconcileGameplayCanvasVisibility();
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
