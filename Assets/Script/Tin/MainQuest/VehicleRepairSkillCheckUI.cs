using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Runtime repair dialog and DBD-style skill-check presentation.</summary>
public sealed class VehicleRepairSkillCheckUI : MonoBehaviour
{
    private enum PanelState { Closed, WaitingForServer, Minigame }

    private static VehicleRepairSkillCheckUI instance;
    private PanelState state;
    private MilitaryBaseQuestManager manager;
    private RoadsideVehicleRepairStation station;
    private bool pendingCancel;
    private int submittedSequence = -1;
    private int ringSequence = -1;
    private Texture2D ringTexture;
    private string statusMessage = string.Empty;
    private float statusUntil;
    private readonly Dictionary<Canvas, bool> suppressedCanvases = new Dictionary<Canvas, bool>();
    private bool localPresentationActive;
    private float nextCanvasSuppressionAt;

    private const float CanvasSuppressionInterval = 0.2f;

    public static bool BlocksGameplayInput => instance != null && instance.state != PanelState.Closed;
    public static bool IsLocalRepairSessionActive =>
        instance != null && instance.state == PanelState.Minigame;

    public static void EnsureExists()
    {
        if (instance != null) return;
        GameObject root = new GameObject("Vehicle Repair Skill Check UI");
        instance = root.AddComponent<VehicleRepairSkillCheckUI>();
    }

    public static void PrepareFromInspection(MilitaryBaseQuestManager targetManager,
        RoadsideVehicleRepairStation targetStation)
    {
        EnsureExists();
        instance.manager = targetManager;
        instance.station = targetStation;
        instance.pendingCancel = false;
        instance.state = PanelState.WaitingForServer;
    }

    public static void NotifyStartResponse(bool accepted, string message)
    {
        if (instance == null) return;
        instance.pendingCancel = false;
        if (accepted)
        {
            instance.station?.CloseInspectionForMinigame();
            instance.state = PanelState.Minigame;
            instance.BeginLocalPresentation();
            instance.statusMessage = GameLocalization.Get("quest.skill_check.start");
        }
        else
        {
            instance.EndLocalPresentation();
            instance.state = PanelState.Closed;
            instance.station?.NotifyRepairRequestFailed(message);
            instance.statusMessage = message;
        }
        instance.statusUntil = Time.unscaledTime + 2.5f;
    }

    public static void NotifyOutcome(VehicleRepairSkillCheckResult result)
    {
        if (instance == null) return;
        instance.statusMessage = result switch
        {
            VehicleRepairSkillCheckResult.Perfect => GameLocalization.Get("quest.skill_check.perfect"),
            VehicleRepairSkillCheckResult.Success => GameLocalization.Get("quest.skill_check.success"),
            _ => GameLocalization.Get("quest.skill_check.miss")
        };
        instance.statusUntil = Time.unscaledTime + 1.35f;
    }

    public static void NotifyInterrupted(string message)
    {
        if (instance == null) return;
        instance.statusMessage = message;
        instance.statusUntil = Time.unscaledTime + 2.5f;
        instance.EndLocalPresentation();
        instance.state = PanelState.Closed;
        instance.pendingCancel = false;
    }

    public static void NotifyCancelled()
    {
        if (instance == null) return;
        instance.pendingCancel = false;
        instance.EndLocalPresentation();
        instance.state = PanelState.Closed;
    }

    public static void NotifyCompleted(PoliceCarRepairAction action, bool allComplete)
    {
        if (instance == null) return;
        instance.statusMessage = allComplete ? GameLocalization.Get("quest.skill_check.all_done") : GameLocalization.Get("quest.skill_check.item_done");
        instance.statusUntil = Time.unscaledTime + 3f;
        instance.EndLocalPresentation();
        instance.state = PanelState.Closed;
        instance.pendingCancel = false;
        instance.station?.ReopenInspection(instance.statusMessage);
    }

    private void Awake()
    {
        if (instance == null) instance = this;
        else if (instance != this) Destroy(gameObject);
    }

    private void OnDestroy()
    {
        EndLocalPresentation();
        if (instance == this) instance = null;
        if (ringTexture != null) Destroy(ringTexture);
    }

