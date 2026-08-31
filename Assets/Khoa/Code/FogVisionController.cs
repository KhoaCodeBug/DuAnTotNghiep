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
    private static readonly Unity.Profiling.ProfilerMarker RenderMarker = new Unity.Profiling.ProfilerMarker("FogVision.UpdateMaterial");
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
    public float flashlightFogClearance = 0.11f;
    [Range(0f, 1f), Tooltip("Very subtle fog tint only. Actual environment illumination comes from the Player Light2D.")]
    public float flashlightIllumination = 0.06f;

    [Header("Indoor visibility")]
    [Range(0f, 1f)] public float indoorAmbientOpacity = 0.10f;
    [Range(0f, 1f)] public float indoorExteriorOpacity = 0.88f;
    [Range(0f, 1f), Tooltip("Softly reveals the immediate exterior near an indoor Player so doors and exits remain navigable.")]
    public float indoorExitAwarenessClearance = 0.32f;
    [Min(0.25f)] public float indoorExitAwarenessRadius = 2.4f;
    [Range(0f, 1f), Tooltip("How much a flashlight can open the indoor exterior mask through a doorway.")]
    public float indoorExteriorFlashlightClearance = 0.68f;
    [Range(0f, 0.25f)] public float visionEdgeSoftness = 0.12f;
    [Header("Indoor wall occlusion")]
    [Range(64, 180), Tooltip("Number of local physics rays used to clip indoor fog/light against this building's walls.")]
    public int indoorOcclusionRayCount = 180;
    [Range(0.03f, 0.25f)] public float indoorOcclusionEdgeSoftness = 0.10f;
    [Range(0.9f, 1f), Tooltip("Minimum fog cover behind a structural wall, including leaked Light2D illumination.")]
    public float indoorWallOccludedOpacity = 0.98f;
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
    private static readonly int IndoorOcclusionOriginId = Shader.PropertyToID("_IndoorOcclusionOrigin");
    private static readonly int IndoorOcclusionEdgeSoftnessId = Shader.PropertyToID("_IndoorOcclusionEdgeSoftness");
    private static readonly int IndoorWallOccludedOpacityId = Shader.PropertyToID("_IndoorWallOccludedOpacity");
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
    private readonly Vector4[] indoorPoints = new Vector4[16];
    private readonly List<Vector2> polygonPoints = new List<Vector2>(16);
    private const int MaxIndoorOcclusionRays = 180;
    private readonly float[] indoorOcclusionDistances = new float[MaxIndoorOcclusionRays];
    private readonly List<RaycastHit2D> indoorOcclusionHits = new List<RaycastHit2D>(32);
    private ContactFilter2D indoorObstacleFilter;
    private Collider2D cachedIndoorCollider;
    private Transform cachedIndoorStructureRoot;
    private Vector2 lastOcclusionOrigin;
    private float nextIndoorOcclusionUpdate;
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
        indoorObstacleFilter.SetLayerMask(LayerMask.GetMask("Obstacle"));
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
        using (RenderMarker.Auto()) UpdateMaterial();
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
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = -1000;

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
        Vector3 playerPosition = cinematicVisionTarget != null
            ? cinematicVisionTarget.position
            : (Vector3)targetVision.VisionWorldPosition;
        float fogPlaneDistance = Mathf.Abs(playerPosition.z - worldCamera.transform.position.z);
        Vector3 fogWorldBottomLeft = worldCamera.ViewportToWorldPoint(new Vector3(0f, 0f, fogPlaneDistance));
        Vector3 fogWorldRight = worldCamera.ViewportToWorldPoint(new Vector3(1f, 0f, fogPlaneDistance)) - fogWorldBottomLeft;
        Vector3 fogWorldUp = worldCamera.ViewportToWorldPoint(new Vector3(0f, 1f, fogPlaneDistance)) - fogWorldBottomLeft;

        Vector2 lookDirection = cinematicVisionTarget != null
            ? cinematicVisionDirection
            : targetVision.VisionWorldDirection;

        bool isTutorialReveal = tutorialRevealTarget != null;
        Vector3 visionCenter = isTutorialReveal ? tutorialRevealTarget.position : playerPosition;
        bool isIndoor = !isTutorialReveal && cinematicVisionTarget == null &&
                        targetVision.ActiveIndoorCollider != null;
        int indoorPointCount = isIndoor ? BuildIndoorWorldPolygon(targetVision.ActiveIndoorCollider) : 0;
        isIndoor &= indoorPointCount >= 3;
        float cameraRayDistance = fogWorldRight.magnitude + fogWorldUp.magnitude + 1f;
        bool indoorOcclusionActive = isIndoor && UpdateIndoorOcclusion(
            targetVision.ActiveIndoorCollider, visionCenter, cameraRayDistance);

        float nightBlend = DayNightManager.Instance != null
            ? DayNightManager.EvaluateNightBlend(DayNightManager.Instance.CurrentTime)
            : 0f;
        // Explicit opt-in only: unconfigured interiors retain their accepted renderer.
        IndoorFogSurfaceMap surfaceMap = isIndoor
            ? targetVision.ActiveIndoorCollider.GetComponentInParent<IndoorFogSurfaceMap>() : null;
        bool useSurfaceMap = surfaceMap != null && surfaceMap.indoorVolume == targetVision.ActiveIndoorCollider && surfaceMap.EnsureAtlas();
        overlayMaterial.SetFloat("_IndoorSurfaceActive", useSurfaceMap ? 1f : 0f);
        if (useSurfaceMap)
        {
            overlayMaterial.SetTexture("_IndoorSurfaceAtlas", surfaceMap.Atlas);
            overlayMaterial.SetVector("_IndoorSurfaceBounds", surfaceMap.AtlasBounds);
            overlayMaterial.SetFloat("_IndoorSurfaceProbe", surfaceMap.surfaceProbeInset);
            overlayMaterial.SetVector("_IndoorSurfaceLighting", new Vector4(
                Mathf.Lerp(surfaceMap.dayAmbientOpacity, surfaceMap.nightAmbientOpacity, nightBlend),
                surfaceMap.litOpacity, surfaceMap.coneInset, 0f));
        }
        Color nightFogColor = new Color(0.075f, 0.105f, 0.17f, fogColor.a);
        overlayMaterial.SetColor(FogColorId, Color.Lerp(fogColor, nightFogColor, nightBlend * 0.78f));
        overlayMaterial.SetFloat(FogDensityId, CurrentFogDensity);
        // Day phase comes from Fusion's networked clock. The shader never uses local _Time.
        overlayMaterial.SetFloat(FogDayPhaseId, GetDayPhase());
        overlayMaterial.SetVector(FogSeedId, fogSeed);
        overlayMaterial.SetFloat(PlayerBubbleClearanceId, isTutorialReveal ? 0.92f : playerBubbleClearance);
        overlayMaterial.SetFloat(PlayerBubbleRadiusId, isTutorialReveal ? tutorialRevealRadius : Mathf.Max(targetVision.AmbientVisionRadius * playerBubbleRadiusMultiplier, 0.05f));
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
        overlayMaterial.SetFloat(IndoorAmbientOpacityId, indoorAmbientOpacity);
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
        overlayMaterial.SetVector(IndoorOcclusionOriginId, lastOcclusionOrigin);
        overlayMaterial.SetFloat(IndoorOcclusionEdgeSoftnessId, indoorOcclusionEdgeSoftness);
        overlayMaterial.SetFloat(IndoorWallOccludedOpacityId, indoorWallOccludedOpacity);
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

    private bool UpdateIndoorOcclusion(Collider2D indoorCollider, Vector2 origin, float maxDistance)
    {
        if (indoorCollider == null) return false;

        if (cachedIndoorCollider != indoorCollider)
        {
            cachedIndoorCollider = indoorCollider;
            cachedIndoorStructureRoot = ResolveIndoorStructureRoot(indoorCollider);
            nextIndoorOcclusionUpdate = 0f;
        }

        float updateInterval = 1f / Mathf.Max(5f, indoorOcclusionRefreshRate);
        if (Time.unscaledTime < nextIndoorOcclusionUpdate &&
            Vector2.SqrMagnitude(origin - lastOcclusionOrigin) < 0.0025f)
            return true;

        nextIndoorOcclusionUpdate = Time.unscaledTime + updateInterval;
        lastOcclusionOrigin = origin;
        int rayCount = Mathf.Clamp(indoorOcclusionRayCount, 64, MaxIndoorOcclusionRays);
        float safeMaxDistance = Mathf.Max(1f, maxDistance);

        for (int rayIndex = 0; rayIndex < rayCount; rayIndex++)
        {
            float angle = rayIndex * Mathf.PI * 2f / rayCount;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Physics2D.Raycast(origin, direction, indoorObstacleFilter, indoorOcclusionHits, safeMaxDistance);

            float nearestStructureHit = safeMaxDistance;
            for (int hitIndex = 0; hitIndex < indoorOcclusionHits.Count; hitIndex++)
            {
                RaycastHit2D hit = indoorOcclusionHits[hitIndex];
                if (!IsIndoorStructuralHit(indoorCollider, cachedIndoorStructureRoot,
                        direction, hit))
                    continue;

                nearestStructureHit = Mathf.Min(nearestStructureHit, Mathf.Max(0f, hit.distance));
            }

            indoorOcclusionDistances[rayIndex] = nearestStructureHit;
        }

        for (int rayIndex = rayCount; rayIndex < MaxIndoorOcclusionRays; rayIndex++)
            indoorOcclusionDistances[rayIndex] = safeMaxDistance;

        return true;
    }

    private static bool IsIndoorStructuralHit(Collider2D indoorCollider, Transform structureRoot,
        Vector2 rayDirection, RaycastHit2D hit)
    {
        if (indoorCollider == null || hit.collider == null) return false;

        // Small houses usually keep their wall colliders below the same authored
        // root as the roof trigger. Preserve that fast and exact path.
        if (structureRoot != null && hit.collider.transform.IsChildOf(structureRoot))
            return true;

        // Large authored sites (Hospital/School) keep roof triggers and wall
        // Tilemaps in separate hierarchy branches. A hierarchy-only test therefore
        // ignored every wall in those buildings. Geometry is the canonical link:
        // the point immediately before a wall hit must still be inside the active
        // indoor volume. A fence reached after the ray has left the building fails
        // this test and does not black out the flashlight.
        Bounds indoorBounds = indoorCollider.bounds;
        indoorBounds.Expand(0.5f);
        if (!indoorBounds.Contains(hit.point)) return false;

        Vector2 direction = rayDirection.sqrMagnitude > 0.0001f
            ? rayDirection.normalized
            : Vector2.right;
        const float insideProbeDistance = 0.08f;
        Vector2 pointBeforeWall = hit.point - direction * insideProbeDistance;
        return indoorCollider.OverlapPoint(pointBeforeWall) || indoorCollider.OverlapPoint(hit.point);
    }

    private static Transform ResolveIndoorStructureRoot(Collider2D indoorCollider)
    {
        RoofVisibility roof = indoorCollider.GetComponentInParent<RoofVisibility>();
        if (roof != null) return roof.transform;

        IndoorVisionArea indoorArea = indoorCollider.GetComponentInParent<IndoorVisionArea>();
        if (indoorArea != null) return indoorArea.transform;

        // Legacy Main houses use a trigger Tilemap named "nocnha". Its direct
        // parent is the smallest authored structure group available at runtime.
        return indoorCollider.transform.parent != null
            ? indoorCollider.transform.parent
            : indoorCollider.transform;
    }

    private int BuildIndoorWorldPolygon(Collider2D indoorCollider)
    {
        if (indoorCollider is PolygonCollider2D polygon && polygon.pathCount == 1)
        {
            polygonPoints.Clear();
            polygon.GetPath(0, polygonPoints);
            int count = Mathf.Min(polygonPoints.Count, indoorPoints.Length);
            for (int i = 0; i < count; i++)
            {
                Vector3 worldPoint = polygon.transform.TransformPoint(polygonPoints[i] + polygon.offset);
                indoorPoints[i] = new Vector4(worldPoint.x, worldPoint.y, 0f, 0f);
            }
            return count;
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

        Bounds bounds = indoorCollider.bounds;
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
