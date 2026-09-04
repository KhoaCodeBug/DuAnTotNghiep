using System.Collections.Generic;
using Pathfinding;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Controls the authored CongRao gate. The fence tiles painted beside
/// ViTriDongCong remain hidden until the cinematic closes the entrance; this
/// component only supplies the missing physical blocker and damage bridge.
/// </summary>
public sealed class MilitaryGateController : MonoBehaviour
{
    private struct AuthoredTile
    {
        public Vector3Int Cell;
        public TileBase Tile;
        public Matrix4x4 Transform;
        public Color Color;
        public TileFlags Flags;
    }

    [SerializeField, Min(0.1f)] private float gateDamagePerHit = 12f;
    [SerializeField, Min(1)] private int maximumDamageHitsPerSecond = 4;
    [SerializeField, Min(0.1f)] private float maximumHordeHitRange = 1.1f;
    [SerializeField] private LayerMask hordeHitObstacleMask;

    private readonly List<AuthoredTile> authoredTiles = new();
    private MilitaryBaseQuestManager manager;
    private BoxCollider2D gateCollider;
    private Tilemap sourceTilemap;
    private bool tilesVisible;
    private bool graphBlocked;
    private Vector2 gateCenter;
    private Vector2 gateDirection = Vector2.right;
    private float gateLength = 4.4f;
    private float damageWindowStartedAt;
    private int damageHitsThisWindow;
    private readonly HashSet<int> attackSlotOwners = new();

    public static MilitaryGateController Create(Transform runtimeParent, Vector2 position,
        MilitaryBaseQuestManager targetManager)
    {
        _ = runtimeParent;
        GameObject authoredGate = GameObject.Find("CongRao");
        if (authoredGate == null)
        {
            Debug.LogError("[MILITARY GATE] Không tìm thấy CongRao trong scene. Chỉ tạo collider dự phòng, không tự vẽ hình.");
            authoredGate = new GameObject("CongRao [MISSING AUTHORED OBJECT]");
        }

        MilitaryGateController controller = authoredGate.GetComponent<MilitaryGateController>();
        if (controller == null) controller = authoredGate.AddComponent<MilitaryGateController>();
        controller.manager = targetManager;
        controller.ResolveAuthoredFenceTiles(position);
        controller.CreatePhysicalBlocker(position);
        controller.SetGateVisible(false);
        authoredGate.SetActive(false);
        return controller;
    }

    public void TakeGateDamage(float damage) => manager?.TakeGateDamage(damage);

    public bool TryAcquireAttackSlot(int stableId)
    {
        if (attackSlotOwners.Contains(stableId)) return true;
        int playerCount = manager != null ? manager.CountActivePlayers() : 1;
        if (attackSlotOwners.Count >= MilitaryStoryFlowRules.GetGateAttackSlotCap(playerCount)) return false;
        attackSlotOwners.Add(stableId);
        return true;
    }

    public void ReleaseAttackSlot(int stableId) => attackSlotOwners.Remove(stableId);

    public bool TryApplyHordeHit(int stableId, Vector2 attackerPosition)
    {
        bool phaseAllowsAttack = manager != null &&
            (manager.CurrentPhase == MilitaryBaseQuestManager.Phase.SiegeAndRepair ||
             manager.CurrentPhase == MilitaryBaseQuestManager.Phase.ReadyToEscape) && !manager.IsGateBroken;
        float surfaceDistance = gateCollider != null
            ? Vector2.Distance(attackerPosition, gateCollider.ClosestPoint(attackerPosition))
            : float.PositiveInfinity;
        bool hasLineOfSight = HasValidHordeLineOfSight(attackerPosition);
        if (!MilitaryQuestRules.IsAuthorityAttackValid(manager != null && manager.HasStateAuthority,
                phaseAllowsAttack, attackSlotOwners.Contains(stableId), surfaceDistance,
                maximumHordeHitRange, hasLineOfSight)) return false;
        // Solo damage becomes a deterministic three-minute DPS countdown after
        // the first visible zombie strike. Later strikes remain visual beats.
        if (manager.IsSoloSiege) return manager.TryStartSoloGateDps();
        if (Time.time - damageWindowStartedAt >= 1f)
        {
            damageWindowStartedAt = Time.time;
            damageHitsThisWindow = 0;
        }
        if (damageHitsThisWindow >= maximumDamageHitsPerSecond) return false;
        damageHitsThisWindow++;
        TakeGateDamage(gateDamagePerHit);
        return true;
    }

