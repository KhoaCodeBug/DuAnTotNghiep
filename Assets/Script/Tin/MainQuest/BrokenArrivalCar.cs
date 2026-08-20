using System.Collections;
using UnityEngine;

/// <summary>
/// The same car shown breaking down in Intro_Cinematic, now left at Main's
/// arrival point. Inspecting it is the causal hand-off into the neighborhood
/// investigation; this is not the military vehicle-parts repair mechanic.
/// </summary>
[DisallowMultipleComponent]
public sealed class BrokenArrivalCar : MonoBehaviour
{
    private static readonly Vector2[] DefaultInspectionPolygon =
    {
        new Vector2(-7.2f, 0.3f),
        new Vector2(-7.2f, 3.1f),
        new Vector2(-5.1f, 4.4f),
        new Vector2(-2.7f, 2.4f),
        new Vector2(-3.6f, 0.2f)
    };

    public static BrokenArrivalCar Instance { get; private set; }

    [Header("Front inspection zone")]
    [Tooltip("Optional authorable polygon. Assign one here, or create a child named VungKiemTraXe.")]
    [SerializeField] private PolygonCollider2D inspectionPolygon;
    [SerializeField, Min(0.2f)] private float inspectionDuration = 1.6f;
    [SerializeField, Min(0.01f)] private float zoneLineWidth = 0.06f;

    private Coroutine inspectionRoutine;
    private ArrivalCarInspectionUI inspectionUI;
    private LineRenderer frontZoneLine;
    private SpriteRenderer carRenderer;
    private BoxCollider2D bodyCollider;
    private float nextInspectionAllowedAt;
    private bool driveVehicleActivated;

    public Vector3 InspectionZoneWorldCenter => inspectionPolygon != null
        ? inspectionPolygon.bounds.center
        : transform.position;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        bodyCollider = GetComponent<BoxCollider2D>();
        if (bodyCollider == null) bodyCollider = gameObject.AddComponent<BoxCollider2D>();
        bodyCollider.size = new Vector2(9.2f, 4.5f);
        bodyCollider.offset = new Vector2(0f, -0.35f);

        ResolveInspectionPolygon();
        carRenderer = GetComponent<SpriteRenderer>();
        inspectionUI = GetComponent<ArrivalCarInspectionUI>();
        if (inspectionUI == null) inspectionUI = gameObject.AddComponent<ArrivalCarInspectionUI>();
        BuildFrontZonePresentation();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        MainQuestManager manager = MainQuestManager.Instance;
        ApplyRepairedVehiclePresentation(manager);
        if (driveVehicleActivated) return;

        PlayerMovement player = PlayerMovement.LocalPlayerInstance;
        if (manager == null || !manager.IsNetworkReady || player == null)
        {
            CancelInspection();
            SetFrontZoneVisible(false);
            return;
        }

        bool canInspect = CanInspect(player.transform.position);
        bool blockedByUI = (AutoUIManager.Instance != null && AutoUIManager.Instance.IsAnyMenuOpen()) ||
                           (QuestFlowUIPrototype.Instance != null && QuestFlowUIPrototype.Instance.IsQuestOverlayOpen);
        SetFrontZoneVisible(canInspect && !blockedByUI && (inspectionUI == null || !inspectionUI.IsOpen));

        if (!canInspect || blockedByUI)
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
            MainQuestManager manager = MainQuestManager.Instance;
            if (player == null || manager == null ||
                !CanInspect(player.transform.position) || !Input.GetKey(KeyCode.E))
            {
                EndInspectionPresentation();
                inspectionRoutine = null;
                yield break;
            }

