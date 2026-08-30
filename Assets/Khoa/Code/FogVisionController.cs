using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;

/// <summary>
/// Local renderer for the shared outdoor weather state.
/// DayNightManager.CurrentTime is networked by Fusion, so every client evaluates
/// the same fog density and drift without spawning or syncing particles.
/// </summary>
[DisallowMultipleComponent]
public class FogVisionController : MonoBehaviour
{
    public static FogVisionController Instance { get; private set; }

    [Header("Outdoor weather (00:00 / 09:00 / 12:00 / 17:00 / 19:30 / 24:00)")]
    [Tooltip("Global fog coverage. 09:00-17:00 is clear enough to navigate; midnight is capped at the old 19:30 density.")]
    public AnimationCurve outdoorFogDensity = new AnimationCurve(
        new Keyframe(0f, 0.80f),
        new Keyframe(9f / 24f, 0.28f),
        new Keyframe(12f / 24f, 0.22f),
        new Keyframe(17f / 24f, 0.30f),
        new Keyframe(19.5f / 24f, 0.55f),
        new Keyframe(1f, 0.80f));

    [Tooltip("Actual Player vision multiplier outside. Night fog adds danger without making the Player completely blind.")]
    public AnimationCurve outdoorVisionMultiplier = new AnimationCurve(
        new Keyframe(0f, 0.70f),
        new Keyframe(9f / 24f, 0.93f),
        new Keyframe(12f / 24f, 0.97f),
        new Keyframe(17f / 24f, 0.93f),
        new Keyframe(19.5f / 24f, 0.80f),
        new Keyframe(1f, 0.70f));

    [Tooltip("Stable seed shared by all clients. Change only to change the world's fog pattern.")]
    public Vector2 fogSeed = new Vector2(17.31f, 41.73f);

    [Range(0f, 1f)]
    [Tooltip("How much the soft awareness bubble can thin outdoor fog around the Player. It never clears a cone.")]
    public float playerBubbleClearance = 0.58f;

    [Range(0.5f, 2f)]
    [Tooltip("Visual bubble size relative to the Player's true vision radius.")]
    public float playerBubbleRadiusMultiplier = 1.08f;
    [Range(0f, 1f), Tooltip("How strongly an active flashlight thins the night fog inside its soft cone.")]
    public float flashlightFogClearance = 0.85f;
    [Range(0f, 1f), Tooltip("Very subtle fog tint only. Actual environment illumination comes from the Player Light2D.")]
    public float flashlightIllumination = 0.08f;

    [Header("Indoor visibility")]
    [Range(0f, 1f)] public float indoorAmbientOpacity = 0.88f;
    [Range(0f, 1f)] public float indoorExteriorOpacity = 0.94f;
    [Range(0f, 1f), Tooltip("Softly reveals the immediate exterior near an indoor Player so doors and exits remain navigable.")]
    public float indoorExitAwarenessClearance = 0.50f;
    [Min(0.25f)] public float indoorExitAwarenessRadius = 2.5f;
    [Range(0f, 1f), Tooltip("How much a flashlight can open the indoor exterior mask through a doorway.")]
    public float indoorExteriorFlashlightClearance = 0.78f;
    [Range(0f, 0.25f)] public float visionEdgeSoftness = 0.10f;
    [Header("Indoor wall occlusion")]
    [Range(64, 180), Tooltip("Number of local physics rays used to clip indoor fog/light against this building's walls.")]
    public int indoorOcclusionRayCount = 180;
    [Range(0.03f, 0.25f)] public float indoorOcclusionEdgeSoftness = 0.08f;
    [Range(0.9f, 1f), Tooltip("Minimum fog cover behind a structural wall, including leaked Light2D illumination.")]
    public float indoorWallOccludedOpacity = 1f;
    [Range(0.25f, 3f), Tooltip("Maximum gap between an indoor trigger edge and its authored DoorBlocker portal.")]
    public float indoorPortalAssociationPadding = 1.25f;
    [Header("World line of sight")]
    [Range(64, 180), Tooltip("World-space LOS samples used by the FOW mask outside buildings.")]
    public int lineOfSightRayCount = 180;
    [Range(0.03f, 0.25f)] public float lineOfSightEdgeSoftness = 0.08f;
    [Range(0.9f, 1f), Tooltip("Fog opacity behind an obstacle that blocks world LOS.")]
    public float lineOfSightBlockedOpacity = 1f;
    [Range(5f, 30f), Tooltip("How often the local indoor visibility fan is rebuilt. This is visual-only and never networked.")]
    public float indoorOcclusionRefreshRate = 15f;
    public Color fogColor = new Color(0.72f, 0.75f, 0.77f, 1f);
    public Color indoorAmbientColor = new Color(0.025f, 0.03f, 0.04f, 1f);
    public Color indoorExteriorColor = new Color(0.008f, 0.01f, 0.014f, 1f);

    public float CurrentFogDensity { get; private set; }
    public float CurrentVisionMultiplier { get; private set; }
    public bool IsQuestSearchBoundaryActive { get; private set; }