    private bool HasValidHordeLineOfSight(Vector2 attackerPosition)
    {
        if (gateCollider == null || !gateCollider.enabled) return false;
        Vector2 target = gateCollider.ClosestPoint(attackerPosition);
        int mask = hordeHitObstacleMask.value != 0
            ? hordeHitObstacleMask.value
            : LayerMask.GetMask("Obstacle");
        if (mask == 0) return true;
        RaycastHit2D hit = Physics2D.Linecast(attackerPosition, target, mask);
        return hit.collider == null || hit.collider == gateCollider || hit.collider.transform.IsChildOf(transform);
    }

    public void RefreshPresentation()
    {
        if (manager == null || !manager.IsNetworkReady) return;
        if (manager.IsGateBroken)
        {
            BreakGate();
            return;
        }

        bool shouldBeClosed = manager.CurrentPhase == MilitaryBaseQuestManager.Phase.SiegeAndRepair ||
                              manager.CurrentPhase == MilitaryBaseQuestManager.Phase.ReadyToEscape;
        if (gameObject.activeSelf != shouldBeClosed) gameObject.SetActive(shouldBeClosed);
        SetGateVisible(shouldBeClosed);
        SetColliderEnabled(shouldBeClosed);
    }

    public void BreakGate()
    {
        attackSlotOwners.Clear();
        SetColliderEnabled(false);
        SetGateVisible(false);
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }

    private void ResolveAuthoredFenceTiles(Vector2 gatePosition)
    {
        authoredTiles.Clear();
        Transform school = transform.parent;
        Tilemap[] maps = school != null
            ? school.GetComponentsInChildren<Tilemap>(true)
            : FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < maps.Length; i++)
            if (maps[i] != null && maps[i].name == "tuong1")
            {
                sourceTilemap = maps[i];
                break;
            }

        if (sourceTilemap == null)
        {
            Debug.LogWarning("[MILITARY GATE] CongRao chưa có renderer con và không tìm thấy Tilemap tuong1.");
            return;
        }

        Vector3Int markerCell = sourceTilemap.WorldToCell(gatePosition);
        int selectedX = markerCell.x;
        int bestCount = -1;
        for (int xOffset = -2; xOffset <= 1; xOffset++)
        {
            int count = 0;
            for (int yOffset = -4; yOffset <= 1; yOffset++)
                if (sourceTilemap.HasTile(new Vector3Int(markerCell.x + xOffset,
                        markerCell.y + yOffset, markerCell.z)))
                    count++;
            if (count <= bestCount) continue;
            bestCount = count;
            selectedX = markerCell.x + xOffset;
        }

        for (int yOffset = -4; yOffset <= 1; yOffset++)
        {
            Vector3Int cell = new Vector3Int(selectedX, markerCell.y + yOffset, markerCell.z);
            TileBase tile = sourceTilemap.GetTile(cell);
            if (tile == null) continue;
            authoredTiles.Add(new AuthoredTile
            {
                Cell = cell,
                Tile = tile,
                Transform = sourceTilemap.GetTransformMatrix(cell),
                Color = sourceTilemap.GetColor(cell),
                Flags = sourceTilemap.GetTileFlags(cell)
            });
        }