            elapsed = Mathf.Min(inspectionDuration, elapsed + Time.unscaledDeltaTime);
            AutoUIManager.Instance?.ShowReloadUI(elapsed, inspectionDuration, "ĐANG KIỂM TRA ĐỘNG CƠ...");
            yield return null;
        }

        EndInspectionPresentation();
        inspectionRoutine = null;
        inspectionUI?.Open(this);
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

    public bool CanInspect(Vector3 playerPosition)
    {
        return inspectionPolygon != null && inspectionPolygon.enabled &&
               inspectionPolygon.OverlapPoint(playerPosition);
    }

    public void NotifyInspectionUIClosed()
    {
        nextInspectionAllowedAt = Time.unscaledTime + 0.25f;
        MainQuestManager manager = MainQuestManager.Instance;
        if (manager != null && manager.IsNetworkReady && !manager.IsArrivalCarInspected)
            manager.RequestInspectArrivalCar();
    }

    private void ApplyRepairedVehiclePresentation(MainQuestManager manager)
    {
        bool shouldActivate = manager != null && manager.IsNetworkReady &&
                              manager.RepairedArrivalCarObject != null;
        if (driveVehicleActivated == shouldActivate) return;

        driveVehicleActivated = shouldActivate;
        if (!shouldActivate) return;
        CancelInspection();
        inspectionUI?.Close();
        SetFrontZoneVisible(false);
        if (inspectionPolygon != null) inspectionPolygon.enabled = false;
        if (bodyCollider != null) bodyCollider.enabled = false;
        if (carRenderer != null) carRenderer.enabled = false;
    }

    private void BuildFrontZonePresentation()
    {
        GameObject lineObject = new GameObject("Front Inspection Zone");
        lineObject.transform.SetParent(transform, false);
        frontZoneLine = lineObject.AddComponent<LineRenderer>();
        frontZoneLine.useWorldSpace = true;
        frontZoneLine.loop = true;
        frontZoneLine.positionCount = 0;
        frontZoneLine.startWidth = zoneLineWidth;
        frontZoneLine.endWidth = zoneLineWidth;
        frontZoneLine.numCornerVertices = 2;
        frontZoneLine.sortingOrder = 40;
        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null) frontZoneLine.material = new Material(shader);
        Color green = new Color(0.22f, 1f, 0.36f, 0.92f);
        frontZoneLine.startColor = green;
        frontZoneLine.endColor = green;
        UpdateFrontZoneLinePositions();
        frontZoneLine.enabled = false;
    }

    private void UpdateFrontZoneLinePositions()
    {
        if (frontZoneLine == null || inspectionPolygon == null || inspectionPolygon.pathCount == 0) return;

        Vector2[] points = inspectionPolygon.GetPath(0);
        frontZoneLine.positionCount = points.Length;
        for (int i = 0; i < points.Length; i++)
        {
            Vector2 localPoint = points[i] + inspectionPolygon.offset;
            frontZoneLine.SetPosition(i, inspectionPolygon.transform.TransformPoint(localPoint));
        }
    }

    private void SetFrontZoneVisible(bool visible)
    {
        if (frontZoneLine == null) return;
        if (visible) UpdateFrontZoneLinePositions();
        frontZoneLine.enabled = visible;
    }

    private void OnGUI()
    {
        if (driveVehicleActivated) return;
        MainQuestManager manager = MainQuestManager.Instance;
        if (manager == null || !manager.IsNetworkReady || inspectionUI == null || inspectionUI.IsOpen) return;
        Camera camera = Camera.main;
        if (camera == null) return;

        PlayerMovement player = PlayerMovement.LocalPlayerInstance;
        if (player == null || !CanInspect(player.transform.position) || inspectionRoutine != null) return;
        Vector3 point = camera.WorldToScreenPoint(InspectionZoneWorldCenter);
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
        GUI.Box(new Rect(x, y, 300f, 46f), "KIỂM TRA TÌNH TRẠNG XE\nGIỮ [E]", prompt);
    }

    private void ResolveInspectionPolygon()
    {
        if (inspectionPolygon == null)
        {
            PolygonCollider2D[] childPolygons = GetComponentsInChildren<PolygonCollider2D>(true);
            for (int i = 0; i < childPolygons.Length; i++)
            {
                if (IsInspectionZoneName(childPolygons[i].gameObject.name))
                {
                    inspectionPolygon = childPolygons[i];
                    break;
                }
            }
        }

        if (inspectionPolygon == null)
        {
            string[] supportedNames = { "VungKiemTraXe", "ViTriKiemTraXe", "Vehicle Inspection Zone" };
            for (int i = 0; i < supportedNames.Length && inspectionPolygon == null; i++)
            {
                GameObject authoredZone = GameObject.Find(supportedNames[i]);
                if (authoredZone != null) inspectionPolygon = authoredZone.GetComponent<PolygonCollider2D>();
            }
        }

        if (inspectionPolygon == null)
        {
            GameObject zoneObject = new GameObject("VungKiemTraXe [AUTO]");
            zoneObject.transform.SetParent(transform, false);
            inspectionPolygon = zoneObject.AddComponent<PolygonCollider2D>();
            inspectionPolygon.SetPath(0, DefaultInspectionPolygon);
        }

        inspectionPolygon.isTrigger = true;
        inspectionPolygon.enabled = true;
    }

    private static bool IsInspectionZoneName(string objectName)
    {
        return objectName == "VungKiemTraXe" || objectName == "VungKiemTraXe [AUTO]" ||
               objectName == "ViTriKiemTraXe" || objectName == "Vehicle Inspection Zone";
    }
}
