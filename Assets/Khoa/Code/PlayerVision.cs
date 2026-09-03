using UnityEngine;
using UnityEngine.Rendering.Universal;
using Fusion;
using System.Collections.Generic;

public class PlayerVision : NetworkBehaviour
{
    [Header("=== Ánh sáng của Player ===")]
    public Light2D playerLight;

    [Header("=== Cài đặt Tầm Nhìn (Ngày/Đêm) ===")]
    public AnimationCurve radiusCurve;
    public AnimationCurve intensityCurve;

    [Header("=== ĐÈN PIN: NGUỒN SÁNG THẬT ===")]
    [Range(0.1f, 2f)] public float flashlightWorldIntensity = 0.85f;
    [Range(0f, 1f)] public float flashlightFalloffIntensity = 0.82f;
    [Range(0f, 0.5f)] public float flashlightInnerRadiusRatio = 0.16f;
    [Range(1f, 20f)] public float flashlightLightTransitionSpeed = 10f;
    public Color flashlightWorldColor = new Color(1f, 0.91f, 0.72f, 1f);

    [Header("=== Cài đặt Ngắm Bắn (Chuột Phải) ===")]
    public float normalInnerAngle = 100f;
    public float normalOuterAngle = 140f;
    public float aimInnerAngle = 80f;
    public float aimOuterAngle = 120f;
    public float aimTransitionSpeed = 8f;

    [Header("=== FOG OF WAR (Tầm nhìn thực tế) ===")]
    public LayerMask zombieLayer;
    public LayerMask obstacleLayer;

    [Tooltip("Khoảng cách cảm nhận 360 độ quanh Player. Chỉ bỏ LOS nội bộ khi Player và zombie cùng một indoor area ổn định.")]
    public float passiveVisionRadius = 1.5f;

    [Range(0.05f, 1f)]
    [Tooltip("Thời gian zombie chuyển từ mờ sang rõ hoặc ngược lại.")]
    public float zombieVisibilityFadeDuration = 0.25f;

    [Range(0f, 0.75f)]
    [Tooltip("Alpha khởi đầu khi zombie vừa đi vào vùng cảm nhận.")]
    public float zombieAwarenessInitialAlpha = 0.18f;

    [Header("=== ÁNH SÁNG TRONG NHÀ ===")]
    [Range(0f, 1f)]
    [Tooltip("Ánh sáng mắt người trong nhà. Đèn pin sau này sẽ dùng mức sáng mạnh riêng.")]
    public float indoorLightIntensityMultiplier = 0.12f;

    [Header("=== HIỂN THỊ PLAYER CỤC BỘ ===")]
    [Range(0.2f, 1f)]
    [Tooltip("Độ sáng của Player do người chơi điều khiển. Không chịu Global Light hoặc hướng của nón nhìn.")]
    public float localPlayerReadableBrightness = 0.88f;

    [Range(0.05f, 0.8f)]
    [Tooltip("Độ đậm silhouette X-Ray local-only, luôn vẽ trên vật thể đang che Player.")]
    public float localPlayerXRayAlpha = 0.32f;

    public Color localPlayerXRayColor = new Color(0.55f, 0.92f, 1f, 1f);

    private Collider2D[] zombiesInRadius = new Collider2D[100];
    private ContactFilter2D zombieFilter;
    private ContactFilter2D obstacleFilter;
    private readonly RaycastHit2D[] sightObstacleHits = new RaycastHit2D[16];
    private PlayerMovement pMove;
    private PlayerHealth playerHealth;
    private PlayerInteraction playerInteraction;
    private RoofDetector roofDetector;
    private FlashlightController flashlightController;
    private SpriteRenderer playerBodyRenderer;
    private Material originalPlayerMaterial;
    private Color originalPlayerColor;
    private Material localPlayerUnlitMaterial;
    private SpriteRenderer localPlayerXRayRenderer;
    private readonly Dictionary<SpriteRenderer, float> zombieOriginalAlphas = new Dictionary<SpriteRenderer, float>();
    private readonly Dictionary<SpriteRenderer, float> nearAwarenessMaskAlphas = new Dictionary<SpriteRenderer, float>();
    private readonly HashSet<SpriteRenderer> nearAwarenessUpdated = new HashSet<SpriteRenderer>();
    internal const int MaxNearAwarenessFogMasks = 16;

