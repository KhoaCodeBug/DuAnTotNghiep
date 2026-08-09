using UnityEngine;

/// <summary>
/// The non-combat first chapter of the standalone tutorial. Every new concept
/// starts with a modal instruction; active steps are deliberately small,
/// measurable objectives so players cannot get lost in the scene.
/// </summary>
public sealed class TutorialPhaseOneController : MonoBehaviour
{
    private enum Step
    {
        WaitingForIntro,
        MoveBrief, Move,
        ZoomBrief, Zoom,
        AimBrief, Aim,
        NeedsBrief, NeedsFocus,
        HouseBrief, GoToKitchen,
        CabinetBrief, Loot,
        ConsumeBrief, Consume,
        CompleteBrief, Complete
    }

    [Header("Targets")]
    [SerializeField] private IntroTutorialDirector introDirector;
    [SerializeField] private Transform kitchenCabinet;
    [SerializeField] private TutorialPhaseOneText tutorialText;
    [SerializeField] private RoofVisibility targetHouseRoof;

    [Header("Progress tuning")]
    [SerializeField, Min(0.5f)] private float movementSecondsRequired = 3f;
    [SerializeField, Min(0.5f)] private float requiredIndoorSeconds = 2f;
    [SerializeField, Min(0.5f)] private float aimingSecondsRequired = 1.2f;
    [SerializeField, Min(0.5f)] private float zoomPracticeSecondsRequired = 2f;
    [SerializeField, Range(0.05f, 0.39f)] private float tutorialNeedRatio = 0.35f;
    [SerializeField, Range(0f, 1f)] private float initialZoomInAmount = 0.85f;

    private Step step = Step.WaitingForIntro;
    private PlayerMovement localPlayer;
    private PlayerSurvival survival;
    private InventorySystem inventory;
    private RoofDetector roofDetector;
    private float movementProgress;
    private float aimingProgress;
    private float zoomPracticeProgress;
    private float indoorProgress;
    private bool needsApplied;
    private bool initialZoomApplied;
    private string modalTitle;
    private string modalBody;

    private void Awake()
    {
        // Directly playing Intro_Cinematic from the editor must behave exactly
        // like entering it from the future HƯỚNG DẪN menu button.
        TutorialSession.Begin();
        introDirector ??= FindFirstObjectByType<IntroTutorialDirector>();
        tutorialText ??= Resources.Load<TutorialPhaseOneText>("Tutorial/TutorialPhaseOneText");
        if (kitchenCabinet == null)
        {
            GameObject cabinet = GameObject.Find("Prefab_Kitchen1_E (1)");
            if (cabinet != null) kitchenCabinet = cabinet.transform;
        }
        if (targetHouseRoof == null)
        {
            GameObject house = GameObject.Find("Nha8 (1)");
            if (house != null) targetHouseRoof = house.GetComponent<RoofVisibility>();
        }
    }

    private void OnDisable()
    {
        TutorialInputGate.Clear();
    }

