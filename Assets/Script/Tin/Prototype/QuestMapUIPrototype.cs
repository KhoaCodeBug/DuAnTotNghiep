using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Full-screen, always-available map demo. The map itself can be opened from
/// the beginning; quest progress controls how much information it reveals.
/// </summary>
public sealed class QuestMapUIPrototype : MonoBehaviour
{
    private const float RasterMaxWidth = 1090f;
    private const float RasterMaxHeight = 670f;
    private static readonly Color Ink = new Color(0.025f, 0.045f, 0.043f, 0.99f);
    private static readonly Color Panel = new Color(0.055f, 0.082f, 0.078f, 0.99f);
    private static readonly Color Road = new Color(0.19f, 0.235f, 0.225f, 1f);
    private static readonly Color Amber = new Color(1f, 0.67f, 0.14f, 1f);
    private static readonly Color Purple = new Color(0.72f, 0.36f, 0.98f, 1f);
    private static readonly Color Mint = new Color(0.28f, 0.88f, 0.7f, 1f);
    private static readonly Color Muted = new Color(0.62f, 0.69f, 0.67f, 1f);
    private static Sprite circleMarkerSprite;

    private PreMilitaryQuestProgress progress;
    private TMP_FontAsset font;
    private GameObject root;
    private RectTransform viewport;
    private RectTransform mapContent;
    private GameObject schematicRoot;
    private Camera worldMapTemplate;
    private Transform worldOfficeTarget;
    private Transform worldPlayerTarget;
    private Camera worldMapCamera;
    private RenderTexture worldMapTexture;
    private RawImage worldMapImage;
    private RectTransform worldOverlayRoot;
    private RectTransform worldPlayerMarker;
    private RectTransform worldOfficeMarker;
    private RectTransform worldApproximateArea;
    private RectTransform worldRoute;
    private bool useWorldMap;
    private bool useSceneLayoutMap;
    private Vector3[] sceneHousePositions;
    private Vector3 sceneLayoutMin;
    private Vector3 sceneLayoutMax;
    private RectTransform sceneLayoutRoot;
    private RectTransform scenePlayerMarker;
    private bool useRasterMap;
    private Texture2D rasterMapTexture;
    private RectTransform rasterArtRoot;
    private RectTransform rasterPlayerMarker;
    private RectTransform rasterSearchZone;
    private RectTransform rasterOfficeRevealFog;
    private Image rasterOfficeRevealFogImage;
    private readonly List<RectTransform> rasterRestrictedFog = new List<RectTransform>();
    private int activeRasterFogCount;
    private Vector2 rasterOfficeNormalized;
    private Vector2 rasterPlayerNormalized;
    private Vector2 rasterSearchZoneMin;
    private Vector2 rasterSearchZoneMax;
    private Vector2 rasterOfficeAreaMin;
    private Vector2 rasterOfficeAreaMax;
    private int searchZoneHouseCount;
    private bool hasSearchZone;
    private bool hasOfficeSearchArea;
    // Main is tall in Grid space, so the readable cartographic default is the
    // 90-degree landscape orientation used by the reference town maps.
    private int rasterRotationQuarterTurns = 1;
    private TextMeshProUGUI rotationLabel;
    private GameObject approximateArea;
    private GameObject exactRoute;
    private GameObject officeMarker;
    private GameObject unknownOfficeMarker;
    private TextMeshProUGUI stateLabel;
    private TextMeshProUGUI officeKnowledgeText;
    private TextMeshProUGUI clueSummaryText;
    private float zoom = 1f;
    private bool dragging;
    private Vector3 lastMousePosition;
    private GameObject unlockRevealRoot;
    private CanvasGroup unlockRevealGroup;
    private Image unlockRevealDarkness;
    private RectTransform unlockRevealPulse;
    private RectTransform unlockRevealCore;
    private TextMeshProUGUI unlockRevealTitle;
    private TextMeshProUGUI unlockRevealBody;
    private Coroutine unlockRevealRoutine;
    private bool unlockRevealPending;
    private bool unlockRevealCompleted;
    private Action unlockRevealFinished;
    private bool officeRegionRevealVisualComplete;
    private static bool escapeClosePending;

    public bool IsOpen => root != null && root.activeSelf;
    public string CurrentKnowledgeLabel => stateLabel == null ? string.Empty : stateLabel.text;
    public string CurrentClueSummary => clueSummaryText == null ? string.Empty : clueSummaryText.text;
    public int CurrentRasterRotationQuarterTurns => rasterRotationQuarterTurns;
    public Vector2 CurrentRasterPlayerNormalized => rasterPlayerNormalized;
    public Vector2 CurrentRasterPlayerPoint => rasterPlayerMarker == null ? Vector2.zero : rasterPlayerMarker.anchoredPosition;
    public int SearchZoneHouseCount => searchZoneHouseCount;
    public bool HasPendingUnlockReveal => unlockRevealPending;
    public int ActiveRestrictedFogCount => activeRasterFogCount;

    public static bool ConsumeEscapeCloseRequest()
    {
        bool consumed = escapeClosePending;
        escapeClosePending = false;
        return consumed;
    }

    /// <summary>
    /// Switches the prototype from its schematic fallback to a live render of
    /// the Main scene. The supplied camera is only used as a settings template;
    /// a private camera and RenderTexture are created so the existing minimap is
    /// never enabled, moved, or otherwise disturbed.
    /// </summary>
    public void ConfigureWorldMap(Camera cameraTemplate, Transform officeTarget, Transform playerTarget = null)
    {
        worldMapTemplate = cameraTemplate;
        worldOfficeTarget = officeTarget;
        worldPlayerTarget = playerTarget;
        useWorldMap = worldMapTemplate != null;

        if (root != null)
            BuildWorldMapIfNeeded();
    }

    /// <summary>
    /// Builds a clean illustrated city map from real Main-scene coordinates.
    /// Buildings are symbols, not camera pixels, but every house, office and
    /// player marker keeps its actual relative position from the scene.
    /// </summary>
    public void ConfigureSceneLayoutMap(Vector3[] housePositions, Transform officeTarget, Transform playerTarget = null)
    {
        sceneHousePositions = housePositions ?? new Vector3[0];
        worldOfficeTarget = officeTarget;
        worldPlayerTarget = playerTarget;
        useSceneLayoutMap = sceneHousePositions.Length > 0;
        useWorldMap = false;

        if (root != null)
            BuildSceneLayoutMapIfNeeded();
    }

    public void ConfigureRasterMap(Texture2D mapTexture, Vector2 officeNormalized, Vector2 playerNormalized)
    {
        rasterMapTexture = mapTexture;
        rasterOfficeNormalized = officeNormalized;
        rasterPlayerNormalized = playerNormalized;
        useRasterMap = rasterMapTexture != null;
        useSceneLayoutMap = false;
        useWorldMap = false;
        if (root != null)
            BuildRasterMapIfNeeded();
    }

    public void SetRasterMapPlayerPosition(Vector2 playerNormalized)
    {
        rasterPlayerNormalized = playerNormalized;
        UpdateRasterMapMarkers();
    }

    public void ConfigureSearchZone(Vector2 minimumNormalized, Vector2 maximumNormalized, int houseCount)
    {
        rasterSearchZoneMin = Vector2.Min(minimumNormalized, maximumNormalized);
        rasterSearchZoneMax = Vector2.Max(minimumNormalized, maximumNormalized);
        searchZoneHouseCount = Mathf.Max(0, houseCount);
        hasSearchZone = searchZoneHouseCount > 0;
        BuildRasterSearchZoneIfNeeded();
        UpdateRasterMapMarkers();
        Refresh();
    }

    public void ConfigureOfficeSearchArea(Vector2 minimumNormalized, Vector2 maximumNormalized)
    {
        rasterOfficeAreaMin = Vector2.Min(minimumNormalized, maximumNormalized);
        rasterOfficeAreaMax = Vector2.Max(minimumNormalized, maximumNormalized);
        hasOfficeSearchArea = true;
        UpdateRasterMapMarkers();
        Refresh();
    }

    public void RotateRasterMap(int quarterTurnDelta)
    {
        SetRasterMapRotation(rasterRotationQuarterTurns + quarterTurnDelta);
    }

    public void SetRasterMapRotation(int quarterTurns)
    {
        rasterRotationQuarterTurns = ((quarterTurns % 4) + 4) % 4;
        ApplyRasterRotationLayout();
    }

    public void Initialize(Transform canvasRoot, TMP_FontAsset targetFont, PreMilitaryQuestProgress targetProgress)
    {
        if (root != null)
            return;

        font = targetFont;
        progress = targetProgress;
        Build(canvasRoot);
        Refresh();
        SetOpen(false);
    }

