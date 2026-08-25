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
        bool blockedByUI = LocalGameplayUIState.BlocksWorldInteractionHints;
        SetFrontZoneVisible(canInspect && !blockedByUI && (inspectionUI == null || !inspectionUI.IsOpen));

        if (!canInspect || blockedByUI)
        {
            CancelInspection();
            return;
        }

        if (inspectionRoutine == null && Time.unscaledTime >= nextInspectionAllowedAt &&
            Input.GetKeyDown(KeyCode.E))
        {
            if (manager.IsArrivalCarInspected)
                inspectionUI?.Open(this);
            else
                inspectionRoutine = StartCoroutine(InspectionRoutine(player));
        }
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
        if (driveVehicleActivated || LocalGameplayUIState.BlocksWorldInteractionHints) return;
        MainQuestManager manager = MainQuestManager.Instance;
        if (manager == null || !manager.IsNetworkReady || inspectionUI == null || inspectionUI.IsOpen) return;

        PlayerMovement player = PlayerMovement.LocalPlayerInstance;
        if (player == null || !CanInspect(player.transform.position) || inspectionRoutine != null) return;

        Camera camera = Camera.main;
        if (camera == null) return;
        Vector3 screenPoint = camera.WorldToScreenPoint(InspectionZoneWorldCenter);
        if (screenPoint.z <= 0f) return;

        Vector2 target = new Vector2(screenPoint.x, Screen.height - screenPoint.y);
        float promptWidth = Mathf.Clamp(Screen.width * 0.105f, 165f, 210f);
        float promptHeight = Mathf.Clamp(Screen.height * 0.058f, 52f, 64f);
        float horizontalOffset = Mathf.Clamp(Screen.width * 0.055f, 70f, 105f);
        float verticalOffset = Mathf.Clamp(Screen.height * 0.085f, 62f, 92f);
        float x = Mathf.Clamp(target.x - promptWidth - horizontalOffset, 12f,
            Screen.width - promptWidth - 12f);
        float y = Mathf.Clamp(target.y - promptHeight - verticalOffset, 48f,
            Screen.height - promptHeight - 100f);
        Rect promptRect = new Rect(x, y, promptWidth, promptHeight);

        Color accent = new Color(0.22f, 1f, 0.36f, 0.95f);
        Vector2 lineStart = new Vector2(promptRect.xMax, promptRect.yMax - 10f);
        Vector2 elbow = new Vector2(lineStart.x + Mathf.Clamp(Screen.width * 0.018f, 24f, 36f), lineStart.y);
        DrawGuiLine(lineStart, elbow, accent, 2f);
        DrawGuiLine(elbow, target, accent, 2f);

        Color previousColor = GUI.color;
        GUI.color = new Color(0.035f, 0.045f, 0.04f, 0.9f);
        GUI.DrawTexture(promptRect, Texture2D.whiteTexture);
        GUI.color = accent;
        const float borderWidth = 2f;
        GUI.DrawTexture(new Rect(promptRect.x, promptRect.y, promptRect.width, borderWidth), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(promptRect.x, promptRect.yMax - borderWidth, promptRect.width, borderWidth), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(promptRect.x, promptRect.y, borderWidth, promptRect.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(promptRect.xMax - borderWidth, promptRect.y, borderWidth, promptRect.height), Texture2D.whiteTexture);
        GUI.color = previousColor;

        GUIStyle prompt = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.017f), 15, 19),
            fontStyle = FontStyle.Bold
        };
        prompt.normal.textColor = accent;
        Rect textRect = new Rect(promptRect.x + 14f, promptRect.y + 4f,
            promptRect.width - 28f, promptRect.height - 8f);
        GUI.Label(textRect, manager.IsArrivalCarInspected
            ? "NHẤN [E]\nMỞ NẮP XE"
            : "GIỮ [E]\nKIỂM TRA XE", prompt);
    }

    private static void DrawGuiLine(Vector2 start, Vector2 end, Color color, float width)
    {
        Matrix4x4 previousMatrix = GUI.matrix;
        Color previousColor = GUI.color;
        Vector2 delta = end - start;
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        GUI.color = color;
        GUIUtility.RotateAroundPivot(angle, start);
        GUI.DrawTexture(new Rect(start.x, start.y - width * 0.5f, delta.magnitude, width),
            Texture2D.whiteTexture);
        GUI.matrix = previousMatrix;
        GUI.color = previousColor;
    }

    private void ResolveInspectionPolygon()
    {
        GameObject sceneAuthoredZone = GameObject.Find("VungKiemTraXeDauGame");
        if (sceneAuthoredZone != null && sceneAuthoredZone.GetComponent<PolygonCollider2D>() is { } authoredPolygon)
            inspectionPolygon = authoredPolygon;

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
            string[] supportedNames =
            {
                "VungKiemTraXeDauGame", "VungKiemTraXe", "ViTriKiemTraXe", "Vehicle Inspection Zone"
            };
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
        return objectName == "VungKiemTraXeDauGame" || objectName == "VungKiemTraXe" ||
               objectName == "VungKiemTraXe [AUTO]" ||
               objectName == "ViTriKiemTraXe" || objectName == "Vehicle Inspection Zone";
    }
}