    private static readonly int FogColorId = Shader.PropertyToID("_FogColor");
    private static readonly int FogDensityId = Shader.PropertyToID("_FogDensity");
    private static readonly int FogDayPhaseId = Shader.PropertyToID("_FogDayPhase");
    private static readonly int FogSeedId = Shader.PropertyToID("_FogSeed");
    private static readonly int PlayerBubbleClearanceId = Shader.PropertyToID("_PlayerBubbleClearance");
    private static readonly int PlayerBubbleRadiusId = Shader.PropertyToID("_PlayerBubbleRadius");
    private static readonly int VisionWorldCenterId = Shader.PropertyToID("_VisionWorldCenter");
    private static readonly int VisionDirectionId = Shader.PropertyToID("_VisionDirection");
    private static readonly int VisionCosHalfAngleId = Shader.PropertyToID("_VisionCosHalfAngle");
    private static readonly int VisionEdgeSoftnessId = Shader.PropertyToID("_VisionEdgeSoftness");
    private static readonly int IndoorActiveId = Shader.PropertyToID("_IndoorActive");
    private static readonly int IndoorPointCountId = Shader.PropertyToID("_IndoorPointCount");
    private static readonly int IndoorPointsId = Shader.PropertyToID("_IndoorPoints");
    private static readonly int IndoorBoundsId = Shader.PropertyToID("_IndoorBounds");
    private static readonly int IndoorAmbientOpacityId = Shader.PropertyToID("_IndoorAmbientOpacity");
    private static readonly int IndoorExteriorOpacityId = Shader.PropertyToID("_IndoorExteriorOpacity");
    private static readonly int IndoorAmbientColorId = Shader.PropertyToID("_IndoorAmbientColor");
    private static readonly int IndoorExteriorColorId = Shader.PropertyToID("_IndoorExteriorColor");
    private static readonly int IndoorExitAwarenessClearanceId = Shader.PropertyToID("_IndoorExitAwarenessClearance");
    private static readonly int IndoorExitAwarenessRadiusId = Shader.PropertyToID("_IndoorExitAwarenessRadius");
    private static readonly int IndoorExteriorFlashlightClearanceId = Shader.PropertyToID("_IndoorExteriorFlashlightClearance");
    private static readonly int IndoorOcclusionActiveId = Shader.PropertyToID("_IndoorOcclusionActive");
    private static readonly int IndoorOcclusionRayCountId = Shader.PropertyToID("_IndoorOcclusionRayCount");
    private static readonly int IndoorOcclusionDistancesId = Shader.PropertyToID("_IndoorOcclusionDistances");
    private static readonly int IndoorPortalDistancesId = Shader.PropertyToID("_IndoorPortalDistances");
    private static readonly int IndoorOcclusionEdgeSoftnessId = Shader.PropertyToID("_IndoorOcclusionEdgeSoftness");
    private static readonly int IndoorWallOccludedOpacityId = Shader.PropertyToID("_IndoorWallOccludedOpacity");
    private static readonly int LineOfSightActiveId = Shader.PropertyToID("_LineOfSightActive");
    private static readonly int LineOfSightRayCountId = Shader.PropertyToID("_LineOfSightRayCount");
    private static readonly int LineOfSightDistancesId = Shader.PropertyToID("_LineOfSightDistances");
    private static readonly int LineOfSightEdgeSoftnessId = Shader.PropertyToID("_LineOfSightEdgeSoftness");
    private static readonly int LineOfSightBlockedOpacityId = Shader.PropertyToID("_LineOfSightBlockedOpacity");
    private static readonly int FogWorldBottomLeftId = Shader.PropertyToID("_FogWorldBottomLeft");
    private static readonly int FogWorldRightId = Shader.PropertyToID("_FogWorldRight");
    private static readonly int FogWorldUpId = Shader.PropertyToID("_FogWorldUp");
    private static readonly int FogBankTextureId = Shader.PropertyToID("_FogBankTex");
    private static readonly int FlashlightActiveId = Shader.PropertyToID("_FlashlightActive");
    private static readonly int FlashlightClearanceId = Shader.PropertyToID("_FlashlightClearance");
    private static readonly int FlashlightRadiusId = Shader.PropertyToID("_FlashlightRadius");
    private static readonly int FlashlightIlluminationId = Shader.PropertyToID("_FlashlightIllumination");
    private static readonly int QuestBoundaryActiveId = Shader.PropertyToID("_QuestBoundaryActive");
    private static readonly int QuestBoundaryOriginId = Shader.PropertyToID("_QuestBoundaryOrigin");
    private static readonly int QuestBoundaryRightId = Shader.PropertyToID("_QuestBoundaryRight");
    private static readonly int QuestBoundaryUpId = Shader.PropertyToID("_QuestBoundaryUp");
    private static readonly int QuestBoundaryFadeId = Shader.PropertyToID("_QuestBoundaryFade");
    private static readonly int QuestBoundaryOpacityId = Shader.PropertyToID("_QuestBoundaryOpacity");