    private void Update()
    {
        CachePlayerReferences();

        switch (step)
        {
            case Step.WaitingForIntro:
                if (introDirector != null && introDirector.IsComplete && localPlayer != null)
                {
                    SetInitialTutorialZoom();
                    TutorialInputGate.SetCameraZoomLocked(true);
                    ShowModal(Step.MoveBrief, tutorialText.moveTitle, tutorialText.moveBrief);
                }
                break;

            case Step.MoveBrief:
                if (ContinueModalPressed())
                {
                    step = Step.Move;
                    TutorialInputGate.Configure(false, true);
                }
                break;

            case Step.Move:
                if (localPlayer != null && localPlayer.NetIsMoving)
                    movementProgress += Time.unscaledDeltaTime;
                if (movementProgress >= movementSecondsRequired)
                    ShowModal(Step.ZoomBrief, tutorialText.zoomTitle, tutorialText.zoomBrief);
                break;

            case Step.ZoomBrief:
                if (ContinueModalPressed())
                {
                    step = Step.Zoom;
                    TutorialInputGate.Configure(true, true);
                    TutorialInputGate.SetCameraZoomLocked(false);
                }
                break;

            case Step.Zoom:
                if (Mathf.Abs(Input.GetAxis("Mouse ScrollWheel")) > 0.001f)
                    zoomPracticeProgress = Mathf.Max(zoomPracticeProgress, Time.unscaledDeltaTime);
                if (zoomPracticeProgress > 0f)
                    zoomPracticeProgress += Time.unscaledDeltaTime;
                if (zoomPracticeProgress >= zoomPracticeSecondsRequired)
                    ShowModal(Step.AimBrief, tutorialText.aimTitle, tutorialText.aimBrief);
                break;

            case Step.AimBrief:
                if (ContinueModalPressed())
                {
                    step = Step.Aim;
                    TutorialInputGate.Configure(true, true);
                }
                break;

            case Step.Aim:
                if (localPlayer != null && localPlayer.NetIsAiming)
                    aimingProgress += Time.unscaledDeltaTime;
                if (aimingProgress >= aimingSecondsRequired)
                {
                    ApplyTutorialNeeds();
                    ShowModal(Step.NeedsBrief, tutorialText.needsTitle, tutorialText.needsBrief);
                }
                break;

            case Step.NeedsBrief:
                if (ContinueModalPressed())
                    step = Step.NeedsFocus;
                break;

            case Step.NeedsFocus:
                if (Input.GetMouseButtonDown(0))
                    ShowModal(Step.HouseBrief, tutorialText.houseTitle, tutorialText.houseBrief);
                break;

            case Step.HouseBrief:
                if (ContinueModalPressed())
                {
                    step = Step.GoToKitchen;
                    TutorialInputGate.Configure(false, true);
                }
                break;

            case Step.GoToKitchen:
                if (HasStayedInsideTargetHouse())
                    ShowModal(Step.CabinetBrief, tutorialText.cabinetTitle, tutorialText.cabinetBrief);
                break;

            case Step.CabinetBrief:
                if (ContinueModalPressed())
                {
                    step = Step.Loot;
                    TutorialInputGate.Configure(false, true);
                }
                break;

            case Step.Loot:
                TryOpenTutorialCabinet();
                if (TutorialLootCount() >= 5)
                    ShowModal(Step.ConsumeBrief, tutorialText.consumeTitle, tutorialText.consumeBrief);
                break;

            case Step.ConsumeBrief:
                if (ContinueModalPressed())
                {
                    step = Step.Consume;
                    TutorialInputGate.Configure(false, true);
                }
                break;

            case Step.Consume:
                if (survival != null && survival.currentHunger >= survival.maxHunger * 0.50f && survival.currentThirst >= survival.maxThirst * 0.50f)
                    ShowModal(Step.CompleteBrief, tutorialText.completeTitle, tutorialText.completeBrief);
                break;

            case Step.CompleteBrief:
                if (ContinueModalPressed())
                {
                    step = Step.Complete;
                    TutorialInputGate.Clear();
                }
                break;
        }
    }

    private void CachePlayerReferences()
    {
        if (localPlayer == null) localPlayer = PlayerMovement.LocalPlayerInstance;
        if (localPlayer == null) return;
        if (survival == null) survival = localPlayer.GetComponent<PlayerSurvival>();
        if (inventory == null) inventory = localPlayer.GetComponent<InventorySystem>();
        if (roofDetector == null) roofDetector = localPlayer.GetComponentInChildren<RoofDetector>();
    }

    private void ApplyTutorialNeeds()
    {
        if (needsApplied || survival == null) return;
        needsApplied = true;
        survival.SetTutorialNeeds(tutorialNeedRatio, tutorialNeedRatio);
    }

    private void SetInitialTutorialZoom()
    {
        if (initialZoomApplied) return;
        initialZoomApplied = true;
        FindFirstObjectByType<IntroCameraFollow>()?.SetZoomInAmount(initialZoomInAmount);
    }

