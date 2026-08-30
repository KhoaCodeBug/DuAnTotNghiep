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

    [Header("=== ĐÈN PIN: NGUỒN SÁNG ĐỊNH HƯỚNG THẬT ===")]
    [Range(4f, 20f)] public float flashlightRange = 11.5f;
    [Range(10f, 90f)] public float flashlightNormalInnerAngle = 35f;
    [Range(20f, 120f)] public float flashlightNormalOuterAngle = 55f;
    [Range(10f, 90f)] public float flashlightAimInnerAngle = 22f;
    [Range(20f, 120f)] public float flashlightAimOuterAngle = 40f;
    [Range(0.1f, 2f)] public float flashlightWorldIntensity = 0.95f;
    [Range(0f, 1f)] public float flashlightFalloffIntensity = 0.82f;
    [Range(0f, 0.5f)] public float flashlightInnerRadiusRatio = 0.16f;
    [Range(1f, 20f)] public float flashlightLightTransitionSpeed = 12f;
    public Color flashlightWorldColor = new Color(1f, 0.93f, 0.78f, 1f);

    [Header("=== TẦM NHÌN MẮT THƯỜNG / AMBIENT ===")]
    [Range(1f, 8f)] public float ambientNightRadius = 2.8f;
    [Range(0f, 1f)] public float ambientNightIntensity = 0.08f;
    [Range(0f, 1f)] public float indoorAmbientNightIntensity = 0.035f;
    public float aimTransitionSpeed = 8f;

    [Header("=== FOG OF WAR (Tầm nhìn thực tế) ===")]
    public LayerMask zombieLayer;
    public LayerMask obstacleLayer;

    [Tooltip("Khoảng cách cảm nhận 360 độ quanh Player. Zombie phía sau vẫn hiện, nhưng tường kín vẫn chặn cảm nhận.")]
    public float passiveVisionRadius = 1.8f;

    [Range(0.05f, 1f)]
    [Tooltip("Thời gian zombie chuyển từ mờ sang rõ hoặc ngược lại.")]
    public float zombieVisibilityFadeDuration = 0.25f;

    [Range(0f, 0.75f)]
    [Tooltip("Alpha khởi đầu khi zombie vừa đi vào vùng cảm nhận.")]
    public float zombieAwarenessInitialAlpha = 0.18f;

    [Header("=== ÁNH SÁNG TRONG NHÀ ===")]
    [Range(0f, 1f)]
    [Tooltip("Ánh sáng mắt người trong nhà khi không bật đèn pin.")]
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

    public float CurrentVisionRadius { get; private set; }
    public float AmbientVisionRadius { get; private set; }
    public float CurrentVisionAngle { get; private set; }
    public VehicleControllerFusion CurrentVisionVehicle => playerInteraction != null && playerInteraction.IsInVehicle
        ? playerInteraction.CurrentVehicleController
        : null;
    public bool IsUsingVehicleVision => CurrentVisionVehicle != null;

    private static bool ShouldRenderPhysicalLight(
        bool isTarget,
        bool usingVehicleVision,
        bool isIndoor,
        bool flashlightActive)
    {
        if (!isTarget || usingVehicleVision) return false;
        return !isIndoor || !flashlightActive;
    }

    public Transform FlashlightOriginTransform
    {
        get
        {
            if (pMove != null && pMove.flashlightTransform != null) return pMove.flashlightTransform;
            if (playerLight != null) return playerLight.transform;
            return transform;
        }
    }

    public Vector2 FlashlightOrigin => IsUsingVehicleVision
        ? CurrentVisionVehicle.VisionOrigin
        : (Vector2)FlashlightOriginTransform.position;

    public Vector2 VisionWorldPosition => FlashlightOrigin;
    public Vector2 VisionWorldDirection
    {
        get
        {
            if (IsUsingVehicleVision) return CurrentVisionVehicle.VisionDirection;
            Vector2 direction = pMove != null ? pMove.NetLastLookDir : (Vector2)transform.up;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.up;
        }
    }

    // FOW and gameplay LOS use the survivor body as their eye origin. The
    // flashlight origin may be offset on a child transform and belongs to the
    // later flashlight pass, not to the base visibility contract.
    public Vector2 LineOfSightOrigin => IsUsingVehicleVision
        ? CurrentVisionVehicle.VisionOrigin
        : (pMove != null ? (Vector2)pMove.transform.position : (Vector2)transform.position);
    public Vector2 LineOfSightDirection => VisionWorldDirection;
    public float LineOfSightRadius => IsUsingVehicleVision ? CurrentVisionRadius : AmbientVisionRadius;
    public LayerMask VisionObstacleLayer => obstacleLayer;

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

        // NOTE ON PHYSICAL LIGHT SHADOW LIMITATION:
        // Enabling shadowsEnabled prepares the Light2D for any ShadowCaster2D components if authored in the future.
        // However, because this project contains 0 authored ShadowCaster2D components on walls, physical Light2D
        // does not cast hardware shadow boundaries on its own.
        // The authoritative visual and gameplay line-of-sight barrier is provided by FogVisionController
        // (180-ray building-scoped physics occlusion) and FogVisionOverlay.shader (_IndoorWallOccludedOpacity = 1.0),
        // combined with PlayerVision.IsSightBlocked raycasts against structural obstacles.
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

        bool isInside = ActiveIndoorCollider != null;

        if (playerLight != null)
        {
            bool renderPhysicalLight = ShouldRenderPhysicalLight(
                isTarget,
                useVehicleVision,
                isInside,
                IsFlashlightActive);
            playerLight.gameObject.SetActive(renderPhysicalLight);
        }

        if (!isTarget)
        {
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

        // 1. ÁNH SÁNG NGÀY ĐÊM VÀ ĐÈN PIN
        if (DayNightManager.Instance != null)
        {
            float timePercent = DayNightManager.Instance.GetTimePercent();
            float baseRadius = radiusCurve.Evaluate(timePercent);
            float fogMultiplier = !isInside && FogVisionController.Instance != null
                ? FogVisionController.Instance.GetOutdoorVisionMultiplier()
                : 1f;

            float naturalVisionRadius = isInside ? Mathf.Min(baseRadius, ambientNightRadius) : baseRadius * fogMultiplier;
            AmbientVisionRadius = naturalVisionRadius;

            bool questOverlayOpen = QuestFlowUIPrototype.Instance != null &&
                                    QuestFlowUIPrototype.Instance.IsQuestOverlayOpen;
            bool isAiming = HasInputAuthority ? !questOverlayOpen && Input.GetMouseButton(1) : pMove.NetIsAiming;

            float targetLightRadius;
            float targetLightIntensity;
            Color targetLightColor;
            float targetFalloff;
            float targetInnerRadius;
            float targetInnerAngle;
            float targetOuterAngle;

            if (IsFlashlightActive)
            {
                CurrentVisionRadius = flashlightRange;
                targetInnerAngle = isAiming ? flashlightAimInnerAngle : flashlightNormalInnerAngle;
                targetOuterAngle = isAiming ? flashlightAimOuterAngle : flashlightNormalOuterAngle;
                CurrentVisionAngle = targetOuterAngle;

                targetLightRadius = flashlightRange;
                targetLightIntensity = flashlightWorldIntensity;
                targetLightColor = flashlightWorldColor;
                targetFalloff = flashlightFalloffIntensity;
                targetInnerRadius = flashlightRange * flashlightInnerRadiusRatio;
            }
            else
            {
                CurrentVisionRadius = naturalVisionRadius;
                CurrentVisionAngle = 360f;
                targetInnerAngle = 0f;
                targetOuterAngle = 360f;

                targetLightRadius = naturalVisionRadius;
                float baseIntensity = intensityCurve.Evaluate(timePercent);
                float naturalIntensity = baseIntensity * (isInside ? indoorAmbientNightIntensity : 1f);
                targetLightIntensity = Mathf.Min(naturalIntensity, isInside ? indoorAmbientNightIntensity : ambientNightIntensity);
                targetLightColor = Color.white;
                targetFalloff = 0.55f;
                targetInnerRadius = 0f;
            }

            float transition = 1f - Mathf.Exp(-flashlightLightTransitionSpeed * Time.deltaTime);

            playerLight.pointLightOuterRadius = Mathf.Lerp(playerLight.pointLightOuterRadius, targetLightRadius, transition);
            playerLight.pointLightInnerRadius = Mathf.Lerp(playerLight.pointLightInnerRadius, targetInnerRadius, transition);
            playerLight.intensity = Mathf.Lerp(playerLight.intensity, targetLightIntensity, transition);
            playerLight.falloffIntensity = Mathf.Lerp(playerLight.falloffIntensity, targetFalloff, transition);
            playerLight.color = Color.Lerp(playerLight.color, targetLightColor, transition);

            playerLight.pointLightInnerAngle = Mathf.Lerp(playerLight.pointLightInnerAngle, targetInnerAngle, Time.deltaTime * aimTransitionSpeed);
            playerLight.pointLightOuterAngle = Mathf.Lerp(playerLight.pointLightOuterAngle, targetOuterAngle, Time.deltaTime * aimTransitionSpeed);
        }
        else
        {
            CurrentVisionRadius = playerLight.pointLightOuterRadius;
            AmbientVisionRadius = CurrentVisionRadius;
            CurrentVisionAngle = playerLight.pointLightOuterAngle;
        }

        // 2. ĐỒNG BỘ TÂM VÀ HƯỚNG XOAY CỦA LIGHT2D VỚI FLASHLIGHT ORIGIN & AIM DIRECTION
        if (playerLight != null)
        {
            if (IsUsingVehicleVision)
            {
                playerLight.transform.position = CurrentVisionVehicle.VisionOrigin;
                float vehicleAngle = Mathf.Atan2(CurrentVisionVehicle.VisionDirection.y, CurrentVisionVehicle.VisionDirection.x) * Mathf.Rad2Deg;
                playerLight.transform.rotation = Quaternion.Euler(0, 0, vehicleAngle - 90f);
            }
            else
            {
                if (playerLight.transform != FlashlightOriginTransform &&
                    (playerLight.transform.parent == null || playerLight.transform.parent == transform))
                {
                    playerLight.transform.position = FlashlightOrigin;
                }

                Vector2 lookDir = VisionWorldDirection;
                if (lookDir.sqrMagnitude > 0.0001f)
                {
                    float lookAngle = Mathf.Atan2(lookDir.y, lookDir.x) * Mathf.Rad2Deg;
                    Quaternion targetLightRot = Quaternion.Euler(0, 0, lookAngle - 90f);
                    playerLight.transform.rotation = Quaternion.Lerp(playerLight.transform.rotation, targetLightRot, flashlightLightTransitionSpeed * Time.deltaTime);
                }
            }
        }

        // 3. FOG OF WAR - TẮT/BẬT ZOMBIE
        UpdateZombieVisibility(CurrentVisionAngle);
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
        Vector2 visionOrigin = LineOfSightOrigin;
        Vector2 lookDir = LineOfSightDirection;
        float visionRadius = CurrentVisionRadius;

        int zombieCount = Physics2D.OverlapCircle(visionOrigin, 40f, zombieFilter, zombiesInRadius);
        Collider2D indoorCollider = ActiveIndoorCollider;
        bool isInside = indoorCollider != null;

        for (int i = 0; i < zombieCount; i++)
        {
            Collider2D zCollider = zombiesInRadius[i];
            if (zCollider == null) continue;

            SpriteRenderer[] srs = zCollider.GetComponentsInChildren<SpriteRenderer>();
            Vector2 dirToZombie = (Vector2)zCollider.bounds.center - visionOrigin;
            float dstToZombie = dirToZombie.magnitude;
            dirToZombie.Normalize();

            bool isVisible = false;

            // Tutorial camera pans briefly to the first zombie while the
            // survivor is still indoors. Keep that one actor visible for the
            // cinematic even though normal indoor fog hides exterior sprites.
            if ((zCollider.TryGetComponent(out ZOmbieAI_Khoa tutorialZombie) && tutorialZombie.TutorialForceVisible) ||
                (zCollider.TryGetComponent(out ZombieAIKhoaRebuilt rebuiltTutorialZombie) && rebuiltTutorialZombie.TutorialForceVisible))
            {
                isVisible = true;
            }
            // Vùng cảm nhận 360 độ luôn hoạt động: zombie sát người trong tầm nhìn trực tiếp
            else if (dstToZombie <= passiveVisionRadius &&
                     !IsSightBlocked(visionOrigin, dirToZombie, dstToZombie))
            {
                isVisible = true;
            }
            // Tầm nhìn trực tiếp: trong bán kính nhìn thấy (ambient 360 hoặc nón đèn pin) và không bị cản tường
            else if (dstToZombie <= visionRadius)
            {
                float angleToZombie = Vector2.Angle(lookDir, dirToZombie);

                if (angleToZombie <= currentLogicAngle / 2f)
                {
                    // Bắn tia Raycast kiểm tra xem có kẹt tường / cửa đóng không
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
                if (sr != null) SetZombieRendererVisibility(sr, isVisible, false);
            }
        }
    }

    private bool IsSightBlocked(Vector2 origin, Vector2 direction, float distance)
    {
        return VisionLineOfSight.IsBlocked(origin, direction, distance,
            obstacleFilter, sightObstacleHits);
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
}
