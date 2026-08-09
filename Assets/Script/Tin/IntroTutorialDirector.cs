using Fusion;
using UnityEngine;

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

    [Header("Timing")]
    [SerializeField, Min(0.1f)] private float fadeDuration = 0.65f;
    [SerializeField, Range(0.02f, 0.2f)] private float cinematicBarHeight = 0.11f;

    private enum State { Driving, Dialogue, FadingOut, WaitingForPlayer, FadingIn, Complete }
    private State state = State.Driving;
    private float stateStartedAt;
    private float fadeAlpha;
    private PlayerMovement localPlayer;
    private IntroDialogueSequence dialogueSequence;
    private int dialogueLineIndex;

    private void Awake()
    {
        TutorialSession.Begin();
        carDrive ??= FindFirstObjectByType<IntroCarDriveSetup>();
        cameraFollow ??= FindFirstObjectByType<IntroCameraFollow>();
        playerSpawner ??= FindFirstObjectByType<HostModeSpawner>();
        dialogueSequence = Resources.Load<IntroDialogueSequence>("IntroDialogue/IntroOpeningDialogue");
    }

    private void Update()
    {
        switch (state)
        {
            case State.Driving:
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
    }

    private int DialogueLineCount => dialogueSequence != null && dialogueSequence.lines.Count > 0
        ? dialogueSequence.lines.Count
        : 1;

    private string CurrentDialogueLine => dialogueSequence != null && dialogueSequence.lines.Count > 0
        ? dialogueSequence.lines[Mathf.Clamp(dialogueLineIndex, 0, dialogueSequence.lines.Count - 1)]
        : "Xe đã chết máy. Phải xuống kiểm tra thôi.";

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

        float barAlpha = state == State.Driving ? 1f
            : state == State.Dialogue ? 1f - Mathf.Clamp01((Time.unscaledTime - stateStartedAt) / 0.35f)
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
}
