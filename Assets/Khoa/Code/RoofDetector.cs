using SmallScaleInc.ZombieRural;
using UnityEngine;
using Fusion;

public class RoofDetector : MonoBehaviour
{
    private PlayerMovement localPlayerMovement;
    private Collider2D myCollider;

    // Nhớ lại cái mái nhà sếp đang đứng trong đó
    private RoofVisibility currentRoof;

    private void Start()
    {
        // Lấy script gốc từ cha
        localPlayerMovement = GetComponentInParent<PlayerMovement>();
        // Lấy cái vòng Trigger dưới chân
        myCollider = GetComponent<Collider2D>();
    }

    private void Update()
    {
        // 1. Chặn lỗi rác
        if (localPlayerMovement == null || myCollider == null) return;

        // 2. CHỈ CÓ CHỦ MÁY MỚI ĐƯỢC PHÉP QUÉT MÁI NHÀ
        if (!localPlayerMovement.HasInputAuthority) return;

        // 3. RADAR QUÉT CHỦ ĐỘNG (Phá vỡ giới hạn mù vật lý của Client)
        Collider2D[] hitColliders = new Collider2D[10]; // Quét tối đa 10 vật thể chạm vào chân
        ContactFilter2D filter = new ContactFilter2D();
        filter.NoFilter(); // Không lọc, lấy hết

        int hitCount = myCollider.Overlap(filter, hitColliders);
        RoofVisibility foundRoof = null;

        // Kiểm tra xem trong mớ dẫm trúng, có cái nào là Mái Nhà không?
        for (int i = 0; i < hitCount; i++)
        {
            RoofVisibility roof = hitColliders[i].GetComponentInParent<RoofVisibility>();
            if (roof != null)
            {
                foundRoof = roof;
                break; // Tìm thấy mái nhà là ngừng quét
            }
        }

        // 4. KIỂM TRA ĐI VÀO / ĐI RA
        // Trường hợp A: Đang ở ngoài sân (currentRoof rỗng) -> Vừa dẫm trúng nhà (foundRoof có)
        if (foundRoof != null && currentRoof == null)
        {
            currentRoof = foundRoof;
            currentRoof.EnterRoof();
            Debug.Log("✅ [ROOF] BẰNG RADAR: Đã chui vào nhà!");
        }
        // Trường hợp B: Đang ở trong nhà (currentRoof có) -> Vừa bước ra sân (foundRoof rỗng)
        else if (foundRoof == null && currentRoof != null)
        {
            currentRoof.ExitRoof();
            Debug.Log("✅ [ROOF] BẰNG RADAR: Đã bước ra sân!");
            currentRoof = null;
        }
    }

    // Tui xóa luôn 2 hàm OnTriggerEnter/Exit cũ vì nó liệt rồi, không thèm xài nữa!
}