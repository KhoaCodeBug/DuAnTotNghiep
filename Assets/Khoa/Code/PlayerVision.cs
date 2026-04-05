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
    public float passiveVisionRadius = 2.5f;

    private Collider2D[] zombiesInRadius = new Collider2D[100];
    private ContactFilter2D zombieFilter;
    private PlayerMovement pMove;

    private void Awake()
    {
        // Setup filter để fix cái lỗi vàng (Warning) hôm bữa
        zombieFilter = new ContactFilter2D();
        zombieFilter.SetLayerMask(zombieLayer);
        pMove = GetComponent<PlayerMovement>();

    }
    public override void Spawned()
    {
        // Gọi hàm gốc của Fusion (Bắt buộc phải có)
        base.Spawned();

        // 🔥 NẾU ĐÂY KHÔNG PHẢI LÀ NHÂN VẬT CỦA MÌNH (Đồng đội hoặc người lạ)
        if (!HasInputAuthority)
        {
            // Tắt tịt cái bóng đèn Fog of War của nó đi!
            if (playerLight != null)
            {
                playerLight.gameObject.SetActive(false);
            }

            // Tắt luôn cái script PlayerVision này cho đỡ tốn tài nguyên máy
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

        // 3. FOG OF WAR - TẮT/BẬT ZOMBIE (Truyền góc thực tế vào)
        UpdateZombieVisibility(targetOuter);
    }

    private void UpdateZombieVisibility(float currentLogicAngle)
    {
        // 🔥 FIX QUAN TRỌNG: Lấy hướng nhìn CHUẨN từ PlayerMovement (vẩy chuột là đổi liền)
        Vector2 lookDir = pMove.NetLastLookDir.normalized;

        int zombieCount = Physics2D.OverlapCircle(transform.position, 40f, zombieFilter, zombiesInRadius);

        for (int i = 0; i < zombieCount; i++)
        {
            Collider2D zCollider = zombiesInRadius[i];
            if (zCollider == null) continue;

            SpriteRenderer[] srs = zCollider.GetComponentsInChildren<SpriteRenderer>();
            Vector2 dirToZombie = (zCollider.bounds.center - transform.position).normalized;
            float dstToZombie = Vector2.Distance(transform.position, zCollider.bounds.center);

            bool isVisible = false;

            // A. NHÌN THẤY NẾU Ở QUÁ GẦN (Sát lưng cũng thấy)
            if (dstToZombie <= passiveVisionRadius)
            {
                isVisible = true;
            }
            // B. NẰM TRONG BÁN KÍNH ĐÈN PIN
            else if (dstToZombie <= playerLight.pointLightOuterRadius)
            {
                // Tính góc giữa hướng vẩy chuột và vị trí con Zombie
                float angleToZombie = Vector2.Angle(lookDir, dirToZombie);

                // 🔥 FIX: Dùng biến currentLogicAngle (140 hoặc 120), tuyệt đối không dùng số của Light2D
                if (angleToZombie <= currentLogicAngle / 2f)
                {
                    // Bắn tia raycast kiểm tra xem có bị tường che không
                    if (!Physics2D.Raycast(transform.position, dirToZombie, dstToZombie, obstacleLayer))
                    {
                        isVisible = true; // Chạy lọt vào vùng đèn + Không kẹt tường = THẤY!
                    }
                }
            }

            // Áp dụng tàng hình / hiện hình
            foreach (var sr in srs)
            {
                sr.enabled = isVisible;
            }
        }
    }
}