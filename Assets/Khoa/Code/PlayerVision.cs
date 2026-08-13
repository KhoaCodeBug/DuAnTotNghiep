using UnityEngine;
using UnityEngine.Rendering.Universal;
using Fusion;

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

    [Tooltip("Khoảng cách cảm nhận sau lưng (Mặc định 1.5f - Vào vùng là hiện)")]
    public float passiveVisionRadius = 1.5f;

    [Header("=== ÁNH SÁNG TRONG NHÀ ===")]
    [Range(0f, 1f)]
    [Tooltip("Ánh sáng mắt người trong nhà. Đèn pin sau này sẽ dùng mức sáng mạnh riêng.")]
    public float indoorLightIntensityMultiplier = 0.12f;

    [Header("=== HIỂN THỊ PLAYER CỤC BỘ ===")]
    [Range(0.2f, 1f)]
    [Tooltip("Độ sáng của Player do người chơi điều khiển. Không chịu Global Light hoặc hướng của nón nhìn.")]
    public float localPlayerReadableBrightness = 0.88f;

    private Collider2D[] zombiesInRadius = new Collider2D[100];
    private ContactFilter2D zombieFilter;
    private PlayerMovement pMove;
    private PlayerInteraction playerInteraction;
    private RoofDetector roofDetector;
    private FlashlightController flashlightController;
    private SpriteRenderer playerBodyRenderer;
    private Material originalPlayerMaterial;
    private Color originalPlayerColor;
    private Material localPlayerUnlitMaterial;

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

        pMove = GetComponent<PlayerMovement>();
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
        bool isAiming = HasInputAuthority ? Input.GetMouseButton(1) : pMove.NetIsAiming;
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
                if (sr != null) sr.enabled = false;
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
        }
        else if (playerBodyRenderer.sharedMaterial == localPlayerUnlitMaterial)
        {
            playerBodyRenderer.sharedMaterial = originalPlayerMaterial;
            playerBodyRenderer.color = originalPlayerColor;
        }
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
    }

    private void UpdateZombieVisibility(float currentLogicAngle)
    {
        Vector2 visionOrigin = VisionWorldPosition;
        Vector2 lookDir = VisionWorldDirection;
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

            // Without a flashlight, indoor vision cannot reveal exterior zombie silhouettes.
            // With one, the regular radius/cone/LOS checks below may see through an open doorway.
            else if (isInside && !indoorCollider.OverlapPoint(zCollider.bounds.center) && !IsFlashlightActive)
            {
                isVisible = false;
            }
            // The close-range safety sense is outdoor-only; indoors the 80% ambient area remains a blind spot.
            else if (!isInside && dstToZombie <= passiveVisionRadius)
            {
                isVisible = true;
            }
            // B. NHÌN TRỰC TIẾP TRONG BÁN KÍNH ĐÈN PIN
            else if (dstToZombie <= visionRadius)
            {
                float angleToZombie = Vector2.Angle(lookDir, dirToZombie);

                if (angleToZombie <= currentLogicAngle / 2f)
                {
                    // Bắn tia Raycast kiểm tra xem có kẹt tường không
                    RaycastHit2D hit = Physics2D.Raycast(visionOrigin, dirToZombie, dstToZombie, obstacleLayer);
                    if (hit.collider == null)
                    {
                        isVisible = true;
                    }
                }
            }

            // C. BẬT / TẮT ZOMBIE (Chỉ bật/tắt khi trạng thái có sự thay đổi để tối ưu game)
            foreach (var sr in srs)
            {
                if (sr.enabled != isVisible)
                {
                    sr.enabled = isVisible;
                }
            }
        }
    }
}
