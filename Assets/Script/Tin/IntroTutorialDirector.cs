using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Solo-only opening flow. It deliberately does not pause time or synchronize UI,
/// so multiplayer can continue using Main without this component.
/// </summary>
public sealed class IntroTutorialDirector : MonoBehaviour
{
    public bool IsComplete => state == State.Complete;

    [Header("Scene references")]
    [SerializeField] private IntroCarDriveSetup carDrive;
    [SerializeField] private IntroCameraFollow cameraFollow;
    [SerializeField] private HostModeSpawner playerSpawner;
    [SerializeField] private IntroRoadLooper roadLooper;

    [Header("Timing")]
    [SerializeField, Min(0.1f)] private float fadeDuration = 0.65f;
    [SerializeField, Range(0.02f, 0.2f)] private float cinematicBarHeight = 0.11f;

    [Header("Opening eye sequence")]
    [SerializeField, Min(0f)] private float eyeClosedHoldDuration = 0.7f;
    [SerializeField, Min(0.1f)] private float eyeOpeningDuration = 2.2f;
    [SerializeField] private AudioClip eyeOpeningVoice;

    [Header("Trailer road sequence")]
    [SerializeField, Min(1f)] private float trailerLoopDuration = 14f;
    [SerializeField, Min(0.2f)] private float carExitShotDuration = 1.7f;
    [SerializeField, Min(0f)] private float exitFadeDelay = 0.35f;
    [SerializeField, Min(0.1f)] private float blackTransitionHold = 1.2f;
    [SerializeField, Min(0.1f)] private float troubleFadeInDuration = 1.4f;
    [SerializeField, Range(0f, 1f)] private float trailerRadioVolume = 1f;

    private enum State
    {
        EyeOpening,
        LoopDriving,
        CarExitShot,
        BlackTransition,
        FadeIntoTrouble,
        TroubleDriving,
        Dialogue,
        FadingOut,
        WaitingForPlayer,
        FadingIn,
        Complete
    }
    private State state = State.EyeOpening;
    private float stateStartedAt;
    private float fadeAlpha;
    private PlayerMovement localPlayer;
    private IntroDialogueSequence dialogueSequence;
    private int dialogueLineIndex;
    private AudioSource eyeOpeningAudioSource;
    private bool eyeOpeningTimerStarted;
    private bool trailerDriveStarted;
    private GameObject eyeOpeningOverlay;
    private RectTransform upperEyelid;
    private RectTransform lowerEyelid;
    private readonly Dictionary<Canvas, bool> cinematicCanvasStates = new Dictionary<Canvas, bool>();
    private bool gameplayUiHidden;

    private void Awake()
    {
        TutorialSession.Begin();
        carDrive ??= FindFirstObjectByType<IntroCarDriveSetup>();
        cameraFollow ??= FindFirstObjectByType<IntroCameraFollow>();
        playerSpawner ??= FindFirstObjectByType<HostModeSpawner>();
        roadLooper ??= FindFirstObjectByType<IntroRoadLooper>();
        if (roadLooper == null) roadLooper = gameObject.AddComponent<IntroRoadLooper>();
        roadLooper.Prepare();
        dialogueSequence = Resources.Load<IntroDialogueSequence>("IntroDialogue/IntroOpeningDialogue");
        eyeOpeningVoice ??= Resources.Load<AudioClip>("Intro/EyeOpeningVoice");

        eyeOpeningAudioSource = GetComponent<AudioSource>();
        if (eyeOpeningAudioSource == null) eyeOpeningAudioSource = gameObject.AddComponent<AudioSource>();
        eyeOpeningAudioSource.playOnAwake = false;
        eyeOpeningAudioSource.spatialBlend = 0f;
        if (eyeOpeningVoice != null) eyeOpeningVoice.LoadAudioData();
        else Debug.LogError("Intro eye-opening voice was not found at Resources/Intro/EyeOpeningVoice.", this);
        stateStartedAt = Time.unscaledTime;
        CreateEyeOpeningOverlay();
        UpdateEyeOpeningOverlay(0f);
        BeginCinematicUIGate();
    }

