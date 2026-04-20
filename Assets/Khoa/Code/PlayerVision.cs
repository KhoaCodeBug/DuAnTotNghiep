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

    private Collider2D[] zombiesInRadius = new Collider2D[100];
    private ContactFilter2D zombieFilter;
    private PlayerMovement pMove;

    private void Awake()
    {
        // Setup filter để quét mảng
        zombieFilter = new ContactFilter2D();
        zombieFilter.useLayerMask = true;
        zombieFilter.useTriggers = false; // Tối ưu: bỏ qua các collider dạng trigger
        zombieFilter.SetLayerMask(zombieLayer);

        pMove = GetComponent<PlayerMovement>();
    }

    public override void Spawned()
    {
        base.Spawned();

        // 🔥 Tắt xử lý bóng đèn của những người chơi khác (Đồng đội)
        if (!HasInputAuthority)
        {
            if (playerLight != null)
            {
                playerLight.gameObject.SetActive(false);
            }
            this.enabled = false;
        }
    }

    private void Update()
    {
        if (!HasInputAuthority || playerLight == null || pMove == null) return;

        // 1. ÁNH SÁNG NGÀY ĐÊM
        if (DayNightManager.Instance != null)
        {
            float timePercent = DayNightManager.Instance.GetTimePercent();
            playerLight.pointLightOuterRadius = radiusCurve.Evaluate(timePercent);
            playerLight.intensity = intensityCurve.Evaluate(timePercent);
        }

        // 2. BÓP GÓC KHI NGẮM BẮN
        bool isAiming = Input.GetMouseButton(1);
        float targetInner = isAiming ? aimInnerAngle : normalInnerAngle;
        float targetOuter = isAiming ? aimOuterAngle : normalOuterAngle;

        playerLight.pointLightInnerAngle = Mathf.Lerp(playerLight.pointLightInnerAngle, targetInner, Time.deltaTime * aimTransitionSpeed);
        playerLight.pointLightOuterAngle = Mathf.Lerp(playerLight.pointLightOuterAngle, targetOuter, Time.deltaTime * aimTransitionSpeed);

        // 3. FOG OF WAR - TẮT/BẬT ZOMBIE
        UpdateZombieVisibility(targetOuter);
    }

    private void UpdateZombieVisibility(float currentLogicAngle)
    {
        Vector2 lookDir = pMove.NetLastLookDir;
        if (lookDir.sqrMagnitude == 0)
        {
            lookDir = transform.up;
        }
        lookDir.Normalize();

        int zombieCount = Physics2D.OverlapCircle(transform.position, 40f, zombieFilter, zombiesInRadius);

        for (int i = 0; i < zombieCount; i++)
        {
            Collider2D zCollider = zombiesInRadius[i];
            if (zCollider == null) continue;

            SpriteRenderer[] srs = zCollider.GetComponentsInChildren<SpriteRenderer>();
            Vector2 dirToZombie = (zCollider.bounds.center - transform.position);
            float dstToZombie = dirToZombie.magnitude;
            dirToZombie.Normalize();

            bool isVisible = false;

            // A. CẢM NHẬN SAU LƯNG (Bước vào vùng 1.5f là bật hiện hình luôn)
            if (dstToZombie <= passiveVisionRadius)
            {
                isVisible = true;
            }
            // B. NHÌN TRỰC TIẾP TRONG BÁN KÍNH ĐÈN PIN
            else if (dstToZombie <= playerLight.pointLightOuterRadius)
            {
                float angleToZombie = Vector2.Angle(lookDir, dirToZombie);

                if (angleToZombie <= currentLogicAngle / 2f)
                {
                    // Bắn tia Raycast kiểm tra xem có kẹt tường không
                    RaycastHit2D hit = Physics2D.Raycast(transform.position, dirToZombie, dstToZombie, obstacleLayer);
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