    public float CurrentVisionRadius { get; private set; }
    public float AmbientVisionRadius { get; private set; }
    public float CurrentVisionAngle { get; private set; }
    public VehicleControllerFusion CurrentVisionVehicle => playerInteraction != null && playerInteraction.IsInVehicle
        ? playerInteraction.CurrentVehicleController
        : null;
    public bool IsUsingVehicleVision => CurrentVisionVehicle != null;
    public Vector2 VisionWorldPosition => IsUsingVehicleVision
        ? CurrentVisionVehicle.VisionOrigin
        : (Vector2)transform.position;
    public Vector2 VisionWorldDirection
    {
        get
        {
            if (IsUsingVehicleVision) return CurrentVisionVehicle.VisionDirection;
            Vector2 direction = pMove != null ? pMove.NetLastLookDir : (Vector2)transform.up;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.up;
        }
    }
    public Collider2D ActiveIndoorCollider => !IsUsingVehicleVision && roofDetector != null
        ? roofDetector.CurrentIndoorCollider
        : null;
    public bool IsFlashlightActive => flashlightController != null && flashlightController.IsFlashlightActive;

    private void Awake()
    {
        // Setup filter để quét mảng
        zombieFilter = new ContactFilter2D();
        zombieFilter.useLayerMask = true;
        zombieFilter.useTriggers = false; // Tối ưu: bỏ qua các collider dạng trigger
        zombieFilter.SetLayerMask(zombieLayer);
        obstacleFilter = new ContactFilter2D();
        obstacleFilter.useLayerMask = true;
        obstacleFilter.useTriggers = false;
        obstacleFilter.SetLayerMask(obstacleLayer);

        pMove = GetComponent<PlayerMovement>();
        playerHealth = GetComponent<PlayerHealth>();
        playerInteraction = GetComponent<PlayerInteraction>();
        roofDetector = GetComponentInChildren<RoofDetector>();
        flashlightController = GetComponent<FlashlightController>();
        ConfigureLightForWorldEnvironment();
        SetupLocalPlayerReadability();
    }

    private void ConfigureLightForWorldEnvironment()
    {
        if (playerLight == null) return;

        // The old prefab only targeted Default + Player, so most map and prop
        // sprites on Gameplay/Foreground/Background could never receive this
        // light. Keep the list future-proof when new sorting layers are added.
        SortingLayer[] sortingLayers = SortingLayer.layers;
        int[] sortingLayerIds = new int[sortingLayers.Length];
        for (int i = 0; i < sortingLayers.Length; i++)
            sortingLayerIds[i] = sortingLayers[i].id;

        playerLight.targetSortingLayers = sortingLayerIds;
        playerLight.shadowsEnabled = true;
        playerLight.shadowSoftness = 0.82f;
    }

