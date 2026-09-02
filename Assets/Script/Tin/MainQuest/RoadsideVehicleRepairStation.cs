using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RoadsideVehicleRepairStation : MonoBehaviour
{
    [Header("Inspection Zone")]
    [Tooltip("Optional authorable polygon for the military police car. Assign one here, or attach VehicleInspectionZoneAuthoring on a child object.")]
    [SerializeField] private PolygonCollider2D inspectionPolygon;
    [SerializeField, Min(0.2f)] private float inspectionDuration = 1.6f;
    [SerializeField, Min(0.01f)] private float zoneLineWidth = 0.06f;

    private MilitaryBaseQuestManager manager;
    private VehicleControllerFusion vehicle;
    private LineRenderer frontZoneLine;
    private ArrivalCarInspectionUI inspectionUI;
    private Coroutine inspectionRoutine;
    private float nextInspectionAllowedAt;

    public PolygonCollider2D InspectionPolygon => inspectionPolygon;

    public Vector2 InteractionPosition => inspectionPolygon != null
        ? (Vector2)inspectionPolygon.bounds.center
        : (Vector2)transform.position;

    private bool IsValidLocalPolygon(PolygonCollider2D candidate)
    {
        return candidate != null && (candidate.transform == transform || candidate.transform.IsChildOf(transform));
    }

    private void Awake()
    {
        ResolveInspectionPolygon(allowCreateAutoFallback: false);
    }

    private void OnValidate()
    {
        ResolveInspectionPolygon(allowCreateAutoFallback: false);
    }

    public void Configure(MilitaryBaseQuestManager targetManager, VehicleControllerFusion targetVehicle)
    {
        manager = targetManager;
        vehicle = targetVehicle;
        // Only State Authority may mutate the replicated vehicle lock. Other
        // peers receive it from VehicleControllerFusion; they still build the
        // local inspection presentation below.
        if (vehicle != null && vehicle.HasStateAuthority)
            vehicle.SetRepairEntryLocked(true);
        ResolveInspectionPolygon(allowCreateAutoFallback: true);
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
            AutoUIManager.Instance?.ShowReloadUI(elapsed, inspectionDuration, GameLocalization.Get("quest.police.inspecting"));
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

    public void ResolveInspectionPolygon(bool allowCreateAutoFallback = true)
    {
        // 1. Kiểm tra serialized reference có thuộc hierarchy của xe không
        if (inspectionPolygon != null)
        {
            if (IsValidLocalPolygon(inspectionPolygon))
            {
                inspectionPolygon.isTrigger = true;
                inspectionPolygon.enabled = true;
                return;
            }
            // Reference trỏ ra ngoài hierarchy -> Bỏ qua và resolve local
            inspectionPolygon = null;
        }

        // 2. Kiểm tra child có gắn VehicleInspectionZoneAuthoring
        var childAuthoring = GetComponentInChildren<VehicleInspectionZoneAuthoring>(true);
        if (childAuthoring != null && childAuthoring.TryGetComponent<PolygonCollider2D>(out var authoredPolygon) && IsValidLocalPolygon(authoredPolygon))
        {
            inspectionPolygon = authoredPolygon;
        }

        // 3. Kiểm tra child PolygonCollider2D theo tên chuẩn
        if (inspectionPolygon == null)
        {
            PolygonCollider2D[] childPolygons = GetComponentsInChildren<PolygonCollider2D>(true);
            for (int i = 0; i < childPolygons.Length; i++)
            {
                if (IsInspectionZoneName(childPolygons[i].gameObject.name) && IsValidLocalPolygon(childPolygons[i]))
                {
                    inspectionPolygon = childPolygons[i];
                    break;
                }
            }
        }

        // 4. Fallback runtime tự động tạo (chỉ khi allowCreateAutoFallback = true)
        if (inspectionPolygon == null && allowCreateAutoFallback)
        {
            Transform existingAuto = transform.Find("VungKiemTraXeCanhSat [AUTO]");
            if (existingAuto != null && existingAuto.TryGetComponent<PolygonCollider2D>(out var existingPoly))
            {
                inspectionPolygon = existingPoly;
            }
            else
            {
                GameObject zone = new GameObject("VungKiemTraXeCanhSat [AUTO]");
                zone.transform.SetParent(transform, false);
                inspectionPolygon = zone.AddComponent<PolygonCollider2D>();

                Vector2 forward = vehicle != null ? vehicle.VisionDirection : Vector2.up;
                if (forward.sqrMagnitude < 0.01f) forward = Vector2.up;
                forward.Normalize();
                Vector2 right = new Vector2(forward.y, -forward.x);
                Vector2 origin = vehicle != null ? (Vector2)vehicle.transform.position : (Vector2)transform.position;
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
            }
        }

        if (inspectionPolygon != null)
        {
            inspectionPolygon.isTrigger = true;
            inspectionPolygon.enabled = true;
        }
    }

    private void BuildInspectionPolygon()
    {
        ResolveInspectionPolygon(allowCreateAutoFallback: true);
    }

    private static bool IsInspectionZoneName(string name)
    {
        return name == "VungKiemTraXeCanhSat" || name == "VungKiemTraXe" ||
               name == "VungKiemTraXeCanhSat [AUTO]" || name == "ViTriKiemTraXe" ||
               name == "Vehicle Inspection Zone";
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
            ? GameLocalization.Get("quest.police.prompt_inspect")
            : GameLocalization.Get("quest.police.prompt_repair");
        GUI.Box(new Rect(x, y, 300f, 46f), label, prompt);
    }
}
