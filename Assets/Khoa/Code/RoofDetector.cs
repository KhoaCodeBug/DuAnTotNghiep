using SmallScaleInc.ZombieRural;
using UnityEngine;
using Fusion;
using UnityEngine.Tilemaps;

public class RoofDetector : MonoBehaviour
{
    private PlayerMovement localPlayerMovement;
    private Collider2D myCollider;
    private readonly Collider2D[] hitColliders = new Collider2D[10];
    private ContactFilter2D overlapFilter;

    private RoofVisibility currentRoof;
    private Collider2D currentIndoorCollider;

    public RoofVisibility CurrentRoof => currentRoof;
    public Collider2D CurrentIndoorCollider => currentIndoorCollider;

    private void Start()
    {
        // Lấy script gốc từ cha
        localPlayerMovement = GetComponentInParent<PlayerMovement>();
        myCollider = GetComponent<Collider2D>();
        overlapFilter = new ContactFilter2D();
        overlapFilter.NoFilter();
    }

    private void Update()
    {
        if (localPlayerMovement == null || myCollider == null) return;

        bool isTarget = false;
        if (PZ_CameraController.Instance != null && PZ_CameraController.Instance.isSpectatingMode)
        {
            Transform camTarget = PZ_CameraController.Instance.CurrentTarget;
            if (camTarget != null && localPlayerMovement != null)
            {
                Transform pTrans = localPlayerMovement.transform;
                isTarget = (camTarget == pTrans || camTarget.IsChildOf(pTrans) || pTrans.IsChildOf(camTarget));
            }
        }
        else
        {
            isTarget = localPlayerMovement != null && localPlayerMovement.HasInputAuthority;
        }

        if (!isTarget) return;

        int hitCount = myCollider.Overlap(overlapFilter, hitColliders);
        RoofVisibility foundRoof = null;
        Collider2D foundIndoorCollider = null;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D candidate = hitColliders[i];
            // A house's obstacle colliders (walls, furniture blockers) are also children
            // of RoofVisibility. Only its trigger volume represents the whole indoor area.
            if (candidate == null || !candidate.isTrigger)
                continue;

            RoofVisibility roof = candidate.GetComponentInParent<RoofVisibility>();
            IndoorVisionArea indoorArea = candidate.GetComponentInParent<IndoorVisionArea>();

            // Main uses trigger colliders on its roof Tilemaps. This makes every
            // existing "nocnha" room an indoor area without reserializing the map.
            bool isMainRoofArea = candidate.GetComponent<Tilemap>() != null &&
                                  candidate.gameObject.name.StartsWith("nocnha");
            if (roof != null || indoorArea != null || isMainRoofArea)
            {
                foundRoof = roof;
                foundIndoorCollider = candidate;
                break;
            }
        }

        if (foundRoof == currentRoof)
        {
            // Keep the first valid house trigger for this stay. Physics2D overlap ordering
            // is not stable, so replacing it every frame caused the indoor mask to jump.
            if (foundRoof != null || foundIndoorCollider == currentIndoorCollider)
                return;
        }

        if (currentRoof != null)
        {
            currentRoof.ExitRoof();
        }

        currentRoof = foundRoof;
        currentIndoorCollider = foundIndoorCollider;

        if (currentRoof != null)
            currentRoof.EnterRoof();
    }

    private void OnDisable()
    {
        if (currentRoof != null)
            currentRoof.ExitRoof();

        currentRoof = null;
        currentIndoorCollider = null;
    }
}