    private void Update()
    {
        if (gameplayUiHidden)
            CaptureAndHideGameplayCanvases();

        switch (state)
        {
            case State.EyeOpening:
                if (!eyeOpeningTimerStarted)
                {
                    eyeOpeningTimerStarted = true;
                    stateStartedAt = Time.unscaledTime;
                    UpdateEyeOpeningOverlay(0f);
                    break;
                }

                float openingElapsed = Time.unscaledTime - stateStartedAt - eyeClosedHoldDuration;
                float eyeOpeningProgress = Mathf.Clamp01(openingElapsed / eyeOpeningDuration);
                UpdateEyeOpeningOverlay(eyeOpeningProgress);

                if (openingElapsed >= 0f && !trailerDriveStarted)
                    StartTrailerLoop();

                if (eyeOpeningProgress >= 1f)
                    EnterState(State.LoopDriving);
                break;

            case State.LoopDriving:
                if (Time.unscaledTime - stateStartedAt >= trailerLoopDuration)
                    EnterState(State.CarExitShot);
                break;

            case State.CarExitShot:
                float exitElapsed = Time.unscaledTime - stateStartedAt;
                fadeAlpha = Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(exitFadeDelay, carExitShotDuration, exitElapsed));
                if (eyeOpeningAudioSource != null)
                    eyeOpeningAudioSource.volume = trailerRadioVolume * (1f - fadeAlpha);
                if (roadLooper != null && roadLooper.IsExitComplete && fadeAlpha >= 1f)
                {
                    roadLooper.SwitchToTroubleRoad();
                    if (cameraFollow != null)
                    {
                        cameraFollow.enabled = true;
                        cameraFollow.SetTarget(carDrive.transform, true);
                    }
                    carDrive?.BeginDrive();
                    EnterState(State.BlackTransition);
                }
                break;

            case State.BlackTransition:
                fadeAlpha = 1f;
                if (Time.unscaledTime - stateStartedAt >= blackTransitionHold)
                    EnterState(State.FadeIntoTrouble);
                break;

            case State.FadeIntoTrouble:
                fadeAlpha = 1f - Mathf.SmoothStep(0f, 1f,
                    Mathf.Clamp01((Time.unscaledTime - stateStartedAt) / troubleFadeInDuration));
                if (fadeAlpha <= 0f)
                    EnterState(State.TroubleDriving);
                break;

            case State.TroubleDriving:
                if (carDrive != null && carDrive.IsComplete)
                    EnterState(State.Dialogue);
                break;

            case State.Dialogue:
                if (Time.unscaledTime - stateStartedAt > 0.35f && Input.GetKeyDown(KeyCode.E))
                {
                    if (dialogueLineIndex < DialogueLineCount - 1)
                        dialogueLineIndex++;
                    else
                        EnterState(State.FadingOut);
                }
                break;

            case State.FadingOut:
                fadeAlpha = Mathf.Clamp01((Time.unscaledTime - stateStartedAt) / fadeDuration);
                if (fadeAlpha >= 1f)
                {
                    if (playerSpawner == null)
                    {
                        Debug.LogError("IntroTutorialDirector needs a HostModeSpawner configured for the Intro scene.", this);
                        EnterState(State.Complete);
                        break;
                    }

                    playerSpawner.BeginInitialSpawn();
                    EnterState(State.WaitingForPlayer);
                }
                break;

            case State.WaitingForPlayer:
                localPlayer = FindFirstObjectByType<PlayerMovement>();
                if (localPlayer != null)
                {
                    cameraFollow?.SetTarget(localPlayer.transform, true);
                    EnterState(State.FadingIn);
                }
                break;

            case State.FadingIn:
                fadeAlpha = 1f - Mathf.Clamp01((Time.unscaledTime - stateStartedAt) / fadeDuration);
                if (fadeAlpha <= 0f)
                    EnterState(State.Complete);
                break;
        }
    }

    private void EnterState(State nextState)
    {
        state = nextState;
        stateStartedAt = Time.unscaledTime;

        if (nextState == State.Dialogue)
            dialogueLineIndex = 0;

        if (nextState == State.EyeOpening)
        {
            eyeOpeningTimerStarted = false;
            trailerDriveStarted = false;
            eyeOpeningAudioSource?.Stop();
        }

        if (nextState == State.FadingOut)
            EndCinematicUIGate();

        if (nextState == State.LoopDriving)
        {
            UpdateEyeOpeningOverlay(1f);
        }

        if (nextState == State.CarExitShot)
        {
            fadeAlpha = 0f;
            if (cameraFollow != null) cameraFollow.enabled = false;
            roadLooper?.BeginExitShot(carExitShotDuration);
        }

        if (nextState == State.BlackTransition)
        {
            fadeAlpha = 1f;
            if (eyeOpeningAudioSource != null)
            {
                eyeOpeningAudioSource.volume = 0f;
                eyeOpeningAudioSource.Stop();
            }
        }

        if (nextState == State.TroubleDriving)
            fadeAlpha = 0f;

        if (nextState == State.Dialogue)
        {
            if (eyeOpeningOverlay != null) eyeOpeningOverlay.SetActive(false);
            eyeOpeningAudioSource?.Stop();
        }
    }

    private int DialogueLineCount => dialogueSequence != null && dialogueSequence.lines.Count > 0
        ? dialogueSequence.lines.Count
        : 1;

    private string CurrentDialogueLine => dialogueSequence != null && dialogueSequence.lines.Count > 0
        ? dialogueSequence.lines[Mathf.Clamp(dialogueLineIndex, 0, dialogueSequence.lines.Count - 1)]
        : "Xe đã chết máy. Phải xuống kiểm tra thôi.";

    private void StartTrailerLoop()
    {
        trailerDriveStarted = true;
        roadLooper?.BeginLoop();

        if (eyeOpeningVoice == null || eyeOpeningAudioSource == null) return;
        eyeOpeningAudioSource.clip = eyeOpeningVoice;
        eyeOpeningAudioSource.loop = true;
        eyeOpeningAudioSource.volume = trailerRadioVolume;
        eyeOpeningAudioSource.Play();
    }

    private void OnGUI()
    {
        if (state == State.Dialogue)
        {
            float width = Mathf.Min(740f, Screen.width - 80f);
            Rect box = new Rect((Screen.width - width) * 0.5f, Screen.height - 190f, width, 125f);
            GUI.Box(box, string.Empty);

            GUIStyle messageStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
                normal = { textColor = Color.white }
            };

            GUI.Label(new Rect(box.x + 28f, box.y + 22f, box.width - 56f, 64f), CurrentDialogueLine, messageStyle);

            GUIStyle promptStyle = new GUIStyle(messageStyle)
            {
                fontSize = 16,
                alignment = TextAnchor.LowerRight,
                normal = { textColor = new Color(0.75f, 0.9f, 1f) }
            };

            GUI.Label(new Rect(box.x + 28f, box.y + 78f, box.width - 56f, 28f),
                dialogueLineIndex < DialogueLineCount - 1 ? "[E] Tiếp" : "[E] Rời xe", promptStyle);
        }

        float barAlpha = state == State.Dialogue
            ? 1f - Mathf.Clamp01((Time.unscaledTime - stateStartedAt) / 0.35f)
            : 0f;

        if (barAlpha > 0f)
        {
            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, barAlpha);
            float barHeight = Screen.height * cinematicBarHeight;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, barHeight), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0f, Screen.height - barHeight, Screen.width, barHeight), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        if (fadeAlpha > 0f)
        {
            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, fadeAlpha);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }

    private void CreateEyeOpeningOverlay()
    {
        eyeOpeningOverlay = new GameObject("IntroEyeOpeningOverlay", typeof(RectTransform), typeof(Canvas));
        Canvas canvas = eyeOpeningOverlay.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        upperEyelid = CreateEyelid("UpperEyelid", true);
        lowerEyelid = CreateEyelid("LowerEyelid", false);
    }

    private void UpdateEyeOpeningOverlay(float progress)
    {
        if (upperEyelid == null || lowerEyelid == null) return;

        // Both lids move away from the centre: upper goes up, lower goes down.
        // They finish at the exact cinematic-bar height instead of disappearing.
        float easedProgress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress));
        float startHeight = Screen.height * 0.5f;
        float finalHeight = Screen.height * cinematicBarHeight;
        float lidHeight = Mathf.Lerp(startHeight, finalHeight, easedProgress);
        upperEyelid.sizeDelta = new Vector2(0f, lidHeight);
        lowerEyelid.sizeDelta = new Vector2(0f, lidHeight);
    }

    private RectTransform CreateEyelid(string name, bool isUpper)
    {
        GameObject lid = new GameObject(name, typeof(RectTransform), typeof(Image));
        lid.transform.SetParent(eyeOpeningOverlay.transform, false);
        RectTransform lidTransform = lid.GetComponent<RectTransform>();
        lidTransform.anchorMin = isUpper ? new Vector2(0f, 1f) : Vector2.zero;
        lidTransform.anchorMax = isUpper ? Vector2.one : new Vector2(1f, 0f);
        lidTransform.pivot = isUpper ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0f);
        lidTransform.anchoredPosition = Vector2.zero;
        Image image = lid.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;
        return lidTransform;
    }

    private void BeginCinematicUIGate()
    {
        gameplayUiHidden = true;
        CaptureAndHideGameplayCanvases();
    }

    private void CaptureAndHideGameplayCanvases()
    {
        Canvas introCanvas = eyeOpeningOverlay != null ? eyeOpeningOverlay.GetComponent<Canvas>() : null;
        Canvas[] canvases = Resources.FindObjectsOfTypeAll<Canvas>();
        foreach (Canvas canvas in canvases)
        {
            if (canvas == null || canvas == introCanvas || !canvas.gameObject.scene.IsValid())
                continue;

            if (!cinematicCanvasStates.ContainsKey(canvas))
                cinematicCanvasStates.Add(canvas, canvas.enabled);

            canvas.enabled = false;
        }
    }

    private void EndCinematicUIGate()
    {
        if (!gameplayUiHidden) return;

        gameplayUiHidden = false;
        foreach (KeyValuePair<Canvas, bool> entry in cinematicCanvasStates)
        {
            if (entry.Key != null)
                entry.Key.enabled = entry.Value;
        }
        cinematicCanvasStates.Clear();
    }

    private void OnDestroy()
    {
        EndCinematicUIGate();
    }
}