    private Camera worldCamera;
    private GameObject overlayRoot;
    private RawImage overlayImage;
    private Material overlayMaterial;
    private Texture2D fogBankTexture;
    private PlayerVision targetVision;
    private Transform cinematicVisionTarget;
    private PlayerVision cinematicVisionSource;
    private Vector2 cinematicVisionDirection = Vector2.down;
    private PlayerMovement targetMovement;
    private Transform tutorialRevealTarget;
    private float tutorialRevealRadius;
    private readonly Vector4[] indoorPoints = new Vector4[32];
    private readonly List<Vector2> polygonPoints = new List<Vector2>(32);
    private const int MaxIndoorOcclusionRays = 180;
    private readonly float[] indoorOcclusionDistances = new float[MaxIndoorOcclusionRays];
    private readonly float[] indoorPortalDistances = new float[MaxIndoorOcclusionRays];
    private readonly float[] lineOfSightDistances = new float[MaxIndoorOcclusionRays];
    private readonly List<RaycastHit2D> indoorOcclusionHits = new List<RaycastHit2D>(32);
    private readonly List<RaycastHit2D> lineOfSightHits = new List<RaycastHit2D>(32);
    private readonly List<Collider2D> indoorPortalCandidates = new List<Collider2D>(8);
    private readonly List<Bounds> openPortalBounds = new List<Bounds>(8);
    private ContactFilter2D indoorObstacleFilter;
    private Collider2D cachedIndoorCollider;
    private Transform cachedIndoorStructureRoot;
    private Collider2D cachedPortalIndoorCollider;
    private Transform cachedPortalStructureRoot;
    private Vector2 lastOcclusionOrigin;
    private float nextIndoorOcclusionUpdate;
    private Vector2 lastLineOfSightOrigin;
    private float nextLineOfSightUpdate;
    private int configuredObstacleMask;
    private Vector2 questBoundaryOrigin;
    private Vector2 questBoundaryRight;
    private Vector2 questBoundaryUp;
    private float questBoundaryFade = 3f;
    private float questBoundaryOpacity = 0.96f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        worldCamera = GetComponent<Camera>();
        if (worldCamera == null) worldCamera = GetComponentInChildren<Camera>();
        fogBankTexture = Resources.Load<Texture2D>("Fog/FogBankDensity");
        indoorObstacleFilter = new ContactFilter2D();
        indoorObstacleFilter.useLayerMask = true;
        indoorObstacleFilter.useTriggers = false;
        ConfigureObstacleFilter(LayerMask.GetMask("Obstacle"));
        if (fogBankTexture == null)
        {
            Debug.LogError("[FogVision] Missing Resources/Fog/FogBankDensity texture.");
        }
        else
        {
            fogBankTexture.wrapMode = TextureWrapMode.Repeat;
            fogBankTexture.filterMode = FilterMode.Bilinear;
        }
        CreateOverlay();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (overlayRoot != null) Destroy(overlayRoot);
        if (overlayMaterial != null) Destroy(overlayMaterial);
    }

    private void LateUpdate()
    {
        if (worldCamera == null || overlayMaterial == null || overlayImage == null) return;

        ResolveCameraTarget();
        PlayerHealth targetHealth = targetVision != null ? targetVision.GetComponent<PlayerHealth>() : null;
        if (targetVision == null || targetMovement == null ||
            targetVision.Object == null || !targetVision.Object.IsValid ||
            targetMovement.Object == null || !targetMovement.Object.IsValid ||
            (targetHealth != null && (targetHealth.isDead || targetHealth.isTransforming)))
        {
            targetVision = null;
            targetMovement = null;
            overlayImage.enabled = false;
            return;
        }

        overlayImage.enabled = true;
        UpdateWeatherState();
        UpdateMaterial();
    }

    public float GetOutdoorVisionMultiplier()
    {
        UpdateWeatherState();
        return CurrentVisionMultiplier;
    }

    public void SetTutorialCinematicReveal(Transform revealTarget, float radius = 3.5f)
    {
        tutorialRevealTarget = revealTarget;
        tutorialRevealRadius = Mathf.Max(0.1f, radius);
    }

    public void ClearTutorialCinematicReveal()
    {
        tutorialRevealTarget = null;
        tutorialRevealRadius = 0f;
    }

    public void SetMilitaryCinematicVision(PlayerVision source, Transform visualTarget, Vector2 direction)
    {
        cinematicVisionSource = source;
        cinematicVisionTarget = visualTarget;
        cinematicVisionDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.down;
        targetVision = null;
        targetMovement = null;
    }

    public void UpdateMilitaryCinematicVisionDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude > 0.001f) cinematicVisionDirection = direction.normalized;
    }

    public void ClearMilitaryCinematicVision()
    {
        cinematicVisionTarget = null;
        cinematicVisionSource = null;
        targetVision = null;
        targetMovement = null;
    }

    /// <summary>
    /// Defines the client-local story search area as a world-space parallelogram.
    /// Fog outside the area is visual guidance only; server authority performs
    /// any position correction for the individual player who crossed it.
    /// </summary>
    public void SetQuestSearchBoundary(Vector2 origin, Vector2 right, Vector2 up,
        float fadeDistance, float outsideOpacity)
    {
        questBoundaryOrigin = origin;
        questBoundaryRight = right;
        questBoundaryUp = up;
        questBoundaryFade = Mathf.Max(0.1f, fadeDistance);
        questBoundaryOpacity = Mathf.Clamp01(outsideOpacity);
        IsQuestSearchBoundaryActive = Mathf.Abs(Cross(right, up)) > 0.001f;
    }

    public void ClearQuestSearchBoundary()
    {
        IsQuestSearchBoundaryActive = false;
    }

    private void UpdateWeatherState()
    {
        float dayPhase = GetDayPhase();
        CurrentFogDensity = Mathf.Clamp01(outdoorFogDensity.Evaluate(dayPhase));
        CurrentVisionMultiplier = Mathf.Clamp(outdoorVisionMultiplier.Evaluate(dayPhase), 0.05f, 1f);
    }

    private float GetDayPhase()
    {
        return DayNightManager.Instance != null
            ? Mathf.Repeat(DayNightManager.Instance.GetTimePercent(), 1f)
            : 0.5f;
    }

    private void CreateOverlay()
    {
        Shader shader = Shader.Find("ProjectZomboid/FogVisionOverlay");
        if (shader == null)
        {
            Debug.LogError("[FogVision] Missing ProjectZomboid/FogVisionOverlay shader.");
            enabled = false;
            return;
        }

        overlayMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
        overlayRoot = new GameObject("Local Fog Vision Overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasRenderer), typeof(RawImage));
        overlayRoot.hideFlags = HideFlags.DontSave;

        Canvas canvas = overlayRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = worldCamera;
        canvas.planeDistance = 0.5f;
        canvas.overrideSorting = true;
        canvas.sortingLayerName = "Foreground";
        canvas.sortingOrder = 32767;

        RectTransform rect = overlayRoot.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        overlayImage = overlayRoot.GetComponent<RawImage>();
        overlayImage.texture = Texture2D.whiteTexture;
        overlayImage.color = Color.white;
        overlayImage.raycastTarget = false;
        overlayImage.material = overlayMaterial;
    }

    private void ResolveCameraTarget()
    {
        if (cinematicVisionTarget != null && cinematicVisionSource != null)
        {
            targetVision = cinematicVisionSource;
            targetMovement = cinematicVisionSource.GetComponent<PlayerMovement>();
            return;
        }

        Transform cameraTarget = PZ_CameraController.Instance != null ? PZ_CameraController.Instance.CurrentTarget : null;
        if (cameraTarget == null && TutorialSession.IsActive)
        {
            IntroCameraFollow introCamera = FindFirstObjectByType<IntroCameraFollow>();
            cameraTarget = introCamera != null ? introCamera.CurrentTarget : null;
        }
        if (cameraTarget == null)
        {
            targetVision = null;
            targetMovement = null;
            return;
        }

        if (targetVision != null && targetVision.transform == cameraTarget) return;

        targetVision = cameraTarget.GetComponentInParent<PlayerVision>();
        if (targetVision == null) targetVision = cameraTarget.GetComponentInChildren<PlayerVision>();
        targetMovement = cameraTarget.GetComponentInParent<PlayerMovement>();
        if (targetMovement == null) targetMovement = cameraTarget.GetComponentInChildren<PlayerMovement>();

        // While riding, the camera follows the vehicle. Keep the local
        // player's vision service as the fog source, but let that service
        // publish the vehicle's origin/direction/radius instead of the body.
        if (targetVision == null && cameraTarget.GetComponentInParent<VehicleControllerFusion>() != null &&
            PlayerMovement.LocalPlayerInstance != null)
        {
            targetMovement = PlayerMovement.LocalPlayerInstance;
            targetVision = targetMovement.GetComponent<PlayerVision>();
        }
    }

    private void UpdateMaterial()
    {
        Vector2 lineOfSightOrigin = targetVision.LineOfSightOrigin;
        Vector3 playerPosition = cinematicVisionTarget != null
            ? cinematicVisionTarget.position
            : (Vector3)lineOfSightOrigin;
        float fogPlaneDistance = Mathf.Abs(playerPosition.z - worldCamera.transform.position.z);
        Vector3 fogWorldBottomLeft = worldCamera.ViewportToWorldPoint(new Vector3(0f, 0f, fogPlaneDistance));
        Vector3 fogWorldRight = worldCamera.ViewportToWorldPoint(new Vector3(1f, 0f, fogPlaneDistance)) - fogWorldBottomLeft;
        Vector3 fogWorldUp = worldCamera.ViewportToWorldPoint(new Vector3(0f, 1f, fogPlaneDistance)) - fogWorldBottomLeft;

        Vector2 lookDirection = cinematicVisionTarget != null
            ? cinematicVisionDirection
            : targetVision.LineOfSightDirection;

        ConfigureObstacleFilter(targetVision.VisionObstacleLayer);

        bool isTutorialReveal = tutorialRevealTarget != null;
        Vector3 visionCenter = isTutorialReveal ? tutorialRevealTarget.position : playerPosition;
        bool isIndoor = !isTutorialReveal && cinematicVisionTarget == null &&
                        targetVision.ActiveIndoorCollider != null;
        int indoorPointCount = isIndoor ? BuildIndoorWorldPolygon(targetVision.ActiveIndoorCollider) : 0;
        isIndoor &= indoorPointCount >= 3;
        float cameraRayDistance = fogWorldRight.magnitude + fogWorldUp.magnitude + 1f;
        bool lineOfSightActive = !isTutorialReveal && cinematicVisionTarget == null && !isIndoor &&
                                 UpdateOutdoorLineOfSight(lineOfSightOrigin, cameraRayDistance);
        bool indoorOcclusionActive = isIndoor && UpdateIndoorOcclusion(
            targetVision.ActiveIndoorCollider, visionCenter, cameraRayDistance);

        float nightBlend = DayNightManager.Instance != null
            ? DayNightManager.EvaluateNightBlend(DayNightManager.Instance.CurrentTime)
            : 0f;
        Color nightFogColor = new Color(0.075f, 0.105f, 0.17f, fogColor.a);
        overlayMaterial.SetColor(FogColorId, Color.Lerp(fogColor, nightFogColor, nightBlend * 0.78f));
        overlayMaterial.SetFloat(FogDensityId, CurrentFogDensity);
        // Day phase comes from Fusion's networked clock. The shader never uses local _Time.
        overlayMaterial.SetFloat(FogDayPhaseId, GetDayPhase());
        overlayMaterial.SetVector(FogSeedId, fogSeed);
        overlayMaterial.SetFloat(PlayerBubbleClearanceId, isTutorialReveal ? 0.92f : playerBubbleClearance);
        overlayMaterial.SetFloat(PlayerBubbleRadiusId, isTutorialReveal ? tutorialRevealRadius : Mathf.Max(targetVision.LineOfSightRadius * playerBubbleRadiusMultiplier, 0.05f));
        overlayMaterial.SetVector(VisionWorldCenterId, new Vector2(visionCenter.x, visionCenter.y));
        overlayMaterial.SetVector(VisionDirectionId, lookDirection);
        overlayMaterial.SetFloat(VisionCosHalfAngleId, Mathf.Cos(targetVision.CurrentVisionAngle * 0.5f * Mathf.Deg2Rad));
        overlayMaterial.SetFloat(VisionEdgeSoftnessId, visionEdgeSoftness);
        overlayMaterial.SetFloat(FlashlightActiveId, !isTutorialReveal && targetVision.IsFlashlightActive ? 1f : 0f);
        overlayMaterial.SetFloat(FlashlightClearanceId, flashlightFogClearance);
        overlayMaterial.SetFloat(FlashlightRadiusId, Mathf.Max(targetVision.CurrentVisionRadius, 0.05f));
        overlayMaterial.SetFloat(FlashlightIlluminationId, flashlightIllumination);
        overlayMaterial.SetFloat(IndoorActiveId, isIndoor ? 1f : 0f);
        overlayMaterial.SetFloat(IndoorPointCountId, indoorPointCount);
        overlayMaterial.SetVectorArray(IndoorPointsId, indoorPoints);
        float effectiveIndoorAmbient = Mathf.Lerp(0.35f, indoorAmbientOpacity, nightBlend);
        overlayMaterial.SetFloat(IndoorAmbientOpacityId, effectiveIndoorAmbient);
        overlayMaterial.SetFloat(IndoorExteriorOpacityId, indoorExteriorOpacity);
        overlayMaterial.SetColor(IndoorAmbientColorId, indoorAmbientColor);
        overlayMaterial.SetColor(IndoorExteriorColorId, indoorExteriorColor);
        overlayMaterial.SetFloat(IndoorExitAwarenessClearanceId, indoorExitAwarenessClearance);
        overlayMaterial.SetFloat(IndoorExitAwarenessRadiusId, indoorExitAwarenessRadius);
        overlayMaterial.SetFloat(IndoorExteriorFlashlightClearanceId, indoorExteriorFlashlightClearance);
        overlayMaterial.SetFloat(IndoorOcclusionActiveId, indoorOcclusionActive ? 1f : 0f);
        overlayMaterial.SetFloat(IndoorOcclusionRayCountId,
            indoorOcclusionActive ? Mathf.Clamp(indoorOcclusionRayCount, 64, MaxIndoorOcclusionRays) : 0f);
        overlayMaterial.SetFloatArray(IndoorOcclusionDistancesId, indoorOcclusionDistances);
        overlayMaterial.SetFloatArray(IndoorPortalDistancesId, indoorPortalDistances);
        overlayMaterial.SetFloat(IndoorOcclusionEdgeSoftnessId, indoorOcclusionEdgeSoftness);
        overlayMaterial.SetFloat(IndoorWallOccludedOpacityId, indoorWallOccludedOpacity);
        overlayMaterial.SetFloat(LineOfSightActiveId, lineOfSightActive ? 1f : 0f);
        overlayMaterial.SetFloat(LineOfSightRayCountId,
            lineOfSightActive ? Mathf.Clamp(lineOfSightRayCount, 64, MaxIndoorOcclusionRays) : 0f);
        overlayMaterial.SetFloatArray(LineOfSightDistancesId, lineOfSightDistances);
        overlayMaterial.SetFloat(LineOfSightEdgeSoftnessId, lineOfSightEdgeSoftness);
        overlayMaterial.SetFloat(LineOfSightBlockedOpacityId, lineOfSightBlockedOpacity);
        overlayMaterial.SetFloat(QuestBoundaryActiveId, IsQuestSearchBoundaryActive ? 1f : 0f);
        overlayMaterial.SetVector(QuestBoundaryOriginId, questBoundaryOrigin);
        overlayMaterial.SetVector(QuestBoundaryRightId, questBoundaryRight);
        overlayMaterial.SetVector(QuestBoundaryUpId, questBoundaryUp);
        overlayMaterial.SetFloat(QuestBoundaryFadeId, questBoundaryFade);
        overlayMaterial.SetFloat(QuestBoundaryOpacityId, questBoundaryOpacity);
        overlayMaterial.SetVector(FogWorldBottomLeftId, fogWorldBottomLeft);
        overlayMaterial.SetVector(FogWorldRightId, fogWorldRight);
        overlayMaterial.SetVector(FogWorldUpId, fogWorldUp);
        overlayMaterial.SetTexture(FogBankTextureId, fogBankTexture);
    }

    private static float Cross(Vector2 left, Vector2 right)
    {
        return left.x * right.y - left.y * right.x;
    }

    private void ConfigureObstacleFilter(LayerMask requestedMask)
    {
        int fallbackMask = LayerMask.GetMask("Obstacle");
        int mask = requestedMask.value != 0 ? requestedMask.value : fallbackMask;
        if (configuredObstacleMask == mask && indoorObstacleFilter.useLayerMask)
            return;

        indoorObstacleFilter.useLayerMask = true;
        indoorObstacleFilter.useTriggers = false;
        indoorObstacleFilter.SetLayerMask(mask);
        configuredObstacleMask = mask;
    }

    private bool UpdateOutdoorLineOfSight(Vector2 origin, float maxDistance)
    {
        float updateInterval = 1f / Mathf.Max(5f, indoorOcclusionRefreshRate);
        if (Time.unscaledTime < nextLineOfSightUpdate &&
            Vector2.SqrMagnitude(origin - lastLineOfSightOrigin) < 0.0025f)
            return true;

        nextLineOfSightUpdate = Time.unscaledTime + updateInterval;
        lastLineOfSightOrigin = origin;
        int rayCount = Mathf.Clamp(lineOfSightRayCount, 64, MaxIndoorOcclusionRays);
        float safeMaxDistance = Mathf.Max(1f, maxDistance);

        for (int rayIndex = 0; rayIndex < rayCount; rayIndex++)
        {
            float angle = rayIndex * Mathf.PI * 2f / rayCount;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            lineOfSightDistances[rayIndex] = VisionLineOfSight.FindNearestBlockingDistance(
                origin, direction, safeMaxDistance, indoorObstacleFilter, lineOfSightHits);
        }

        for (int rayIndex = rayCount; rayIndex < MaxIndoorOcclusionRays; rayIndex++)
            lineOfSightDistances[rayIndex] = safeMaxDistance;

        return true;
    }

    private bool UpdateIndoorOcclusion(Collider2D indoorCollider, Vector2 origin, float maxDistance)
    {
        if (indoorCollider == null) return false;

        if (cachedIndoorCollider != indoorCollider)
        {
            cachedIndoorCollider = indoorCollider;
            cachedIndoorStructureRoot = ResolveIndoorStructureRoot(indoorCollider);
            nextIndoorOcclusionUpdate = 0f;
        }

        if (cachedIndoorStructureRoot == null) return false;

        float updateInterval = 1f / Mathf.Max(5f, indoorOcclusionRefreshRate);
        if (Time.unscaledTime < nextIndoorOcclusionUpdate &&
            Vector2.SqrMagnitude(origin - lastOcclusionOrigin) < 0.0025f)
            return true;

        nextIndoorOcclusionUpdate = Time.unscaledTime + updateInterval;
        lastOcclusionOrigin = origin;
        int rayCount = Mathf.Clamp(indoorOcclusionRayCount, 64, MaxIndoorOcclusionRays);
        float safeMaxDistance = Mathf.Max(1f, maxDistance);
        float outdoorPortalMaxDistance = targetVision != null
            ? Mathf.Min(safeMaxDistance, Mathf.Clamp(targetVision.CurrentVisionRadius, 4.5f, 7.5f))
            : Mathf.Min(safeMaxDistance, 5.5f);

        int openPortalCount = FindOpenIndoorPortals(indoorCollider, cachedIndoorStructureRoot,
            indoorPortalAssociationPadding, openPortalResults, openPortalBounds);

        for (int rayIndex = 0; rayIndex < rayCount; rayIndex++)
        {
            float angle = rayIndex * Mathf.PI * 2f / rayCount;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Physics2D.Raycast(origin, direction, indoorObstacleFilter, indoorOcclusionHits, safeMaxDistance);

            float nearestStructureHit = float.MaxValue;
            float nearestAnyObstacle = outdoorPortalMaxDistance;

            for (int hitIndex = 0; hitIndex < indoorOcclusionHits.Count; hitIndex++)
            {
                RaycastHit2D hit = indoorOcclusionHits[hitIndex];
                if (!VisionLineOfSight.IsBlocking(hit.collider)) continue;

                float hitDist = Mathf.Max(0f, hit.distance);
                nearestAnyObstacle = Mathf.Min(nearestAnyObstacle, hitDist);

                if (VisionLineOfSight.IsBlocking(hit.collider, cachedIndoorStructureRoot))
                {
                    nearestStructureHit = Mathf.Min(nearestStructureHit, hitDist);
                }
            }

            indoorOcclusionDistances[rayIndex] = nearestStructureHit < safeMaxDistance
                ? nearestStructureHit
                : safeMaxDistance;

            if (openPortalCount > 0)
            {
                indoorPortalDistances[rayIndex] = FindOpenPortalDistance(
                    origin, direction, nearestStructureHit, nearestAnyObstacle,
                    safeMaxDistance, outdoorPortalMaxDistance, openPortalBounds);
            }
            else
            {
                indoorPortalDistances[rayIndex] = 0f;
            }
        }

        for (int rayIndex = rayCount; rayIndex < MaxIndoorOcclusionRays; rayIndex++)
        {
            indoorOcclusionDistances[rayIndex] = safeMaxDistance;
            indoorPortalDistances[rayIndex] = 0f;
        }

        return true;
    }

    private readonly List<Collider2D> openPortalResults = new List<Collider2D>(8);

    private int FindOpenIndoorPortals(Collider2D indoorCollider, Transform structureRoot,
        float associationPadding, List<Collider2D> results, List<Bounds> boundsResults)
    {
        results.Clear();
        boundsResults.Clear();
        if (indoorCollider == null) return 0;

        if (cachedPortalIndoorCollider != indoorCollider || cachedPortalStructureRoot != structureRoot)
            RefreshIndoorPortalCandidates(indoorCollider, structureRoot, associationPadding);

        for (int i = 0; i < indoorPortalCandidates.Count; i++)
        {
            Collider2D col = indoorPortalCandidates[i];
            if (col == null) continue;

            if ((!col.enabled || !col.gameObject.activeInHierarchy) &&
                IsPortalAssociatedWithIndoor(indoorCollider, col, associationPadding) &&
                TryGetPortalWorldBounds(col, out Bounds portalBounds))
            {
                results.Add(col);
                boundsResults.Add(portalBounds);
            }
        }

        return results.Count;
    }

    private void RefreshIndoorPortalCandidates(Collider2D indoorCollider, Transform structureRoot,
        float associationPadding)
    {
        cachedPortalIndoorCollider = indoorCollider;
        cachedPortalStructureRoot = structureRoot;
        indoorPortalCandidates.Clear();

        // Some imported/fixed buildings keep the roof/indoor trigger and the
        // interactive DoorBlocker in sibling branches. Search only the
        // smallest common authored group, then use the indoor trigger's own
        // geometry to associate the doorway. This prevents a nearby house's
        // open door from becoming a portal for the current room.
        Transform searchRoot = structureRoot != null && structureRoot.parent != null
            ? structureRoot.parent
            : structureRoot;
        Collider2D[] allColliders = searchRoot != null
            ? searchRoot.GetComponentsInChildren<Collider2D>(true)
            : System.Array.Empty<Collider2D>();
        for (int i = 0; i < allColliders.Length; i++)
            AddIndoorPortalCandidate(allColliders[i]);

        bool hasAssociatedCandidate = false;
        for (int i = 0; i < indoorPortalCandidates.Count; i++)
        {
            if (IsPortalAssociatedWithIndoor(indoorCollider, indoorPortalCandidates[i], associationPadding))
            {
                hasAssociatedCandidate = true;
                break;
            }
        }

        // Main's hospital has its DoorBlocker beside a stripped prefab branch,
        // so the parent search can legitimately find no associated candidate.
        // Fall back to the loaded scene only in that case, and retain only
        // DoorBlockers whose own geometry touches this indoor trigger. The
        // candidate list is cached until the active indoor area changes.
        if (!hasAssociatedCandidate)
        {
            Collider2D[] sceneColliders = FindObjectsByType<Collider2D>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < sceneColliders.Length; i++)
            {
                Collider2D candidate = sceneColliders[i];
                if (!IsPortalAssociatedWithIndoor(indoorCollider, candidate, associationPadding)) continue;
                AddIndoorPortalCandidate(candidate);
            }
        }
    }

    private void AddIndoorPortalCandidate(Collider2D candidate)
    {
        if (candidate == null) return;

        bool isDoorBlocker = candidate.name.Equals("DoorBlocker", System.StringComparison.OrdinalIgnoreCase) ||
                             candidate.name.EndsWith("DoorBlocker", System.StringComparison.OrdinalIgnoreCase);
        if (!isDoorBlocker || indoorPortalCandidates.Contains(candidate)) return;

        indoorPortalCandidates.Add(candidate);
    }

    private static bool IsPortalAssociatedWithIndoor(Collider2D indoorCollider, Collider2D portal,
        float associationPadding)
    {
        if (indoorCollider == null || portal == null) return false;

        // Disabled DoorBlockers can report an empty runtime bounds. Use the
        // authored collider shape so an offset doorway still associates with
        // the correct indoor trigger after the blocker is turned off.
        if (!TryGetPortalWorldBounds(portal, out Bounds portalWorldBounds)) return false;

        Vector2 portalPoint = portalWorldBounds.center;
        if (indoorCollider.OverlapPoint(portalPoint)) return true;

        Vector2 nearestIndoorPoint = indoorCollider.ClosestPoint(portalPoint);
        return Vector2.Distance(nearestIndoorPoint, portalPoint) <= Mathf.Max(0.1f, associationPadding);
    }

    private static float FindOpenPortalDistance(Vector2 origin, Vector2 direction,
        float nearestStructureHit, float nearestAnyObstacle, float safeMaxDistance,
        float outdoorPortalMaxDistance, List<Bounds> portalBounds)
    {
        float bestDistance = 0f;
        Ray ray = new Ray(origin, direction);

        for (int i = 0; i < portalBounds.Count; i++)
        {
            Bounds b = portalBounds[i];
            b.Expand(0.1f);
            if (!b.IntersectRay(ray, out float portalEntryDistance)) continue;
            if (portalEntryDistance <= 0.01f || portalEntryDistance >= safeMaxDistance) continue;

            // A portal is usable only when no structural or unrelated obstacle
            // is in front of its aperture. A no-hit ray by itself is never an
            // opening.
            if (nearestStructureHit < portalEntryDistance - 0.05f) continue;
            if (nearestAnyObstacle < portalEntryDistance - 0.05f) continue;

            float portalEndDistance = Mathf.Min(outdoorPortalMaxDistance, nearestAnyObstacle);
            if (nearestStructureHit < safeMaxDistance)
                portalEndDistance = Mathf.Min(portalEndDistance, nearestStructureHit);

            if (portalEndDistance > portalEntryDistance + 0.05f)
                bestDistance = Mathf.Max(bestDistance, portalEndDistance);
        }

        return bestDistance;
    }

    private static bool TryGetPortalWorldBounds(Collider2D portal, out Bounds worldBounds)
    {
        worldBounds = default;
        if (portal == null) return false;

        Bounds liveBounds = portal.bounds;
        if (liveBounds.size.x > 0.0001f && liveBounds.size.y > 0.0001f)
        {
            worldBounds = liveBounds;
            return true;
        }

        Transform portalTransform = portal.transform;
        if (portal is BoxCollider2D box)
        {
            Vector2 halfSize = box.size * 0.5f;
            Vector2 center = box.offset;
            Bounds bounds = new Bounds(
                portalTransform.TransformPoint(center + new Vector2(-halfSize.x, -halfSize.y)),
                Vector3.zero);
            bounds.Encapsulate(portalTransform.TransformPoint(center + new Vector2(-halfSize.x, halfSize.y)));
            bounds.Encapsulate(portalTransform.TransformPoint(center + new Vector2(halfSize.x, -halfSize.y)));
            bounds.Encapsulate(portalTransform.TransformPoint(center + new Vector2(halfSize.x, halfSize.y)));
            worldBounds = bounds;
            return bounds.size.x > 0.0001f && bounds.size.y > 0.0001f;
        }

        if (portal is CircleCollider2D circle)
        {
            const int sampleCount = 16;
            Bounds bounds = new Bounds(
                portalTransform.TransformPoint(circle.offset + Vector2.right * circle.radius),
                Vector3.zero);
            for (int i = 1; i < sampleCount; i++)
            {
                float angle = i * Mathf.PI * 2f / sampleCount;
                Vector2 point = circle.offset + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * circle.radius;
                bounds.Encapsulate(portalTransform.TransformPoint(point));
            }
            worldBounds = bounds;
            return bounds.size.x > 0.0001f && bounds.size.y > 0.0001f;
        }

        if (portal is CapsuleCollider2D capsule)
        {
            Vector2 halfSize = capsule.size * 0.5f;
            Vector2 center = capsule.offset;
            Bounds bounds = new Bounds(
                portalTransform.TransformPoint(center + new Vector2(-halfSize.x, -halfSize.y)),
                Vector3.zero);
            bounds.Encapsulate(portalTransform.TransformPoint(center + new Vector2(-halfSize.x, halfSize.y)));
            bounds.Encapsulate(portalTransform.TransformPoint(center + new Vector2(halfSize.x, -halfSize.y)));
            bounds.Encapsulate(portalTransform.TransformPoint(center + new Vector2(halfSize.x, halfSize.y)));
            worldBounds = bounds;
            return bounds.size.x > 0.0001f && bounds.size.y > 0.0001f;
        }

        if (portal is PolygonCollider2D polygon)
        {
            bool hasPoint = false;
            Bounds bounds = default;
            List<Vector2> path = new List<Vector2>();
            for (int pathIndex = 0; pathIndex < polygon.pathCount; pathIndex++)
            {
                path.Clear();
                polygon.GetPath(pathIndex, path);
                for (int pointIndex = 0; pointIndex < path.Count; pointIndex++)
                {
                    Vector3 worldPoint = portalTransform.TransformPoint(path[pointIndex]);
                    if (!hasPoint)
                    {
                        bounds = new Bounds(worldPoint, Vector3.zero);
                        hasPoint = true;
                    }
                    else
                    {
                        bounds.Encapsulate(worldPoint);
                    }
                }
            }

            if (hasPoint)
            {
                worldBounds = bounds;
                return bounds.size.x > 0.0001f && bounds.size.y > 0.0001f;
            }
        }

        return false;
    }

    private static Transform ResolveIndoorStructureRoot(Collider2D indoorCollider)
    {
        RoofVisibility roof = indoorCollider.GetComponentInParent<RoofVisibility>();
        if (roof != null)
        {
            // The scene stores many buildings as siblings under Map. Returning
            // roof.transform.parent here therefore promotes a single house to
            // the whole Map and makes the ray fan hit unrelated buildings.
            // Use the smallest authored group containing both the indoor
            // trigger and the roof controller so only this structure's walls
            // can clip the indoor visibility fan.
            Transform commonRoot = FindCommonAncestor(indoorCollider.transform, roof.transform);
            if (commonRoot != null) return commonRoot;
        }

        IndoorVisionArea indoorArea = indoorCollider.GetComponentInParent<IndoorVisionArea>();
        if (indoorArea != null)
        {
            Transform commonRoot = FindCommonAncestor(indoorCollider.transform, indoorArea.transform);
            if (commonRoot != null) return commonRoot;
        }

        // Legacy Main houses use a trigger Tilemap named "nocnha". Its direct
        // parent is the smallest authored structure group available at runtime.
        return indoorCollider.transform.parent != null
            ? indoorCollider.transform.parent
            : indoorCollider.transform;
    }

    private static Transform FindCommonAncestor(Transform first, Transform second)
    {
        if (first == null || second == null) return null;

        Transform candidate = first;
        while (candidate != null)
        {
            if (candidate == second || second.IsChildOf(candidate)) return candidate;
            candidate = candidate.parent;
        }

        return null;
    }

    private int BuildIndoorWorldPolygon(Collider2D indoorCollider)
    {
        if (indoorCollider == null)
        {
            overlayMaterial.SetVector(IndoorBoundsId, Vector4.zero);
            return 0;
        }

        Bounds bounds = indoorCollider.bounds;
        overlayMaterial.SetVector(IndoorBoundsId, new Vector4(bounds.min.x, bounds.min.y, bounds.max.x, bounds.max.y));

        if (indoorCollider is PolygonCollider2D polygon && polygon.pathCount >= 1)
        {
            polygonPoints.Clear();
            polygon.GetPath(0, polygonPoints);
            int total = polygonPoints.Count;
            if (total >= 3)
            {
                int count = Mathf.Min(total, indoorPoints.Length);
                if (total <= indoorPoints.Length)
                {
                    for (int i = 0; i < count; i++)
                    {
                        Vector3 worldPoint = polygon.transform.TransformPoint(polygonPoints[i] + polygon.offset);
                        indoorPoints[i] = new Vector4(worldPoint.x, worldPoint.y, 0f, 0f);
                    }
                }
                else
                {
                    for (int i = 0; i < indoorPoints.Length; i++)
                    {
                        int srcIndex = Mathf.FloorToInt(i * (float)total / indoorPoints.Length);
                        Vector3 worldPoint = polygon.transform.TransformPoint(polygonPoints[srcIndex] + polygon.offset);
                        indoorPoints[i] = new Vector4(worldPoint.x, worldPoint.y, 0f, 0f);
                    }
                    count = indoorPoints.Length;
                }
                return count;
            }
        }

        if (indoorCollider is BoxCollider2D box)
        {
            Vector2 halfSize = box.size * 0.5f;
            SetIndoorPoint(0, box.transform.TransformPoint(box.offset + new Vector2(-halfSize.x, -halfSize.y)));
            SetIndoorPoint(1, box.transform.TransformPoint(box.offset + new Vector2(-halfSize.x, halfSize.y)));
            SetIndoorPoint(2, box.transform.TransformPoint(box.offset + new Vector2(halfSize.x, halfSize.y)));
            SetIndoorPoint(3, box.transform.TransformPoint(box.offset + new Vector2(halfSize.x, -halfSize.y)));
            return 4;
        }

        SetIndoorPoint(0, new Vector3(bounds.min.x, bounds.min.y, indoorCollider.transform.position.z));
        SetIndoorPoint(1, new Vector3(bounds.min.x, bounds.max.y, indoorCollider.transform.position.z));
        SetIndoorPoint(2, new Vector3(bounds.max.x, bounds.max.y, indoorCollider.transform.position.z));
        SetIndoorPoint(3, new Vector3(bounds.max.x, bounds.min.y, indoorCollider.transform.position.z));
        return 4;
    }

    private void SetIndoorPoint(int index, Vector3 worldPoint)
    {
        indoorPoints[index] = new Vector4(worldPoint.x, worldPoint.y, 0f, 0f);
    }
}