    private void Update()
    {
        if (state == PanelState.Closed) return;
        if (manager == null || !manager.IsNetworkReady)
        {
            EndLocalPresentation();
            state = PanelState.Closed;
            return;
        }

        if (localPresentationActive && Time.unscaledTime >= nextCanvasSuppressionAt)
        {
            nextCanvasSuppressionAt = Time.unscaledTime + CanvasSuppressionInterval;
            SuppressForeignCanvases();
        }

        bool localOwnsSession = manager.IsLocalPlayerRepairer;

        if (state == PanelState.Minigame && !pendingCancel && !localOwnsSession)
        {
            EndLocalPresentation();
            state = PanelState.Closed;
            return;
        }

        if (state == PanelState.WaitingForServer && Input.GetKeyDown(KeyCode.Escape))
        {
            EndLocalPresentation();
            state = PanelState.Closed;
            return;
        }

        if (state != PanelState.Minigame) return;
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            pendingCancel = true;
            manager.RequestCancelRepairSkillCheck();
            EndLocalPresentation();
            state = PanelState.Closed;
            station?.ReopenInspection(GameLocalization.Get("quest.skill_check.stopped_saved"));
            return;
        }

        if (!manager.RepairSkillCheckEventActive || submittedSequence == manager.RepairSkillCheckSequence ||
            !Input.GetKeyDown(KeyCode.Space)) return;

