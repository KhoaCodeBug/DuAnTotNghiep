using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RoadsideVehicleRepairStation : MonoBehaviour
{
    [SerializeField, Min(0.2f)] private float inspectionDuration = 1.6f;
    [SerializeField, Min(0.01f)] private float zoneLineWidth = 0.06f;

    private MilitaryBaseQuestManager manager;
    private VehicleControllerFusion vehicle;
    private PolygonCollider2D inspectionPolygon;
    private LineRenderer frontZoneLine;
    private ArrivalCarInspectionUI inspectionUI;
    private Coroutine inspectionRoutine;
    private float nextInspectionAllowedAt;

    public Vector2 InteractionPosition => inspectionPolygon != null
        ? inspectionPolygon.bounds.center
        : (Vector2)transform.position;

    public void Configure(MilitaryBaseQuestManager targetManager, VehicleControllerFusion targetVehicle)
    {
        manager = targetManager;
        vehicle = targetVehicle;
        // Only State Authority may mutate the replicated vehicle lock. Other
        // peers receive it from VehicleControllerFusion; they still build the
        // local inspection presentation below.
        if (vehicle != null && vehicle.HasStateAuthority)
            vehicle.SetRepairEntryLocked(true);
        BuildInspectionPolygon();
        if (inspectionUI == null)
        {
            inspectionUI = GetComponent<ArrivalCarInspectionUI>();
            if (inspectionUI == null) inspectionUI = gameObject.AddComponent<ArrivalCarInspectionUI>();
        }
        BuildFrontZonePresentation();
        VehicleRepairSkillCheckUI.EnsureExists();
    }

    private void Update()
    {
        bool storyInteraction = manager != null && manager.ShouldOfferStoryCarInteraction;
        bool repairInteraction = manager != null && manager.CanUsePoliceRepairMinigame;
        if (!storyInteraction && !repairInteraction)
        {
            SetFrontZoneVisible(false);
            CancelInspection();
            return;
        }

        PlayerMovement player = PlayerMovement.LocalPlayerInstance;
        bool inZone = player != null && IsPlayerInRepairPosition(player.transform.position);
        bool blocked = LocalGameplayUIState.BlocksWorldInteractionHints;
        SetFrontZoneVisible(inZone && !blocked && (inspectionUI == null || !inspectionUI.IsOpen));
        if (!inZone || blocked)
        {
            CancelInspection();
            return;
        }

        if (inspectionRoutine == null && Time.unscaledTime >= nextInspectionAllowedAt &&
            Input.GetKeyDown(KeyCode.E))
            inspectionRoutine = StartCoroutine(InspectionRoutine(player));
    }

    private IEnumerator InspectionRoutine(PlayerMovement player)
    {
        if (AutoUIManager.Instance != null) AutoUIManager.Instance.isDoingAction = true;
        float elapsed = 0f;
        while (elapsed < inspectionDuration)
        {
            if (player == null || !IsPlayerInRepairPosition(player.transform.position) || !Input.GetKey(KeyCode.E))
            {
                EndInspectionPresentation();
                inspectionRoutine = null;
                yield break;
            }
            elapsed = Mathf.Min(inspectionDuration, elapsed + Time.unscaledDeltaTime);
            AutoUIManager.Instance?.ShowReloadUI(elapsed, inspectionDuration, "ĐANG KIỂM TRA XE CẢNH SÁT...");
            yield return null;
        }

        EndInspectionPresentation();
        inspectionRoutine = null;
        if (manager != null && manager.ShouldOfferStoryCarInteraction)
            manager.RequestInspectPoliceCarStory();
        else if (manager != null && manager.CanUsePoliceRepairMinigame)
            inspectionUI?.Open(this);
    }

    public bool IsPlayerInRepairPosition(Vector3 playerPosition) =>
        inspectionPolygon != null && inspectionPolygon.enabled && inspectionPolygon.OverlapPoint(playerPosition);

    public void NotifyInspectionUIClosed() => nextInspectionAllowedAt = Time.unscaledTime + 0.25f;

    public void CloseInspectionForMinigame()
    {
        if (inspectionUI != null && inspectionUI.IsOpen) inspectionUI.CloseForPoliceMinigame();
        SetFrontZoneVisible(false);
    }

    public void NotifyRepairRequestFailed(string message) => inspectionUI?.NotifyPoliceRepairRequestFailed(message);

    public void ReopenInspection(string message) => inspectionUI?.ReopenPoliceInspection(this, message);

    public void NotifyTimedRepairStart(PoliceCarRepairAction action, bool accepted, float duration, string message) =>
        inspectionUI?.NotifyPoliceTimedRepairStart(action, accepted, duration, message);

    public void NotifyTimedRepairInterrupted(string message) =>
        inspectionUI?.NotifyPoliceTimedRepairInterrupted(message);

    public void NotifyTimedRepairCompleted(bool allComplete) =>
        inspectionUI?.NotifyPoliceTimedRepairCompleted(allComplete);

    public void PlayTimedRepairAudio(PoliceCarRepairAction action, float duration) =>
        inspectionUI?.PlayRepairAudioForNetwork(PoliceCarRepairRules.ToArrivalCarRepairAction(action), duration);

    public void StopTimedRepairAudio() => inspectionUI?.StopRepairAudioForNetwork();

    private void BuildInspectionPolygon()
    {
        if (inspectionPolygon == null)
        {
            GameObject authoredZone = GameObject.Find("VungKiemTraXeCanhSat");
            if (authoredZone != null) inspectionPolygon = authoredZone.GetComponent<PolygonCollider2D>();
        }

        if (inspectionPolygon != null)
        {
            inspectionPolygon.isTrigger = true;
            inspectionPolygon.enabled = true;
            return;
        }

        GameObject zone = new GameObject("VungKiemTraXeCanhSat [AUTO]");
        zone.transform.SetParent(transform, false);
        inspectionPolygon = zone.AddComponent<PolygonCollider2D>();

        Vector2 forward = vehicle != null ? vehicle.VisionDirection : Vector2.up;
        if (forward.sqrMagnitude < 0.01f) forward = Vector2.up;
        forward.Normalize();
        Vector2 right = new Vector2(forward.y, -forward.x);
        Vector2 origin = vehicle != null ? vehicle.transform.position : transform.position;
        Vector2[] worldPoints =
        {
            origin + forward * 0.75f - right * 1.0f,
            origin + forward * 0.75f + right * 1.0f,
            origin + forward * 2.65f + right * 1.35f,
            origin + forward * 2.65f - right * 1.35f
        };
        Vector2[] localPoints = new Vector2[worldPoints.Length];
        for (int i = 0; i < worldPoints.Length; i++)
            localPoints[i] = inspectionPolygon.transform.InverseTransformPoint(worldPoints[i]);
        inspectionPolygon.pathCount = 1;
        inspectionPolygon.SetPath(0, localPoints);
        inspectionPolygon.isTrigger = true;
        inspectionPolygon.enabled = true;
    }

    private void BuildFrontZonePresentation()
    {
        if (frontZoneLine != null) return;
        GameObject lineObject = new GameObject("Police Car Front Inspection Zone");
        lineObject.transform.SetParent(transform, false);
        frontZoneLine = lineObject.AddComponent<LineRenderer>();
        frontZoneLine.useWorldSpace = true;
        frontZoneLine.loop = true;
        frontZoneLine.startWidth = zoneLineWidth;
        frontZoneLine.endWidth = zoneLineWidth;
        frontZoneLine.sortingOrder = 40;
        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null) frontZoneLine.material = new Material(shader);
        Color green = new Color(0.22f, 1f, 0.36f, 0.92f);
        frontZoneLine.startColor = green;
        frontZoneLine.endColor = green;
        SetFrontZoneVisible(false);
    }

    private void SetFrontZoneVisible(bool visible)
    {
        if (frontZoneLine == null || inspectionPolygon == null) return;
        if (visible)
        {
            Vector2[] points = inspectionPolygon.GetPath(0);
            frontZoneLine.positionCount = points.Length;
            for (int i = 0; i < points.Length; i++)
                frontZoneLine.SetPosition(i, inspectionPolygon.transform.TransformPoint(points[i] + inspectionPolygon.offset));
        }
        frontZoneLine.enabled = visible;
    }

    private void CancelInspection()
    {
        if (inspectionRoutine == null) return;
        StopCoroutine(inspectionRoutine);
        inspectionRoutine = null;
        EndInspectionPresentation();
    }

    private static void EndInspectionPresentation()
    {
        if (AutoUIManager.Instance == null) return;
        AutoUIManager.Instance.HideReloadUI();
        AutoUIManager.Instance.isDoingAction = false;
    }

    private void OnGUI()
    {
        if (manager == null || !manager.IsNetworkReady || inspectionRoutine != null ||
            inspectionUI == null || inspectionUI.IsOpen || LocalGameplayUIState.BlocksWorldInteractionHints) return;
        bool storyInteraction = manager.ShouldOfferStoryCarInteraction;
        bool repairInteraction = manager.CanUsePoliceRepairMinigame;
        if (!storyInteraction && !repairInteraction) return;
        PlayerMovement player = PlayerMovement.LocalPlayerInstance;
        Camera camera = Camera.main;
        if (player == null || camera == null || !IsPlayerInRepairPosition(player.transform.position)) return;
        Vector3 point = camera.WorldToScreenPoint(InteractionPosition);
        if (point.z <= 0f) return;
        GUIStyle prompt = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 15,
            fontStyle = FontStyle.Bold
        };
        prompt.normal.textColor = new Color(0.52f, 1f, 0.58f);
        float x = Mathf.Clamp(point.x - 150f, 8f, Screen.width - 308f);
        float y = Mathf.Clamp(Screen.height - point.y - 66f, 8f, Screen.height - 54f);
        string label = storyInteraction
            ? "KIỂM TRA XE CẢNH SÁT\nGIỮ [E]"
            : "KIỂM TRA / SỬA XE\nGIỮ [E]";
        GUI.Box(new Rect(x, y, 300f, 46f), label, prompt);
    }
}