    public void SetOpen(bool open)
    {
        if (root == null)
            return;

        dragging = false;
        root.SetActive(open);
        if (worldMapCamera != null)
            worldMapCamera.enabled = open;
        if (open)
        {
            root.transform.SetAsLastSibling();
            Refresh();
            ResetView();
            // The replicated 3/3 state is the durable source of truth. Even if
            // an RPC was received before this UI existed (or its queue was lost
            // during a scene transition), the first manual map open must still
            // play the reveal exactly once.
            bool completedCluesNeedReveal = progress != null && progress.HasMapFragment1 &&
                                            !unlockRevealCompleted;
            if ((unlockRevealPending || completedCluesNeedReveal) && Application.isPlaying)
            {
                unlockRevealPending = true;
                StartUnlockReveal();
            }
        }
    }

    public void QueueUnlockReveal(Action onFinished = null)
    {
        if (onFinished != null)
        {
            if (unlockRevealCompleted && unlockRevealRoutine == null)
            {
                onFinished.Invoke();
                return;
            }
            unlockRevealFinished += onFinished;
        }

        if (unlockRevealCompleted || unlockRevealPending || unlockRevealRoutine != null)
            return;
        unlockRevealPending = true;
        Debug.Log("[QUEST MAP] Unlock reveal queued for the next map open.");
        if (IsOpen && Application.isPlaying)
            StartUnlockReveal();
    }

    private void StartUnlockReveal()
    {
        unlockRevealPending = false;
        if (unlockRevealRoot == null) return;
        unlockRevealCompleted = true;
        Debug.Log("[QUEST MAP] Unlock reveal started.");
        if (unlockRevealRoutine != null) StopCoroutine(unlockRevealRoutine);
        unlockRevealRoutine = StartCoroutine(UnlockRevealRoutine());
    }

    private IEnumerator UnlockRevealRoutine()
    {
        unlockRevealRoot.SetActive(true);
        unlockRevealRoot.transform.SetAsLastSibling();
        unlockRevealGroup.alpha = 1f;
        // Raster maps already show the known neighborhood through permanent
        // fog openings. Preserve that exact view and reveal only the office
        // opening; fallback maps still use the full-screen darkness transition.
        SetUnlockRevealDarkness(useRasterMap ? 0f : 0.96f);
        SetRasterOfficeRevealProgress(0f);
        unlockRevealTitle.text = "ĐANG GIẢI MÃ DỮ LIỆU BẢN ĐỒ";
        unlockRevealBody.text = "Đối chiếu ba tài liệu về vật tư và tuyến sơ tán...";
        unlockRevealPulse.localScale = Vector3.one * 0.3f;
        unlockRevealCore.localScale = Vector3.one * 0.7f;
        // The raster map already owns the real purple office pin. Hide the
        // decorative diamond so the scan reads as a ring around one marker,
        // rather than a second destination marker stacked on top of it.
        unlockRevealCore.gameObject.SetActive(!useRasterMap);
        AlignUnlockRevealToOfficeMarker();

        Vector2 startContentPosition = mapContent != null ? mapContent.anchoredPosition : Vector2.zero;
        float startZoom = mapContent != null ? mapContent.localScale.x : 1f;
        const float targetZoom = 1.58f;
        Vector2 targetContentPosition = startContentPosition;
        if (mapContent != null && viewport != null && officeMarker != null)
        {
            Vector2 officeInViewport = viewport.InverseTransformPoint(officeMarker.transform.position);
            Vector2 unscaledOfficePoint = (officeInViewport - startContentPosition) /
                                          Mathf.Max(0.001f, startZoom);
            targetContentPosition = -unscaledOfficePoint * targetZoom;

            // Reuse the normal pan limits so the camera focuses as close to the
            // office as the map edges allow without exposing empty space.
            zoom = targetZoom;
            mapContent.localScale = Vector3.one * targetZoom;
            mapContent.anchoredPosition = targetContentPosition;
            ClampPan();
            targetContentPosition = mapContent.anchoredPosition;
            zoom = startZoom;
            mapContent.localScale = Vector3.one * startZoom;
            mapContent.anchoredPosition = startContentPosition;
        }

        if (exactRoute != null) exactRoute.SetActive(false);
        if (officeMarker != null) officeMarker.SetActive(false);
        CanvasGroup officeMarkerGroup = null;
        if (officeMarker != null)
        {
            officeMarkerGroup = officeMarker.GetComponent<CanvasGroup>();
            if (officeMarkerGroup == null) officeMarkerGroup = officeMarker.AddComponent<CanvasGroup>();
            officeMarkerGroup.alpha = 0f;
            officeMarker.transform.localScale = Vector3.one * 0.72f;
        }

        float elapsed = 0f;
        const float scanDuration = 1.35f;
        while (elapsed < scanDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / scanDuration);
            float eased = t * t * (3f - 2f * t);
            unlockRevealPulse.localScale = Vector3.one * Mathf.Lerp(0.3f, 1.18f, eased);
            unlockRevealPulse.localRotation = Quaternion.Euler(0f, 0f, elapsed * 55f);
            unlockRevealCore.localRotation = Quaternion.Euler(0f, 0f, -elapsed * 80f);
            AlignUnlockRevealToOfficeMarker();
            if (useRasterMap)
                SetRasterOfficeRevealProgress(Mathf.Lerp(0f, 0.42f, eased));
            else
                SetUnlockRevealDarkness(Mathf.Lerp(0.96f, 0.62f, eased));
            yield return null;
        }

        // The destination becomes visible at the visual impact, not before the
        // player chooses to inspect the map.
        if (exactRoute != null) exactRoute.SetActive(true);
        if (officeMarker != null) officeMarker.SetActive(true);
        unlockRevealTitle.text = "ĐÃ MỞ KHÓA VỊ TRÍ MỚI";
        unlockRevealBody.text = "Văn phòng màu tím đã được đánh dấu trên bản đồ.";