        submittedSequence = manager.RepairSkillCheckSequence;
        manager.RequestResolveRepairSkillCheck(submittedSequence, CurrentNeedleAngle());
    }

    private void BeginLocalPresentation()
    {
        if (localPresentationActive) return;
        localPresentationActive = true;
        QuestUIDialogueState.SetRepairActive(true);
        nextCanvasSuppressionAt = 0f;
        SuppressForeignCanvases();
    }

    private void SuppressForeignCanvases()
    {
        Canvas[] canvases = Resources.FindObjectsOfTypeAll<Canvas>();
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas candidate = canvases[i];
            if (candidate == null || !candidate.gameObject.scene.IsValid()) continue;
            // World fog is part of the scene presentation, not gameplay HUD.
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
        QuestUIDialogueState.SetRepairActive(false);
        foreach (KeyValuePair<Canvas, bool> entry in suppressedCanvases)
            if (entry.Key != null) entry.Key.enabled = entry.Value;
        suppressedCanvases.Clear();
    }

    private void OnGUI()
    {
        if (state == PanelState.Closed)
        {
            DrawTransientStatus();
            return;
        }

        GUI.depth = -500;
        if (state == PanelState.Minigame)
        {
            DrawScreenDim();
            DrawMinigame();
        }
        DrawTransientStatus();
    }

    private void DrawMinigame()
    {
        if (manager == null) return;

        float uiScale = Mathf.Clamp(Screen.height / 720f, 0.45f, 1f);
        float barHeight = Mathf.Clamp(34f * uiScale, 16f, 34f);
        float footerHeight = Mathf.Max(10f, 22f * uiScale);
        float barY = Screen.height - barHeight - footerHeight - 10f;
        float promptHeight = Mathf.Max(14f, 30f * uiScale);
        float size = Mathf.Min(276f * uiScale, Screen.height * 0.43f, Screen.width * 0.55f);
        float ringCenterY = Mathf.Min(Screen.height * 0.42f, barY - promptHeight - size * 0.5f - 8f);
        Rect ring = new Rect((Screen.width - size) * 0.5f, ringCenterY - size * 0.5f, size, size);
        if (manager.RepairSkillCheckEventActive)
        {
            EnsureRingTexture();
            if (ringTexture != null) GUI.DrawTexture(ring, ringTexture, ScaleMode.StretchToFill, true);
            DrawNeedle(ring, CurrentNeedleAngle());
            GUI.Label(new Rect(ring.x, ring.y + ring.height + 4f, ring.width, promptHeight),
                GameLocalization.Get("quest.skill_check.space_hint"), CenteredStyle(Mathf.Max(8, Mathf.RoundToInt(16f * uiScale)),
                    FontStyle.Bold));
        }
        else
        {
            GUI.Label(ring, manager.RepairPenaltyRemaining > 0f ? GameLocalization.Get("quest.skill_check.recovering") : GameLocalization.Get("quest.skill_check.preparing"),
                CenteredStyle(Mathf.Max(9, Mathf.RoundToInt(20f * uiScale)), FontStyle.Bold));
        }

        float barWidth = Mathf.Min(560f, Screen.width - 20f);
        DrawProgressBar(new Rect((Screen.width - barWidth) * 0.5f, barY, barWidth, barHeight),
            manager.RepairSkillCheckProgress, Mathf.Max(8, Mathf.RoundToInt(14f * uiScale)));
        GUI.Label(new Rect(10f, barY + barHeight + 2f, Screen.width - 20f, footerHeight),
            GameLocalization.Get("quest.skill_check.esc_hint"),
            CenteredStyle(Mathf.Max(8, Mathf.RoundToInt(12f * uiScale)), FontStyle.Bold));
    }

    private void EnsureRingTexture()
    {
        if (manager == null || !manager.RepairSkillCheckEventActive) return;
        if (ringTexture != null && ringSequence == manager.RepairSkillCheckSequence) return;
        if (ringTexture != null) Destroy(ringTexture);

        const int size = 256;
        ringTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "REPAIR_SKILL_CHECK_RING",
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave
        };
        Color32[] pixels = new Color32[size * size];
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            Vector2 delta = new Vector2(x, y) - center;
            float radius = delta.magnitude / (size * 0.5f);
            if (radius < 0.70f || radius > 0.96f)
            {
                pixels[y * size + x] = new Color32(0, 0, 0, 0);
                continue;
            }

            float angle = Mathf.Repeat(Mathf.Atan2(delta.x, delta.y) * Mathf.Rad2Deg, 360f);
            float distance = Mathf.Abs(Mathf.DeltaAngle(angle, manager.RepairSkillCheckTargetAngle));
            if (distance <= manager.RepairSkillCheckPerfectArcDegrees * 0.5f)
                pixels[y * size + x] = new Color32(255, 205, 75, 255);
            else if (distance <= manager.RepairSkillCheckSuccessArcDegrees * 0.5f)
                pixels[y * size + x] = new Color32(70, 220, 125, 255);
            else
                pixels[y * size + x] = new Color32(42, 49, 55, 235);
        }
        ringTexture.SetPixels32(pixels);
        ringTexture.Apply(false, true);
        ringSequence = manager.RepairSkillCheckSequence;
    }

    private float CurrentNeedleAngle()
    {
        if (manager == null || manager.RepairSkillCheckRotationSeconds <= 0f) return 0f;
        return Mathf.Repeat(manager.RepairSkillCheckElapsed / manager.RepairSkillCheckRotationSeconds * 360f, 360f);
    }

    private static void DrawNeedle(Rect ring, float angle)
    {
        Vector2 pivot = ring.center;
        Matrix4x4 previous = GUI.matrix;
        GUIUtility.RotateAroundPivot(angle, pivot);
        Color old = GUI.color;
        GUI.color = new Color(0.95f, 0.2f, 0.18f);
        GUI.DrawTexture(new Rect(pivot.x - 3f, ring.y + 22f, 6f, ring.height * 0.46f), Texture2D.whiteTexture);
        GUI.color = old;
        GUI.matrix = previous;
    }

    private static void DrawProgressBar(Rect rect, float progress, int fontSize = 14)
    {
        GUI.Box(rect, GUIContent.none);
        Color old = GUI.color;
        GUI.color = new Color(0.18f, 0.82f, 0.45f);
        GUI.DrawTexture(new Rect(rect.x + 4f, rect.y + 4f,
            (rect.width - 8f) * Mathf.Clamp01(progress / 100f), rect.height - 8f), Texture2D.whiteTexture);
        GUI.color = old;
        GUI.Label(rect, string.Format(GameLocalization.Get("quest.skill_check.progress_bar"), progress), CenteredStyle(fontSize, FontStyle.Bold));
    }

    private static void DrawScreenDim()
    {
        Color old = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.48f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = old;
    }

    private void DrawTransientStatus()
    {
        if (string.IsNullOrEmpty(statusMessage) || Time.unscaledTime >= statusUntil) return;
        float width = Mathf.Min(520f, Screen.width - 12f);
        float height = Mathf.Min(46f, Mathf.Max(24f, Screen.height * 0.24f));
        GUIStyle style = CenteredStyle(Mathf.Max(9, Mathf.RoundToInt(18f * Mathf.Clamp(Screen.height / 720f,
            0.5f, 1f))), FontStyle.Bold);
        GUI.Box(new Rect((Screen.width - width) * 0.5f, Mathf.Min(54f, Screen.height * 0.05f), width, height),
            statusMessage, style);
    }

    private static GUIStyle CenteredStyle(int fontSize, FontStyle fontStyle) => new GUIStyle(GUI.skin.label)
    {
        alignment = TextAnchor.MiddleCenter,
        fontSize = fontSize,
        fontStyle = fontStyle,
        wordWrap = true,
        normal = { textColor = Color.white }
    };
}