    private void SetupLocalPlayerReadability()
    {
        // The player body's renderer lives on the root in both Player prefabs.
        // Fallback keeps this resilient if a future prefab moves it to a child.
        playerBodyRenderer = GetComponent<SpriteRenderer>();
        if (playerBodyRenderer == null)
            playerBodyRenderer = GetComponentInChildren<SpriteRenderer>();

        if (playerBodyRenderer == null) return;

        originalPlayerMaterial = playerBodyRenderer.sharedMaterial;
        originalPlayerColor = playerBodyRenderer.color;

        Shader unlitShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (unlitShader == null)
            unlitShader = Shader.Find("Sprites/Default");

        if (unlitShader == null)
        {
            Debug.LogWarning("[PlayerVision] No unlit sprite shader found; local Player readability fallback is disabled.");
            return;
        }

        localPlayerUnlitMaterial = new Material(unlitShader)
        {
            name = "Local Player Readability (Runtime)",
            hideFlags = HideFlags.DontSave
        };

        GameObject xrayObject = new GameObject("LocalPlayerXRaySilhouette");
        xrayObject.hideFlags = HideFlags.DontSave;
        xrayObject.transform.SetParent(playerBodyRenderer.transform, false);
        localPlayerXRayRenderer = xrayObject.AddComponent<SpriteRenderer>();
        localPlayerXRayRenderer.sharedMaterial = localPlayerUnlitMaterial;
        localPlayerXRayRenderer.enabled = false;

        SortingLayer[] layers = SortingLayer.layers;
        int frontLayerId = playerBodyRenderer.sortingLayerID;
        int frontLayerValue = int.MinValue;
        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i].value <= frontLayerValue) continue;
            frontLayerValue = layers[i].value;
            frontLayerId = layers[i].id;
        }

        localPlayerXRayRenderer.sortingLayerID = frontLayerId;
        localPlayerXRayRenderer.sortingOrder = short.MaxValue;
        localPlayerXRayRenderer.spriteSortPoint = playerBodyRenderer.spriteSortPoint;
    }


    public override void Spawned()
    {
        base.Spawned();

        if (playerLight != null)
        {
            playerLight.gameObject.SetActive(HasInputAuthority);
        }
    }

    private void Update()
    {
        if (playerHealth != null && playerHealth.Object != null && playerHealth.Object.IsValid &&
            (playerHealth.isDead || playerHealth.isTransforming))
        {
            SetLocalPlayerReadability(false);
            if (playerLight != null) playerLight.gameObject.SetActive(false);
            return;
        }

        bool isTarget = false;
        if (PZ_CameraController.Instance != null && PZ_CameraController.Instance.isSpectatingMode)
        {
            Transform camTarget = PZ_CameraController.Instance.CurrentTarget;
            if (camTarget != null)
            {
                isTarget = (camTarget == transform || camTarget.IsChildOf(transform) || transform.IsChildOf(camTarget));
            }
        }
        else
        {
            isTarget = HasInputAuthority;
        }

        bool useVehicleVision = IsUsingVehicleVision;
        SetLocalPlayerReadability(isTarget && !useVehicleVision);

        if (playerLight != null)
        {
            playerLight.gameObject.SetActive(isTarget && !useVehicleVision);
        }

        if (!isTarget)
        {
            ClearNearAwarenessMasksImmediate();
            return;
        }

        if (pMove == null) return;

        if (useVehicleVision)
        {
            CurrentVisionRadius = CurrentVisionVehicle.VisionRadius;
            AmbientVisionRadius = CurrentVisionRadius;
            CurrentVisionAngle = CurrentVisionVehicle.VisionAngle;
            UpdateZombieVisibility(CurrentVisionAngle);
            return;
        }

        if (playerLight == null) return;

        // 1. ÁNH SÁNG NGÀY ĐÊM
        if (DayNightManager.Instance != null)
        {
            float timePercent = DayNightManager.Instance.GetTimePercent();
            float baseRadius = radiusCurve.Evaluate(timePercent);
            bool isInside = ActiveIndoorCollider != null;
            float fogMultiplier = !isInside && FogVisionController.Instance != null
                ? FogVisionController.Instance.GetOutdoorVisionMultiplier()
                : 1f;

            // The flashlight brings practical visibility back to the 10:00 AM
            // radius, while the fog renderer still leaves a soft haze in front.
            float flashlightDayRadius = radiusCurve.Evaluate(10f / 24f);
            float naturalVisionRadius = baseRadius * fogMultiplier;
            AmbientVisionRadius = naturalVisionRadius;
            CurrentVisionRadius = IsFlashlightActive
                ? Mathf.Max(naturalVisionRadius, flashlightDayRadius)
                : naturalVisionRadius;
            float baseIntensity = intensityCurve.Evaluate(timePercent);
            float naturalIntensity = baseIntensity * (isInside ? indoorLightIntensityMultiplier : 1f);
            float targetLightRadius = IsFlashlightActive ? CurrentVisionRadius : naturalVisionRadius;
            float targetLightIntensity = IsFlashlightActive
                ? Mathf.Max(naturalIntensity, flashlightWorldIntensity)
                : naturalIntensity;
            Color targetLightColor = IsFlashlightActive ? flashlightWorldColor : Color.white;
            float transition = 1f - Mathf.Exp(-flashlightLightTransitionSpeed * Time.deltaTime);

            // This is the actual URP 2D light. It illuminates lit tiles/sprites,
            // respects ShadowCaster2D walls, and softly falls off before the fog
            // shader reaches its own feathered edge.
            playerLight.pointLightOuterRadius = Mathf.Lerp(playerLight.pointLightOuterRadius, targetLightRadius, transition);
            playerLight.pointLightInnerRadius = Mathf.Lerp(playerLight.pointLightInnerRadius,
                IsFlashlightActive ? targetLightRadius * flashlightInnerRadiusRatio : 0f, transition);
            playerLight.intensity = Mathf.Lerp(playerLight.intensity, targetLightIntensity, transition);
            playerLight.falloffIntensity = Mathf.Lerp(playerLight.falloffIntensity,
                IsFlashlightActive ? flashlightFalloffIntensity : 0.55f, transition);
            playerLight.color = Color.Lerp(playerLight.color, targetLightColor, transition);
        }
        else
        {
            CurrentVisionRadius = playerLight.pointLightOuterRadius;
            AmbientVisionRadius = CurrentVisionRadius;
        }

        // 2. BÓP GÓC KHI NGẮM BẮN
        bool questOverlayOpen = QuestFlowUIPrototype.Instance != null &&
                                QuestFlowUIPrototype.Instance.IsQuestOverlayOpen;
        bool isAiming = HasInputAuthority ? !questOverlayOpen && Input.GetMouseButton(1) : pMove.NetIsAiming;
        float physicalInner = isAiming ? aimInnerAngle : normalInnerAngle;
        float physicalOuter = isAiming ? aimOuterAngle : normalOuterAngle;
        float targetInner = physicalInner;
        float targetOuter = physicalOuter;
        if (IsFlashlightActive)
        {
            targetInner = Mathf.Max(targetInner, 105f);
            targetOuter = Mathf.Max(targetOuter, 145f);
        }
        CurrentVisionAngle = targetOuter;

        playerLight.pointLightInnerAngle = Mathf.Lerp(playerLight.pointLightInnerAngle, targetInner, Time.deltaTime * aimTransitionSpeed);
        playerLight.pointLightOuterAngle = Mathf.Lerp(playerLight.pointLightOuterAngle, targetOuter, Time.deltaTime * aimTransitionSpeed);

        // 3. FOG OF WAR - TẮT/BẬT ZOMBIE
        UpdateZombieVisibility(targetOuter);
    }

    private void HideAllZombies()
    {
        int zombieCount = Physics2D.OverlapCircle(transform.position, 40f, zombieFilter, zombiesInRadius);
        for (int i = 0; i < zombieCount; i++)
        {
            Collider2D zCollider = zombiesInRadius[i];
            if (zCollider == null) continue;
            SpriteRenderer[] srs = zCollider.GetComponentsInChildren<SpriteRenderer>();
            foreach (var sr in srs)
            {
                if (sr != null) SetZombieRendererVisibility(sr, false, true);
            }
        }
    }

    private void SetLocalPlayerReadability(bool isLocalViewTarget)
    {
        if (playerBodyRenderer == null || localPlayerUnlitMaterial == null) return;

        if (isLocalViewTarget)
        {
            if (playerBodyRenderer.sharedMaterial != localPlayerUnlitMaterial)
                playerBodyRenderer.sharedMaterial = localPlayerUnlitMaterial;

            Color readableColor = originalPlayerColor;
            readableColor.r *= localPlayerReadableBrightness;
            readableColor.g *= localPlayerReadableBrightness;
            readableColor.b *= localPlayerReadableBrightness;
            playerBodyRenderer.color = readableColor;
            SyncLocalPlayerXRaySilhouette();
        }
        else
        {
            if (playerBodyRenderer.sharedMaterial == localPlayerUnlitMaterial)
            {
                playerBodyRenderer.sharedMaterial = originalPlayerMaterial;
                playerBodyRenderer.color = originalPlayerColor;
            }
            if (localPlayerXRayRenderer != null)
                localPlayerXRayRenderer.enabled = false;
        }
    }

    private void SyncLocalPlayerXRaySilhouette()
    {
        if (localPlayerXRayRenderer == null || playerBodyRenderer == null) return;

        localPlayerXRayRenderer.enabled = playerBodyRenderer.enabled;
        localPlayerXRayRenderer.sprite = playerBodyRenderer.sprite;
        localPlayerXRayRenderer.flipX = playerBodyRenderer.flipX;
        localPlayerXRayRenderer.flipY = playerBodyRenderer.flipY;
        localPlayerXRayRenderer.drawMode = playerBodyRenderer.drawMode;
        localPlayerXRayRenderer.size = playerBodyRenderer.size;

        Color tint = localPlayerXRayColor;
        tint.a = localPlayerXRayAlpha;
        localPlayerXRayRenderer.color = tint;
    }

    private void OnDestroy()
    {
        if (playerBodyRenderer != null && playerBodyRenderer.sharedMaterial == localPlayerUnlitMaterial)
        {
            playerBodyRenderer.sharedMaterial = originalPlayerMaterial;
            playerBodyRenderer.color = originalPlayerColor;
        }

        if (localPlayerUnlitMaterial != null)
            Destroy(localPlayerUnlitMaterial);

        foreach (KeyValuePair<SpriteRenderer, float> entry in zombieOriginalAlphas)
        {
            if (entry.Key == null) continue;
            Color color = entry.Key.color;
            color.a = entry.Value;
            entry.Key.color = color;
            entry.Key.enabled = true;
        }
        zombieOriginalAlphas.Clear();
    }

    private void UpdateZombieVisibility(float currentLogicAngle)
    {
        Vector2 visionOrigin = VisionWorldPosition;
        Vector2 lookDir = VisionWorldDirection;
        float visionRadius = CurrentVisionRadius;

        int zombieCount = Physics2D.OverlapCircle(visionOrigin, 40f, zombieFilter, zombiesInRadius);
        Collider2D indoorCollider = ActiveIndoorCollider;
        bool isInside = indoorCollider != null;
        nearAwarenessUpdated.Clear();

        for (int i = 0; i < zombieCount; i++)
        {
            Collider2D zCollider = zombiesInRadius[i];
            if (zCollider == null) continue;

            SpriteRenderer[] srs = zCollider.GetComponentsInChildren<SpriteRenderer>();
            Vector2 dirToZombie = (Vector2)zCollider.bounds.center - visionOrigin;
            float dstToZombie = dirToZombie.magnitude;
            dirToZombie.Normalize();
            bool sameIndoorNear = isInside && dstToZombie <= passiveVisionRadius &&
                IsPointInsideActiveIndoorArea(indoorCollider, visionOrigin, zCollider.bounds.center);

            bool isVisible = false;

            // Tutorial camera pans briefly to the first zombie while the
            // survivor is still indoors. Keep that one actor visible for the
            // cinematic even though normal indoor fog hides exterior sprites.
            if ((zCollider.TryGetComponent(out ZOmbieAI_Khoa tutorialZombie) && tutorialZombie.TutorialForceVisible) ||
                (zCollider.TryGetComponent(out ZombieAIKhoaRebuilt rebuiltTutorialZombie) && rebuiltTutorialZombie.TutorialForceVisible))
            {
                isVisible = true;
            }

            // Near awareness is observer-local, but only actors inside the same stable
            // indoor identity may ignore internal LOS. Exterior actors keep the old
            // doorway/cone/LOS rules even when they are physically close.
            else if (sameIndoorNear || (!isInside && dstToZombie <= passiveVisionRadius))
            {
                isVisible = true;
            }
            // Without a flashlight, indoor vision cannot reveal distant exterior zombie silhouettes.
            // With one, the regular radius/cone/LOS checks below may see through an open doorway.
            else if (isInside &&
                     !IsPointInsideActiveIndoorArea(indoorCollider, visionOrigin, zCollider.bounds.center) &&
                     !IsFlashlightActive)
            {
                isVisible = false;
            }
            // B. NHÌN TRỰC TIẾP TRONG BÁN KÍNH ĐÈN PIN
            else if (dstToZombie <= visionRadius)
            {
                float angleToZombie = Vector2.Angle(lookDir, dirToZombie);

                if (angleToZombie <= currentLogicAngle / 2f)
                {
                    // Bắn tia Raycast kiểm tra xem có kẹt tường không
                    if (!IsSightBlocked(visionOrigin, dirToZombie, dstToZombie))
                    {
                        isVisible = true;
                    }
                }
            }

            // C. Fade local-only. Không network alpha/renderer state vì mỗi client có
            // camera target và vùng nhìn riêng.
            foreach (var sr in srs)
            {
                if (sr == null) continue;
                SetZombieRendererVisibility(sr, isVisible, false);
                UpdateNearAwarenessMask(sr, sameIndoorNear);
            }
        }

        FadeUnusedNearAwarenessMasks();
    }

    private bool IsSightBlocked(Vector2 origin, Vector2 direction, float distance)
    {
        if (distance <= 0.001f) return false;

        int hitCount = Physics2D.Raycast(origin, direction, obstacleFilter,
            sightObstacleHits, distance);
        for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
        {
            Collider2D hitCollider = sightObstacleHits[hitIndex].collider;
            if (hitCollider == null ||
                hitCollider.GetComponent<MilitaryGateVisionPassThrough>() != null)
                continue;
            return true;
        }

        return false;
    }

    private static bool IsPointInsideActiveIndoorArea(Collider2D indoorCollider,
        Vector2 playerPosition, Vector2 targetPosition)
    {
        if (indoorCollider == null) return false;

        IndoorFogSurfaceMap stableArea = indoorCollider.GetComponentInParent<IndoorFogSurfaceMap>();
        if (stableArea != null && stableArea.MatchesIndoorVolume(indoorCollider))
            return stableArea.ContainsIndoorPoint(targetPosition);

        if (indoorCollider.OverlapPoint(targetPosition)) return true;

        // Roof triggers generated from several roof-tile islands can overlap the
        // player's body while neither the player pivot nor a nearby actor lies in
        // one of the collider's filled polygon islands. The school roof is authored
        // this way. Only use the broad bounds fallback for that malformed/split
        // case; valid concave polygons keep their exact containment semantics.
        if (indoorCollider.OverlapPoint(playerPosition)) return false;

        Bounds bounds = indoorCollider.bounds;
        return targetPosition.x >= bounds.min.x && targetPosition.x <= bounds.max.x &&
               targetPosition.y >= bounds.min.y && targetPosition.y <= bounds.max.y;
    }

    private void SetZombieRendererVisibility(SpriteRenderer renderer, bool visible, bool immediate)
    {
        if (!zombieOriginalAlphas.TryGetValue(renderer, out float originalAlpha))
        {
            originalAlpha = renderer.color.a;
            zombieOriginalAlphas.Add(renderer, originalAlpha);
        }

        Color color = renderer.color;
        float targetAlpha = visible ? originalAlpha : 0f;
        if (visible && !renderer.enabled)
        {
            renderer.enabled = true;
            color.a = Mathf.Min(zombieAwarenessInitialAlpha, originalAlpha);
        }

        if (immediate || zombieVisibilityFadeDuration <= 0.001f)
            color.a = targetAlpha;
        else
            color.a = Mathf.MoveTowards(color.a, targetAlpha,
                Time.deltaTime * Mathf.Max(0.01f, originalAlpha) / zombieVisibilityFadeDuration);

        renderer.color = color;
        if (!visible && color.a <= 0.001f)
            renderer.enabled = false;
    }

    private void UpdateNearAwarenessMask(SpriteRenderer source, bool sameIndoorNear)
    {
        nearAwarenessUpdated.Add(source);
        if (!nearAwarenessMaskAlphas.TryGetValue(source, out float alpha)) alpha = 0f;
        float targetAlpha = sameIndoorNear ? 1f : 0f;
        nearAwarenessMaskAlphas[source] = zombieVisibilityFadeDuration <= 0.001f
            ? targetAlpha
            : Mathf.MoveTowards(alpha, targetAlpha, Time.deltaTime / zombieVisibilityFadeDuration);
    }

    private void FadeUnusedNearAwarenessMasks()
    {
        if (nearAwarenessMaskAlphas.Count == 0) return;
        var sources = new List<SpriteRenderer>(nearAwarenessMaskAlphas.Keys);
        foreach (SpriteRenderer source in sources)
        {
            if (source == null)
            {
                nearAwarenessMaskAlphas.Remove(source);
                continue;
            }
            bool unused = !nearAwarenessUpdated.Contains(source);
            if (unused) UpdateNearAwarenessMask(source, false);
            if (nearAwarenessMaskAlphas[source] <= 0.001f && unused)
                nearAwarenessMaskAlphas.Remove(source);
        }
    }

    private void ClearNearAwarenessMasksImmediate()
    {
        if (nearAwarenessMaskAlphas.Count == 0) return;
        var sources = new List<SpriteRenderer>(nearAwarenessMaskAlphas.Keys);
        foreach (SpriteRenderer source in sources)
            if (source == null) nearAwarenessMaskAlphas.Remove(source);
            else nearAwarenessMaskAlphas[source] = 0f;
    }

    internal int FillNearAwarenessFogMasks(Vector4[] bounds, float[] strengths)
    {
        if (bounds == null || strengths == null) return 0;
        int capacity = Mathf.Min(MaxNearAwarenessFogMasks, Mathf.Min(bounds.Length, strengths.Length));
        int count = 0;
        foreach (KeyValuePair<SpriteRenderer, float> entry in nearAwarenessMaskAlphas)
        {
            SpriteRenderer renderer = entry.Key;
            float alpha = entry.Value;
            if (renderer == null || alpha <= 0.001f || !renderer.gameObject.activeInHierarchy || count >= capacity)
                continue;
            Bounds rendererBounds = renderer.bounds;
            bounds[count] = new Vector4(rendererBounds.center.x, rendererBounds.center.y,
                Mathf.Max(0.06f, rendererBounds.extents.x), Mathf.Max(0.08f, rendererBounds.extents.y));
            strengths[count] = Mathf.Clamp01(alpha);
            count++;
        }
        return count;
    }
}