    private bool HasStayedInsideTargetHouse()
    {
        bool isInsideTargetHouse = roofDetector != null && targetHouseRoof != null &&
                                   roofDetector.CurrentRoof == targetHouseRoof;
        indoorProgress = isInsideTargetHouse ? indoorProgress + Time.unscaledDeltaTime : 0f;
        return indoorProgress >= requiredIndoorSeconds;
    }

    private int TutorialLootCount()
    {
        if (inventory == null) return 0;
        int count = 0;
        if (inventory.HasItemNamed("Meat")) count++;
        if (inventory.HasItemNamed("Water")) count++;
        if (inventory.HasItemNamed("Bandage")) count++;
        if (inventory.HasItemNamed("Ammo12Gauge")) count++;
        if (inventory.HasItemNamed("S12K")) count++;
        return count;
    }

    // The tutorial cabinet is a tiny isometric target.  Its normal click
    // detection remains in LootContainer for the game proper; this assist
    // preserves the same proximity/wall validation but lets a left click
    // while standing at the highlighted cabinet reliably open it.
    private void TryOpenTutorialCabinet()
    {
        if (!Input.GetMouseButtonDown(0) || kitchenCabinet == null) return;
        LootContainer container = kitchenCabinet.GetComponent<LootContainer>();
        if (container == null) container = kitchenCabinet.GetComponentInChildren<LootContainer>();
        container?.TryOpenForLocalPlayer();
    }

    private void ShowModal(Step nextStep, string title, string body)
    {
        step = nextStep;
        modalTitle = title;
        modalBody = body;
        TutorialInputGate.Configure(true, true);
    }

    private bool ContinueModalPressed() => Input.GetMouseButtonDown(0);

    private void OnGUI()
    {
        if (step == Step.WaitingForIntro || step == Step.Complete) return;

        GUI.depth = -100;
        if (IsModalStep()) DrawModal();
        else if (step == Step.NeedsFocus) DrawNeedsFocus();
        else DrawObjective();
    }

    private bool IsModalStep()
    {
        return step == Step.MoveBrief || step == Step.AimBrief || step == Step.NeedsBrief ||
               step == Step.ZoomBrief || step == Step.HouseBrief || step == Step.CabinetBrief || step == Step.ConsumeBrief ||
               step == Step.CompleteBrief;
    }

    private void DrawModal()
    {
        Color old = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.72f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = old;

        float width = Mathf.Min(720f, Screen.width - 80f);
        GUIStyle bodyStyle = BodyStyle();
        float bodyHeight = bodyStyle.CalcHeight(new GUIContent(modalBody), width - 56f);
        float boxHeight = Mathf.Clamp(98f + bodyHeight, 205f, Screen.height - 70f);
        Rect box = new Rect((Screen.width - width) * 0.5f, (Screen.height - boxHeight) * 0.5f, width, boxHeight);
        GUI.Box(box, string.Empty);
        GUI.Label(new Rect(box.x + 28, box.y + 28, box.width - 56, 36), modalTitle, TitleStyle());
        GUI.Label(new Rect(box.x + 28, box.y + 75, box.width - 56, box.height - 92), modalBody, bodyStyle);
    }

    private void DrawNeedsFocus()
    {
        Rect hole = new Rect(Screen.width - 75f, 121f, 68f, 110f);
        DrawDimWithHole(hole);
        DrawSpotlightOutline(hole);

        float messageWidth = Mathf.Min(610f, Screen.width - 160f);
        GUIStyle bodyStyle = BodyStyle();
        float messageBodyHeight = bodyStyle.CalcHeight(new GUIContent(tutorialText.needsFocusBody), messageWidth - 44f);
        float messageHeight = Mathf.Clamp(86f + messageBodyHeight, 165f, Screen.height - 70f);
        Rect message = new Rect(40f, (Screen.height - messageHeight) * 0.5f, messageWidth, messageHeight);
        GUI.Box(message, string.Empty);
        GUI.Label(new Rect(message.x + 22, message.y + 18, message.width - 44, 38), tutorialText.needsFocusTitle, TitleStyle());
        GUI.Label(new Rect(message.x + 22, message.y + 65, message.width - 44, message.height - 78),
            tutorialText.needsFocusBody, bodyStyle);
    }