        if (authoredTiles.Count < 3)
            Debug.LogWarning($"[MILITARY GATE] Chỉ nhận diện được {authoredTiles.Count}/6 tile CongRao quanh ViTriDongCong.");
        tilesVisible = authoredTiles.Count > 0;
    }

    private void CreatePhysicalBlocker(Vector2 fallbackPosition)
    {
        Transform previous = transform.Find("CongRao Collider [RUNTIME]");
        if (previous != null) Destroy(previous.gameObject);
        GameObject colliderObject = new GameObject("CongRao Collider [RUNTIME]");
        colliderObject.transform.SetParent(transform, true);
        Vector2 center = fallbackPosition;
        Vector2 direction = new Vector2(-0.8944272f, 0.4472136f);
        float length = 4.4f;

        if (sourceTilemap != null && authoredTiles.Count > 1)
        {
            Vector2 first = sourceTilemap.GetCellCenterWorld(authoredTiles[0].Cell);
            Vector2 last = sourceTilemap.GetCellCenterWorld(authoredTiles[^1].Cell);
            center = (first + last) * 0.5f;
            Vector2 delta = last - first;
            if (delta.sqrMagnitude > 0.001f)
            {
                direction = delta.normalized;
                length = delta.magnitude + 1.15f;
            }
        }

        colliderObject.transform.position = center;
        colliderObject.transform.rotation = Quaternion.Euler(0f, 0f,
            Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        gateCollider = colliderObject.AddComponent<BoxCollider2D>();
        gateCollider.size = new Vector2(length, 0.72f);
        gateCollider.enabled = false;
        int obstacleLayer = LayerMask.NameToLayer("Obstacle");
        if (obstacleLayer >= 0) colliderObject.layer = obstacleLayer;
        colliderObject.AddComponent<MilitaryGateVisionPassThrough>();
        gateCenter = center;
        gateDirection = direction.normalized;
        gateLength = length;
    }

    public Vector2 GetAssaultPosition(int stableId, Vector2 approachPosition)
    {
        // Continuous stable offsets avoid both exact transform overlap and the
        // synchronized 13-lane stacks that appeared with large ambient hordes.
        float across = StableHash01(stableId, 41) - 0.5f;
        float depth = Mathf.Lerp(0.12f, 0.68f, StableHash01(stableId, 73));
        Vector2 normal = new Vector2(-gateDirection.y, gateDirection.x);
        if (Vector2.Dot(approachPosition - gateCenter, normal) < 0f) normal = -normal;
        return gateCenter + gateDirection * (across * gateLength * 0.9f) + normal * depth;
    }

    private static float StableHash01(int value, int salt)
    {
        unchecked
        {
            uint x = (uint)value ^ ((uint)salt * 0x9E3779B9u);
            x ^= x >> 16;
            x *= 0x7FEB352Du;
            x ^= x >> 15;
            x *= 0x846CA68Bu;
            x ^= x >> 16;
            return (x & 0x00FFFFFFu) / 16777215f;
        }
    }

    private void SetColliderEnabled(bool enabled)
    {
        if (gateCollider == null) return;
        if (gateCollider.enabled != enabled) gateCollider.enabled = enabled;
        if (graphBlocked == enabled) return;
        graphBlocked = enabled;
        Physics2D.SyncTransforms();
        UpdateAstarGraph();
    }

    private void UpdateAstarGraph()
    {
        if (AstarPath.active == null || gateCollider == null) return;
        Bounds bounds = gateCollider.bounds;
        bounds.Expand(new Vector3(1.2f, 1.2f, 2f));
        GraphUpdateObject update = new GraphUpdateObject(bounds) { updatePhysics = true };
        AstarPath.active.UpdateGraphs(update);
        AstarPath.active.FlushGraphUpdates();
    }

    private void SetGateVisible(bool visible)
    {
        if (tilesVisible == visible || sourceTilemap == null || authoredTiles.Count == 0) return;
        tilesVisible = visible;
        for (int i = 0; i < authoredTiles.Count; i++)
        {
            AuthoredTile authored = authoredTiles[i];
            if (!visible)
            {
                sourceTilemap.SetTile(authored.Cell, null);
                continue;
            }

            sourceTilemap.SetTile(authored.Cell, authored.Tile);
            sourceTilemap.SetTileFlags(authored.Cell, TileFlags.None);
            sourceTilemap.SetTransformMatrix(authored.Cell, authored.Transform);
            sourceTilemap.SetColor(authored.Cell, authored.Color);
            sourceTilemap.SetTileFlags(authored.Cell, authored.Flags);
        }
        sourceTilemap.RefreshAllTiles();
    }

    private void OnDestroy()
    {
        // Restore runtime-cleared cells when leaving Play Mode; no scene asset
        // or authored tile data is saved by this controller.
        SetGateVisible(true);
        if (gateCollider != null && graphBlocked)
        {
            gateCollider.enabled = false;
            graphBlocked = false;
            Physics2D.SyncTransforms();
            UpdateAstarGraph();
        }
    }
}

/// <summary>Physical/A* obstacle that intentionally does not block Player fog line-of-sight.</summary>
[DisallowMultipleComponent]
public sealed class MilitaryGateVisionPassThrough : MonoBehaviour
{
}