        elapsed = 0f;
        const float impactDuration = 1.8f;
        while (elapsed < impactDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / impactDuration);
            float eased = t * t * (3f - 2f * t);
            float pulse = 1f + Mathf.Sin(t * Mathf.PI) * 0.28f;
            unlockRevealPulse.localScale = Vector3.one * Mathf.Lerp(1.18f, 1.55f, t);
            unlockRevealCore.localScale = Vector3.one * pulse;
            unlockRevealPulse.localRotation = Quaternion.Euler(0f, 0f, 50f + elapsed * 90f);
            // Reveal the map and its new marker in the same visual beat while
            // keeping the scan graphics fully visible above the brightening map.
            if (useRasterMap)
                SetRasterOfficeRevealProgress(Mathf.Lerp(0.42f, 1f, eased));
            else
                SetUnlockRevealDarkness(Mathf.Lerp(0.62f, 0f, eased));
            if (officeMarker != null)
            {
                float markerArrival = Mathf.Lerp(0.72f, 1f, eased);
                float markerPulse = 1f + Mathf.Sin(t * Mathf.PI * 4f) * 0.1f * (1f - t * 0.35f);
                officeMarker.transform.localScale = Vector3.one * markerArrival * markerPulse;
                if (officeMarkerGroup != null)
                    officeMarkerGroup.alpha = Mathf.Clamp01(Mathf.Lerp(0.35f, 1f, eased) +
                                                             Mathf.Sin(t * Mathf.PI * 4f) * 0.12f);
            }
            if (mapContent != null)
            {
                zoom = Mathf.Lerp(startZoom, targetZoom, eased);
                mapContent.localScale = Vector3.one * zoom;
                mapContent.anchoredPosition = Vector2.Lerp(startContentPosition, targetContentPosition, eased);
                AlignUnlockRevealToOfficeMarker();
            }
            yield return null;
        }

        elapsed = 0f;
        const float markerPulseDuration = 0.9f;
        while (elapsed < markerPulseDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float markerPulse = 1f + Mathf.Sin(elapsed * Mathf.PI * 3.5f) * 0.075f;
            if (officeMarker != null) officeMarker.transform.localScale = Vector3.one * markerPulse;
            if (officeMarkerGroup != null)
                officeMarkerGroup.alpha = 0.86f + Mathf.Sin(elapsed * Mathf.PI * 3.5f) * 0.14f;
            yield return null;
        }
        if (officeMarker != null) officeMarker.transform.localScale = Vector3.one;
        if (officeMarkerGroup != null) officeMarkerGroup.alpha = 1f;

        elapsed = 0f;
        const float fadeDuration = 0.35f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            unlockRevealGroup.alpha = 1f - Mathf.Clamp01(elapsed / fadeDuration);
            yield return null;
        }

        unlockRevealRoot.SetActive(false);
        unlockRevealGroup.alpha = 0f;
        officeRegionRevealVisualComplete = true;
        SetRasterOfficeRevealProgress(1f);
        unlockRevealRoutine = null;
        Action finished = unlockRevealFinished;
        unlockRevealFinished = null;
        finished?.Invoke();
    }

    private void SetUnlockRevealDarkness(float alpha)
    {
        if (unlockRevealDarkness == null) return;
        Color color = unlockRevealDarkness.color;
        color.a = Mathf.Clamp01(alpha);
        unlockRevealDarkness.color = color;
    }

    private void AlignUnlockRevealToOfficeMarker()
    {
        if (!useRasterMap || viewport == null || officeMarker == null || unlockRevealPulse == null)
            return;

        Vector2 markerPoint = viewport.InverseTransformPoint(officeMarker.transform.position);
        unlockRevealPulse.anchoredPosition = markerPoint;
        if (unlockRevealCore != null) unlockRevealCore.anchoredPosition = markerPoint;
    }

    private void SetRasterOfficeRevealProgress(float revealProgress)
    {
        if (rasterOfficeRevealFog == null || rasterOfficeRevealFogImage == null) return;
        float progressValue = Mathf.Clamp01(revealProgress);
        rasterOfficeRevealFog.gameObject.SetActive(!officeRegionRevealVisualComplete && progressValue < 1f);
        Color color = rasterOfficeRevealFogImage.color;
        color.a = 1f - progressValue;
        rasterOfficeRevealFogImage.color = color;
    }

    public void Refresh()
    {
        if (progress == null || stateLabel == null)
            return;

        OfficeKnowledgeLevel knowledge = progress.OfficeKnowledge;
        bool approximate = knowledge == OfficeKnowledgeLevel.ApproximateArea;
        bool exact = knowledge == OfficeKnowledgeLevel.ExactLocation || knowledge == OfficeKnowledgeLevel.Discovered;

        approximateArea.SetActive(approximate);
        unknownOfficeMarker.SetActive(approximate);
        exactRoute.SetActive(exact);
        officeMarker.SetActive(exact);
        if (worldApproximateArea != null) worldApproximateArea.gameObject.SetActive(approximate);
        if (worldOfficeMarker != null) worldOfficeMarker.gameObject.SetActive(exact);
        if (worldRoute != null) worldRoute.gameObject.SetActive(exact);
        UpdateRasterMapMarkers();

        switch (knowledge)
        {
            case OfficeKnowledgeLevel.ApproximateArea:
                stateLabel.text = "ĐÃ KHOANH VÙNG TÌM KIẾM";
                stateLabel.color = Amber;
                officeKnowledgeText.text = "Văn phòng  •  Chỉ biết khu vực tương đối";
                break;
            case OfficeKnowledgeLevel.ExactLocation:
                stateLabel.text = "MẢNH 1  •  VỊ TRÍ CHÍNH XÁC";
                stateLabel.color = Purple;
                officeKnowledgeText.text = "Văn phòng  •  Đã đánh dấu chính xác";
                break;
            case OfficeKnowledgeLevel.Discovered:
                stateLabel.text = "ĐÃ KHÁM PHÁ VĂN PHÒNG";
                stateLabel.color = Mint;
                officeKnowledgeText.text = "Văn phòng  •  Địa điểm đã xác nhận";
                break;
            default:
                stateLabel.text = "CHƯA CÓ MANH MỐI VĂN PHÒNG";
                stateLabel.color = Muted;
                officeKnowledgeText.text = "Văn phòng  •  Chưa xác định";
                break;
        }

        int clueCount = Mathf.Min(progress.RouteClueCount, PreMilitaryQuestProgress.RequiredRouteClues);
        clueSummaryText.text =
            $"Manh mối đã thu thập  {clueCount}/{PreMilitaryQuestProgress.RequiredRouteClues}" +
            (searchZoneHouseCount > 0 ? "\nPhạm vi  •  Các ngôi nhà xung quanh" : string.Empty);
    }

    private void Update()
    {
        if (!IsOpen || viewport == null || mapContent == null)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            SetOpen(false);
            escapeClosePending = true;
            return;
        }

        if (useWorldMap && worldMapCamera != null)
        {
            UpdateWorldMapInput();
            UpdateWorldMapMarkers();
            return;
        }

        if (useSceneLayoutMap)
            UpdateSceneLayoutMarkers();
        if (useRasterMap)
        {
            UpdateRasterMapMarkers();
        }

        bool pointerInside = RectTransformUtility.RectangleContainsScreenPoint(viewport, Input.mousePosition);
        float scroll = pointerInside ? Input.GetAxis("Mouse ScrollWheel") : 0f;
        if (!Mathf.Approximately(scroll, 0f))
        {
            zoom = Mathf.Clamp(zoom + scroll * 0.8f, 1f, 2.5f);
            mapContent.localScale = Vector3.one * zoom;
            ClampPan();
        }

        if (pointerInside && Input.GetMouseButtonDown(0))
        {
            dragging = true;
            lastMousePosition = Input.mousePosition;
        }

        if (dragging && Input.GetMouseButton(0))
        {
            Vector3 delta = Input.mousePosition - lastMousePosition;
            mapContent.anchoredPosition += new Vector2(delta.x, delta.y);
            lastMousePosition = Input.mousePosition;
            ClampPan();
        }

        if (Input.GetMouseButtonUp(0))
            dragging = false;
    }

    private void ResetView()
    {
        if (useWorldMap && worldMapCamera != null)
        {
            FrameWorldMapForCurrentKnowledge();
            UpdateWorldMapMarkers();
            return;
        }

        zoom = 1f;
        mapContent.localScale = Vector3.one;
        mapContent.anchoredPosition = Vector2.zero;
        if (useRasterMap)
            ApplyRasterRotationLayout();
    }

    private void BuildSceneLayoutMapIfNeeded()
    {
        if (!useSceneLayoutMap || mapContent == null || sceneLayoutRoot != null)
            return;

        if (schematicRoot != null)
            schematicRoot.SetActive(false);
        Image contentImage = mapContent.GetComponent<Image>();
        if (contentImage != null)
            contentImage.color = new Color(0.88f, 0.87f, 0.73f, 1f);

        CalculateSceneLayoutBounds();
        GameObject layoutObject = new GameObject("Main Scene Illustrated Map", typeof(RectTransform));
        layoutObject.transform.SetParent(mapContent, false);
        sceneLayoutRoot = layoutObject.GetComponent<RectTransform>();
        Stretch(sceneLayoutRoot);

        // Green borders and parks give the paper map a readable city silhouette.
        Box("North Green Belt", sceneLayoutRoot, new Vector2(1120f, 44f), new Vector2(0f, 328f), new Color(0.34f, 0.58f, 0.27f, 1f));
        Box("South Green Belt", sceneLayoutRoot, new Vector2(1120f, 38f), new Vector2(0f, -331f), new Color(0.29f, 0.52f, 0.23f, 1f));
        Box("Central Park", sceneLayoutRoot, new Vector2(92f, 70f), SceneToMapPoint(new Vector3(30f, 2f)), new Color(0.28f, 0.62f, 0.25f, 1f));

        float[] verticalRoads = { -86f, -59f, -22f, 10f, 39f, 64f };
        float[] horizontalRoads = { -21f, -7f, 10f, 31f, 53f };
        for (int i = 0; i < verticalRoads.Length; i++)
            SceneRoad(new Vector3(verticalRoads[i], sceneLayoutMin.y), new Vector3(verticalRoads[i], sceneLayoutMax.y), 8f);
        for (int i = 0; i < horizontalRoads.Length; i++)
            SceneRoad(new Vector3(sceneLayoutMin.x, horizontalRoads[i]), new Vector3(sceneLayoutMax.x, horizontalRoads[i]), 8f);

        Color[] residentialColors =
        {
            new Color(0.78f, 0.56f, 0.30f, 1f),
            new Color(0.62f, 0.72f, 0.27f, 1f),
            new Color(0.88f, 0.75f, 0.25f, 1f),
            new Color(0.43f, 0.67f, 0.32f, 1f)
        };
        for (int i = 0; i < sceneHousePositions.Length; i++)
        {
            Vector2 point = SceneToMapPoint(sceneHousePositions[i]);
            float width = i % 9 == 0 ? 24f : 15f;
            float height = i % 9 == 0 ? 16f : 10f;
            RectTransform building = Box("Scene House " + (i + 1), sceneLayoutRoot,
                new Vector2(width, height), point, residentialColors[i % residentialColors.Length]);
            Border(building, new Color(0.28f, 0.27f, 0.21f, 0.32f));
        }

        approximateArea = Box("Approximate Office Area", sceneLayoutRoot, new Vector2(118f, 94f),
            SceneToMapPoint(worldOfficeTarget != null ? worldOfficeTarget.position : Vector3.zero),
            new Color(Amber.r, Amber.g, Amber.b, 0.18f)).gameObject;
        Border(approximateArea.GetComponent<RectTransform>(), new Color(Amber.r, Amber.g, Amber.b, 0.9f));
        unknownOfficeMarker = Box("Unknown Office Marker", approximateArea.transform, new Vector2(34f, 34f),
            Vector2.zero, Amber).gameObject;
        Text(unknownOfficeMarker.transform, "Question Mark", "?", 21f, Ink, FontStyles.Bold,
            TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(30f, 30f), Vector2.zero);

        officeMarker = Box("Exact Office Marker", sceneLayoutRoot, new Vector2(74f, 48f),
            SceneToMapPoint(worldOfficeTarget != null ? worldOfficeTarget.position : Vector3.zero), Purple).gameObject;
        Border(officeMarker.GetComponent<RectTransform>(), Color.white);
        Text(officeMarker.transform, "Office Label", "VĂN PHÒNG", 10f, Color.white, FontStyles.Bold,
            TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(70f, 40f), Vector2.zero);

        exactRoute = Box("Exact Route", sceneLayoutRoot, new Vector2(100f, 5f), Vector2.zero,
            new Color(Purple.r, Purple.g, Purple.b, 0.92f)).gameObject;
        scenePlayerMarker = PlayerCircleMarker("Scene Player Marker", sceneLayoutRoot, 12f,
            SceneToMapPoint(GetPlayerMapPosition()));

        UpdateSceneLayoutMarkers();
        Refresh();
    }

    private void BuildRasterMapIfNeeded()
    {
        if (!useRasterMap || mapContent == null || rasterArtRoot != null)
            return;

        if (schematicRoot != null) schematicRoot.SetActive(false);
        if (sceneLayoutRoot != null) sceneLayoutRoot.gameObject.SetActive(false);
        Image contentImage = mapContent.GetComponent<Image>();
        if (contentImage != null)
            contentImage.color = new Color(0.12f, 0.2f, 0.11f, 1f);

        Vector2 artSize = CalculateRasterArtSize(false);

        GameObject artObject = new GameObject("Cell Accurate Main Map", typeof(RectTransform));
        artObject.transform.SetParent(mapContent, false);
        rasterArtRoot = artObject.GetComponent<RectTransform>();
        SetRect(rasterArtRoot, artSize, Vector2.zero);

        GameObject imageObject = new GameObject("Zomboid Map Raster", typeof(RectTransform), typeof(RawImage));
        imageObject.transform.SetParent(rasterArtRoot, false);
        Stretch(imageObject.GetComponent<RectTransform>());
        RawImage rasterImage = imageObject.GetComponent<RawImage>();
        rasterImage.texture = rasterMapTexture;
        rasterImage.color = Color.white;
        rasterImage.raycastTarget = false;

        BuildRasterSearchZoneIfNeeded();

        rasterOfficeRevealFog = Box("Office Region Reveal Fog", rasterArtRoot,
            new Vector2(104f, 104f), Vector2.zero, Color.black);
        rasterOfficeRevealFogImage = rasterOfficeRevealFog.GetComponent<Image>();
        rasterOfficeRevealFogImage.raycastTarget = false;

        approximateArea = Box("Approximate Office Area", rasterArtRoot, new Vector2(72f, 72f), Vector2.zero,
            new Color(Amber.r, Amber.g, Amber.b, 0.22f)).gameObject;
        Border(approximateArea.GetComponent<RectTransform>(), new Color(Amber.r, Amber.g, Amber.b, 0.95f));
        unknownOfficeMarker = Box("Unknown Office Marker", approximateArea.transform, new Vector2(30f, 30f),
            Vector2.zero, Amber).gameObject;
        Text(unknownOfficeMarker.transform, "Question Mark", "?", 20f, Ink, FontStyles.Bold,
            TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(28f, 28f), Vector2.zero);

        officeMarker = new GameObject("Exact Office Marker", typeof(RectTransform));
        officeMarker.transform.SetParent(rasterArtRoot, false);
        SetRect(officeMarker.GetComponent<RectTransform>(), new Vector2(104f, 34f), Vector2.zero);
        RectTransform officePin = Box("Office Pin", officeMarker.transform, new Vector2(18f, 18f),
            new Vector2(-40f, 0f), Purple);
        officePin.localRotation = Quaternion.Euler(0f, 0f, 45f);
        RectTransform officeCaption = Box("Office Caption", officeMarker.transform, new Vector2(80f, 26f),
            new Vector2(12f, 0f), new Color(0.16f, 0.08f, 0.2f, 0.94f));
        Text(officeCaption, "Office Label", "VĂN PHÒNG", 9f, Color.white, FontStyles.Bold,
            TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(76f, 22f), Vector2.zero);

        // Mảnh 1 reveals a destination marker, not a fake straight road. The
        // old line implied a traversable route even when it crossed buildings.
        exactRoute = new GameObject("Exact Location Revealed", typeof(RectTransform));
        exactRoute.transform.SetParent(rasterArtRoot, false);
        rasterPlayerMarker = PlayerCircleMarker("Raster Player Marker", rasterArtRoot, 12f, Vector2.zero);

        ApplyRasterRotationLayout();
        Refresh();
    }

    private Vector2 CalculateRasterArtSize(bool rotatedByQuarterTurn)
    {
        float textureAspect = rasterMapTexture.width / (float)Mathf.Max(1, rasterMapTexture.height);
        float visibleAspect = rotatedByQuarterTurn ? 1f / Mathf.Max(0.0001f, textureAspect) : textureAspect;
        Vector2 visibleSize = visibleAspect >= RasterMaxWidth / RasterMaxHeight
            ? new Vector2(RasterMaxWidth, RasterMaxWidth / visibleAspect)
            : new Vector2(RasterMaxHeight * visibleAspect, RasterMaxHeight);
        return rotatedByQuarterTurn
            ? new Vector2(visibleSize.y, visibleSize.x)
            : visibleSize;
    }

    private void ApplyRasterRotationLayout()
    {
        if (!useRasterMap || rasterArtRoot == null || rasterMapTexture == null)
        {
            UpdateRotationLabel();
            return;
        }

        bool quarterTurn = (rasterRotationQuarterTurns & 1) == 1;
        rasterArtRoot.sizeDelta = CalculateRasterArtSize(quarterTurn);
        rasterArtRoot.localRotation = Quaternion.Euler(0f, 0f, -90f * rasterRotationQuarterTurns);

        // Quest labels stay upright while the cartographic layer rotates.
        if (officeMarker != null)
            officeMarker.transform.localRotation = Quaternion.Euler(0f, 0f, 90f * rasterRotationQuarterTurns);
        if (unknownOfficeMarker != null)
            unknownOfficeMarker.transform.localRotation = Quaternion.Euler(0f, 0f, 90f * rasterRotationQuarterTurns);
        if (rasterPlayerMarker != null)
            rasterPlayerMarker.localRotation = Quaternion.Euler(0f, 0f, 90f * rasterRotationQuarterTurns);
        UpdateRasterMapMarkers();
        UpdateRotationLabel();
    }

    private void UpdateRotationLabel()
    {
        if (rotationLabel != null)
            rotationLabel.text = $"HƯỚNG BẢN ĐỒ  {rasterRotationQuarterTurns * 90}°";
    }

    private void UpdateRasterMapMarkers()
    {
        if (!useRasterMap || rasterArtRoot == null)
            return;
        Vector2 playerPoint = NormalizedToRasterPoint(rasterPlayerNormalized);
        Vector2 officePoint = NormalizedToRasterPoint(rasterOfficeNormalized);
        rasterPlayerMarker.anchoredPosition = playerPoint;
        officeMarker.GetComponent<RectTransform>().anchoredPosition = officePoint;
        approximateArea.GetComponent<RectTransform>().anchoredPosition = officePoint;
        if (hasOfficeSearchArea)
        {
            Vector2 areaMin = NormalizedToRasterPoint(rasterOfficeAreaMin);
            Vector2 areaMax = NormalizedToRasterPoint(rasterOfficeAreaMax);
            RectTransform areaRect = approximateArea.GetComponent<RectTransform>();
            areaRect.anchoredPosition = (areaMin + areaMax) * 0.5f;
            areaRect.sizeDelta = new Vector2(
                Mathf.Max(56f, Mathf.Abs(areaMax.x - areaMin.x)),
                Mathf.Max(56f, Mathf.Abs(areaMax.y - areaMin.y)));
        }
        exactRoute.GetComponent<RectTransform>().anchoredPosition = officePoint;
        if (rasterSearchZone != null)
        {
            Vector2 minPoint = NormalizedToRasterPoint(rasterSearchZoneMin);
            Vector2 maxPoint = NormalizedToRasterPoint(rasterSearchZoneMax);
            rasterSearchZone.anchoredPosition = (minPoint + maxPoint) * 0.5f;
            rasterSearchZone.sizeDelta = new Vector2(
                Mathf.Max(56f, Mathf.Abs(maxPoint.x - minPoint.x)),
                Mathf.Max(56f, Mathf.Abs(maxPoint.y - minPoint.y)));
            Rect neighborhoodOpening = RectFromPoints(minPoint, maxPoint);
            Rect? officeOpening = null;

            // Fragment 1 opens a second landmark-aligned rectangle around the
            // office. It deliberately stays independent from the neighborhood
            // opening so the space above/below the route remains under fog.
            if (progress != null && progress.HasMapFragment1)
            {
                Vector2 officeMin;
                Vector2 officeMax;
                if (hasOfficeSearchArea)
                {
                    officeMin = NormalizedToRasterPoint(rasterOfficeAreaMin);
                    officeMax = NormalizedToRasterPoint(rasterOfficeAreaMax);
                }
                else
                {
                    const float officeRevealPadding = 52f;
                    officeMin = officePoint - Vector2.one * officeRevealPadding;
                    officeMax = officePoint + Vector2.one * officeRevealPadding;
                }
                officeOpening = RectFromPoints(officeMin, officeMax);
            }

            if (rasterOfficeRevealFog != null && officeOpening.HasValue)
            {
                Rect revealRect = officeOpening.Value;
                rasterOfficeRevealFog.anchoredPosition = revealRect.center;
                rasterOfficeRevealFog.sizeDelta = revealRect.size;
                rasterOfficeRevealFog.gameObject.SetActive(!officeRegionRevealVisualComplete);
            }
            else if (rasterOfficeRevealFog != null)
            {
                rasterOfficeRevealFog.gameObject.SetActive(false);
            }

            UpdateRasterRestrictionFog(neighborhoodOpening, officeOpening);
            UpdateSearchRestrictionVisibility();
        }
    }

    private void BuildRasterSearchZoneIfNeeded()
    {
        if (!useRasterMap || rasterArtRoot == null || rasterSearchZone != null) return;
        EnsureRasterFogCount(8);

        rasterSearchZone = Box("Quest Search Zone", rasterArtRoot, new Vector2(120f, 100f), Vector2.zero,
            new Color(0f, 0f, 0f, 0f));
        Border(rasterSearchZone, new Color(Amber.r, Amber.g, Amber.b, 0.9f));
        rasterSearchZone.SetSiblingIndex(Mathf.Min(5, rasterArtRoot.childCount - 1));
        UpdateSearchRestrictionVisibility();
    }

    private void UpdateRasterRestrictionFog(Rect neighborhoodOpening, Rect? officeOpening)
    {
        if (rasterArtRoot == null) return;
        float halfWidth = rasterArtRoot.rect.width * 0.5f;
        float halfHeight = rasterArtRoot.rect.height * 0.5f;
        Rect mapRect = Rect.MinMaxRect(-halfWidth, -halfHeight, halfWidth, halfHeight);
        var fogRects = new List<Rect> { mapRect };
        SubtractOpening(fogRects, ClampRect(neighborhoodOpening, mapRect));
        if (officeOpening.HasValue)
            SubtractOpening(fogRects, ClampRect(officeOpening.Value, mapRect));

        EnsureRasterFogCount(Mathf.Max(8, fogRects.Count));
        activeRasterFogCount = fogRects.Count;
        for (int i = 0; i < fogRects.Count; i++)
            SetFogRect(rasterRestrictedFog[i], fogRects[i].size, fogRects[i].center);
    }

    private void EnsureRasterFogCount(int count)
    {
        if (rasterArtRoot == null) return;
        string[] names =
        {
            "Restricted Fog West", "Restricted Fog East", "Restricted Fog South", "Restricted Fog North"
        };
        while (rasterRestrictedFog.Count < count)
        {
            int index = rasterRestrictedFog.Count;
            string name = index < names.Length ? names[index] : $"Restricted Fog Segment {index + 1}";
            RectTransform fog = Box(name, rasterArtRoot, Vector2.zero, Vector2.zero,
                // Unknown districts should read as genuinely unavailable, not
                // as a dim preview of streets the player has not uncovered.
                new Color(0f, 0f, 0f, 1f));
            fog.SetSiblingIndex(Mathf.Min(index + 1, rasterArtRoot.childCount - 1));
            rasterRestrictedFog.Add(fog);
        }
    }

    private static Rect RectFromPoints(Vector2 first, Vector2 second)
    {
        Vector2 minimum = Vector2.Min(first, second);
        Vector2 maximum = Vector2.Max(first, second);
        return Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
    }

    private static Rect ClampRect(Rect value, Rect limits)
    {
        return Rect.MinMaxRect(
            Mathf.Clamp(value.xMin, limits.xMin, limits.xMax),
            Mathf.Clamp(value.yMin, limits.yMin, limits.yMax),
            Mathf.Clamp(value.xMax, limits.xMin, limits.xMax),
            Mathf.Clamp(value.yMax, limits.yMin, limits.yMax));
    }

    private static void SubtractOpening(List<Rect> fogRects, Rect opening)
    {
        const float epsilon = 0.01f;
        for (int i = fogRects.Count - 1; i >= 0; i--)
        {
            Rect fog = fogRects[i];
            float xMin = Mathf.Max(fog.xMin, opening.xMin);
            float yMin = Mathf.Max(fog.yMin, opening.yMin);
            float xMax = Mathf.Min(fog.xMax, opening.xMax);
            float yMax = Mathf.Min(fog.yMax, opening.yMax);
            if (xMax - xMin <= epsilon || yMax - yMin <= epsilon)
                continue;

            fogRects.RemoveAt(i);
            AddFogRect(fogRects, Rect.MinMaxRect(fog.xMin, fog.yMin, xMin, fog.yMax), epsilon);
            AddFogRect(fogRects, Rect.MinMaxRect(xMax, fog.yMin, fog.xMax, fog.yMax), epsilon);
            AddFogRect(fogRects, Rect.MinMaxRect(xMin, fog.yMin, xMax, yMin), epsilon);
            AddFogRect(fogRects, Rect.MinMaxRect(xMin, yMax, xMax, fog.yMax), epsilon);
        }
    }

    private static void AddFogRect(List<Rect> fogRects, Rect value, float epsilon)
    {
        if (value.width > epsilon && value.height > epsilon)
            fogRects.Add(value);
    }

    private static void SetFogRect(RectTransform rect, Vector2 size, Vector2 position)
    {
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    private void UpdateSearchRestrictionVisibility()
    {
        bool borderVisible = hasSearchZone && (progress == null || !progress.HasMapFragment1);
        if (rasterSearchZone != null) rasterSearchZone.gameObject.SetActive(borderVisible);

        // Fog remains active and is cut into independent neighborhood/office
        // segments by UpdateRasterMapMarkers as regions are discovered.
        bool fogVisible = hasSearchZone;
        for (int i = 0; i < rasterRestrictedFog.Count; i++)
            if (rasterRestrictedFog[i] != null)
                rasterRestrictedFog[i].gameObject.SetActive(fogVisible && i < activeRasterFogCount);
    }

    private Vector2 NormalizedToRasterPoint(Vector2 normalized)
    {
        return new Vector2(
            Mathf.Lerp(-rasterArtRoot.rect.width * 0.5f, rasterArtRoot.rect.width * 0.5f, normalized.x),
            Mathf.Lerp(-rasterArtRoot.rect.height * 0.5f, rasterArtRoot.rect.height * 0.5f, normalized.y));
    }

    private void CalculateSceneLayoutBounds()
    {
        sceneLayoutMin = new Vector3(float.MaxValue, float.MaxValue, 0f);
        sceneLayoutMax = new Vector3(float.MinValue, float.MinValue, 0f);
        for (int i = 0; i < sceneHousePositions.Length; i++)
        {
            sceneLayoutMin = Vector3.Min(sceneLayoutMin, sceneHousePositions[i]);
            sceneLayoutMax = Vector3.Max(sceneLayoutMax, sceneHousePositions[i]);
        }
        if (worldOfficeTarget != null)
        {
            sceneLayoutMin = Vector3.Min(sceneLayoutMin, worldOfficeTarget.position);
            sceneLayoutMax = Vector3.Max(sceneLayoutMax, worldOfficeTarget.position);
        }
        sceneLayoutMin -= new Vector3(8f, 8f, 0f);
        sceneLayoutMax += new Vector3(8f, 8f, 0f);
    }

    private Vector2 SceneToMapPoint(Vector3 scenePosition)
    {
        float x = Mathf.Lerp(-520f, 520f, Mathf.InverseLerp(sceneLayoutMin.x, sceneLayoutMax.x, scenePosition.x));
        float y = Mathf.Lerp(-300f, 300f, Mathf.InverseLerp(sceneLayoutMin.y, sceneLayoutMax.y, scenePosition.y));
        return new Vector2(x, y);
    }

    private void SceneRoad(Vector3 fromWorld, Vector3 toWorld, float width)
    {
        Vector2 from = SceneToMapPoint(fromWorld);
        Vector2 to = SceneToMapPoint(toWorld);
        Vector2 difference = to - from;
        RectTransform road = Box("Scene Road", sceneLayoutRoot, new Vector2(difference.magnitude, width),
            (from + to) * 0.5f, new Color(0.56f, 0.58f, 0.54f, 1f));
        road.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(difference.y, difference.x) * Mathf.Rad2Deg);
    }

    private void UpdateSceneLayoutMarkers()
    {
        if (!useSceneLayoutMap || sceneLayoutRoot == null)
            return;
        Vector2 playerPoint = SceneToMapPoint(GetPlayerMapPosition());
        Vector2 officePoint = SceneToMapPoint(worldOfficeTarget != null ? worldOfficeTarget.position : Vector3.zero);
        scenePlayerMarker.anchoredPosition = playerPoint;
        officeMarker.GetComponent<RectTransform>().anchoredPosition = officePoint;
        approximateArea.GetComponent<RectTransform>().anchoredPosition = officePoint;
        Vector2 difference = officePoint - playerPoint;
        RectTransform route = exactRoute.GetComponent<RectTransform>();
        route.sizeDelta = new Vector2(difference.magnitude, 5f);
        route.anchoredPosition = (playerPoint + officePoint) * 0.5f;
        route.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(difference.y, difference.x) * Mathf.Rad2Deg);
    }

    private void ClampPan()
    {
        float maxX = Mathf.Max(0f, (mapContent.rect.width * zoom - viewport.rect.width) * 0.5f);
        float maxY = Mathf.Max(0f, (mapContent.rect.height * zoom - viewport.rect.height) * 0.5f);
        Vector2 position = mapContent.anchoredPosition;
        position.x = Mathf.Clamp(position.x, -maxX, maxX);
        position.y = Mathf.Clamp(position.y, -maxY, maxY);
        mapContent.anchoredPosition = position;
    }

    private void BuildWorldMapIfNeeded()
    {
        if (!useWorldMap || mapContent == null || worldMapCamera != null)
            return;

        if (schematicRoot != null)
            schematicRoot.SetActive(false);

        GameObject cameraObject = new GameObject("Quest World Map Camera");
        worldMapCamera = cameraObject.AddComponent<Camera>();
        worldMapCamera.CopyFrom(worldMapTemplate);
        worldMapCamera.targetTexture = null;
        worldMapCamera.enabled = false;
        worldMapCamera.depth = -100f;
        worldMapCamera.transform.position = worldMapTemplate.transform.position;
        worldMapCamera.transform.rotation = worldMapTemplate.transform.rotation;

        worldMapTexture = new RenderTexture(1280, 800, 24, RenderTextureFormat.ARGB32)
        {
            name = "QuestWorldMapTexture",
            filterMode = FilterMode.Bilinear,
            useMipMap = false
        };
        worldMapTexture.Create();
        worldMapCamera.targetTexture = worldMapTexture;

        GameObject imageObject = new GameObject("Live Main Scene Map", typeof(RectTransform), typeof(RawImage));
        imageObject.transform.SetParent(mapContent, false);
        imageObject.transform.SetAsFirstSibling();
        Stretch(imageObject.GetComponent<RectTransform>());
        worldMapImage = imageObject.GetComponent<RawImage>();
        worldMapImage.texture = worldMapTexture;
        worldMapImage.color = Color.white;
        worldMapImage.raycastTarget = false;

        GameObject overlayObject = new GameObject("Live Quest Markers", typeof(RectTransform));
        overlayObject.transform.SetParent(mapContent, false);
        worldOverlayRoot = overlayObject.GetComponent<RectTransform>();
        Stretch(worldOverlayRoot);

        worldApproximateArea = Box("Live Approximate Office Area", worldOverlayRoot,
            new Vector2(230f, 170f), Vector2.zero, new Color(Amber.r, Amber.g, Amber.b, 0.16f));
        Border(worldApproximateArea, new Color(Amber.r, Amber.g, Amber.b, 0.92f));
        Text(worldApproximateArea, "Live Approximate Label", "VÙNG NGHI VẤN", 12f, Amber, FontStyles.Bold,
            TextAlignmentOptions.Top, new Vector2(0.5f, 1f), new Vector2(200f, 28f), new Vector2(0f, -14f));

        worldRoute = Box("Live Exact Route", worldOverlayRoot, new Vector2(100f, 7f), Vector2.zero, Purple);

        worldOfficeMarker = Box("Live Office Marker", worldOverlayRoot,
            new Vector2(126f, 72f), Vector2.zero, new Color(Purple.r, Purple.g, Purple.b, 0.82f));
        Border(worldOfficeMarker, Color.white);
        Text(worldOfficeMarker, "Live Office Label", "VĂN PHÒNG", 12f, Color.white, FontStyles.Bold,
            TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(116f, 60f), Vector2.zero);

        worldPlayerMarker = PlayerCircleMarker("Live Player Marker", worldOverlayRoot, 12f, Vector2.zero);

        Refresh();
    }

    private void UpdateWorldMapInput()
    {
        bool pointerInside = RectTransformUtility.RectangleContainsScreenPoint(viewport, Input.mousePosition);
        float scroll = pointerInside ? Input.GetAxis("Mouse ScrollWheel") : 0f;
        if (!Mathf.Approximately(scroll, 0f))
            worldMapCamera.orthographicSize = Mathf.Clamp(worldMapCamera.orthographicSize * (1f - scroll * 1.8f), 3f, 60f);

        if (pointerInside && Input.GetMouseButtonDown(0))
        {
            dragging = true;
            lastMousePosition = Input.mousePosition;
        }

        if (dragging && Input.GetMouseButton(0))
        {
            Vector3 delta = Input.mousePosition - lastMousePosition;
            float unitsPerPixel = (worldMapCamera.orthographicSize * 2f) / Mathf.Max(1f, viewport.rect.height);
            Vector3 position = worldMapCamera.transform.position;
            position -= new Vector3(delta.x * unitsPerPixel, delta.y * unitsPerPixel, 0f);
            worldMapCamera.transform.position = position;
            lastMousePosition = Input.mousePosition;
        }

        if (Input.GetMouseButtonUp(0))
            dragging = false;
    }

    private void FrameWorldMapForCurrentKnowledge()
    {
        Vector3 playerPosition = GetPlayerMapPosition();
        Vector3 focus = playerPosition;
        float size = 14f;

        bool knowsOffice = progress != null &&
            (progress.OfficeKnowledge == OfficeKnowledgeLevel.ExactLocation ||
             progress.OfficeKnowledge == OfficeKnowledgeLevel.Discovered);
        if (knowsOffice && worldOfficeTarget != null)
        {
            Vector3 officePosition = worldOfficeTarget.position;
            focus = (playerPosition + officePosition) * 0.5f;
            float horizontalSize = Mathf.Abs(officePosition.x - playerPosition.x) / (2f * 1.6f) + 5f;
            float verticalSize = Mathf.Abs(officePosition.y - playerPosition.y) * 0.5f + 5f;
            size = Mathf.Clamp(Mathf.Max(horizontalSize, verticalSize), 8f, 55f);
        }

        Vector3 cameraPosition = worldMapCamera.transform.position;
        cameraPosition.x = focus.x;
        cameraPosition.y = focus.y;
        worldMapCamera.transform.position = cameraPosition;
        worldMapCamera.orthographicSize = size;
    }

    private void UpdateWorldMapMarkers()
    {
        if (worldMapCamera == null || worldOverlayRoot == null)
            return;

        Vector2 playerPoint = WorldToMapPoint(GetPlayerMapPosition());
        worldPlayerMarker.anchoredPosition = playerPoint;

        if (worldOfficeTarget == null)
            return;

        Vector2 officePoint = WorldToMapPoint(worldOfficeTarget.position);
        worldOfficeMarker.anchoredPosition = officePoint;
        worldApproximateArea.anchoredPosition = officePoint;

        Vector2 difference = officePoint - playerPoint;
        worldRoute.sizeDelta = new Vector2(difference.magnitude, 7f);
        worldRoute.anchoredPosition = (playerPoint + officePoint) * 0.5f;
        worldRoute.localRotation = Quaternion.Euler(0f, 0f,
            Mathf.Atan2(difference.y, difference.x) * Mathf.Rad2Deg);
    }

    private Vector3 GetPlayerMapPosition()
    {
        if (worldPlayerTarget != null)
            return worldPlayerTarget.position;
        if (Camera.main != null)
            return Camera.main.transform.position;
        return Vector3.zero;
    }

    private Vector2 WorldToMapPoint(Vector3 worldPosition)
    {
        Vector3 viewportPoint = worldMapCamera.WorldToViewportPoint(worldPosition);
        return new Vector2(
            (viewportPoint.x - 0.5f) * worldOverlayRoot.rect.width,
            (viewportPoint.y - 0.5f) * worldOverlayRoot.rect.height);
    }

    private void OnDestroy()
    {
        if (worldMapCamera != null)
            worldMapCamera.targetTexture = null;
        if (worldMapTexture != null)
        {
            worldMapTexture.Release();
            if (Application.isPlaying) Destroy(worldMapTexture);
            else DestroyImmediate(worldMapTexture);
        }
        if (worldMapCamera != null)
        {
            if (Application.isPlaying) Destroy(worldMapCamera.gameObject);
            else DestroyImmediate(worldMapCamera.gameObject);
        }
    }

    private void Build(Transform canvasRoot)
    {
        root = new GameObject("Quest Map", typeof(RectTransform));
        root.transform.SetParent(canvasRoot, false);
        Stretch(root.GetComponent<RectTransform>());
        StretchBox("Map Dimmer", root.transform, new Color(0f, 0f, 0f, 0.84f));

        RectTransform shell = Box("Map Shell", root.transform, new Vector2(1540f, 860f), Vector2.zero, Ink);
        Border(shell, new Color(0.38f, 0.47f, 0.44f, 0.9f));
        Box("Map Top Accent", shell, new Vector2(1540f, 5f), new Vector2(0f, 427.5f), Amber);
        Text(shell, "Map Title", "BẢN ĐỒ KHU DÂN CƯ", 28f, Color.white, FontStyles.Bold,
            TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(720f, 46f), new Vector2(38f, -38f));
        Text(shell, "Map Subtitle", "DỮ LIỆU KHÁM PHÁ  //  BẢN ĐỒ LUÔN CÓ THỂ MỞ", 12f, Muted, FontStyles.Bold,
            TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(720f, 24f), new Vector2(40f, -72f));

        RectTransform close = Box("Map Close Hint", shell, new Vector2(160f, 44f), new Vector2(660f, 375f), Panel);
        Border(close, new Color(0.28f, 0.36f, 0.34f, 0.8f));
        Text(close, "Map Close Text", "[M]  ĐÓNG", 14f, Color.white, FontStyles.Bold,
            TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(150f, 38f), Vector2.zero);
        MakeClickable(close, () => SetOpen(false));

        viewport = Box("Map Viewport", shell, new Vector2(1120f, 700f), new Vector2(-185f, -45f),
            new Color(0.03f, 0.055f, 0.052f, 1f));
        Border(viewport, new Color(0.28f, 0.36f, 0.34f, 0.85f));
        viewport.gameObject.AddComponent<RectMask2D>();

        mapContent = Box("Map Content", viewport, new Vector2(1120f, 700f), Vector2.zero,
            new Color(0.045f, 0.075f, 0.07f, 1f));
        BuildUnlockReveal(viewport);
        schematicRoot = new GameObject("Schematic Map Fallback", typeof(RectTransform));
        schematicRoot.transform.SetParent(mapContent, false);
        Stretch(schematicRoot.GetComponent<RectTransform>());
        BuildMapGeometry(schematicRoot.transform);
        BuildWorldMapIfNeeded();

        RectTransform info = Box("Map Information", shell, new Vector2(330f, 700f), new Vector2(575f, -45f), Panel);
        Border(info, new Color(0.28f, 0.36f, 0.34f, 0.85f));
        Text(info, "Known Header", "THÔNG TIN ĐÃ BIẾT", 13f, Amber, FontStyles.Bold,
            TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(285f, 24f), new Vector2(22f, -25f));
        stateLabel = Text(info, "Knowledge State", string.Empty, 12f, Muted, FontStyles.Bold,
            TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(285f, 28f), new Vector2(22f, -61f));

        RectTransform known = Box("Known Locations", info, new Vector2(286f, 150f), new Vector2(0f, 135f),
            new Color(0.035f, 0.06f, 0.057f, 1f));
        Text(known, "Safehouse Known", "[01]  Nhà trú ẩn  •  Đã biết", 14f, Mint, FontStyles.Bold,
            TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(250f, 30f), new Vector2(16f, -18f));
        Text(known, "Neighborhood Known", "■  Khu dân cư  •  Đã biết", 14f, Color.white, FontStyles.Normal,
            TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(250f, 30f), new Vector2(16f, -60f));
        officeKnowledgeText = Text(known, "Office Knowledge", string.Empty, 13f, Muted, FontStyles.Normal,
            TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(255f, 45f), new Vector2(16f, -101f));

        Text(info, "Progress Header", "TIẾN ĐỘ MANH MỐI", 12f, Muted, FontStyles.Bold,
            TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(280f, 24f), new Vector2(22f, -320f));
        clueSummaryText = Text(info, "Clue Summary", string.Empty, 15f, Color.white, FontStyles.Normal,
            TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(280f, 78f), new Vector2(22f, -358f));

        RectTransform legend = Box("Map Legend", info, new Vector2(286f, 132f), new Vector2(0f, -140f),
            new Color(0.035f, 0.06f, 0.057f, 1f));
        Text(legend, "Legend", "ĐEN — khu vực tạm thời bị chặn\nCAM — vùng văn phòng tương đối\nTÍM — vị trí chính xác từ Mảnh 1\nXANH SÁNG — vị trí Player", 11f,
            Muted, FontStyles.Normal, TextAlignmentOptions.TopLeft, new Vector2(0f, 1f),
            new Vector2(252f, 92f), new Vector2(16f, -16f));

        rotationLabel = Text(info, "Map Rotation State", "HƯỚNG BẢN ĐỒ  0°", 11f, Mint,
            FontStyles.Bold, TextAlignmentOptions.BottomLeft, new Vector2(0f, 0f),
            new Vector2(285f, 24f), new Vector2(22f, 78f));
        rotationLabel.gameObject.SetActive(false);
        Text(info, "Map Controls", "CUỘN CHUỘT: ZOOM\nGIỮ CHUỘT TRÁI: KÉO BẢN ĐỒ", 11f, Muted,
            FontStyles.Bold, TextAlignmentOptions.BottomLeft, new Vector2(0f, 0f),
            new Vector2(285f, 48f), new Vector2(22f, 10f));
        UpdateRotationLabel();
    }

    private void BuildUnlockReveal(Transform parent)
    {
        unlockRevealRoot = new GameObject(
            "Map Unlock Reveal", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        unlockRevealRoot.transform.SetParent(parent, false);
        RectTransform revealRect = unlockRevealRoot.GetComponent<RectTransform>();
        Stretch(revealRect);
        unlockRevealDarkness = unlockRevealRoot.GetComponent<Image>();
        unlockRevealDarkness.color = new Color(0.008f, 0.006f, 0.016f, 0.96f);
        unlockRevealDarkness.raycastTarget = true;
        unlockRevealGroup = unlockRevealRoot.GetComponent<CanvasGroup>();
        unlockRevealGroup.alpha = 0f;
        unlockRevealGroup.interactable = false;
        unlockRevealGroup.blocksRaycasts = true;

        unlockRevealPulse = Box("Unlock Scan Pulse", revealRect, new Vector2(260f, 260f),
            new Vector2(0f, 18f), new Color(Purple.r, Purple.g, Purple.b, 0.035f));
        Border(unlockRevealPulse, new Color(0.86f, 0.68f, 1f, 0.96f));
        Box("Pulse Horizontal", unlockRevealPulse, new Vector2(330f, 2f), Vector2.zero,
            new Color(Purple.r, Purple.g, Purple.b, 0.62f));
        Box("Pulse Vertical", unlockRevealPulse, new Vector2(2f, 330f), Vector2.zero,
            new Color(Purple.r, Purple.g, Purple.b, 0.62f));

        unlockRevealCore = Box("Unlock Core", revealRect, new Vector2(82f, 82f),
            new Vector2(0f, 18f), new Color(Purple.r, Purple.g, Purple.b, 0.22f));
        Border(unlockRevealCore, Color.white);
        RectTransform diamond = Box("Unlock Diamond", unlockRevealCore, new Vector2(34f, 34f),
            Vector2.zero, Purple);
        diamond.localRotation = Quaternion.Euler(0f, 0f, 45f);

        unlockRevealTitle = Text(revealRect, "Unlock Reveal Title", "ĐANG GIẢI MÃ DỮ LIỆU BẢN ĐỒ",
            24f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center,
            new Vector2(0.5f, 0.5f), new Vector2(700f, 44f), new Vector2(0f, -166f));
        unlockRevealBody = Text(revealRect, "Unlock Reveal Body", "Đối chiếu ba tài liệu về vật tư và tuyến sơ tán...",
            14f, new Color(0.86f, 0.78f, 0.96f, 1f), FontStyles.Normal, TextAlignmentOptions.Center,
            new Vector2(0.5f, 0.5f), new Vector2(720f, 34f), new Vector2(0f, -205f));

        unlockRevealRoot.SetActive(false);
    }

    private void BuildMapGeometry(Transform parent)
    {
        RoadLine(parent, new Vector2(0f, -15f), new Vector2(1220f, 62f), 12f);
        RoadLine(parent, new Vector2(-210f, 35f), new Vector2(820f, 48f), 64f);
        RoadLine(parent, new Vector2(235f, 80f), new Vector2(700f, 44f), -52f);
        RoadLine(parent, new Vector2(210f, -160f), new Vector2(720f, 38f), 28f);

        for (int i = 0; i < 12; i++)
        {
            float x = -470f + (i % 4) * 215f;
            float y = 230f - (i / 4) * 210f;
            Vector2 size = new Vector2(110f + (i % 3) * 18f, 72f + (i % 2) * 14f);
            Box("Building " + i, parent, size, new Vector2(x, y),
                new Color(0.12f, 0.17f, 0.16f, 1f));
        }

        RectTransform home = Box("Safehouse Marker", parent, new Vector2(112f, 72f), new Vector2(-410f, -210f),
            new Color(Mint.r, Mint.g, Mint.b, 0.2f));
        Border(home, new Color(Mint.r, Mint.g, Mint.b, 0.8f));
        Text(home, "Safehouse Label", "NHÀ TRÚ ẨN", 11f, Color.white, FontStyles.Bold,
            TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(100f, 56f), Vector2.zero);

        PlayerCircleMarker("Player Marker", parent, 12f, new Vector2(-320f, -145f));
        Text(parent, "Player Label", "BẠN", 11f, Mint, FontStyles.Bold, TextAlignmentOptions.Center,
            new Vector2(0.5f, 0.5f), new Vector2(62f, 26f), new Vector2(-320f, -112f));

        approximateArea = new GameObject("Approximate Office Area", typeof(RectTransform));
        approximateArea.transform.SetParent(parent, false);
        SetRect(approximateArea.GetComponent<RectTransform>(), new Vector2(330f, 250f), new Vector2(325f, 120f));
        Image areaImage = approximateArea.AddComponent<Image>();
        areaImage.color = new Color(Amber.r, Amber.g, Amber.b, 0.12f);
        Border(approximateArea.GetComponent<RectTransform>(), new Color(Amber.r, Amber.g, Amber.b, 0.82f));
        Text(approximateArea.transform, "Approximate Label", "VÙNG NGHI VẤN", 12f, Amber, FontStyles.Bold,
            TextAlignmentOptions.Top, new Vector2(0.5f, 1f), new Vector2(270f, 30f), new Vector2(0f, -18f));

        unknownOfficeMarker = new GameObject("Unknown Office Marker", typeof(RectTransform));
        unknownOfficeMarker.transform.SetParent(parent, false);
        SetRect(unknownOfficeMarker.GetComponent<RectTransform>(), new Vector2(50f, 50f), new Vector2(325f, 120f));
        Image unknownImage = unknownOfficeMarker.AddComponent<Image>();
        unknownImage.color = new Color(Amber.r, Amber.g, Amber.b, 0.9f);
        Text(unknownOfficeMarker.transform, "Question Mark", "?", 27f, Ink, FontStyles.Bold,
            TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(44f, 44f), Vector2.zero);

        exactRoute = new GameObject("Exact Route", typeof(RectTransform));
        exactRoute.transform.SetParent(parent, false);
        RectTransform exactRouteRect = exactRoute.GetComponent<RectTransform>();
        SetRect(exactRouteRect, new Vector2(655f, 8f), new Vector2(5f, -12f));
        exactRouteRect.localRotation = Quaternion.Euler(0f, 0f, 28f);
        exactRoute.AddComponent<Image>().color = new Color(Purple.r, Purple.g, Purple.b, 0.9f);

        officeMarker = new GameObject("Exact Office Marker", typeof(RectTransform));
        officeMarker.transform.SetParent(parent, false);
        RectTransform office = Box("Office Body", officeMarker.transform, new Vector2(145f, 92f), Vector2.zero,
            new Color(Purple.r, Purple.g, Purple.b, 0.28f));
        Border(office, Purple);
        Text(office, "Office Label", "VĂN PHÒNG", 13f, Color.white, FontStyles.Bold,
            TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(132f, 70f), Vector2.zero);
        SetRect(officeMarker.GetComponent<RectTransform>(), new Vector2(145f, 92f), new Vector2(390f, 155f));

        // Fog remains at the outer edges even though the map can always be opened.
        Box("Fog North", parent, new Vector2(1120f, 90f), new Vector2(0f, 305f), new Color(0f, 0f, 0f, 0.42f));
        Box("Fog East", parent, new Vector2(90f, 700f), new Vector2(515f, 0f), new Color(0f, 0f, 0f, 0.42f));
    }

    private void RoadLine(Transform parent, Vector2 position, Vector2 size, float angle)
    {
        RectTransform road = Box("Road", parent, size, position, Road);
        road.localRotation = Quaternion.Euler(0f, 0f, angle);
        Box("Road Center", road, new Vector2(size.x, 2f), Vector2.zero,
            new Color(0.55f, 0.5f, 0.28f, 0.45f));
    }

    private TextMeshProUGUI Text(Transform parent, string name, string value, float size, Color color,
        FontStyles style, TextAlignmentOptions alignment, Vector2 anchor, Vector2 dimensions, Vector2 position)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.sizeDelta = dimensions;
        rect.anchoredPosition = position;
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.font = font;
        text.fontSize = size;
        text.color = color;
        text.fontStyle = style;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    private RectTransform Box(string name, Transform parent, Vector2 size, Vector2 position, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        SetRect(rect, size, position);
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    private RectTransform PlayerCircleMarker(string name, Transform parent, float diameter, Vector2 position)
    {
        RectTransform marker = Box(name, parent, new Vector2(diameter, diameter), position, Mint);
        Image image = marker.GetComponent<Image>();
        image.sprite = GetCircleMarkerSprite();
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        return marker;
    }

    private static Sprite GetCircleMarkerSprite()
    {
        if (circleMarkerSprite != null)
            return circleMarkerSprite;

        const int textureSize = 32;
        float center = (textureSize - 1) * 0.5f;
        float radius = center;
        Color[] pixels = new Color[textureSize * textureSize];

        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float alpha = Mathf.Clamp01(radius + 0.5f - distance);
                pixels[y * textureSize + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
        {
            name = "Runtime Circle Marker",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixels(pixels);
        texture.Apply(false, true);

        circleMarkerSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, textureSize, textureSize),
            new Vector2(0.5f, 0.5f),
            textureSize);
        circleMarkerSprite.name = "Runtime Circle Marker";
        circleMarkerSprite.hideFlags = HideFlags.HideAndDontSave;
        return circleMarkerSprite;
    }

    private RectTransform StretchBox(string name, Transform parent, Color color)
    {
        RectTransform rect = Box(name, parent, Vector2.zero, Vector2.zero, color);
        Stretch(rect);
        return rect;
    }

    private static Button MakeClickable(RectTransform target, Action action)
    {
        Image image = target.GetComponent<Image>();
        image.raycastTarget = true;
        Button button = target.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.82f);
        colors.pressedColor = new Color(0.68f, 0.68f, 0.68f, 1f);
        colors.selectedColor = Color.white;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        button.onClick.AddListener(() => action?.Invoke());
        return button;
    }

    private static void SetRect(RectTransform rect, Vector2 size, Vector2 position)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void Border(RectTransform rect, Color color)
    {
        const float thickness = 1f;
        BorderEdge(rect, "Border Top", color,
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0f, -thickness), Vector2.zero);
        BorderEdge(rect, "Border Bottom", color,
            new Vector2(0f, 0f), new Vector2(1f, 0f),
            Vector2.zero, new Vector2(0f, thickness));
        BorderEdge(rect, "Border Left", color,
            new Vector2(0f, 0f), new Vector2(0f, 1f),
            Vector2.zero, new Vector2(thickness, 0f));
        BorderEdge(rect, "Border Right", color,
            new Vector2(1f, 0f), new Vector2(1f, 1f),
            new Vector2(-thickness, 0f), Vector2.zero);
    }

    private static void BorderEdge(RectTransform parent, string name, Color color,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject edge = new GameObject(name, typeof(RectTransform), typeof(Image));
        edge.transform.SetParent(parent, false);
        RectTransform edgeRect = edge.GetComponent<RectTransform>();
        edgeRect.anchorMin = anchorMin;
        edgeRect.anchorMax = anchorMax;
        edgeRect.offsetMin = offsetMin;
        edgeRect.offsetMax = offsetMax;
        edge.GetComponent<Image>().color = color;
    }
}