    private void DrawDimWithHole(Rect hole)
    {
        Color old = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.70f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, hole.y), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(0, hole.yMax, Screen.width, Screen.height - hole.yMax), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(0, hole.y, hole.x, hole.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(hole.xMax, hole.y, Screen.width - hole.xMax, hole.height), Texture2D.whiteTexture);
        GUI.color = old;
    }

    private void DrawSpotlightOutline(Rect hole)
    {
        float pulse = 1f + Mathf.PingPong(Time.unscaledTime * 2f, 1f);
        Color old = GUI.color;
        GUI.color = new Color(1f, 0.84f, 0.12f, 0.95f);
        DrawOutline(new Rect(hole.x - 4f - pulse, hole.y - 4f - pulse, hole.width + 8f + pulse * 2f, hole.height + 8f + pulse * 2f), 2f);
        GUI.Label(new Rect(hole.center.x - 28f, hole.yMax + 6f, 56f, 42f), "↑", PointerStyle());
        GUI.color = old;
    }

    private static void DrawOutline(Rect rect, float thickness)
    {
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), Texture2D.whiteTexture);
    }

    private void DrawObjective()
    {
        string text;
        switch (step)
        {
            case Step.Move: text = tutorialText.moveObjective; break;
            case Step.Zoom: text = tutorialText.zoomObjective; break;
            case Step.Aim: text = tutorialText.aimObjective; break;
            case Step.GoToKitchen: text = tutorialText.houseObjective; break;
            case Step.Loot: text = tutorialText.lootObjective; break;
            default: text = tutorialText.consumeObjective; break;
        }

        Rect box = new Rect((Screen.width - 560f) * 0.5f, 30f, 560f, 55f);
        GUI.Box(box, string.Empty);
        GUI.Label(new Rect(box.x + 15, box.y + 11, box.width - 30, 30), text, ObjectiveStyle());

        if (step == Step.GoToKitchen || step == Step.Loot)
            DrawWorldMarker();
    }

    private void DrawWorldMarker()
    {
        if (kitchenCabinet == null || Camera.main == null) return;
        Vector3 point = Camera.main.WorldToScreenPoint(kitchenCabinet.position + Vector3.up * 0.8f);
        if (point.z < 0) return;
        float x = point.x;
        float y = Screen.height - point.y;
        float pulse = 24f + Mathf.PingPong(Time.unscaledTime * 30f, 13f);
        Color old = GUI.color;
        GUI.color = new Color(1f, 0.82f, 0.1f, 0.95f);
        GUI.Box(new Rect(x - pulse, y - pulse, pulse * 2f, pulse * 2f), string.Empty);
        GUI.color = old;
        GUI.Label(new Rect(x - 95, y - pulse - 30, 190, 28), step == Step.Loot ? tutorialText.cabinetMarker : tutorialText.houseMarker, MarkerStyle());
    }

    private GUIStyle TitleStyle() => new GUIStyle(GUI.skin.label)
    {
        fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
        normal = { textColor = new Color(1f, 0.83f, 0.22f) }
    };

    private GUIStyle BodyStyle() => new GUIStyle(GUI.skin.label)
    {
        fontSize = 19, alignment = TextAnchor.UpperCenter, wordWrap = true,
        normal = { textColor = Color.white }
    };

    private GUIStyle ObjectiveStyle() => new GUIStyle(GUI.skin.label)
    {
        fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
        normal = { textColor = Color.white }
    };

    private GUIStyle MarkerStyle() => new GUIStyle(GUI.skin.label)
    {
        fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
        normal = { textColor = new Color(1f, 0.88f, 0.2f) }
    };

    private GUIStyle PointerStyle() => new GUIStyle(GUI.skin.label)
    {
        fontSize = 38, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
        normal = { textColor = new Color(1f, 0.86f, 0.15f) }
    };
}
