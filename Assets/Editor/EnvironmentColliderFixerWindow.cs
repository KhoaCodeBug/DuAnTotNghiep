using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Pathfinding;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Audits environment prefabs/scene roots without guessing their intended layout.
/// It prefers authored Sprite physics shapes through TilemapCollider2D and only
/// uses PolygonCollider2D paths as explicit patches for missing/broken cells.
/// </summary>
public sealed class EnvironmentColliderFixerWindow : EditorWindow
{
    private const string PatchObjectName = "__ColliderPatches";
    private const string ProxyPrefix = "__ColliderProxy_";
    private const string BroadWallPatchPrefix = "__BroadWallFootPatches_";
    private const string DoorSidePatchPrefix = "__DoorSideFootPatches_";
    private const string ExternalObjectsName = "__ExternalEnvironment";
    private const string SchoolRoofTriggerName = "__SchoolRoofTrigger_FIXED";

    private sealed class TilemapAudit
    {
        public Tilemap Tilemap;
        public bool Included;
        public int TileCount;
        public int GridColliderCount;
        public int SpriteColliderCount;
        public int NoneColliderCount;
        public int SpriteWithoutShapeCount;
        public readonly List<Vector3Int> GridCells = new List<Vector3Int>();
        public readonly List<Vector3Int> FullBoundsShapeCells = new List<Vector3Int>();
        public readonly List<Vector3Int> MissingCells = new List<Vector3Int>();

        public int AuthoredColliderCount => GridColliderCount + SpriteColliderCount - SpriteWithoutShapeCount;
        public bool HasTilemapCollider => Tilemap != null && Tilemap.GetComponent<TilemapCollider2D>() != null;
        public bool HasEnabledTilemapCollider => Tilemap != null &&
                                                 Tilemap.GetComponent<TilemapCollider2D>() != null &&
                                                 Tilemap.GetComponent<TilemapCollider2D>().enabled;
    }

    private sealed class SpriteAudit
    {
        public SpriteRenderer Renderer;
        public bool Included;
        public int PhysicsShapeCount;
        public bool HasCollider;
    }

    private sealed class MergedFootprintBuild
    {
        public GameObject GroupObject;
        public int AcceptedCells;
        public int FallbackCells;
        public int RejectedCells;
        public int ClusterCount;
        public readonly List<string> AcceptedSprites = new List<string>();
        public readonly List<Vector2[]> Paths = new List<Vector2[]>();
        public readonly HashSet<Tilemap> Sources = new HashSet<Tilemap>();
    }

    private GameObject root;
    private readonly List<TilemapAudit> tilemaps = new List<TilemapAudit>();
    private readonly List<SpriteAudit> sprites = new List<SpriteAudit>();
    private Vector2 scroll;

    private int collisionLayer;
    private int sortingLayerIndex;
    private int sortingOrder;
    private bool forceIndividualTilemap = true;
    private bool forcePivotSprite = true;

    private Tilemap reviewTilemap;
    private bool showMissingCells = true;
    private bool pickMissingCells;
    private readonly HashSet<Vector3Int> pickedCells = new HashSet<Vector3Int>();

    private PolygonCollider2D patchCollider;
    private bool drawingPolygon;
    private readonly List<Vector2> drawingPoints = new List<Vector2>();
    private float snapStep = 0.05f;

    private GUIStyle smallBadge;
    private GUIStyle warningBadge;
    private GUIStyle goodBadge;

    [MenuItem("Tools/Environment/Collider & Sorting Fixer")]
    public static void Open()
    {
        GetWindow<EnvironmentColliderFixerWindow>("Environment Fixer");
    }

    [MenuItem("Tools/Environment/Audit Selected Root To Console")]
    public static void AuditSelectedRootToConsole()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            Debug.LogWarning("[Environment Fixer] Hãy chọn root/prefab instance cần audit.");
            return;
        }

        RoofVisibility selectedRoofOwner = selected.GetComponentInParent<RoofVisibility>(true);
        if (selectedRoofOwner != null)
            selected = selectedRoofOwner.gameObject;

        Tilemap[] selectedTilemaps = selected.GetComponentsInChildren<Tilemap>(true);
        int tileCount = 0;
        int authored = 0;
        int missing = 0;
        int spriteColliderCells = 0;
        int gridColliderCells = 0;
        int suspiciousFullBoundsCells = 0;
        int tilemapColliders = 0;
        List<string> tilemapDetails = new List<string>();
        foreach (Tilemap selectedTilemap in selectedTilemaps)
        {
            TilemapAudit audit = AuditTilemap(selectedTilemap);
            tileCount += audit.TileCount;
            authored += audit.AuthoredColliderCount;
            missing += audit.MissingCells.Count;
            spriteColliderCells += audit.SpriteColliderCount;
            gridColliderCells += audit.GridColliderCount;
            suspiciousFullBoundsCells += audit.FullBoundsShapeCells.Count;
            if (audit.HasEnabledTilemapCollider)
                tilemapColliders++;

            TilemapRenderer renderer = selectedTilemap.GetComponent<TilemapRenderer>();
            string sorting = renderer == null
                ? "no renderer"
                : $"{renderer.sortingLayerName}/{renderer.sortingOrder}/{renderer.mode}";
            tilemapDetails.Add(
                $"  • {GetRelativePath(selected.transform, selectedTilemap.transform)}: " +
                $"tile {audit.TileCount}, Sprite {audit.SpriteColliderCount}, Grid {audit.GridColliderCount}, " +
                $"None {audit.NoneColliderCount}, broad {audit.FullBoundsShapeCells.Count}, " +
                $"TilemapCollider {(audit.HasEnabledTilemapCollider ? "ON" : audit.HasTilemapCollider ? "disabled" : "off")}, sorting {sorting}" +
                DescribeCells(selectedTilemap, audit.MissingCells, "missing") +
                DescribeCells(selectedTilemap, audit.GridCells, "grid") +
                DescribeCells(selectedTilemap, audit.FullBoundsShapeCells, "broad") +
                DescribeNeighborhoods(selectedTilemap, audit.FullBoundsShapeCells));
        }

        SpriteRenderer[] selectedSprites = selected.GetComponentsInChildren<SpriteRenderer>(true);
        int spriteShapes = selectedSprites.Count(renderer => renderer.sprite != null && renderer.sprite.GetPhysicsShapeCount() > 0);
        int spritesMissingCollider = selectedSprites.Count(renderer =>
            renderer.sprite != null && renderer.sprite.GetPhysicsShapeCount() > 0 && renderer.GetComponent<Collider2D>() == null);

        string overrideReport = DescribePrefabOverrides(selected);

        Debug.Log(
            $"[Environment Fixer] Audit '{selected.name}': {selectedTilemaps.Length} Tilemap, {tileCount:N0} tile, " +
            $"{authored:N0} collider asset (Sprite {spriteColliderCells:N0}, Grid full-cell {gridColliderCells:N0}), " +
            $"{missing:N0} cell thiếu, {suspiciousFullBoundsCells:N0} shape phủ gần full sprite cần soi, {tilemapColliders} TilemapCollider2D; " +
            $"{selectedSprites.Length} SpriteRenderer, {spriteShapes} sprite có shape, {spritesMissingCollider} object thiếu Collider2D.\n" +
            string.Join("\n", tilemapDetails) + overrideReport,
            selected);
    }

    [MenuItem("Tools/Environment/Quick House Pipeline/Audit + Save Report")]
    public static void QuickHouseAuditAndSaveReport()
    {
        GameObject selected = ResolveEnvironmentRoot(Selection.activeGameObject);
        if (selected == null)
        {
            Debug.LogWarning("[Environment Fixer] Hãy chọn root căn nhà/công trình cần audit.");
            return;
        }

        string report = BuildQuickHouseReport(selected);
        string reportDirectory = "Assets/EnvironmentFixerReports";
        if (!Directory.Exists(reportDirectory))
            Directory.CreateDirectory(reportDirectory);
        string safeName = string.Concat(selected.name.Select(character =>
            System.IO.Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
        string path = $"{reportDirectory}/{safeName}_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
        File.WriteAllText(path, report);
        AssetDatabase.ImportAsset(path);
        Debug.Log(report + $"\n[Environment Fixer] Report: {path}", selected);
    }

    [MenuItem("Tools/Environment/Quick House Pipeline/Backup Active Scene Snapshot")]
    public static void BackupActiveSceneSnapshot()
    {
        UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
        {
            Debug.LogError("[Environment Fixer] Active Scene chưa có asset path để backup.");
            return;
        }
        const string directory = "Assets/Scenes/EnvironmentFixerBackups";
        if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
        string path = $"{directory}/{scene.name}_snapshot_{DateTime.Now:yyyyMMdd_HHmmss}.unity";
        if (!EditorSceneManager.SaveScene(scene, path, true))
        {
            Debug.LogError($"[Environment Fixer] Không thể tạo Scene snapshot '{path}'.");
            return;
        }
        AssetDatabase.Refresh();
        Debug.Log($"[Environment Fixer] Đã tạo Scene snapshot (Save as Copy): {path}. Active Scene vẫn là '{scene.path}'.");
    }

    [MenuItem("Tools/Environment/Quick House Pipeline/Audit cuahang In Main")]
    public static void QuickAuditCuahangInMain()
    {
        GameObject target = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(gameObject => gameObject.name.Equals("cuahang", StringComparison.OrdinalIgnoreCase));
        if (target == null)
        {
            Debug.LogError("[Environment Fixer] Không tìm thấy root 'cuahang' trong Scene hiện tại.");
            return;
        }
        Selection.activeGameObject = target;
        QuickHouseAuditAndSaveReport();
    }

    [MenuItem("Tools/Environment/Quick House Pipeline/Audit SieuThi In Main")]
    public static void QuickAuditSieuThiInMain()
    {
        GameObject target = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(gameObject => gameObject.name.Equals("SieuThi", StringComparison.OrdinalIgnoreCase) ||
                                          gameObject.name.Equals("SieuThi_FIX", StringComparison.OrdinalIgnoreCase));
        if (target == null)
        {
            Debug.LogError("[Environment Fixer] Không tìm thấy root 'SieuThi' trong Scene hiện tại.");
            return;
        }
        Selection.activeGameObject = target;
        QuickHouseAuditAndSaveReport();
    }

    [MenuItem("Tools/Environment/Quick House Pipeline/Normalize Safe Structure")]
    public static void NormalizeSafeHouseStructure()
    {
        GameObject selected = ResolveEnvironmentRoot(Selection.activeGameObject);
        if (selected == null)
        {
            Debug.LogWarning("[Environment Fixer] Hãy chọn root căn nhà/công trình cần chuẩn hóa.");
            return;
        }

        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Normalize safe house structure");
        int changed = 0;

        Component[] components = selected.GetComponents<Component>();
        foreach (IGrouping<Type, Component> duplicateGroup in components
                     .Where(component => component != null && !(component is Transform))
                     .GroupBy(component => component.GetType())
                     .Where(group => group.Count() > 1))
        {
            Component keeper = duplicateGroup.FirstOrDefault(ComponentHasMeaningfulSerializedData) ?? duplicateGroup.First();
            foreach (Component duplicate in duplicateGroup.Where(component => component != keeper))
            {
                Undo.DestroyObjectImmediate(duplicate);
                changed++;
            }
        }

        foreach (Tilemap emptyMap in selected.GetComponentsInChildren<Tilemap>(true)
                     .Where(map => map.GetUsedTilesCount() == 0))
        {
            TilemapCollider2D emptyCollider = emptyMap.GetComponent<TilemapCollider2D>();
            if (emptyCollider == null || emptyCollider.shapeCount != 0) continue;
            Undo.DestroyObjectImmediate(emptyCollider);
            changed++;
        }

        foreach (Tilemap tilemap in selected.GetComponentsInChildren<Tilemap>(true))
        {
            BoundsInt boundsBeforeCompress = tilemap.cellBounds;
            Undo.RecordObject(tilemap, "Compress stale tilemap bounds");
            tilemap.CompressBounds();
            if (tilemap.cellBounds != boundsBeforeCompress) changed++;
            TilemapRenderer renderer = tilemap.GetComponent<TilemapRenderer>();
            if (renderer == null || tilemap.name.StartsWith(ProxyPrefix, StringComparison.Ordinal))
                continue;

            string lowerName = tilemap.name.ToLowerInvariant();
            bool isFloor = lowerName.Contains("nen") || lowerName.Contains("floor");
            bool isRoof = lowerName.Contains("noc") || lowerName.Contains("roof");
            bool isGameplay = lowerName.Contains("tuong") || lowerName.Contains("wall") ||
                              lowerName.Contains("decor") || lowerName.Contains("decord");

            if (isFloor && (renderer.sortingLayerName != "Default" || renderer.sortingOrder != -15))
            {
                Undo.RecordObject(renderer, "Normalize floor sorting");
                renderer.sortingLayerName = "Default";
                renderer.sortingOrder = -15;
                changed++;
            }
            else if (isRoof && (renderer.sortingLayerName != "Foreground" || renderer.sortingOrder < 10))
            {
                Undo.RecordObject(renderer, "Normalize roof sorting");
                renderer.sortingLayerName = "Foreground";
                renderer.sortingOrder = 11;
                changed++;
            }
            else if (isGameplay &&
                     (renderer.sortingLayerName != "Gameplay" || renderer.sortingOrder != 0))
            {
                Undo.RecordObject(renderer, "Normalize gameplay sorting");
                renderer.sortingLayerName = "Gameplay";
                renderer.sortingOrder = 0;
                changed++;
            }

            if ((isGameplay || isRoof) && renderer.mode != TilemapRenderer.Mode.Individual)
            {
                Undo.RecordObject(renderer, "Use individual tile sorting");
                renderer.mode = TilemapRenderer.Mode.Individual;
                changed++;
            }
            if ((lowerName.Contains("tuong") || lowerName.Contains("wall")) &&
                tilemap.GetComponent<TilemapCollider2D>() != null &&
                tilemap.gameObject.layer != LayerMask.NameToLayer("Obstacle"))
            {
                Undo.RecordObject(tilemap.gameObject, "Set wall collision layer");
                tilemap.gameObject.layer = LayerMask.NameToLayer("Obstacle");
                changed++;
            }
            EditorUtility.SetDirty(renderer);
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(selected.scene);
        Debug.Log($"[Environment Fixer] Safe Normalize '{selected.name}' hoàn tất: {changed} thay đổi có Undo. " +
                  "Tool không tự di chuyển cell, chỉ bỏ collider rỗng 0 shape, và không tách loot.", selected);
    }

    [MenuItem("Tools/Environment/Quick House Pipeline/Decor Collider Review (No Auto Generation)")]
    public static void ReviewDecorColliders()
    {
        GameObject selected = ResolveEnvironmentRoot(Selection.activeGameObject);
        if (selected == null)
        {
            Debug.LogWarning("[Environment Fixer] Hãy chọn root căn nhà/công trình cần kiểm tra.");
            return;
        }

        Collider2D[] suspicious = selected.GetComponentsInChildren<Collider2D>(true)
            .Where(collider => !collider.isTrigger)
            .Where(collider =>
            {
                string path = GetRelativePath(selected.transform, collider.transform).ToLowerInvariant();
                return ContainsAny(path, "decor", "decord") &&
                       !IsScriptedSolidObject(collider.gameObject);
            })
            .ToArray();

        Debug.Log($"[Environment Fixer] DECOR REVIEW '{selected.name}': {suspicious.Length} collider cần xem thủ công. " +
                  "Lệnh review không thay đổi dữ liệu; collider 2.5D phải là footprint nhỏ ở chân vật thể. " +
                  "Giường/tủ loot và object có script tương tác được giữ riêng.", selected);
        foreach (Collider2D collider in suspicious)
        {
            string pathStats = collider is PolygonCollider2D polygon
                ? $", {polygon.pathCount} path, max path height {GetMaximumPathHeight(polygon):0.###}"
                : string.Empty;
            Debug.Log($"[Environment Fixer] REVIEW FOOTPRINT: {GetRelativePath(selected.transform, collider.transform)} " +
                      $"({collider.GetType().Name}, bounds {collider.bounds.size}{pathStats}).", collider);
        }
    }

    [MenuItem("Tools/Environment/Quick House Pipeline/Create Conservative Decor Footprints")]
    public static void CreateConservativeDecorFootprints()
    {
        GameObject selected = ResolveEnvironmentRoot(Selection.activeGameObject);
        if (selected == null)
        {
            Debug.LogWarning("[Environment Fixer] Hãy chọn root công trình trước khi tạo footprint.");
            return;
        }

        Transform generated = selected.transform.Find("__AutoDecorFootColliders");
        if (generated != null)
            Undo.DestroyObjectImmediate(generated.gameObject);
        EditorSceneManager.MarkSceneDirty(selected.scene);
        Debug.LogWarning($"[Environment Fixer] AUTO DECOR COLLIDER ĐÃ NGỪNG cho '{selected.name}'. " +
                         "Đã xóa nhóm sinh tự động nếu có; decor thường chỉ sửa sorting/layer. " +
                         "Collider chỉ giữ cho object tương tác có collider riêng (loot/bed...).", selected);
    }

    [MenuItem("Tools/Environment/Quick House Pipeline/Rebuild Authored Wall Collider Network")]
    public static void RebuildMergedWallFootprints()
    {
        GameObject selected = ResolveEnvironmentRoot(Selection.activeGameObject);
        if (selected == null)
        {
            Debug.LogWarning("[Environment Fixer] Hãy chọn root công trình trước khi dựng lại chân tường.");
            return;
        }

        int obstacleLayer = LayerMask.NameToLayer("Obstacle");
        if (obstacleLayer < 0)
        {
            Debug.LogError("[Environment Fixer] Project chưa có layer Obstacle.");
            return;
        }

        foreach (string generatedName in new[] { "__StructuralFootColliders", "__AutoDecorFootColliders" })
        {
            Transform generated = selected.transform.Find(generatedName);
            if (generated != null)
                Undo.DestroyObjectImmediate(generated.gameObject);
        }

        Tilemap[] wallSources = selected.GetComponentsInChildren<Tilemap>(true)
            .Where(source =>
            {
                string path = GetRelativePath(selected.transform, source.transform).ToLowerInvariant();
                return !source.name.StartsWith(ProxyPrefix, StringComparison.Ordinal) &&
                       ContainsAny(path, "wall", "tuong") && !ContainsAny(path, "roof", "nocnha");
            }).ToArray();
        int rebuiltTilemaps = 0;
        int authoredShapes = 0;
        int repairedBroadTilemaps = 0;
        foreach (Tilemap source in wallSources)
        {
            List<Vector2[]> paths = ExtractAuthoredTilePhysicsPaths(source);
            if (paths.Count == 0)
            {
                Debug.LogWarning($"[Environment Fixer] Wall '{GetRelativePath(selected.transform, source.transform)}' " +
                                 "không có authored physics shape; giữ collider hiện tại để review tay.", source);
                continue;
            }

            foreach (Collider2D oldCollider in source.GetComponents<Collider2D>()
                         .Where(collider => !collider.isTrigger).ToArray())
                Undo.DestroyObjectImmediate(oldCollider);

            source.gameObject.layer = obstacleLayer;
            TilemapCollider2D tilemapCollider = Undo.AddComponent<TilemapCollider2D>(source.gameObject);
            tilemapCollider.ProcessTilemapChanges();
            EditorUtility.SetDirty(source);
            EditorUtility.SetDirty(tilemapCollider);
            rebuiltTilemaps++;
            authoredShapes += tilemapCollider.shapeCount;
        }

        foreach (Tilemap source in wallSources)
        {
            TilemapCollider2D sourceCollider = source.GetComponent<TilemapCollider2D>();
            if (sourceCollider == null || !sourceCollider.enabled || AuditTilemap(source).FullBoundsShapeCells.Count == 0)
                continue;
            if (RepairBroadColliderCellsUsingDonor(source))
                repairedBroadTilemaps++;
        }

        EditorSceneManager.MarkSceneDirty(selected.scene);
        Debug.Log($"[Environment Fixer] AUTHORED WALL NETWORK '{selected.name}': " +
                  $"{rebuiltTilemaps} TilemapCollider2D, {authoredShapes} shape lấy nguyên vẹn từ asset. " +
                  $"{repairedBroadTilemaps} Tilemap có broad cell đã tự chuyển sang donor proxy. " +
                  "Không ép sang Polygon, không convex hull, không nối chéo, không collider decor; " +
                  "cửa/ô trống được giữ nguyên.", selected);
    }

    [MenuItem("Tools/Environment/Quick House Pipeline/Validate Wall Collider Flow")]
    public static void ValidateMergedFootprints()
    {
        GameObject selected = ResolveEnvironmentRoot(Selection.activeGameObject);
        if (selected == null)
        {
            Debug.LogWarning("[Environment Fixer] Hãy chọn root công trình cần kiểm tra.");
            return;
        }

        List<string> lines = new List<string>();
        bool valid = true;
        bool generatedStructuralExists = selected.transform.Find("__StructuralFootColliders") != null;
        bool generatedDecorExists = selected.transform.Find("__AutoDecorFootColliders") != null;
        valid &= !generatedStructuralExists && !generatedDecorExists;
        lines.Add($"- Generated groups removed: wall={!generatedStructuralExists}, decor={!generatedDecorExists}");

        Tilemap[] wallSources = selected.GetComponentsInChildren<Tilemap>(true)
            .Where(source =>
            {
                string path = GetRelativePath(selected.transform, source.transform).ToLowerInvariant();
                return !source.name.StartsWith(ProxyPrefix, StringComparison.Ordinal) &&
                       ContainsAny(path, "wall", "tuong") && !ContainsAny(path, "roof", "nocnha");
            }).ToArray();
        Dictionary<string, int> wallSprites = new Dictionary<string, int>();
        Dictionary<string, int> missingShapeSprites = new Dictionary<string, int>();
        int totalWallCells = 0;
        int shapedWallCells = 0;
        int doorCells = 0;
        foreach (Tilemap source in wallSources)
        {
            foreach (Vector3Int cell in source.cellBounds.allPositionsWithin)
            {
                if (!source.HasTile(cell)) continue;
                Sprite sprite = source.GetSprite(cell);
                if (sprite == null) continue;
                totalWallCells++;
                string lowerName = sprite.name.ToLowerInvariant();
                if (ContainsAny(lowerName, "door", "cua"))
                {
                    doorCells++;
                    continue;
                }
                if (sprite.GetPhysicsShapeCount() <= 0)
                {
                    missingShapeSprites[sprite.name] = missingShapeSprites.TryGetValue(sprite.name, out int missingCount)
                        ? missingCount + 1
                        : 1;
                    continue;
                }
                shapedWallCells++;
                wallSprites[sprite.name] = wallSprites.TryGetValue(sprite.name, out int count) ? count + 1 : 1;
            }
        }

        int enabledSourceColliders = 0;
        int enabledProxyColliders = 0;
        int effectiveShapeCount = 0;
        int repairedBroadCells = 0;
        int excludedDoorHeadCells = 0;
        bool colliderError = false;
        bool wrongWallLayer = false;
        bool invalidEffectiveNetwork = false;
        foreach (Tilemap source in wallSources)
        {
            TilemapCollider2D sourceCollider = source.GetComponent<TilemapCollider2D>();
            Transform proxyTransform = source.transform.parent?.Find(ProxyPrefix + source.name);
            Tilemap proxy = proxyTransform != null ? proxyTransform.GetComponent<Tilemap>() : null;
            TilemapCollider2D proxyCollider = proxy != null ? proxy.GetComponent<TilemapCollider2D>() : null;
            bool sourceEnabled = sourceCollider != null && sourceCollider.enabled && !sourceCollider.isTrigger;
            bool proxyEnabled = proxyCollider != null && proxyCollider.enabled && !proxyCollider.isTrigger;
            if (sourceEnabled) enabledSourceColliders++;
            if (proxyEnabled) enabledProxyColliders++;
            if (sourceEnabled == proxyEnabled)
            {
                invalidEffectiveNetwork = true;
                continue;
            }

            Tilemap effectiveMap = proxyEnabled ? proxy : source;
            TilemapCollider2D effectiveCollider = proxyEnabled ? proxyCollider : sourceCollider;
            effectiveShapeCount += effectiveCollider.shapeCount;
            colliderError |= effectiveCollider.errorState != ColliderErrorState2D.None;
            wrongWallLayer |= effectiveCollider.gameObject.layer != LayerMask.NameToLayer("Obstacle");
            int effectiveBroad = AuditTilemap(effectiveMap).FullBoundsShapeCells.Count;
            int effectiveDoorHeads = CountElevatedOnlyPhysicsShapeCells(effectiveMap);
            invalidEffectiveNetwork |= effectiveBroad > 0 || effectiveDoorHeads > 0;
            if (proxyEnabled)
            {
                int sourceBroadCells = AuditTilemap(source).FullBoundsShapeCells.Count;
                repairedBroadCells += sourceBroadCells;
                excludedDoorHeadCells += CountElevatedOnlyPhysicsShapeCells(source);
                TilemapRenderer proxyRenderer = proxy.GetComponent<TilemapRenderer>();
                invalidEffectiveNetwork |= proxyRenderer == null || proxyRenderer.enabled;
                Transform patchTransform = source.transform.parent?.Find(BroadWallPatchPrefix + source.name);
                PolygonCollider2D footPatch = patchTransform != null
                    ? patchTransform.GetComponent<PolygonCollider2D>()
                    : null;
                invalidEffectiveNetwork |= sourceBroadCells > 0 &&
                                           (footPatch == null || !footPatch.enabled || footPatch.isTrigger ||
                                            footPatch.pathCount < sourceBroadCells ||
                                            footPatch.gameObject.layer != LayerMask.NameToLayer("Obstacle") ||
                                            GetMaximumPathHeight(footPatch) > 0.16f);
            }
        }
        bool strayWallPolygon = wallSources.Any(source => source.GetComponents<PolygonCollider2D>()
            .Any(collider => !collider.isTrigger));
        valid &= shapedWallCells > 0 &&
                 enabledSourceColliders + enabledProxyColliders == wallSources.Length &&
                 effectiveShapeCount > 0 && !colliderError && !wrongWallLayer &&
                 !strayWallPolygon && !invalidEffectiveNetwork;
        lines.Add($"- Wall sources: {wallSources.Length} Tilemap, {totalWallCells} cell tổng, " +
                  $"{shapedWallCells} cell có physics shape, " +
                  $"{doorCells} cell cửa được chừa, source collider ON={enabledSourceColliders}, " +
                   $"proxy collider ON={enabledProxyColliders}, repaired broad={repairedBroadCells}, " +
                   $"excluded door-head={excludedDoorHeadCells}, " +
                  $"effective shapes={effectiveShapeCount}, Obstacle={!wrongWallLayer}, " +
                   $"errorStateNone={!colliderError}, noBroadOrDoorHeadEffective={!invalidEffectiveNetwork}, " +
                  $"noStrayPolygon={!strayWallPolygon}");
        lines.Add("- Wall sprites: " + string.Join(", ", wallSprites.OrderBy(pair => pair.Key)
            .Select(pair => $"{pair.Key} x{pair.Value}")));
        lines.Add("- Missing-shape wall sprites: " + (missingShapeSprites.Count == 0
            ? "none"
            : string.Join(", ", missingShapeSprites.OrderBy(pair => pair.Key)
                .Select(pair => $"{pair.Key} x{pair.Value}"))));
        lines.Add("- Passage rule: dùng nguyên authored physics shape từng tile; không convex hull, " +
                  "không nối qua cell trống/cửa và không sinh collider decor.");

        string verdict = valid ? "PASS" : "FAIL";
        Debug.Log($"[Environment Fixer] WALL COLLIDER FLOW VALIDATION '{selected.name}': {verdict}\n" +
                  string.Join("\n", lines), selected);
    }

    [MenuItem("Tools/Environment/Quick House Pipeline/Small Houses/Remove Unsafe Decor Proxies (Preserve Manual Polygons)")]
    public static void RemoveUnsafeSmallHouseDecorProxies()
    {
        string[] acceptedPaths =
        {
            "Assets/Khoa/House/nhamauxam_FIXED.prefab",
            "Assets/Khoa/House/cannhasieuvipprodachinhsua_FIXED.prefab"
        };

        int preserved = 0;
        int sceneProxies = 0;
        GameObject[] roots = FindObjectsByType<RoofVisibility>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Select(component => component.gameObject)
            .Where(gameObject => PrefabUtility.IsPartOfPrefabInstance(gameObject))
            .Where(gameObject =>
            {
                UnityEngine.Object source = PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
                string path = source != null ? AssetDatabase.GetAssetPath(source) : string.Empty;
                return acceptedPaths.Contains(path, StringComparer.OrdinalIgnoreCase);
            })
            .Distinct()
            .ToArray();

        foreach (GameObject rootObject in roots)
        {
            Transform manualGroup = null;
            Transform[] proxies = rootObject.GetComponentsInChildren<Transform>(true)
                .Where(transform => transform.name.StartsWith(ProxyPrefix + "decor", StringComparison.OrdinalIgnoreCase) ||
                                    transform.name.StartsWith(ProxyPrefix + "decord", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            sceneProxies += proxies.Length;
            foreach (Transform proxy in proxies)
            {
                foreach (PolygonCollider2D polygon in proxy.GetComponents<PolygonCollider2D>())
                {
                    if (manualGroup == null)
                    {
                        manualGroup = rootObject.transform.Find("__ManualDecorFootColliders");
                        if (manualGroup == null)
                        {
                            GameObject groupObject = new GameObject("__ManualDecorFootColliders");
                            Undo.RegisterCreatedObjectUndo(groupObject, "Preserve manual decor footprints");
                            Undo.SetTransformParent(groupObject.transform, rootObject.transform, "Parent manual decor footprints");
                            groupObject.transform.localPosition = Vector3.zero;
                            groupObject.transform.localRotation = Quaternion.identity;
                            groupObject.transform.localScale = Vector3.one;
                            manualGroup = groupObject.transform;
                        }
                    }

                    GameObject footprintObject = new GameObject($"Footprint_{proxy.parent.name}_{preserved + 1}");
                    Undo.RegisterCreatedObjectUndo(footprintObject, "Preserve manual decor footprint");
                    Undo.SetTransformParent(footprintObject.transform, manualGroup, "Parent manual decor footprint");
                    footprintObject.transform.localPosition = Vector3.zero;
                    footprintObject.transform.localRotation = Quaternion.identity;
                    footprintObject.transform.localScale = Vector3.one;
                    footprintObject.layer = proxy.gameObject.layer;
                    PolygonCollider2D copy = Undo.AddComponent<PolygonCollider2D>(footprintObject);
                    copy.isTrigger = polygon.isTrigger;
                    copy.sharedMaterial = polygon.sharedMaterial;
                    copy.pathCount = polygon.pathCount;
                    for (int pathIndex = 0; pathIndex < polygon.pathCount; pathIndex++)
                    {
                        Vector2[] sourcePath = polygon.GetPath(pathIndex);
                        Vector2[] destinationPath = sourcePath.Select(point =>
                        {
                            Vector3 world = polygon.transform.TransformPoint(point + polygon.offset);
                            return (Vector2)footprintObject.transform.InverseTransformPoint(world);
                        }).ToArray();
                        copy.SetPath(pathIndex, destinationPath);
                    }
                    preserved++;
                }
            }
        }

        int assetProxies = 0;
        foreach (string path in acceptedPaths)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Transform[] proxies = contents.GetComponentsInChildren<Transform>(true)
                    .Where(transform => transform.name.StartsWith(ProxyPrefix + "decor", StringComparison.OrdinalIgnoreCase) ||
                                        transform.name.StartsWith(ProxyPrefix + "decord", StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                foreach (Transform proxy in proxies)
                {
                    DestroyImmediate(proxy.gameObject);
                    assetProxies++;
                }
                if (proxies.Length > 0) PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log($"[Environment Fixer] DECOR PROXY CLEANUP: bảo toàn {preserved} Polygon footprint thủ công; " +
                  $"gỡ {assetProxies} proxy khỏi prefab ({sceneProxies} proxy instance sẽ cập nhật theo prefab). " +
                  "Collider của object có code/giường/tủ loot không bị thay đổi.");
    }

    [MenuItem("Tools/Environment/Quick House Pipeline/Remove Verified Redundant Solid Polygons")]
    public static void RemoveVerifiedRedundantSolidPolygons()
    {
        GameObject selected = ResolveEnvironmentRoot(Selection.activeGameObject);
        if (selected == null)
        {
            Debug.LogWarning("[Environment Fixer] Hãy chọn root cần kiểm tra collider trùng.");
            return;
        }

        TilemapCollider2D[] tilemapColliders = selected.GetComponentsInChildren<TilemapCollider2D>(true)
            .Where(collider => collider.enabled && !collider.isTrigger && collider.shapeCount > 0)
            .ToArray();
        PolygonCollider2D[] polygons = selected.GetComponentsInChildren<PolygonCollider2D>(true)
            .Where(collider => collider.enabled && !collider.isTrigger && collider.pathCount > 0)
            .ToArray();
        int removed = 0;
        List<string> kept = new List<string>();

        foreach (PolygonCollider2D polygon in polygons)
        {
            TilemapCollider2D match = null;
            float bestJaccard = 0f;
            float bestPolygonCoverage = 0f;
            float bestTilemapCoverage = 0f;
            foreach (TilemapCollider2D tilemapCollider in tilemapColliders)
            {
                if (BoundsCoverageRatio2D(polygon.bounds, tilemapCollider.bounds) < 0.90f) continue;
                float jaccard = SampleColliderAgreement(
                    polygon, tilemapCollider, out float polygonCoverage, out float tilemapCoverage);
                if (jaccard <= bestJaccard) continue;
                bestJaccard = jaccard;
                bestPolygonCoverage = polygonCoverage;
                bestTilemapCoverage = tilemapCoverage;
                match = tilemapCollider;
            }

            if (match == null || bestJaccard < 0.94f ||
                bestPolygonCoverage < 0.96f || bestTilemapCoverage < 0.96f)
            {
                kept.Add($"{GetRelativePath(selected.transform, polygon.transform)} " +
                         $"(best J={bestJaccard:0.000}, P={bestPolygonCoverage:0.000}, T={bestTilemapCoverage:0.000})");
                continue;
            }

            GameObject owner = polygon.gameObject;
            string polygonPath = GetRelativePath(selected.transform, polygon.transform);
            Undo.DestroyObjectImmediate(polygon);
            removed++;
            if (owner != selected && owner.transform.childCount == 0 &&
                owner.GetComponents<Component>().All(component => component == null || component is Transform))
                Undo.DestroyObjectImmediate(owner);
            Debug.Log($"[Environment Fixer] Bỏ Polygon trùng '{polygonPath}' sau kiểm tra hình học " +
                      $"J={bestJaccard:0.000}, P={bestPolygonCoverage:0.000}, T={bestTilemapCoverage:0.000}; " +
                      $"giữ TilemapCollider '{GetRelativePath(selected.transform, match.transform)}'.", selected);
        }

        if (removed > 0) EditorSceneManager.MarkSceneDirty(selected.scene);
        Debug.Log($"[Environment Fixer] Redundant collider check '{selected.name}': bỏ {removed} Polygon đã xác minh; " +
                  $"giữ {kept.Count}" + (kept.Count > 0 ? $" [{string.Join(" | ", kept)}]" : string.Empty) + ".", selected);
    }

    [MenuItem("Tools/Environment/Quick House Pipeline/Small Houses/Select nhamauxam In Main")]
    public static void SelectNhaMauXamInMain()
    {
        SelectExactSceneRoot("nhamauxam", "nhamauxam_FIXED");
    }

    [MenuItem("Tools/Environment/Quick House Pipeline/Small Houses/Select cannhasieuvipprodachinhsua In Main")]
    public static void SelectCanNhaSieuVipInMain()
    {
        SelectExactSceneRoot("cannhasieuvipprodachinhsua", "cannhasieuvipprodachinhsua_FIXED");
    }

    [MenuItem("Tools/Environment/Quick House Pipeline/Select cuahang_FIX In Main")]
    public static void SelectCuaHangFixInMain()
    {
        SelectExactSceneRoot("cuahang_FIX");
    }

    private static void SelectExactSceneRoot(params string[] acceptedNames)
    {
        GameObject target = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(gameObject => gameObject.scene == UnityEngine.SceneManagement.SceneManager.GetActiveScene() &&
                                          acceptedNames.Any(name => gameObject.name.Equals(name, StringComparison.OrdinalIgnoreCase)));
        if (target == null)
        {
            Debug.LogError($"[Environment Fixer] Không tìm thấy scene root: {string.Join(" / ", acceptedNames)}.");
            return;
        }
        Selection.activeGameObject = target;
        EditorGUIUtility.PingObject(target);
        Debug.Log($"[Environment Fixer] Selected '{target.name}' in Main.", target);
    }

    [MenuItem("Tools/Environment/Quick House Pipeline/Normalize cuahang In Main")]
    public static void QuickNormalizeCuahangInMain()
    {
        GameObject target = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(gameObject => gameObject.name.Equals("cuahang", StringComparison.OrdinalIgnoreCase));
        if (target == null)
        {
            Debug.LogError("[Environment Fixer] Không tìm thấy root 'cuahang' trong Scene hiện tại.");
            return;
        }
        Selection.activeGameObject = target;
        NormalizeSafeHouseStructure();
    }

    [MenuItem("Tools/Environment/Quick House Pipeline/Normalize SieuThi In Main")]
    public static void QuickNormalizeSieuThiInMain()
    {
        GameObject target = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(gameObject => gameObject.name.Equals("SieuThi", StringComparison.OrdinalIgnoreCase) ||
                                          gameObject.name.Equals("SieuThi_FIX", StringComparison.OrdinalIgnoreCase));
        if (target == null)
        {
            Debug.LogError("[Environment Fixer] Không tìm thấy root 'SieuThi' trong Scene hiện tại.");
            return;
        }
        Selection.activeGameObject = target;
        NormalizeSafeHouseStructure();
    }

    [MenuItem("Tools/Environment/Quick House Pipeline/Extract Decor Clusters Outside Wall Envelope")]
    public static void ExtractOutsideDecorClusters()
    {
        GameObject selected = ResolveEnvironmentRoot(Selection.activeGameObject);
        if (selected == null)
        {
            Debug.LogWarning("[Environment Fixer] Hãy chọn root công trình cần tách cluster decor ngoài phạm vi.");
            return;
        }
        Bounds? envelope = FindStructuralEnvelope(selected.GetComponentsInChildren<Tilemap>(true));
        if (!envelope.HasValue)
        {
            Debug.LogError($"[Environment Fixer] '{selected.name}' không có cluster tường để xác định phạm vi.");
            return;
        }
        Transform mapRoot = selected.transform.parent;
        Transform externalRoot = mapRoot != null ? mapRoot.Find(ExternalObjectsName) : null;
        if (externalRoot == null)
        {
            GameObject external = new GameObject(ExternalObjectsName);
            Undo.RegisterCreatedObjectUndo(external, "Create external environment root");
            external.transform.SetParent(mapRoot, false);
            externalRoot = external.transform;
        }
        int moved = 0;
        foreach (Tilemap source in selected.GetComponentsInChildren<Tilemap>(true))
        {
            string lower = source.name.ToLowerInvariant();
            if (!ContainsAny(lower, "decor", "decord")) continue;
            List<Vector3Int> occupied = new List<Vector3Int>();
            foreach (Vector3Int cell in source.cellBounds.allPositionsWithin)
                if (source.HasTile(cell)) occupied.Add(cell);
            List<Vector3Int> outside = FindTileClusters(occupied)
                .Where(cluster => cluster.Count > 0 && !Contains2D(envelope.Value, GetClusterCenterWorld(source, cluster)))
                .SelectMany(cluster => cluster)
                .ToList();
            if (outside.Count == 0) continue;

            GameObject destinationObject = new GameObject($"{selected.name}_external_{source.name}");
            Undo.RegisterCreatedObjectUndo(destinationObject, "Extract outside decor clusters");
            destinationObject.transform.SetParent(externalRoot, false);
            destinationObject.transform.position = source.transform.position;
            destinationObject.transform.rotation = source.transform.rotation;
            destinationObject.transform.localScale = source.transform.lossyScale;
            destinationObject.layer = source.gameObject.layer;
            Tilemap destination = destinationObject.AddComponent<Tilemap>();
            destination.color = source.color;
            destination.tileAnchor = source.tileAnchor;
            destination.orientation = source.orientation;
            TilemapRenderer destinationRenderer = destinationObject.AddComponent<TilemapRenderer>();
            TilemapRenderer sourceRenderer = source.GetComponent<TilemapRenderer>();
            if (sourceRenderer != null)
            {
                destinationRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
                destinationRenderer.sortingOrder = sourceRenderer.sortingOrder;
                destinationRenderer.mode = sourceRenderer.mode;
                destinationRenderer.sortOrder = sourceRenderer.sortOrder;
                destinationRenderer.sharedMaterial = sourceRenderer.sharedMaterial;
                destinationRenderer.renderingLayerMask = sourceRenderer.renderingLayerMask;
            }
            Undo.RecordObject(source, "Remove outside decor clusters from structure");
            foreach (Vector3Int cell in outside)
            {
                destination.SetTile(cell, source.GetTile(cell));
                destination.SetTransformMatrix(cell, source.GetTransformMatrix(cell));
                source.SetTile(cell, null);
            }
            source.CompressBounds();
            destination.CompressBounds();
            source.RefreshAllTiles();
            destination.RefreshAllTiles();
            EditorUtility.SetDirty(source);
            EditorUtility.SetDirty(destination);
            moved += outside.Count;
        }
        EditorSceneManager.MarkSceneDirty(selected.scene);
        Debug.Log($"[Environment Fixer] Đã tách {moved} cell decor ngoài structural envelope khỏi '{selected.name}' " +
                  $"sang '{ExternalObjectsName}'. Visual/sorting được giữ nguyên; không tạo collider.", selected);
    }

    [MenuItem("Tools/Environment/Quick House Pipeline/SieuThi/Extract External Decor Clusters")]
    public static void ExtractSieuThiOutsideDecorClusters()
    {
        GameObject target = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(gameObject => gameObject.name.Equals("SieuThi", StringComparison.OrdinalIgnoreCase) ||
                                          gameObject.name.Equals("SieuThi_FIX", StringComparison.OrdinalIgnoreCase));
        if (target == null) return;
        Selection.activeGameObject = target;
        ExtractOutsideDecorClusters();
    }

    [MenuItem("Tools/Environment/Quick House Pipeline/Use Verified External Polygon Network")]
    public static void UseVerifiedExternalPolygonNetwork()
    {
        GameObject selected = ResolveEnvironmentRoot(Selection.activeGameObject);
        if (selected == null) return;
        int removed = 0;
        foreach (TilemapCollider2D generated in selected.GetComponentsInChildren<TilemapCollider2D>(true))
        {
            Transform network = FindExternalPolygonNetwork(generated, selected);
            if (network == null) continue;
            string generatedName = generated.name;
            string networkPath = GetHierarchyPath(network);
            Undo.SetTransformParent(network, selected.transform, "Attach authored collider network to structure");
            Undo.RecordObject(network.gameObject, "Rename authored collider network");
            network.name = $"__AuthoredColliderNetwork_{selected.name}";
            Undo.DestroyObjectImmediate(generated);
            removed++;
            Debug.Log($"[Environment Fixer] '{generatedName}' dùng polygon network authored '{networkPath}'; " +
                      "đã đưa network vào root công trình và bỏ TilemapCollider2D trùng sau khi bounds perimeter khớp >= 85% " +
                      "và network có >= 3 polygon.", selected);
        }
        if (removed == 0)
            Debug.LogWarning($"[Environment Fixer] Không tìm thấy external polygon network đủ điều kiện cho '{selected.name}'.", selected);
        else
            EditorSceneManager.MarkSceneDirty(selected.scene);
    }

    [MenuItem("Tools/Environment/Quick House Pipeline/SieuThi/Use Authored Polygon Collider Network")]
    public static void UseSieuThiAuthoredPolygonNetwork()
    {
        GameObject target = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(gameObject => gameObject.name.Equals("SieuThi", StringComparison.OrdinalIgnoreCase) ||
                                          gameObject.name.Equals("SieuThi_FIX", StringComparison.OrdinalIgnoreCase));
        if (target == null) return;
        Selection.activeGameObject = target;
        UseVerifiedExternalPolygonNetwork();
    }

    [MenuItem("Tools/Environment/Quick House Pipeline/cuahang/Extract Fence From Wall")]
    public static void ExtractCuahangFenceFromWall()
    {
        GameObject house = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(gameObject => gameObject.name.Equals("cuahang", StringComparison.OrdinalIgnoreCase));
        Tilemap source = house != null
            ? house.GetComponentsInChildren<Tilemap>(true).FirstOrDefault(map => map.name == "tuongnha")
            : null;
        if (house == null || source == null)
        {
            Debug.LogError("[Environment Fixer] Không tìm thấy cuahang/tuongnha.");
            return;
        }

        List<Vector3Int> fenceCells = new List<Vector3Int>();
        foreach (Vector3Int cell in source.cellBounds.allPositionsWithin)
        {
            Sprite sprite = source.GetSprite(cell);
            if (source.HasTile(cell) && sprite != null && sprite.name == "Object2_E_7")
                fenceCells.Add(cell);
        }
        if (fenceCells.Count == 0)
        {
            Debug.Log("[Environment Fixer] Hàng rào cuahang đã được tách hoặc không còn cell Object2_E_7.", source);
            return;
        }

        Transform mapRoot = house.transform.parent;
        Transform externalRoot = mapRoot != null ? mapRoot.Find(ExternalObjectsName) : null;
        if (externalRoot == null)
        {
            GameObject external = new GameObject(ExternalObjectsName);
            Undo.RegisterCreatedObjectUndo(external, "Create external environment root");
            external.transform.SetParent(mapRoot, false);
            externalRoot = external.transform;
        }

        GameObject fenceObject = new GameObject("cuahang_hangrao_visual");
        Undo.RegisterCreatedObjectUndo(fenceObject, "Extract cuahang fence");
        fenceObject.transform.SetParent(externalRoot, false);
        fenceObject.transform.position = source.transform.position;
        fenceObject.transform.rotation = source.transform.rotation;
        fenceObject.transform.localScale = source.transform.lossyScale;
        fenceObject.layer = LayerMask.NameToLayer("Obstacle");
        Tilemap destination = fenceObject.AddComponent<Tilemap>();
        TilemapRenderer destinationRenderer = fenceObject.AddComponent<TilemapRenderer>();
        TilemapRenderer sourceRenderer = source.GetComponent<TilemapRenderer>();
        destinationRenderer.sortingLayerName = "Gameplay";
        destinationRenderer.sortingOrder = sourceRenderer != null ? sourceRenderer.sortingOrder : 0;
        destinationRenderer.mode = TilemapRenderer.Mode.Individual;
        destinationRenderer.sortOrder = sourceRenderer != null ? sourceRenderer.sortOrder : TilemapRenderer.SortOrder.TopRight;
        destinationRenderer.sharedMaterial = sourceRenderer != null ? sourceRenderer.sharedMaterial : null;
        Undo.RecordObject(source, "Remove fence cells from house wall");
        foreach (Vector3Int cell in fenceCells)
        {
            destination.SetTile(cell, source.GetTile(cell));
            destination.SetTransformMatrix(cell, source.GetTransformMatrix(cell));
            source.SetTile(cell, null);
        }
        source.RefreshAllTiles();
        destination.RefreshAllTiles();
        source.GetComponent<TilemapCollider2D>()?.ProcessTilemapChanges();
        EditorUtility.SetDirty(source);
        EditorUtility.SetDirty(destination);
        EditorSceneManager.MarkSceneDirty(house.scene);
        Selection.activeGameObject = fenceObject;
        Debug.Log($"[Environment Fixer] Đã tách {fenceCells.Count} cell hàng rào Object2_E_7 khỏi cuahang sang " +
                  $"'{ExternalObjectsName}/cuahang_hangrao_visual'. Tilemap mới chỉ giữ hình ảnh; collider chân hàng rào authored " +
                  "đang nằm trong nhóm ==========Wall=========== nên không tạo collider trùng.", fenceObject);
    }

    [MenuItem("Tools/Environment/Quick House Pipeline/cuahang/Use Authored Polygon Wall Collider")]
    public static void UseCuahangAuthoredPolygonWallCollider()
    {
        GameObject house = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(gameObject => gameObject.name.Equals("cuahang", StringComparison.OrdinalIgnoreCase));
        Tilemap wall = house != null
            ? house.GetComponentsInChildren<Tilemap>(true).FirstOrDefault(map => map.name == "tuongnha")
            : null;
        if (wall == null || wall.GetComponent<PolygonCollider2D>() == null)
        {
            Debug.LogError("[Environment Fixer] cuahang/tuongnha không có PolygonCollider2D authored để giữ lại.");
            return;
        }
        TilemapCollider2D generated = wall.GetComponent<TilemapCollider2D>();
        if (generated != null) Undo.DestroyObjectImmediate(generated);
        Undo.RecordObject(wall.gameObject, "Set wall obstacle layer");
        wall.gameObject.layer = LayerMask.NameToLayer("Obstacle");
        EditorSceneManager.MarkSceneDirty(wall.gameObject.scene);
        Debug.Log("[Environment Fixer] cuahang giữ PolygonCollider2D authored; đã bỏ TilemapCollider2D chồng và đặt tuongnha vào Obstacle.", wall);
    }

    [MenuItem("Tools/Environment/Quick House Pipeline/cuahang/Create Store Loot Candidate Markers")]
    public static void CreateCuahangLootCandidateMarkers()
    {
        GameObject house = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(gameObject => gameObject.name.Equals("cuahang", StringComparison.OrdinalIgnoreCase));
        if (house == null) return;
        Transform existing = house.transform.Find("__LootCandidates_REVIEW");
        if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);
        GameObject rootObject = new GameObject("__LootCandidates_REVIEW");
        Undo.RegisterCreatedObjectUndo(rootObject, "Create loot candidate markers");
        rootObject.transform.SetParent(house.transform, false);

        int count = 0;
        foreach (Tilemap map in house.GetComponentsInChildren<Tilemap>(true))
        {
            if (!map.name.Contains("decor", StringComparison.OrdinalIgnoreCase) &&
                !map.name.Contains("decord", StringComparison.OrdinalIgnoreCase)) continue;
            foreach (Vector3Int cell in map.cellBounds.allPositionsWithin)
            {
                Sprite sprite = map.GetSprite(cell);
                if (sprite == null || !sprite.name.StartsWith("Store", StringComparison.OrdinalIgnoreCase)) continue;
                GameObject marker = new GameObject($"{sprite.name}_{cell.x}_{cell.y}");
                marker.transform.SetParent(rootObject.transform, true);
                marker.transform.position = map.GetCellCenterWorld(cell);
                marker.transform.localScale = Vector3.one * 0.25f;
                count++;
            }
        }
        Selection.activeGameObject = rootObject;
        EditorSceneManager.MarkSceneDirty(house.scene);
        Debug.Log($"[Environment Fixer] Đã tạo {count} marker Store* dưới __LootCandidates_REVIEW. Chỉ dùng để duyệt, chưa tách/xóa Tilemap.", rootObject);
    }

    [MenuItem("Tools/Environment/Quick House Pipeline/school/Audit Structure + Physics Bands")]
    public static void AuditSchoolStructureAndPhysicsBands()
    {
        GameObject school = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(candidate => candidate.name == "school" && candidate.scene.IsValid());
        if (school == null)
        {
            Debug.LogError("[Environment Fixer] Không tìm thấy root 'school' trong Scene hiện tại.");
            return;
        }

        string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(school);
        List<string> lines = new List<string>
        {
            $"Root world={school.transform.position}, local={school.transform.localPosition}, prefab='{prefabPath}'",
            DescribePrefabOverrides(school).TrimStart()
        };

        foreach (Tilemap map in school.GetComponentsInChildren<Tilemap>(true)
                     .Where(candidate => !candidate.name.StartsWith(ProxyPrefix, StringComparison.Ordinal) &&
                                         !candidate.name.StartsWith("__", StringComparison.Ordinal)))
        {
            List<Vector3Int> occupied = new List<Vector3Int>();
            foreach (Vector3Int cell in map.cellBounds.allPositionsWithin)
                if (map.HasTile(cell)) occupied.Add(cell);
            List<List<Vector3Int>> clusters = FindTileClusters(occupied)
                .OrderByDescending(cluster => cluster.Count).ToList();
            TilemapAudit audit = AuditTilemap(map);
            TilemapRenderer renderer = map.GetComponent<TilemapRenderer>();
            string topSprites = string.Join(", ", occupied
                .Select(map.GetSprite).Where(sprite => sprite != null)
                .GroupBy(sprite => sprite.name).OrderByDescending(group => group.Count()).Take(12)
                .Select(group => $"{group.Key}×{group.Count()}"));
            string clusterSummary = string.Join(" | ", clusters.Take(12).Select(cluster =>
            {
                int minX = cluster.Min(cell => cell.x);
                int maxX = cluster.Max(cell => cell.x);
                int minY = cluster.Min(cell => cell.y);
                int maxY = cluster.Max(cell => cell.y);
                Vector3 center = GetClusterCenterWorld(map, cluster);
                return $"{cluster.Count}cell[{minX}..{maxX},{minY}..{maxY}]@({center.x:0.##},{center.y:0.##})";
            }));
            int elevated = CountElevatedOnlyPhysicsShapeCells(map);
            lines.Add($"• {GetRelativePath(school.transform, map.transform)}: tiles={occupied.Count}, " +
                      $"clusters={clusters.Count}, collider Sprite/Grid/None={audit.SpriteColliderCount}/" +
                      $"{audit.GridColliderCount}/{audit.NoneColliderCount}, broad={audit.FullBoundsShapeCells.Count}, " +
                      $"elevated-only={elevated}, component={(audit.HasEnabledTilemapCollider ? "ON" : audit.HasTilemapCollider ? "disabled" : "off")}, " +
                      $"sorting={(renderer != null ? renderer.sortingLayerName + "/" + renderer.sortingOrder + "/" + renderer.mode : "none")}\n" +
                      $"  clusters: {clusterSummary}\n  sprites: {topSprites}");
            if (map.name.Equals("tuong1", StringComparison.OrdinalIgnoreCase))
            {
                lines.Add("  wall physics bands:");
                foreach (IGrouping<Sprite, Vector3Int> group in occupied
                             .Where(cell => map.GetSprite(cell) != null)
                             .GroupBy(map.GetSprite)
                             .OrderBy(group => group.Key.name))
                {
                    string sampleCells = string.Join(",", group.Take(10));
                    lines.Add($"    - {group.Key.name}×{group.Count()}: {DescribeSpritePhysicsBand(group.Key)}; " +
                              $"cells={sampleCells}{(group.Count() > 10 ? ",..." : string.Empty)}");
                }
            }
        }

        string report = "[Environment Fixer] SCHOOL AUDIT\n" + string.Join("\n", lines);
        const string reportDirectory = "Assets/EnvironmentFixerReports";
        if (!Directory.Exists(reportDirectory)) Directory.CreateDirectory(reportDirectory);
        const string reportPath = reportDirectory + "/school_current_audit.txt";
        File.WriteAllText(reportPath, report);
        AssetDatabase.ImportAsset(reportPath);
        Debug.Log(report + $"\nReport: {reportPath}", school);
    }

    [MenuItem("Tools/Environment/Quick House Pipeline/school/Rebuild Collider + Roof + Layers")]
    public static void RebuildSchoolColliderRoofAndLayers()
    {
        GameObject school = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(candidate => candidate.name == "school" && candidate.scene.IsValid());
        if (school == null)
        {
            Debug.LogError("[Environment Fixer] Không tìm thấy root 'school' trong Scene hiện tại.");
            return;
        }

        Tilemap walls = school.transform.Find("tuong1")?.GetComponent<Tilemap>();
        Tilemap roof = school.transform.Find("nocnha")?.GetComponent<Tilemap>();
        Tilemap emptySchoolRoof = school.transform.Find("noctruong")?.GetComponent<Tilemap>();
        Tilemap rootMap = school.GetComponent<Tilemap>();
        if (walls == null || roof == null || rootMap == null || CountOccupiedTiles(walls) != 617 ||
            CountOccupiedTiles(roof) != 1197 || CountOccupiedTiles(rootMap) != 0)
        {
            Debug.LogError("[Environment Fixer] school không còn đúng cấu trúc đã audit " +
                           "(root rỗng, tuong1=617, nocnha=1197). Dừng để không sửa nhầm dữ liệu mới.", school);
            return;
        }

        int obstacleLayer = LayerMask.NameToLayer("Obstacle");
        if (obstacleLayer < 0)
        {
            Debug.LogError("[Environment Fixer] Project chưa có layer Obstacle.", school);
            return;
        }

        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Rebuild school collider roof and layers");

        Undo.RecordObject(school, "Keep school root as container");
        school.layer = LayerMask.NameToLayer("Default");
        TilemapRenderer rootRenderer = school.GetComponent<TilemapRenderer>();
        if (rootRenderer != null)
        {
            Undo.RecordObject(rootRenderer, "Disable empty school renderer");
            rootRenderer.enabled = false;
            EditorUtility.SetDirty(rootRenderer);
        }

        Undo.RecordObject(walls.gameObject, "Set school wall obstacle layer");
        walls.gameObject.layer = obstacleLayer;
        TilemapCollider2D sourceCollider = walls.GetComponent<TilemapCollider2D>();
        if (sourceCollider == null)
            sourceCollider = Undo.AddComponent<TilemapCollider2D>(walls.gameObject);
        Undo.RecordObject(sourceCollider, "Configure school source wall collider");
        sourceCollider.isTrigger = false;
        sourceCollider.enabled = true;
        sourceCollider.ProcessTilemapChanges();
        EditorUtility.SetDirty(sourceCollider);

        NormalizeSchoolVisualLayers(school, walls, roof, emptySchoolRoof);
        bool repaired = RepairBroadColliderCellsUsingDonor(walls);
        if (!repaired)
        {
            Debug.LogError("[Environment Fixer] Không dựng được proxy/patch collider cho school. Hãy Undo thao tác này.", school);
            Undo.RevertAllDownToGroup(undoGroup);
            return;
        }

        PolygonCollider2D roofTrigger = BuildSchoolRoofTrigger(school, roof);
        if (roofTrigger == null || roofTrigger.pathCount == 0)
        {
            Debug.LogError("[Environment Fixer] Không dựng được trigger mái school. Đã Undo toàn bộ thao tác.", school);
            Undo.RevertAllDownToGroup(undoGroup);
            return;
        }

        Undo.CollapseUndoOperations(undoGroup);
        Selection.activeGameObject = school;
        EditorSceneManager.MarkSceneDirty(school.scene);
        Debug.Log($"[Environment Fixer] SCHOOL REBUILD hoàn tất: wall source 617 cell, " +
                  $"roof trigger {roofTrigger.pathCount} dải tile. Scene đang dirty, chưa tự Save.", school);
    }

    [MenuItem("Tools/Environment/Quick House Pipeline/school/Validate Fixed Structure")]
    public static void ValidateSchoolFixedStructure()
    {
        GameObject school = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(candidate => candidate.name == "school" && candidate.scene.IsValid());
        Tilemap walls = school != null ? school.transform.Find("tuong1")?.GetComponent<Tilemap>() : null;
        Tilemap roof = school != null ? school.transform.Find("nocnha")?.GetComponent<Tilemap>() : null;
        Tilemap proxy = school != null ? school.transform.Find(ProxyPrefix + "tuong1")?.GetComponent<Tilemap>() : null;
        PolygonCollider2D broadPatch = school != null
            ? school.transform.Find(BroadWallPatchPrefix + "tuong1")?.GetComponent<PolygonCollider2D>()
            : null;
        PolygonCollider2D doorSidePatch = school != null
            ? school.transform.Find(DoorSidePatchPrefix + "tuong1")?.GetComponent<PolygonCollider2D>()
            : null;
        Transform roofTriggerTransform = school != null ? school.transform.Find(SchoolRoofTriggerName) : null;
        PolygonCollider2D roofTrigger = roofTriggerTransform != null
            ? roofTriggerTransform.GetComponent<PolygonCollider2D>()
            : null;
        RoofVisibility roofController = roofTriggerTransform != null
            ? roofTriggerTransform.GetComponent<RoofVisibility>()
            : null;
        if (school == null || walls == null || roof == null || proxy == null)
        {
            Debug.LogError("[Environment Fixer] SCHOOL VALIDATION: FAIL - structure _FIXED chưa đầy đủ.", school);
            return;
        }

        Physics2D.SyncTransforms();
        int obstacleLayer = LayerMask.NameToLayer("Obstacle");
        TilemapCollider2D sourceCollider = walls.GetComponent<TilemapCollider2D>();
        TilemapCollider2D proxyCollider = proxy.GetComponent<TilemapCollider2D>();
        TilemapRenderer proxyRenderer = proxy.GetComponent<TilemapRenderer>();
        int sourceBroad = AuditTilemap(walls).FullBoundsShapeCells.Count;
        int effectiveBroad = AuditTilemap(proxy).FullBoundsShapeCells.Count;
        int sourceDoorHeads = CountElevatedOnlyPhysicsShapeCells(walls);
        int effectiveDoorHeads = CountElevatedOnlyPhysicsShapeCells(proxy);
        int blockedDoorHeads = CountBlockedElevatedDoorHeadSamples(school.transform, walls);
        (int ordinaryTested, int ordinaryBlocked) = CountBlockedOrdinaryWallSamples(school, walls);

        Bounds expandedProxyBounds = proxyCollider != null ? proxyCollider.bounds : new Bounds();
        expandedProxyBounds.Expand(2f);
        bool broadPatchPlacement = broadPatch != null && proxyCollider != null &&
                                   expandedProxyBounds.Contains(broadPatch.bounds.min) &&
                                   expandedProxyBounds.Contains(broadPatch.bounds.max);
        int roofClusterCells = 0;
        int roofTriggerMisses = CountSchoolRoofTriggerMisses(roof, roofTrigger, out roofClusterCells);

        TilemapRenderer wallRenderer = walls.GetComponent<TilemapRenderer>();
        TilemapRenderer roofRenderer = roof.GetComponent<TilemapRenderer>();
        bool sortingValid = wallRenderer != null && wallRenderer.sortingLayerName == "Gameplay" &&
                            wallRenderer.mode == TilemapRenderer.Mode.Individual &&
                            roofRenderer != null && roofRenderer.sortingLayerName == "Foreground" &&
                            roofRenderer.sortingOrder == 20;
        bool roofOwnership = roofTrigger != null && roofTrigger.enabled && roofTrigger.isTrigger &&
                             roofTrigger.errorState == ColliderErrorState2D.None && roofController != null &&
                             roofController.roofTilemaps != null && roofController.roofTilemaps.Length == 1 &&
                             roofController.roofTilemaps[0] == roof && roofTriggerMisses == 0;

        Collider2D[] unintendedSolids = school.GetComponentsInChildren<Collider2D>(true)
            .Where(collider => collider.enabled && !collider.isTrigger &&
                               collider != proxyCollider && collider != broadPatch && collider != doorSidePatch)
            .ToArray();
        bool colliderNetwork = walls.gameObject.layer == obstacleLayer &&
                               sourceCollider != null && !sourceCollider.enabled &&
                               proxy.gameObject.layer == obstacleLayer && proxyCollider != null &&
                               proxyCollider.enabled && !proxyCollider.isTrigger &&
                               proxyCollider.errorState == ColliderErrorState2D.None &&
                               proxyRenderer != null && !proxyRenderer.enabled &&
                               sourceBroad == 22 && effectiveBroad == 0 &&
                               sourceDoorHeads == 9 && effectiveDoorHeads == 0 && blockedDoorHeads == 0 &&
                               broadPatch != null && broadPatch.enabled && !broadPatch.isTrigger &&
                               broadPatch.errorState == ColliderErrorState2D.None && broadPatch.pathCount == 22 &&
                               broadPatch.gameObject.layer == obstacleLayer &&
                               GetMaximumPathHeight(broadPatch) <= 0.16f && broadPatchPlacement &&
                               doorSidePatch != null && doorSidePatch.enabled && !doorSidePatch.isTrigger &&
                               doorSidePatch.errorState == ColliderErrorState2D.None &&
                               doorSidePatch.gameObject.layer == obstacleLayer && doorSidePatch.pathCount > 0 &&
                               ordinaryTested >= 16 && ordinaryBlocked == ordinaryTested &&
                               unintendedSolids.Length == 0;
        bool structureValid = CountOccupiedTiles(school.GetComponent<Tilemap>()) == 0 &&
                              CountOccupiedTiles(walls) == 617 && CountOccupiedTiles(roof) == 1197;
        bool valid = structureValid && colliderNetwork && roofOwnership && sortingValid;

        List<string> lines = new List<string>
        {
            $"- Structure: rootEmpty={CountOccupiedTiles(school.GetComponent<Tilemap>()) == 0}, walls={CountOccupiedTiles(walls)}/617, roof={CountOccupiedTiles(roof)}/1197",
            $"- Wall proxy: shapes={proxyCollider?.shapeCount ?? 0}, error={proxyCollider?.errorState}, broad={sourceBroad}->effective {effectiveBroad}",
            $"- Broad patches: {broadPatch?.pathCount ?? 0}/22, maxHeight={(broadPatch != null ? GetMaximumPathHeight(broadPatch) : -1f):0.###}, placement={broadPatchPlacement}",
            $"- Door lintels: {sourceDoorHeads}/9->effective {effectiveDoorHeads}, blockedCenters={blockedDoorHeads}/9, sidePaths={doorSidePatch?.pathCount ?? 0}",
            $"- Ordinary wall samples: blocked={ordinaryBlocked}/{ordinaryTested}",
            $"- Roof: paths={roofTrigger?.pathCount ?? 0}, coverage={roofClusterCells - roofTriggerMisses}/{roofClusterCells}, ownership={roofOwnership}",
            $"- Sorting: wall={wallRenderer?.sortingLayerName}/{wallRenderer?.sortingOrder}/{wallRenderer?.mode}, roof={roofRenderer?.sortingLayerName}/{roofRenderer?.sortingOrder}/{roofRenderer?.mode}",
            $"- Unintended enabled solid colliders: {unintendedSolids.Length}" +
            (unintendedSolids.Length > 0 ? $" [{string.Join(", ", unintendedSolids.Select(collider => collider.gameObject.name))}]" : string.Empty),
            $"- Prefab remains scene instance: '{PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(school)}' (không Apply All)"
        };
        Debug.Log($"[Environment Fixer] SCHOOL FIXED VALIDATION: {(valid ? "PASS" : "FAIL")}\n" +
                  string.Join("\n", lines), school);
    }

    private static int CountOccupiedTiles(Tilemap map)
    {
        if (map == null) return 0;
        int count = 0;
        foreach (Vector3Int cell in map.cellBounds.allPositionsWithin)
            if (map.HasTile(cell)) count++;
        return count;
    }

    private static void NormalizeSchoolVisualLayers(
        GameObject school, Tilemap walls, Tilemap roof, Tilemap emptySchoolRoof)
    {
        foreach (Tilemap map in school.GetComponentsInChildren<Tilemap>(true))
        {
            if (map.name.StartsWith(ProxyPrefix, StringComparison.Ordinal) ||
                map.name.StartsWith("__", StringComparison.Ordinal))
                continue;
            TilemapRenderer renderer = map.GetComponent<TilemapRenderer>();
            if (renderer == null) continue;
            Undo.RecordObject(renderer, "Normalize school renderer");
            if (map == roof)
            {
                renderer.sortingLayerName = "Foreground";
                renderer.sortingOrder = 20;
                renderer.mode = TilemapRenderer.Mode.Chunk;
            }
            else if (map.name.Equals("nennha", StringComparison.OrdinalIgnoreCase))
            {
                renderer.sortingLayerName = "Default";
                renderer.sortingOrder = 0;
                renderer.mode = TilemapRenderer.Mode.Individual;
            }
            else if (map == emptySchoolRoof)
            {
                renderer.enabled = false;
            }
            else
            {
                renderer.sortingLayerName = "Gameplay";
                renderer.mode = TilemapRenderer.Mode.Individual;
            }
            EditorUtility.SetDirty(renderer);
        }
    }

    private static PolygonCollider2D BuildSchoolRoofTrigger(GameObject school, Tilemap roof)
    {
        Transform oldTrigger = school.transform.Find(SchoolRoofTriggerName);
        if (oldTrigger != null) Undo.DestroyObjectImmediate(oldTrigger.gameObject);

        List<Vector3Int> occupied = new List<Vector3Int>();
        foreach (Vector3Int cell in roof.cellBounds.allPositionsWithin)
            if (roof.HasTile(cell)) occupied.Add(cell);
        List<Vector3Int> footprint = FindTileClusters(occupied)
            .OrderByDescending(cluster => cluster.Count).FirstOrDefault();
        if (footprint == null || footprint.Count == 0) return null;

        GameObject triggerObject = new GameObject(SchoolRoofTriggerName);
        Undo.RegisterCreatedObjectUndo(triggerObject, "Create exact school roof trigger");
        Undo.SetTransformParent(triggerObject.transform, school.transform, "Parent school roof trigger");
        triggerObject.transform.localPosition = Vector3.zero;
        triggerObject.transform.localRotation = Quaternion.identity;
        triggerObject.transform.localScale = Vector3.one;
        triggerObject.layer = LayerMask.NameToLayer("Default");

        PolygonCollider2D trigger = Undo.AddComponent<PolygonCollider2D>(triggerObject);
        trigger.pathCount = 0;
        trigger.isTrigger = true;
        foreach (IGrouping<int, Vector3Int> row in footprint.GroupBy(cell => cell.y).OrderBy(group => group.Key))
        {
            List<int> xs = row.Select(cell => cell.x).Distinct().OrderBy(x => x).ToList();
            int start = xs[0];
            int previous = xs[0];
            for (int index = 1; index <= xs.Count; index++)
            {
                bool closesRun = index == xs.Count || xs[index] != previous + 1;
                if (closesRun)
                {
                    Vector3Int minCell = new Vector3Int(start, row.Key, 0);
                    Vector3Int maxCell = new Vector3Int(previous, row.Key, 0);
                    Vector2[] path =
                    {
                        triggerObject.transform.InverseTransformPoint(roof.CellToWorld(minCell)),
                        triggerObject.transform.InverseTransformPoint(roof.CellToWorld(maxCell + Vector3Int.right)),
                        triggerObject.transform.InverseTransformPoint(roof.CellToWorld(maxCell + Vector3Int.right + Vector3Int.up)),
                        triggerObject.transform.InverseTransformPoint(roof.CellToWorld(minCell + Vector3Int.up))
                    };
                    int pathIndex = trigger.pathCount;
                    trigger.pathCount++;
                    trigger.SetPath(pathIndex, path);
                    if (index < xs.Count) start = xs[index];
                }
                if (index < xs.Count) previous = xs[index];
            }
        }

        RoofVisibility controller = Undo.AddComponent<RoofVisibility>(triggerObject);
        controller.roofTilemaps = new[] { roof };
        controller.fadeDuration = 0.3f;
        controller.targetAlpha = 0f;
        EditorUtility.SetDirty(trigger);
        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(triggerObject);
        return trigger;
    }

    private static (int tested, int blocked) CountBlockedOrdinaryWallSamples(GameObject rootObject, Tilemap source)
    {
        HashSet<Vector3Int> excluded = new HashSet<Vector3Int>(AuditTilemap(source).FullBoundsShapeCells);
        HashSet<Vector3Int> doorHeads = FindElevatedOnlyPhysicsShapeCells(source);
        excluded.UnionWith(doorHeads);
        excluded.UnionWith(FindDoorSideConstraints(source, doorHeads).Keys);
        excluded.UnionWith(FindDoorProjectionClearances(source, doorHeads).Keys);
        List<Vector3Int> candidates = new List<Vector3Int>();
        foreach (Vector3Int cell in source.cellBounds.allPositionsWithin)
            if (source.HasTile(cell) && source.GetColliderType(cell) != Tile.ColliderType.None &&
                !excluded.Contains(cell)) candidates.Add(cell);
        candidates = candidates.OrderBy(cell => cell.y).ThenBy(cell => cell.x).ToList();
        int step = Mathf.Max(1, candidates.Count / 24);
        int tested = 0;
        int blocked = 0;
        for (int index = 0; index < candidates.Count && tested < 24; index += step)
        {
            List<Vector2> samples = GetSingleCellShapeCentroidsWorld(source, candidates[index]);
            if (samples.Count == 0) continue;
            tested++;
            if (Physics2D.OverlapPointAll(samples[0]).Any(collider => collider.enabled && !collider.isTrigger &&
                    collider.transform.IsChildOf(rootObject.transform)))
                blocked++;
        }
        return (tested, blocked);
    }

    private static int CountSchoolRoofTriggerMisses(
        Tilemap roof, PolygonCollider2D trigger, out int footprintCells)
    {
        footprintCells = 0;
        if (roof == null || trigger == null) return int.MaxValue / 4;
        List<Vector3Int> occupied = new List<Vector3Int>();
        foreach (Vector3Int cell in roof.cellBounds.allPositionsWithin)
            if (roof.HasTile(cell)) occupied.Add(cell);
        List<Vector3Int> footprint = FindTileClusters(occupied)
            .OrderByDescending(cluster => cluster.Count).FirstOrDefault();
        if (footprint == null) return int.MaxValue / 4;
        footprintCells = footprint.Count;
        return footprint.Count(cell => !trigger.OverlapPoint(roof.GetCellCenterWorld(cell)));
    }

    [MenuItem("Tools/Environment/Quick House Pipeline/hospital/Rebuild Two-Building Structure")]
    public static void RebuildHospitalTwoBuildingStructure()
    {
        GameObject hospital = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(candidate => candidate.name == "hospital" && candidate.scene.IsValid());
        if (hospital == null)
        {
            Debug.LogError("[Environment Fixer] Không tìm thấy root 'hospital' trong Scene hiện tại.");
            return;
        }

        if (hospital.transform.Find("Hospital_Large_FIXED") != null ||
            hospital.transform.Find("Hospital_Small_FIXED") != null)
        {
            Debug.LogError("[Environment Fixer] hospital đã có structure _FIXED. Dừng để không ghi đè kết quả đang review.", hospital);
            return;
        }

        Tilemap sourceWalls = hospital.GetComponent<Tilemap>();
        TilemapRenderer sourceWallRenderer = hospital.GetComponent<TilemapRenderer>();
        TilemapCollider2D sourceWallCollider = hospital.GetComponent<TilemapCollider2D>();
        if (sourceWalls == null || sourceWallRenderer == null || sourceWallCollider == null)
        {
            Debug.LogError("[Environment Fixer] hospital root phải có Tilemap + TilemapRenderer + TilemapCollider2D trước khi tách.", hospital);
            return;
        }

        Tilemap[] roofMaps = hospital.GetComponentsInChildren<Tilemap>(true)
            .Where(map => map.name.StartsWith("nocnha", StringComparison.OrdinalIgnoreCase) &&
                          map.GetComponent<PolygonCollider2D>() != null &&
                          map.GetComponent<PolygonCollider2D>().isTrigger)
            .OrderByDescending(map =>
            {
                Bounds bounds = map.GetComponent<PolygonCollider2D>().bounds;
                return bounds.size.x * bounds.size.y;
            })
            .ToArray();
        if (roofMaps.Length != 2)
        {
            Debug.LogError($"[Environment Fixer] hospital cần đúng 2 mái có Polygon trigger; hiện tìm thấy {roofMaps.Length}.", hospital);
            return;
        }

        int obstacleLayer = LayerMask.NameToLayer("Obstacle");
        if (obstacleLayer < 0)
        {
            Debug.LogError("[Environment Fixer] Project chưa có layer Obstacle.");
            return;
        }

        List<Vector3Int> occupiedWallCells = new List<Vector3Int>();
        foreach (Vector3Int cell in sourceWalls.cellBounds.allPositionsWithin)
        {
            if (sourceWalls.HasTile(cell))
                occupiedWallCells.Add(cell);
        }
        if (occupiedWallCells.Count == 0)
        {
            Debug.LogError("[Environment Fixer] Tilemap root hospital không có tile tường để tách.", hospital);
            return;
        }

        string[] nonStructuralSprites = occupiedWallCells
            .Select(sourceWalls.GetSprite)
            .Where(sprite => sprite != null &&
                             !ContainsAny(sprite.name.ToLowerInvariant(), "wall", "hospital"))
            .Select(sprite => sprite.name)
            .Distinct()
            .OrderBy(name => name)
            .ToArray();
        if (nonStructuralSprites.Length > 0)
        {
            Debug.LogError("[Environment Fixer] Root hospital còn sprite không chắc là tường: " +
                           string.Join(", ", nonStructuralSprites) + ". Dừng để review.", hospital);
            return;
        }

        RoofVisibility oldRoofVisibility = hospital.GetComponent<RoofVisibility>();
        float fadeDuration = oldRoofVisibility != null ? oldRoofVisibility.fadeDuration : 0.3f;
        float targetAlpha = oldRoofVisibility != null ? oldRoofVisibility.targetAlpha : 0f;
        Material litMaterial = roofMaps
            .Select(map => map.GetComponent<TilemapRenderer>())
            .Where(renderer => renderer != null && renderer.sharedMaterial != null)
            .Select(renderer => renderer.sharedMaterial)
            .FirstOrDefault();

        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Rebuild hospital two-building structure");

        GameObject largeRoot = CreateHospitalGroup(hospital.transform, "Hospital_Large_FIXED");
        GameObject smallRoot = CreateHospitalGroup(hospital.transform, "Hospital_Small_FIXED");
        Tilemap largeWalls = CreateHospitalTilemapClone(sourceWalls, sourceWallRenderer, largeRoot.transform,
            "tuongnha", obstacleLayer, litMaterial);
        Tilemap smallWalls = CreateHospitalTilemapClone(sourceWalls, sourceWallRenderer, smallRoot.transform,
            "tuongnha", obstacleLayer, litMaterial);

        Bounds largeRoofBounds = roofMaps[0].GetComponent<PolygonCollider2D>().bounds;
        Bounds smallRoofBounds = roofMaps[1].GetComponent<PolygonCollider2D>().bounds;
        int largeCount = 0;
        int smallCount = 0;
        float maximumWorldDrift = 0f;
        foreach (Vector3Int cell in occupiedWallCells)
        {
            Vector3 originalWorld = sourceWalls.GetCellCenterWorld(cell);
            float largeDistance = DistanceToBounds2D(largeRoofBounds, originalWorld);
            float smallDistance = DistanceToBounds2D(smallRoofBounds, originalWorld);
            Tilemap destination = largeDistance <= smallDistance ? largeWalls : smallWalls;
            CopyHospitalTileCell(sourceWalls, destination, cell);
            maximumWorldDrift = Mathf.Max(maximumWorldDrift,
                Vector2.Distance(originalWorld, destination.GetCellCenterWorld(cell)));
            if (destination == largeWalls) largeCount++;
            else smallCount++;
        }

        if (largeCount == 0 || smallCount == 0 || largeCount + smallCount != occupiedWallCells.Count ||
            maximumWorldDrift > 0.001f)
        {
            Undo.RevertAllDownToGroup(undoGroup);
            Debug.LogError($"[Environment Fixer] Hospital split validation thất bại: large={largeCount}, " +
                           $"small={smallCount}, source={occupiedWallCells.Count}, drift={maximumWorldDrift:0.#####}. " +
                           "Đã Undo toàn bộ thao tác.", hospital);
            return;
        }

        Undo.RecordObject(sourceWalls, "Clear mixed hospital wall tilemap");
        sourceWalls.ClearAllTiles();
        sourceWalls.CompressBounds();
        Undo.RecordObject(sourceWallRenderer, "Disable empty hospital root renderer");
        sourceWallRenderer.enabled = false;
        Undo.RecordObject(sourceWallCollider, "Disable mixed hospital root collider");
        sourceWallCollider.enabled = false;
        Undo.RecordObject(hospital, "Reset hospital container layer");
        hospital.layer = LayerMask.NameToLayer("Default");

        ConfigureHospitalWallCollider(largeWalls);
        ConfigureHospitalWallCollider(smallWalls);
        MoveHospitalRoofToIndependentController(roofMaps[0], largeRoot, fadeDuration, targetAlpha);
        MoveHospitalRoofToIndependentController(roofMaps[1], smallRoot, fadeDuration, targetAlpha);
        if (oldRoofVisibility != null)
            Undo.DestroyObjectImmediate(oldRoofVisibility);

        NormalizeHospitalVisualLayers(hospital, litMaterial, sourceWalls);
        int extractedFarTiles = ExtractHospitalFarVisualCells(hospital, largeRoofBounds, smallRoofBounds);

        bool repairedLarge = RepairBroadColliderCellsUsingDonor(largeWalls);
        bool repairedSmall = RepairBroadColliderCellsUsingDonor(smallWalls, largeWalls);
        largeWalls.GetComponent<TilemapCollider2D>()?.ProcessTilemapChanges();
        smallWalls.GetComponent<TilemapCollider2D>()?.ProcessTilemapChanges();

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(hospital.scene);
        Selection.activeGameObject = smallRoot;
        Debug.Log(
            $"[Environment Fixer] HOSPITAL SPLIT hoàn tất: wall {occupiedWallCells.Count} cell -> " +
            $"large {largeCount}, small {smallCount}, world drift {maximumWorldDrift:0.#####}; " +
            $"broad repair large={repairedLarge}, small={repairedSmall}; " +
            $"far visual tách review={extractedFarTiles}. Hai RoofVisibility hoạt động độc lập. " +
            "Scene đã dirty nhưng chưa tự Save.", hospital);
    }

    [MenuItem("Tools/Environment/Quick House Pipeline/hospital/Repair Small Walls From Large Donor")]
    public static void RepairHospitalSmallWallsFromLargeDonor()
    {
        GameObject hospital = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(candidate => candidate.name == "hospital" && candidate.scene.IsValid());
        Transform largeRoot = hospital != null ? hospital.transform.Find("Hospital_Large_FIXED") : null;
        Transform smallRoot = hospital != null ? hospital.transform.Find("Hospital_Small_FIXED") : null;
        Tilemap largeWalls = largeRoot != null ? largeRoot.GetComponentInChildren<Tilemap>(true) : null;
        Tilemap smallWalls = smallRoot != null ? smallRoot.GetComponentInChildren<Tilemap>(true) : null;
        if (largeWalls == null || smallWalls == null)
        {
            Debug.LogError("[Environment Fixer] Không tìm thấy hai Tilemap tường hospital _FIXED.", hospital);
            return;
        }

        bool repaired = RepairBroadColliderCellsUsingDonor(smallWalls, largeWalls);
        EditorSceneManager.MarkSceneDirty(hospital.scene);
        Selection.activeGameObject = smallWalls.gameObject;
        Debug.Log($"[Environment Fixer] HOSPITAL SMALL CROSS-DONOR repair={repaired}.", smallWalls);
    }

    [MenuItem("Tools/Environment/Quick House Pipeline/hospital/Rebuild All Broad Wall Foot Patches")]
    public static void RebuildHospitalBroadWallFootPatches()
    {
        GameObject hospital = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(candidate => candidate.name == "hospital" && candidate.scene.IsValid());
        Transform largeRoot = hospital != null ? hospital.transform.Find("Hospital_Large_FIXED") : null;
        Transform smallRoot = hospital != null ? hospital.transform.Find("Hospital_Small_FIXED") : null;
        Tilemap largeWalls = largeRoot != null
            ? largeRoot.GetComponentsInChildren<Tilemap>(true).FirstOrDefault(map => map.name == "tuongnha")
            : null;
        Tilemap smallWalls = smallRoot != null
            ? smallRoot.GetComponentsInChildren<Tilemap>(true).FirstOrDefault(map => map.name == "tuongnha")
            : null;
        if (largeWalls == null || smallWalls == null)
        {
            Debug.LogError("[Environment Fixer] Không tìm thấy hai Tilemap tường hospital _FIXED.", hospital);
            return;
        }

        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Rebuild hospital broad wall foot patches");
        bool repairedLarge = RepairBroadColliderCellsUsingDonor(largeWalls);
        bool repairedSmall = RepairBroadColliderCellsUsingDonor(smallWalls, largeWalls);
        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(hospital.scene);
        Debug.Log($"[Environment Fixer] HOSPITAL ALL BROAD PATCHES: large={repairedLarge}, small={repairedSmall}.", hospital);
    }

    [MenuItem("Tools/Environment/Quick House Pipeline/hospital/Audit Wall Sprite Physics Shape Bands")]
    public static void AuditHospitalWallSpritePhysicsShapeBands()
    {
        GameObject hospital = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(candidate => candidate.name == "hospital" && candidate.scene.IsValid());
        Tilemap[] walls = hospital != null
            ? hospital.GetComponentsInChildren<Tilemap>(true).Where(map => map.name == "tuongnha").ToArray()
            : Array.Empty<Tilemap>();
        Dictionary<Sprite, List<string>> usage = new Dictionary<Sprite, List<string>>();
        foreach (Tilemap wall in walls)
        {
            foreach (Vector3Int cell in wall.cellBounds.allPositionsWithin)
            {
                if (!wall.HasTile(cell)) continue;
                Sprite sprite = wall.GetSprite(cell);
                if (sprite == null) continue;
                if (!usage.TryGetValue(sprite, out List<string> cells))
                {
                    cells = new List<string>();
                    usage.Add(sprite, cells);
                }
                cells.Add($"{wall.transform.parent?.name}/{cell}@{wall.GetCellCenterWorld(cell)}");
            }
        }

        List<string> lines = new List<string>();
        List<Vector2> points = new List<Vector2>();
        foreach (KeyValuePair<Sprite, List<string>> pair in usage.OrderBy(item => item.Key.name, StringComparer.Ordinal))
        {
            Sprite sprite = pair.Key;
            float spriteMinY = sprite.bounds.min.y;
            float spriteHeight = Mathf.Max(sprite.bounds.size.y, 0.0001f);
            List<string> shapes = new List<string>();
            bool hasElevatedBand = false;
            for (int shapeIndex = 0; shapeIndex < sprite.GetPhysicsShapeCount(); shapeIndex++)
            {
                points.Clear();
                sprite.GetPhysicsShape(shapeIndex, points);
                if (points.Count == 0) continue;
                float minY = points.Min(point => point.y);
                float maxY = points.Max(point => point.y);
                float minRatio = (minY - spriteMinY) / spriteHeight;
                float maxRatio = (maxY - spriteMinY) / spriteHeight;
                hasElevatedBand |= minRatio >= 0.4f || maxRatio >= 0.5f;
                shapes.Add($"#{shapeIndex}[y={minY:0.###}..{maxY:0.###}, ratio={minRatio:0.##}..{maxRatio:0.##}]");
            }
            lines.Add($"- {sprite.name} x{pair.Value.Count}, boundsY={sprite.bounds.min.y:0.###}..{sprite.bounds.max.y:0.###}: " +
                      string.Join(" ", shapes) +
                      (hasElevatedBand ? " | ELEVATED cells=" + string.Join(", ", pair.Value) : string.Empty));
        }
        Debug.Log("[Environment Fixer] HOSPITAL WALL SHAPE BANDS\n" + string.Join("\n", lines), hospital);
    }

    [MenuItem("Tools/Environment/Quick House Pipeline/hospital/Repair Independent Roof Controllers")]
    public static void RepairHospitalIndependentRoofControllers()
    {
        GameObject hospital = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(candidate => candidate.name == "hospital" && candidate.scene.IsValid());
        if (hospital == null)
        {
            Debug.LogError("[Environment Fixer] Không tìm thấy hospital để sửa roof controller.");
            return;
        }

        RoofVisibility[] existingControllers = hospital.GetComponentsInChildren<RoofVisibility>(true);
        float fadeDuration = existingControllers.FirstOrDefault() != null
            ? existingControllers.First().fadeDuration
            : 0.3f;
        float targetAlpha = existingControllers.FirstOrDefault() != null
            ? existingControllers.First().targetAlpha
            : 0f;
        foreach (RoofVisibility controller in existingControllers)
        {
            if (!controller.gameObject.name.StartsWith("nocnha", StringComparison.OrdinalIgnoreCase))
                Undo.DestroyObjectImmediate(controller);
        }

        Tilemap[] roofs = hospital.GetComponentsInChildren<Tilemap>(true)
            .Where(map => map.name.StartsWith("nocnha", StringComparison.OrdinalIgnoreCase) &&
                          map.GetComponent<PolygonCollider2D>() != null &&
                          map.GetComponent<PolygonCollider2D>().isTrigger)
            .ToArray();
        foreach (Tilemap roof in roofs)
            ConfigureHospitalRoofControllerOnTilemap(roof, fadeDuration, targetAlpha);

        EditorSceneManager.MarkSceneDirty(hospital.scene);
        Debug.Log($"[Environment Fixer] HOSPITAL ROOF ownership: {roofs.Length} mái, mỗi mái có controller riêng trên trigger.", hospital);
    }

    [MenuItem("Tools/Environment/Quick House Pipeline/hospital/Validate Fixed Structure")]
    public static void ValidateHospitalFixedStructure()
    {
        GameObject hospital = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(candidate => candidate.name == "hospital" && candidate.scene.IsValid());
        Transform largeRoot = hospital != null ? hospital.transform.Find("Hospital_Large_FIXED") : null;
        Transform smallRoot = hospital != null ? hospital.transform.Find("Hospital_Small_FIXED") : null;
        if (hospital == null || largeRoot == null || smallRoot == null)
        {
            Debug.LogError("[Environment Fixer] Hospital fixed structure chưa đầy đủ.", hospital);
            return;
        }

        List<string> lines = new List<string>();
        bool valid = true;
        Tilemap emptyRoot = hospital.GetComponent<Tilemap>();
        TilemapRenderer emptyRenderer = hospital.GetComponent<TilemapRenderer>();
        TilemapCollider2D emptyCollider = hospital.GetComponent<TilemapCollider2D>();
        int rootCells = CountTilemapCells(emptyRoot);
        bool emptySourceDisabled = rootCells == 0 && emptyRenderer != null && !emptyRenderer.enabled &&
                                   emptyCollider != null && !emptyCollider.enabled;
        valid &= emptySourceDisabled;
        lines.Add($"- Mixed root cleared: cells={rootCells}, rendererOff={emptyRenderer != null && !emptyRenderer.enabled}, " +
                  $"colliderOff={emptyCollider != null && !emptyCollider.enabled}");

        Tilemap largeWalls = largeRoot.GetComponentsInChildren<Tilemap>(true)
            .FirstOrDefault(map => map.name == "tuongnha");
        Tilemap smallWalls = smallRoot.GetComponentsInChildren<Tilemap>(true)
            .FirstOrDefault(map => map.name == "tuongnha");
        int largeCells = CountTilemapCells(largeWalls);
        int smallCells = CountTilemapCells(smallWalls);
        valid &= largeCells == 455 && smallCells == 84;
        lines.Add($"- Wall split: large={largeCells}/455, small={smallCells}/84");

        foreach (Transform building in new[] { largeRoot, smallRoot })
        {
            Tilemap walls = building.GetComponentsInChildren<Tilemap>(true)
                .FirstOrDefault(map => map.name == "tuongnha");
            Tilemap proxy = building.GetComponentsInChildren<Tilemap>(true)
                .FirstOrDefault(map => map.name == ProxyPrefix + "tuongnha");
            PolygonCollider2D patch = building.GetComponentsInChildren<PolygonCollider2D>(true)
                .FirstOrDefault(collider => collider.gameObject.name == BroadWallPatchPrefix + "tuongnha");
            PolygonCollider2D doorSidePatch = building.GetComponentsInChildren<PolygonCollider2D>(true)
                .FirstOrDefault(collider => collider.gameObject.name == DoorSidePatchPrefix + "tuongnha");
            TilemapCollider2D sourceCollider = walls != null ? walls.GetComponent<TilemapCollider2D>() : null;
            TilemapCollider2D proxyCollider = proxy != null ? proxy.GetComponent<TilemapCollider2D>() : null;
            TilemapRenderer proxyRenderer = proxy != null ? proxy.GetComponent<TilemapRenderer>() : null;
            int broadEffective = proxy != null ? AuditTilemap(proxy).FullBoundsShapeCells.Count : int.MaxValue;
            int sourceDoorHeads = CountElevatedOnlyPhysicsShapeCells(walls);
            int effectiveDoorHeads = proxy != null ? CountElevatedOnlyPhysicsShapeCells(proxy) : int.MaxValue;
            int expectedPatches = building == largeRoot ? 13 : 3;
            int expectedDoorHeads = building == largeRoot ? 36 : 11;
            int expectedDoorSidePaths = building == largeRoot ? 244 : 41;
            Bounds expandedProxyBounds = proxyCollider != null ? proxyCollider.bounds : new Bounds();
            expandedProxyBounds.Expand(2f);
            bool patchPlacement = patch != null && proxyCollider != null &&
                                  expandedProxyBounds.Contains(patch.bounds.min) &&
                                  expandedProxyBounds.Contains(patch.bounds.max);
            bool buildingValid = walls != null && walls.gameObject.layer == LayerMask.NameToLayer("Obstacle") &&
                                 sourceCollider != null && !sourceCollider.enabled &&
                                 proxyCollider != null && proxyCollider.enabled && !proxyCollider.isTrigger &&
                                 proxyCollider.errorState == ColliderErrorState2D.None &&
                                 proxyRenderer != null && !proxyRenderer.enabled && broadEffective == 0 &&
                                 sourceDoorHeads == expectedDoorHeads && effectiveDoorHeads == 0 &&
                                 patch != null && patch.enabled && !patch.isTrigger &&
                                  patch.pathCount == expectedPatches &&
                                 patch.gameObject.layer == LayerMask.NameToLayer("Obstacle") &&
                                 GetMaximumPathHeight(patch) <= 0.16f && patchPlacement &&
                                 doorSidePatch != null && doorSidePatch.enabled && !doorSidePatch.isTrigger &&
                                 doorSidePatch.errorState == ColliderErrorState2D.None &&
                                 doorSidePatch.gameObject.layer == LayerMask.NameToLayer("Obstacle") &&
                                 doorSidePatch.pathCount == expectedDoorSidePaths;
            valid &= buildingValid;
            lines.Add($"- {building.name}: proxy={(proxyCollider != null && proxyCollider.enabled)}, " +
                      $"shapes={proxyCollider?.shapeCount ?? 0}, error={proxyCollider?.errorState}, " +
                       $"broad={broadEffective}, patches={patch?.pathCount ?? 0}/{expectedPatches}, " +
                       $"doorHeads={sourceDoorHeads}/{expectedDoorHeads}->effective {effectiveDoorHeads}, " +
                       $"doorSidePaths={doorSidePatch?.pathCount ?? 0}/{expectedDoorSidePaths}, " +
                       $"maxPatchHeight={(patch != null ? GetMaximumPathHeight(patch) : -1f):0.###}, " +
                       $"placement={patchPlacement}");
        }

        int blockedDoorHeadSamples = CountBlockedElevatedDoorHeadSamples(largeRoot, largeWalls) +
                                     CountBlockedElevatedDoorHeadSamples(smallRoot, smallWalls);
        valid &= blockedDoorHeadSamples == 0;
        lines.Add($"- Door-head center overlap: blocked={blockedDoorHeadSamples}/47");

        Tilemap[] roofs = hospital.GetComponentsInChildren<Tilemap>(true)
            .Where(map => map.name.StartsWith("nocnha", StringComparison.OrdinalIgnoreCase) &&
                          map.GetComponent<PolygonCollider2D>() != null &&
                          map.GetComponent<PolygonCollider2D>().isTrigger)
            .ToArray();
        bool roofOwnership = roofs.Length == 2 && hospital.GetComponent<RoofVisibility>() == null &&
                             roofs.All(roof =>
                             {
                                 RoofVisibility controller = roof.GetComponent<RoofVisibility>();
                                 return controller != null && controller.roofTilemaps != null &&
                                        controller.roofTilemaps.Length == 1 && controller.roofTilemaps[0] == roof;
                             });
        valid &= roofOwnership;
        lines.Add($"- Independent roofs: count={roofs.Length}, ownership={roofOwnership}");

        Tilemap decor = hospital.GetComponentsInChildren<Tilemap>(true).FirstOrDefault(map => map.name == "decord");
        TilemapRenderer decorRenderer = decor != null ? decor.GetComponent<TilemapRenderer>() : null;
        bool decorSorting = decorRenderer != null && decorRenderer.sortingLayerName == "Gameplay" &&
                            decorRenderer.sortingOrder == 0 && decorRenderer.mode == TilemapRenderer.Mode.Individual;
        valid &= decorSorting;
        string decorSortingDescription = decorRenderer != null
            ? decorRenderer.sortingLayerName + "/" + decorRenderer.sortingOrder + "/" + decorRenderer.mode
            : "missing";
        lines.Add("- Decor Y-sort: " + decorSortingDescription);

        string verdict = valid ? "PASS" : "FAIL";
        Debug.Log($"[Environment Fixer] HOSPITAL FIXED VALIDATION: {verdict}\n" + string.Join("\n", lines), hospital);

        UnityEngine.Object previousSelection = Selection.activeObject;
        Selection.activeGameObject = largeRoot.gameObject;
        ValidateMergedFootprints();
        Selection.activeGameObject = smallRoot.gameObject;
        ValidateMergedFootprints();
        Selection.activeObject = previousSelection;
    }

    private static int CountTilemapCells(Tilemap tilemap)
    {
        if (tilemap == null)
            return 0;
        int count = 0;
        foreach (Vector3Int cell in tilemap.cellBounds.allPositionsWithin)
        {
            if (tilemap.HasTile(cell))
                count++;
        }
        return count;
    }

    private static GameObject CreateHospitalGroup(Transform parent, string name)
    {
        GameObject group = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(group, "Create hospital building group");
        Undo.SetTransformParent(group.transform, parent, "Parent hospital building group");
        group.transform.localPosition = Vector3.zero;
        group.transform.localRotation = Quaternion.identity;
        group.transform.localScale = Vector3.one;
        return group;
    }

    private static Tilemap CreateHospitalTilemapClone(
        Tilemap source,
        TilemapRenderer sourceRenderer,
        Transform parent,
        string name,
        int layer,
        Material materialOverride)
    {
        GameObject destinationObject = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
        Undo.RegisterCreatedObjectUndo(destinationObject, "Create hospital tilemap");
        Undo.SetTransformParent(destinationObject.transform, parent, "Parent hospital tilemap");
        destinationObject.transform.position = source.transform.position;
        destinationObject.transform.rotation = source.transform.rotation;
        destinationObject.transform.localScale = source.transform.lossyScale;
        destinationObject.layer = layer;

        Tilemap destination = destinationObject.GetComponent<Tilemap>();
        destination.animationFrameRate = source.animationFrameRate;
        destination.tileAnchor = source.tileAnchor;
        destination.orientation = source.orientation;
        destination.orientationMatrix = source.orientationMatrix;
        destination.color = source.color;

        TilemapRenderer renderer = destinationObject.GetComponent<TilemapRenderer>();
        renderer.sortingLayerName = "Gameplay";
        renderer.sortingOrder = 0;
        renderer.mode = TilemapRenderer.Mode.Individual;
        renderer.sortOrder = sourceRenderer.sortOrder;
        renderer.sharedMaterial = materialOverride != null ? materialOverride : sourceRenderer.sharedMaterial;
        return destination;
    }

    private static void CopyHospitalTileCell(Tilemap source, Tilemap destination, Vector3Int cell)
    {
        TileBase tile = source.GetTile(cell);
        TileFlags flags = source.GetTileFlags(cell);
        destination.SetTile(cell, tile);
        destination.SetTileFlags(cell, TileFlags.None);
        destination.SetTransformMatrix(cell, source.GetTransformMatrix(cell));
        destination.SetColor(cell, source.GetColor(cell));
        destination.SetTileFlags(cell, flags);
    }

    private static float DistanceToBounds2D(Bounds bounds, Vector3 point)
    {
        Vector3 closest = bounds.ClosestPoint(point);
        return Vector2.Distance(new Vector2(closest.x, closest.y), new Vector2(point.x, point.y));
    }

    private static void ConfigureHospitalWallCollider(Tilemap walls)
    {
        TilemapCollider2D collider = Undo.AddComponent<TilemapCollider2D>(walls.gameObject);
        collider.isTrigger = false;
        collider.ProcessTilemapChanges();
        EditorUtility.SetDirty(walls);
        EditorUtility.SetDirty(collider);
    }

    private static void MoveHospitalRoofToIndependentController(
        Tilemap roof,
        GameObject buildingRoot,
        float fadeDuration,
        float targetAlpha)
    {
        ConfigureHospitalRoofControllerOnTilemap(roof, fadeDuration, targetAlpha);
    }

    private static void ConfigureHospitalRoofControllerOnTilemap(
        Tilemap roof,
        float fadeDuration,
        float targetAlpha)
    {
        RoofVisibility controller = roof.GetComponent<RoofVisibility>();
        if (controller == null)
            controller = Undo.AddComponent<RoofVisibility>(roof.gameObject);
        Undo.RecordObject(controller, "Configure independent hospital roof controller");
        controller.roofTilemaps = new[] { roof };
        controller.fadeDuration = fadeDuration;
        controller.targetAlpha = targetAlpha;
        EditorUtility.SetDirty(controller);
    }

    private static void NormalizeHospitalVisualLayers(GameObject hospital, Material litMaterial, Tilemap emptyRoot)
    {
        foreach (Tilemap map in hospital.GetComponentsInChildren<Tilemap>(true))
        {
            if (map == emptyRoot || map.name.StartsWith(ProxyPrefix, StringComparison.Ordinal))
                continue;
            TilemapRenderer renderer = map.GetComponent<TilemapRenderer>();
            if (renderer == null)
                continue;

            Undo.RecordObject(renderer, "Normalize hospital renderer");
            string lowerName = map.name.ToLowerInvariant();
            bool isRoof = lowerName.StartsWith("nocnha");
            bool isFloor = lowerName.Contains("trangtrigiay");
            if (isRoof)
            {
                renderer.sortingLayerName = "Foreground";
                renderer.sortingOrder = 20;
                renderer.mode = TilemapRenderer.Mode.Chunk;
            }
            else if (isFloor)
            {
                renderer.sortingLayerName = "Default";
                renderer.sortingOrder = -30;
                renderer.mode = TilemapRenderer.Mode.Individual;
            }
            else
            {
                renderer.sortingLayerName = "Gameplay";
                renderer.sortingOrder = 0;
                renderer.mode = TilemapRenderer.Mode.Individual;
            }

            if (litMaterial != null)
                renderer.sharedMaterial = litMaterial;
            EditorUtility.SetDirty(renderer);
        }
    }

    private static int ExtractHospitalFarVisualCells(GameObject hospital, Bounds largeRoof, Bounds smallRoof)
    {
        Tilemap source = hospital.GetComponentsInChildren<Tilemap>(true)
            .FirstOrDefault(map => map.name == "trangtrisautuong");
        if (source == null)
            return 0;

        List<Vector3Int> farCells = new List<Vector3Int>();
        foreach (Vector3Int cell in source.cellBounds.allPositionsWithin)
        {
            if (!source.HasTile(cell))
                continue;
            Vector3 world = source.GetCellCenterWorld(cell);
            if (Mathf.Min(DistanceToBounds2D(largeRoof, world), DistanceToBounds2D(smallRoof, world)) > 20f)
                farCells.Add(cell);
        }
        if (farCells.Count == 0)
            return 0;

        Transform mapRoot = hospital.transform.parent;
        Transform externalRoot = mapRoot != null ? mapRoot.Find(ExternalObjectsName) : null;
        if (externalRoot == null)
        {
            GameObject externalObject = new GameObject(ExternalObjectsName);
            Undo.RegisterCreatedObjectUndo(externalObject, "Create external environment group");
            if (mapRoot != null)
                Undo.SetTransformParent(externalObject.transform, mapRoot, "Parent external environment group");
            externalObject.transform.localPosition = Vector3.zero;
            externalObject.transform.localRotation = Quaternion.identity;
            externalObject.transform.localScale = Vector3.one;
            externalRoot = externalObject.transform;
        }

        TilemapRenderer sourceRenderer = source.GetComponent<TilemapRenderer>();
        Tilemap destination = CreateHospitalTilemapClone(source, sourceRenderer, externalRoot,
            "hospital_far_visual_REVIEW", source.gameObject.layer,
            sourceRenderer != null ? sourceRenderer.sharedMaterial : null);
        TilemapRenderer destinationRenderer = destination.GetComponent<TilemapRenderer>();
        if (sourceRenderer != null)
        {
            destinationRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
            destinationRenderer.sortingOrder = sourceRenderer.sortingOrder;
            destinationRenderer.mode = sourceRenderer.mode;
        }

        Undo.RecordObject(source, "Extract far hospital visual cells");
        foreach (Vector3Int cell in farCells)
        {
            CopyHospitalTileCell(source, destination, cell);
            source.SetTile(cell, null);
        }
        source.CompressBounds();
        destination.CompressBounds();
        EditorUtility.SetDirty(source);
        EditorUtility.SetDirty(destination);
        return farCells.Count;
    }

    private static GameObject ResolveEnvironmentRoot(GameObject selected)
    {
        if (selected == null)
            return null;
        RoofVisibility roofOwner = selected.GetComponentInParent<RoofVisibility>(true);
        return roofOwner != null ? roofOwner.gameObject : selected;
    }

    private static bool ComponentHasMeaningfulSerializedData(Component component)
    {
        SerializedObject serialized = new SerializedObject(component);
        SerializedProperty iterator = serialized.GetIterator();
        if (!iterator.NextVisible(true))
            return false;
        while (iterator.NextVisible(false))
        {
            if (iterator.propertyPath == "m_Script")
                continue;
            switch (iterator.propertyType)
            {
                case SerializedPropertyType.ObjectReference:
                    if (iterator.objectReferenceValue != null) return true;
                    break;
                case SerializedPropertyType.ArraySize:
                    if (iterator.intValue > 0) return true;
                    break;
            }
        }
        return false;
    }

    private static string BuildQuickHouseReport(GameObject selected)
    {
        Tilemap[] maps = selected.GetComponentsInChildren<Tilemap>(true);
        List<string> details = new List<string>();
        Bounds? structuralEnvelope = FindStructuralEnvelope(maps);
        int duplicateComponents = selected.GetComponents<Component>()
            .Where(component => component != null && !(component is Transform))
            .GroupBy(component => component.GetType())
            .Sum(group => Mathf.Max(0, group.Count() - 1));
        int overlappingSolidColliders = selected.GetComponentsInChildren<Transform>(true)
            .Count(transform => transform.GetComponents<Collider2D>().Count(collider => collider.enabled && !collider.isTrigger) > 1);
        int authoredSolidPolygons = selected.GetComponentsInChildren<PolygonCollider2D>(true)
            .Count(collider => collider.enabled && !collider.isTrigger);
        List<string> externalPolygonNetworks = selected.GetComponentsInChildren<TilemapCollider2D>(true)
            .Where(collider => collider.enabled && !collider.isTrigger)
            .Select(collider => (collider, network: FindExternalPolygonNetwork(collider, selected)))
            .Where(item => item.network != null)
            .Select(item => $"{GetRelativePath(selected.transform, item.collider.transform)} => {GetHierarchyPath(item.network)}")
            .ToList();
        int outsideCandidates = 0;
        int lootCandidates = 0;

        foreach (Tilemap map in maps)
        {
            TilemapAudit audit = AuditTilemap(map);
            TilemapRenderer renderer = map.GetComponent<TilemapRenderer>();
            Dictionary<string, int> spriteUsage = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, List<Vector3Int>> spriteCells = new Dictionary<string, List<Vector3Int>>(StringComparer.OrdinalIgnoreCase);
            int fenceCells = 0;
            int storageCells = 0;
            List<Vector3Int> occupiedCells = new List<Vector3Int>();
            foreach (Vector3Int cell in map.cellBounds.allPositionsWithin)
            {
                if (!map.HasTile(cell)) continue;
                occupiedCells.Add(cell);
                Sprite sprite = map.GetSprite(cell);
                string name = sprite != null ? sprite.name : "<no sprite>";
                spriteUsage[name] = spriteUsage.TryGetValue(name, out int count) ? count + 1 : 1;
                if (!spriteCells.TryGetValue(name, out List<Vector3Int> positions))
                {
                    positions = new List<Vector3Int>();
                    spriteCells[name] = positions;
                }
                positions.Add(cell);
                string lower = name.ToLowerInvariant();
                if (ContainsAny(lower, "fence", "hang rao", "hàng rào", "barrier", "gate")) fenceCells++;
                if (ContainsAny(lower, "cabinet", "cupboard", "shelf", "fridge", "freezer", "counter", "kitchen", "food")) storageCells++;
            }
            outsideCandidates += fenceCells;
            lootCandidates += storageCells;
            string topSprites = string.Join(", ", spriteUsage.OrderByDescending(pair => pair.Value).Take(8)
                .Select(pair => $"{pair.Key}×{pair.Value}"));
            string spriteCellMap = string.Join(" | ", spriteCells.OrderByDescending(pair => pair.Value.Count)
                .Select(pair => $"{pair.Key}: {string.Join(",", pair.Value)}"));
            List<List<Vector3Int>> clusters = FindTileClusters(occupiedCells);
            int outsideClusterCells = 0;
            string clusterSummary = string.Join(", ", clusters.OrderByDescending(cluster => cluster.Count).Take(40)
                .Select(cluster =>
                {
                    int minX = cluster.Min(cell => cell.x);
                    int maxX = cluster.Max(cell => cell.x);
                    int minY = cluster.Min(cell => cell.y);
                    int maxY = cluster.Max(cell => cell.y);
                    Vector3 center = map.GetCellCenterWorld(new Vector3Int(
                        Mathf.RoundToInt((float)cluster.Average(cell => cell.x)),
                        Mathf.RoundToInt((float)cluster.Average(cell => cell.y)), 0));
                    bool outside = structuralEnvelope.HasValue && !Contains2D(structuralEnvelope.Value, center);
                    if (outside) outsideClusterCells += cluster.Count;
                    string clusterSprites = string.Join("/", cluster
                        .Select(cell => map.GetSprite(cell)?.name ?? "<none>")
                        .GroupBy(name => name)
                        .OrderByDescending(group => group.Count())
                        .Take(3)
                        .Select(group => $"{group.Key}×{group.Count()}"));
                    return $"{(outside ? "OUTSIDE " : string.Empty)}{cluster.Count}cell[{minX}..{maxX},{minY}..{maxY}]" +
                           $"@({center.x:0.##},{center.y:0.##})<{clusterSprites}>";
                }));
            details.Add($"  • {GetRelativePath(selected.transform, map.transform)}: {audit.TileCount} tile, " +
                        $"sort {(renderer != null ? renderer.sortingLayerName + "/" + renderer.sortingOrder + "/" + renderer.mode : "none")}, " +
                        $"collider {(audit.HasEnabledTilemapCollider ? "ON" : audit.HasTilemapCollider ? "disabled" : "off")}, " +
                        $"broad {audit.FullBoundsShapeCells.Count}, fence-keyword {fenceCells}, storage-keyword {storageCells}; " +
                        $"clusters {clusters.Count}, outside-envelope {outsideClusterCells} cell: {clusterSummary}; top: {topSprites}" +
                        (audit.TileCount <= 80 || (ContainsAny(map.name.ToLowerInvariant(), "tuong", "tường", "wall") && audit.TileCount <= 220)
                            ? $"\n      sprite-cells: {spriteCellMap}"
                            : string.Empty));
        }

        return $"[Environment Fixer] QUICK HOUSE REPORT '{selected.name}'\n" +
               $"Root duplicate component: {duplicateComponents}; object có collider solid chồng: {overlappingSolidColliders}; " +
               $"authored solid polygon trong root: {authoredSolidPolygons}; " +
               $"external polygon network trùng TilemapCollider: {externalPolygonNetworks.Count}" +
               (externalPolygonNetworks.Count > 0 ? $" [{string.Join(" | ", externalPolygonNetworks)}]" : string.Empty) + "; " +
               $"cell nghi hàng rào: {outsideCandidates}; cell nghi tủ/đồ ăn: {lootCandidates}.\n" +
               (structuralEnvelope.HasValue
                   ? $"Structural envelope (largest wall cluster + 4u): center {structuralEnvelope.Value.center}, size {structuralEnvelope.Value.size}.\n"
                   : "Structural envelope: không xác định.\n") +
               string.Join("\n", details) +
               "\nKết luận tự động chỉ là gợi ý. Cell ngoài phạm vi và loot phải duyệt Scene trước khi tách.";
    }

    private static Bounds? FindStructuralEnvelope(IEnumerable<Tilemap> maps)
    {
        List<(Tilemap map, List<Vector3Int> cluster)> candidates = new List<(Tilemap, List<Vector3Int>)>();
        foreach (Tilemap map in maps)
        {
            string lower = map.name.ToLowerInvariant();
            if (!ContainsAny(lower, "tuong", "tường", "wall")) continue;
            List<Vector3Int> occupied = new List<Vector3Int>();
            foreach (Vector3Int cell in map.cellBounds.allPositionsWithin)
                if (map.HasTile(cell)) occupied.Add(cell);
            List<Vector3Int> largest = FindTileClusters(occupied).OrderByDescending(cluster => cluster.Count).FirstOrDefault();
            if (largest != null && largest.Count > 0) candidates.Add((map, largest));
        }
        if (candidates.Count == 0) return null;
        (Tilemap map, List<Vector3Int> cluster) winner = candidates.OrderByDescending(item => item.cluster.Count).First();
        Vector3 first = winner.map.GetCellCenterWorld(winner.cluster[0]);
        Bounds bounds = new Bounds(first, Vector3.zero);
        foreach (Vector3Int cell in winner.cluster) bounds.Encapsulate(winner.map.GetCellCenterWorld(cell));
        bounds.Expand(new Vector3(8f, 8f, 2f));
        return bounds;
    }

    private static Transform FindExternalPolygonNetwork(TilemapCollider2D generated, GameObject selectedRoot)
    {
        if (generated == null || generated.shapeCount == 0) return null;
        PolygonCollider2D[] polygons = FindObjectsByType<PolygonCollider2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (PolygonCollider2D perimeter in polygons)
        {
            if (!perimeter.enabled || perimeter.isTrigger || perimeter.transform.IsChildOf(selectedRoot.transform)) continue;
            if (BoundsCoverageRatio2D(generated.bounds, perimeter.bounds) < 0.85f) continue;
            Transform network = perimeter.transform.parent;
            if (network == null) continue;
            PolygonCollider2D[] members = network.GetComponentsInChildren<PolygonCollider2D>(true);
            if (members.Count(member => member.enabled && !member.isTrigger &&
                                       member.gameObject.layer == LayerMask.NameToLayer("Obstacle")) < 3) continue;
            return network;
        }
        return null;
    }

    private static float BoundsCoverageRatio2D(Bounds first, Bounds second)
    {
        float minX = Mathf.Max(first.min.x, second.min.x);
        float maxX = Mathf.Min(first.max.x, second.max.x);
        float minY = Mathf.Max(first.min.y, second.min.y);
        float maxY = Mathf.Min(first.max.y, second.max.y);
        float intersection = Mathf.Max(0f, maxX - minX) * Mathf.Max(0f, maxY - minY);
        float firstArea = Mathf.Max(0.0001f, first.size.x * first.size.y);
        float secondArea = Mathf.Max(0.0001f, second.size.x * second.size.y);
        return intersection / Mathf.Min(firstArea, secondArea);
    }

    private static string GetHierarchyPath(Transform transform)
    {
        Stack<string> names = new Stack<string>();
        Transform current = transform;
        while (current != null)
        {
            names.Push(current.name);
            current = current.parent;
        }
        return string.Join("/", names);
    }

    private static bool ContainsAny(string value, params string[] tokens)
    {
        return tokens.Any(value.Contains);
    }

    private static bool Contains2D(Bounds bounds, Vector3 point)
    {
        return point.x >= bounds.min.x && point.x <= bounds.max.x &&
               point.y >= bounds.min.y && point.y <= bounds.max.y;
    }

    private static Vector3 GetClusterCenterWorld(Tilemap map, IReadOnlyCollection<Vector3Int> cluster)
    {
        return map.GetCellCenterWorld(new Vector3Int(
            Mathf.RoundToInt((float)cluster.Average(cell => cell.x)),
            Mathf.RoundToInt((float)cluster.Average(cell => cell.y)), 0));
    }

    private static List<List<Vector3Int>> FindTileClusters(IEnumerable<Vector3Int> occupiedCells)
    {
        HashSet<Vector3Int> remaining = new HashSet<Vector3Int>(occupiedCells);
        List<List<Vector3Int>> clusters = new List<List<Vector3Int>>();
        Vector3Int[] neighbors =
        {
            new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0),
            new Vector3Int(0, 1, 0), new Vector3Int(0, -1, 0),
            new Vector3Int(1, 1, 0), new Vector3Int(-1, 1, 0),
            new Vector3Int(1, -1, 0), new Vector3Int(-1, -1, 0)
        };
        while (remaining.Count > 0)
        {
            Vector3Int seed = remaining.First();
            Queue<Vector3Int> queue = new Queue<Vector3Int>();
            List<Vector3Int> cluster = new List<Vector3Int>();
            queue.Enqueue(seed);
            remaining.Remove(seed);
            while (queue.Count > 0)
            {
                Vector3Int current = queue.Dequeue();
                cluster.Add(current);
                foreach (Vector3Int offset in neighbors)
                {
                    Vector3Int neighbor = current + offset;
                    if (!remaining.Remove(neighbor)) continue;
                    queue.Enqueue(neighbor);
                }
            }
            clusters.Add(cluster);
        }
        return clusters;
    }

    private static string DescribePrefabOverrides(GameObject selectedRoot)
    {
        if (!PrefabUtility.IsPartOfPrefabInstance(selectedRoot))
            return "\n  • Prefab overrides: đang ở Prefab Mode/asset contents, không phải scene instance.";

        GameObject instanceRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(selectedRoot) ?? selectedRoot;
        PropertyModification[] modifications = PrefabUtility.GetPropertyModifications(instanceRoot) ?? Array.Empty<PropertyModification>();
        List<AddedGameObject> addedObjects = PrefabUtility.GetAddedGameObjects(instanceRoot);
        List<AddedComponent> addedComponents = PrefabUtility.GetAddedComponents(instanceRoot);
        List<RemovedGameObject> removedObjects = PrefabUtility.GetRemovedGameObjects(instanceRoot);
        List<RemovedComponent> removedComponents = PrefabUtility.GetRemovedComponents(instanceRoot);

        List<string> meaningful = modifications
            .Where(modification => modification.target != null &&
                                   modification.propertyPath != "m_Name" &&
                                   modification.propertyPath != "m_RootOrder")
            .Take(30)
            .Select(modification =>
            {
                string value = modification.objectReference != null
                    ? modification.objectReference.name
                    : modification.value;
                return $"{modification.target.name}.{modification.propertyPath}={value}";
            })
            .ToList();

        string addedNames = string.Join(", ", addedObjects
            .Where(item => item.instanceGameObject != null)
            .Select(item => item.instanceGameObject.name));
        string suffix = modifications.Length > meaningful.Count ? $", ... tổng {modifications.Length}" : string.Empty;
        return
            $"\n  • Prefab overrides: property {modifications.Length}, added GO {addedObjects.Count}" +
            (string.IsNullOrEmpty(addedNames) ? string.Empty : $" [{addedNames}]") +
            $", added component {addedComponents.Count}, removed GO {removedObjects.Count}, removed component {removedComponents.Count}" +
            (meaningful.Count == 0 ? string.Empty : $"\n      properties: {string.Join(" | ", meaningful)}{suffix}");
    }

    private static string GetRelativePath(Transform rootTransform, Transform target)
    {
        if (target == rootTransform)
            return "<root>";

        Stack<string> names = new Stack<string>();
        Transform current = target;
        while (current != null && current != rootTransform)
        {
            names.Push(current.name);
            current = current.parent;
        }
        return string.Join("/", names);
    }

    private static string DescribeCells(Tilemap tilemap, IReadOnlyList<Vector3Int> cells, string label)
    {
        if (cells == null || cells.Count == 0)
            return string.Empty;

        const int limit = 12;
        IEnumerable<string> descriptions = cells.Take(limit).Select(cell =>
        {
            Sprite sprite = tilemap.GetSprite(cell);
            Vector3 world = tilemap.GetCellCenterWorld(cell);
            return $"{cell}:{(sprite != null ? sprite.name : "<no sprite>")}@({world.x:0.###},{world.y:0.###})";
        });
        string suffix = cells.Count > limit ? $", ... +{cells.Count - limit}" : string.Empty;
        return $"\n      {label}: {string.Join(", ", descriptions)}{suffix}";
    }

    private static string DescribeNeighborhoods(Tilemap tilemap, IReadOnlyList<Vector3Int> centers)
    {
        if (centers == null || centers.Count == 0 || centers.Count > 8)
            return string.Empty;

        List<string> neighborhoods = new List<string>();
        foreach (Vector3Int center in centers)
        {
            List<string> neighbors = new List<string>();
            for (int y = 1; y >= -1; y--)
            {
                for (int x = -1; x <= 1; x++)
                {
                    Vector3Int cell = center + new Vector3Int(x, y, 0);
                    Sprite sprite = tilemap.GetSprite(cell);
                    if (sprite != null)
                        neighbors.Add($"{cell}:{sprite.name}");
                }
            }
            neighborhoods.Add($"around {center} => {string.Join(", ", neighbors)}");
        }
        return neighborhoods.Count == 0 ? string.Empty : "\n      " + string.Join("\n      ", neighborhoods);
    }

    [MenuItem("Tools/Environment/Run Fixer Smoke Test")]
    public static void RunFixerSmokeTest()
    {
        const string testPrefabPath = "Assets/Khoa/House/nhachinhxaydautien.prefab";
        GameObject contents = null;
        try
        {
            contents = PrefabUtility.LoadPrefabContents(testPrefabPath);
            Tilemap source = contents.GetComponentsInChildren<Tilemap>(true)
                .FirstOrDefault(map => CountCollidableCells(map) > 1);
            if (source == null)
                throw new InvalidOperationException("Không tìm thấy Tilemap có ít nhất 2 cell collider trong prefab test.");

            Vector3Int excluded = default;
            bool foundExcluded = false;
            foreach (Vector3Int position in source.cellBounds.allPositionsWithin)
            {
                if (!source.HasTile(position) || source.GetColliderType(position) == Tile.ColliderType.None)
                    continue;
                excluded = position;
                foundExcluded = true;
                break;
            }
            if (!foundExcluded)
                throw new InvalidOperationException("Không tìm thấy cell collider để loại trong smoke test.");
            int sourceCount = CountCollidableCells(source);
            Tilemap proxy = BuildCollisionProxy(source, new HashSet<Vector3Int> { excluded }, source.gameObject.layer, false, out int copied);

            if (proxy == null || copied != sourceCount - 1 || CountCollidableCells(proxy) != sourceCount - 1)
                throw new InvalidOperationException($"Proxy count sai. Source {sourceCount}, copied {copied}.");
            if (proxy.GetComponent<TilemapRenderer>() == null || proxy.GetComponent<TilemapRenderer>().enabled)
                throw new InvalidOperationException("Renderer của collision proxy chưa được tắt.");
            if (proxy.GetComponent<TilemapCollider2D>() == null || !proxy.GetComponent<TilemapCollider2D>().enabled)
                throw new InvalidOperationException("Collision proxy thiếu TilemapCollider2D hoạt động.");
            TilemapCollider2D sourceCollider = source.GetComponent<TilemapCollider2D>();
            if (sourceCollider != null && sourceCollider.enabled)
                throw new InvalidOperationException("TilemapCollider2D nguồn chưa được tắt.");

            Debug.Log($"[Environment Fixer] SMOKE TEST PASS — source {sourceCount} cell, proxy {copied} cell, loại đúng 1 cell; không lưu prefab.");
        }
        catch (Exception exception)
        {
            Debug.LogError($"[Environment Fixer] SMOKE TEST FAIL: {exception.Message}\n{exception.StackTrace}");
        }
        finally
        {
            if (contents != null)
                PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    [MenuItem("Tools/Environment/Select Tilemap Collider In Current Root")]
    public static void SelectTilemapColliderInCurrentRoot()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            Debug.LogWarning("[Environment Fixer] Hãy chọn prefab root hoặc một object con.");
            return;
        }

        Transform searchRoot = ResolveEnvironmentRoot(selected).transform;
        TilemapCollider2D collider = searchRoot.GetComponentInChildren<TilemapCollider2D>(true);
        if (collider == null)
        {
            Debug.LogWarning($"[Environment Fixer] Không tìm thấy TilemapCollider2D dưới '{searchRoot.name}'.", searchRoot);
            return;
        }

        SelectAndFrame(collider.gameObject);
        Debug.Log($"[Environment Fixer] Đã chọn collider owner '{collider.name}'.", collider);
    }

    [MenuItem("Tools/Environment/Apply Selected Instance To Prefab (Auto Backup)")]
    public static void ApplySelectedInstanceToPrefabWithBackup()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            Debug.LogWarning("[Environment Fixer] Hãy chọn prefab instance hoặc một object con trong Scene.");
            return;
        }

        RoofVisibility roofOwner = selected.GetComponentInParent<RoofVisibility>(true);
        if (roofOwner != null)
            selected = roofOwner.gameObject;
        GameObject instanceRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(selected);
        if (instanceRoot == null)
        {
            Debug.LogError("[Environment Fixer] Selection không thuộc prefab instance trong Scene.", selected);
            return;
        }

        string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instanceRoot);
        if (string.IsNullOrEmpty(prefabPath))
        {
            Debug.LogError("[Environment Fixer] Không xác định được prefab asset path.", instanceRoot);
            return;
        }

        string directory = System.IO.Path.GetDirectoryName(prefabPath)?.Replace('\\', '/');
        string fileName = System.IO.Path.GetFileNameWithoutExtension(prefabPath);
        string backupPath = $"{directory}/{fileName}_preapply_{DateTime.Now:yyyyMMdd_HHmmss}.prefab";
        if (!AssetDatabase.CopyAsset(prefabPath, backupPath))
        {
            Debug.LogError($"[Environment Fixer] Không tạo được backup '{backupPath}'. Dừng Apply.");
            return;
        }

        PrefabUtility.ApplyPrefabInstance(instanceRoot, InteractionMode.AutomatedAction);
        AssetDatabase.SaveAssets();
        Debug.Log(
            $"[Environment Fixer] Đã Apply instance '{instanceRoot.name}' vào '{prefabPath}'. Backup: '{backupPath}'.",
            instanceRoot);
    }

    [MenuItem("Tools/Environment/Repair Broad Collider Cells Using Donor")]
    public static void RepairBroadColliderCellsUsingDonor()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            Debug.LogWarning("[Environment Fixer] Hãy chọn prefab root hoặc một object con.");
            return;
        }

        Transform searchRoot = ResolveEnvironmentRoot(selected).transform;
        Tilemap source = searchRoot.GetComponentsInChildren<Tilemap>(true)
            .FirstOrDefault(tilemap => tilemap.GetComponent<TilemapCollider2D>() != null &&
                                       !tilemap.name.StartsWith(ProxyPrefix, StringComparison.Ordinal));
        if (source == null)
        {
            Debug.LogError($"[Environment Fixer] Không tìm thấy visual Tilemap có TilemapCollider2D dưới '{searchRoot.name}'.");
            return;
        }

        RepairBroadColliderCellsUsingDonor(source);
    }

    private static bool RepairBroadColliderCellsUsingDonor(Tilemap source, Tilemap fallbackDonorSource = null)
    {
        TilemapAudit audit = AuditTilemap(source);
        HashSet<Vector3Int> broadCells = new HashSet<Vector3Int>(audit.FullBoundsShapeCells);
        HashSet<Vector3Int> elevatedDoorHeadCells = FindElevatedOnlyPhysicsShapeCells(source);
        Dictionary<Vector3Int, List<Vector3Int>> doorSideConstraints =
            FindDoorSideConstraints(source, elevatedDoorHeadCells);
        Dictionary<Vector3Int, List<Rect>> doorProjectionClearances =
            FindDoorProjectionClearances(source, elevatedDoorHeadCells);
        HashSet<Vector3Int> excludedCells = new HashSet<Vector3Int>(broadCells);
        excludedCells.UnionWith(elevatedDoorHeadCells);
        excludedCells.UnionWith(doorSideConstraints.Keys);
        excludedCells.UnionWith(doorProjectionClearances.Keys);
        if (excludedCells.Count == 0)
        {
            Debug.Log($"[Environment Fixer] '{source.name}' không có broad hoặc collider nóc cửa cần sửa.");
            return false;
        }

        Dictionary<Vector3Int, KeyValuePair<Tilemap, Vector3Int>> replacements =
            new Dictionary<Vector3Int, KeyValuePair<Tilemap, Vector3Int>>();
        HashSet<Vector3Int> fallbackRejected = fallbackDonorSource != null
            ? new HashSet<Vector3Int>(AuditTilemap(fallbackDonorSource).FullBoundsShapeCells)
            : new HashSet<Vector3Int>();
        if (fallbackDonorSource != null)
            fallbackRejected.UnionWith(FindElevatedOnlyPhysicsShapeCells(fallbackDonorSource));
        foreach (Vector3Int badCell in broadCells)
        {
            if (TryFindNearestDonor(source, badCell, excludedCells, out Vector3Int donorCell))
                replacements[badCell] = new KeyValuePair<Tilemap, Vector3Int>(source, donorCell);
            else if (fallbackDonorSource != null &&
                     TryFindDonorForSprite(fallbackDonorSource, badCell, source.GetSprite(badCell),
                         fallbackRejected, out donorCell))
                replacements[badCell] = new KeyValuePair<Tilemap, Vector3Int>(fallbackDonorSource, donorCell);
            else
                Debug.LogWarning($"[Environment Fixer] Không tìm được donor cùng hướng cho cell {badCell} ({source.GetSprite(badCell)?.name}).");
        }

        if (replacements.Count == 0 && broadCells.Count > 0)
            return false;

        Tilemap proxy = BuildCollisionProxy(source, excludedCells, source.gameObject.layer, true, out int copied);
        if (proxy == null)
            return false;

        Transform parent = source.transform.parent;
        Transform oldPatch = parent != null ? parent.Find(BroadWallPatchPrefix + source.name) : null;
        if (oldPatch != null)
            Undo.DestroyObjectImmediate(oldPatch.gameObject);
        GameObject patchObject = new GameObject(BroadWallPatchPrefix + source.name);
        Undo.RegisterCreatedObjectUndo(patchObject, "Create broad wall foot patches");
        if (parent != null)
            Undo.SetTransformParent(patchObject.transform, parent, "Parent broad wall foot patches");
        patchObject.transform.localPosition = Vector3.zero;
        patchObject.transform.localRotation = Quaternion.identity;
        patchObject.transform.localScale = Vector3.one;
        patchObject.layer = source.gameObject.layer;
        PolygonCollider2D patchCollider = Undo.AddComponent<PolygonCollider2D>(patchObject);
        patchCollider.pathCount = 0;

        List<string> mapping = new List<string>();
        foreach (KeyValuePair<Vector3Int, KeyValuePair<Tilemap, Vector3Int>> pair in replacements)
        {
            Tilemap donorSource = pair.Value.Key;
            Vector3Int donorCell = pair.Value.Value;
            Vector2[] footPath = ExtractBottomFootPathAtCell(
                source, pair.Key, donorSource, donorCell, patchObject.transform);
            if (footPath == null || footPath.Length < 3)
            {
                Debug.LogWarning($"[Environment Fixer] Không trích được chân tường cho cell {pair.Key}; " +
                                 "cell được để trống collider thay vì dùng full-body shape.", source);
                continue;
            }
            int pathIndex = patchCollider.pathCount;
            patchCollider.pathCount++;
            patchCollider.SetPath(pathIndex, footPath);
            mapping.Add($"{pair.Key}:{source.GetSprite(pair.Key)?.name} <- " +
                        $"{donorSource.transform.parent?.name}/{donorCell}:{donorSource.GetSprite(donorCell)?.name}");
        }
        proxy.GetComponent<TilemapCollider2D>()?.ProcessTilemapChanges();
        EditorUtility.SetDirty(proxy);
        EditorUtility.SetDirty(patchCollider);
        EditorUtility.SetDirty(patchObject);
        PolygonCollider2D doorSidePatch = BuildDoorSideFootPatches(
            source, doorSideConstraints, doorProjectionClearances);
        Selection.activeGameObject = proxy.gameObject;
        Debug.Log(
            $"[Environment Fixer] Donor repair hoàn tất trên '{source.name}': proxy {copied} cell tốt + " +
            $"{patchCollider.pathCount} Polygon chân tường; đã loại hẳn {elevatedDoorHeadCells.Count} cell nóc cửa lơ lửng. " +
            $"Đã cắt {doorSideConstraints.Count} cell cạnh cửa thành {doorSidePatch?.pathCount ?? 0} path không lấn lòng cửa. " +
            $"Đã khoét clearance trên {doorProjectionClearances.Count} cell tường chiếu chéo qua cửa. " +
            "Donor chỉ cung cấp dải thấp nhất, không copy shape lơ lửng. " +
            string.Join(" | ", mapping), proxy);
        return true;
    }

    [MenuItem("Tools/Environment/Validate All Collider Proxies In Scene")]
    public static void ValidateAllColliderProxiesInScene()
    {
        Tilemap[] proxies = FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(tilemap => tilemap.name.StartsWith(ProxyPrefix, StringComparison.Ordinal))
            .ToArray();
        ValidateProxySet(proxies, "Scene");
    }

    [MenuItem("Tools/Environment/Validate Collider Proxies Under Selected Root")]
    public static void ValidateColliderProxiesUnderSelectedRoot()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            Debug.LogWarning("[Environment Fixer] Hãy chọn prefab root hoặc object con.");
            return;
        }

        Transform rootTransform = ResolveEnvironmentRoot(selected).transform;
        Tilemap[] proxies = rootTransform.GetComponentsInChildren<Tilemap>(true)
            .Where(tilemap => tilemap.name.StartsWith(ProxyPrefix, StringComparison.Ordinal))
            .ToArray();
        ValidateProxySet(proxies, rootTransform.name);
    }

    private static void ValidateProxySet(IReadOnlyCollection<Tilemap> proxies, string scope)
    {
        if (proxies.Count == 0)
        {
            Debug.LogWarning($"[Environment Fixer] '{scope}' không có collision proxy.");
            return;
        }

        int passed = 0;
        List<string> failures = new List<string>();
        foreach (Tilemap proxy in proxies)
        {
            string sourceName = proxy.name.Substring(ProxyPrefix.Length);
            Transform parent = proxy.transform.parent;
            Tilemap source = parent == null
                ? null
                : parent.Cast<Transform>()
                    .Where(child => child.name == sourceName)
                    .Select(child => child.GetComponent<Tilemap>())
                    .FirstOrDefault(tilemap => tilemap != null);
            TilemapRenderer renderer = proxy.GetComponent<TilemapRenderer>();
            TilemapCollider2D proxyCollider = proxy.GetComponent<TilemapCollider2D>();
            TilemapCollider2D sourceCollider = source != null ? source.GetComponent<TilemapCollider2D>() : null;
            TilemapAudit proxyAudit = AuditTilemap(proxy);
            string lowerSourceName = source != null ? source.name.ToLowerInvariant() : string.Empty;
            bool isDecorSubset = source != null && ContainsAny(lowerSourceName, "decor", "decord");

            List<string> issues = new List<string>();
            if (source == null) issues.Add("missing source");
            if (renderer == null || renderer.enabled) issues.Add("renderer must be disabled");
            if (proxyCollider == null || !proxyCollider.enabled || proxyCollider.shapeCount == 0) issues.Add("proxy collider inactive/empty");
            if (proxy.gameObject.layer != LayerMask.NameToLayer("Obstacle")) issues.Add("proxy is not on Obstacle");
            if (isDecorSubset)
                issues.Add("unsafe decor proxy: use a small authored foot Polygon or no collider");
            else
            {
                if (sourceCollider != null && sourceCollider.enabled) issues.Add("source collider still active");
                if (source != null)
                {
                    int missingCells = CountCollidableCells(source) - CountCollidableCells(proxy);
                    Transform patchTransform = parent.Find(BroadWallPatchPrefix + source.name);
                    PolygonCollider2D footPatch = patchTransform != null
                        ? patchTransform.GetComponent<PolygonCollider2D>()
                        : null;
                    if (missingCells < 0)
                        issues.Add("proxy has unexpected extra collider cells");
                    else if (missingCells > 0 &&
                             (footPatch == null || !footPatch.enabled || footPatch.isTrigger ||
                              footPatch.pathCount < missingCells ||
                              footPatch.gameObject.layer != LayerMask.NameToLayer("Obstacle")))
                        issues.Add($"{missingCells} excluded cells lack valid foot patches");
                }
            }
            if (proxyAudit.FullBoundsShapeCells.Count > 0) issues.Add($"{proxyAudit.FullBoundsShapeCells.Count} broad cells remain");

            if (issues.Count == 0)
                passed++;
            else
                failures.Add($"{proxy.name}: {string.Join(", ", issues)}");
        }

        if (failures.Count == 0)
            Debug.Log($"[Environment Fixer] PROXY VALIDATION PASS '{scope}' — {passed}/{proxies.Count} proxy hợp lệ.");
        else
            Debug.LogError($"[Environment Fixer] PROXY VALIDATION FAIL '{scope}' — pass {passed}/{proxies.Count}: {string.Join(" | ", failures)}");
    }

    [MenuItem("Tools/Environment/Scan A* Current Scene")]
    public static void ScanAstarCurrentSceneMenu()
    {
        ScanAstar();
    }

    private static bool TryFindNearestDonor(
        Tilemap tilemap,
        Vector3Int badCell,
        ISet<Vector3Int> rejectedCells,
        out Vector3Int donorCell)
    {
        Sprite badSprite = tilemap.GetSprite(badCell);
        return TryFindDonorForSprite(tilemap, badCell, badSprite, rejectedCells, out donorCell);
    }

    private static bool TryFindDonorForSprite(
        Tilemap tilemap,
        Vector3Int targetCell,
        Sprite badSprite,
        ISet<Vector3Int> rejectedCells,
        out Vector3Int donorCell)
    {
        donorCell = default;
        string direction = GetSpriteDirectionSuffix(badSprite != null ? badSprite.name : string.Empty);
        List<Vector3Int> candidates = new List<Vector3Int>();
        foreach (Vector3Int candidate in tilemap.cellBounds.allPositionsWithin)
        {
            if (rejectedCells.Contains(candidate) || !tilemap.HasTile(candidate) ||
                tilemap.GetColliderType(candidate) == Tile.ColliderType.None)
                continue;
            Sprite sprite = tilemap.GetSprite(candidate);
            if (sprite == null || IsBroadPhysicsShape(sprite) || IsElevatedOnlyPhysicsShape(sprite) ||
                (!string.IsNullOrEmpty(direction) && GetSpriteDirectionSuffix(sprite.name) != direction))
                continue;
            candidates.Add(candidate);
        }
        if (candidates.Count == 0)
            return false;

        string preferredSprite = candidates
            .GroupBy(candidate => tilemap.GetSprite(candidate).name)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .First().Key;
        donorCell = candidates
            .Where(candidate => tilemap.GetSprite(candidate).name == preferredSprite)
            .OrderBy(candidate => Mathf.Abs(candidate.x - targetCell.x) + Mathf.Abs(candidate.y - targetCell.y))
            .First();
        return true;
    }

    private static string GetSpriteDirectionSuffix(string spriteName)
    {
        if (string.IsNullOrEmpty(spriteName))
            return string.Empty;
        int underscore = spriteName.LastIndexOf('_');
        if (underscore < 0 || underscore >= spriteName.Length - 1)
            return string.Empty;
        string suffix = spriteName.Substring(underscore + 1).Trim();
        return suffix.Length <= 2 ? suffix : string.Empty;
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += DuringSceneGui;
        collisionLayer = LayerMask.NameToLayer("Obstacle");
        if (collisionLayer < 0)
            collisionLayer = 0;

        string[] layers = SortingLayer.layers.Select(layer => layer.name).ToArray();
        sortingLayerIndex = Mathf.Max(0, Array.IndexOf(layers, "Gameplay"));
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= DuringSceneGui;
        drawingPolygon = false;
        drawingPoints.Clear();
    }

    private void OnGUI()
    {
        BuildStyles();

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Environment Collider & Sorting Fixer", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Ưu tiên collider đã vẽ trong Sprite/Tile asset. Polygon chỉ dùng để vá cell bị None hoặc Sprite không có Physics Shape. " +
            "Tool không tự đoán nhóm tường trước/sau; batch sorting chỉ chạy trên các mục được đánh dấu.",
            MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            root = (GameObject)EditorGUILayout.ObjectField("Root / Prefab", root, typeof(GameObject), true);
            if (GUILayout.Button("Dùng Selection", GUILayout.Width(105)))
            {
                root = Selection.activeGameObject;
                Analyze();
            }
        }

        using (new EditorGUI.DisabledScope(root == null))
        {
            if (GUILayout.Button("Quét lại Collider + Sorting", GUILayout.Height(28)))
                Analyze();
        }

        if (root == null)
            return;

        scroll = EditorGUILayout.BeginScrollView(scroll);
        DrawSummary();
        DrawTilemapSection();
        DrawSpriteSection();
        DrawSortingSection();
        DrawPolygonPatchSection();
        EditorGUILayout.EndScrollView();
    }

    private void BuildStyles()
    {
        if (smallBadge != null)
            return;

        smallBadge = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            padding = new RectOffset(4, 4, 1, 1)
        };
        warningBadge = new GUIStyle(smallBadge);
        warningBadge.normal.textColor = new Color(1f, 0.55f, 0.15f);
        goodBadge = new GUIStyle(smallBadge);
        goodBadge.normal.textColor = new Color(0.35f, 0.8f, 0.4f);
    }

    private void Analyze()
    {
        tilemaps.Clear();
        sprites.Clear();
        pickedCells.Clear();
        reviewTilemap = null;

        if (root == null)
            return;

        foreach (Tilemap tilemap in root.GetComponentsInChildren<Tilemap>(true))
        {
            TilemapAudit audit = AuditTilemap(tilemap);
            // Never silently opt a renderer into a mutation. The recommendation
            // buttons below make the suggested scope explicit and reviewable.
            audit.Included = false;
            tilemaps.Add(audit);
        }

        foreach (SpriteRenderer renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
        {
            Sprite sprite = renderer.sprite;
            int shapeCount = sprite != null ? sprite.GetPhysicsShapeCount() : 0;
            bool hasCollider = renderer.GetComponent<Collider2D>() != null;
            sprites.Add(new SpriteAudit
            {
                Renderer = renderer,
                PhysicsShapeCount = shapeCount,
                HasCollider = hasCollider,
                Included = false
            });
        }

        TilemapAudit firstMissing = tilemaps.FirstOrDefault(item => item.MissingCells.Count > 0);
        if (firstMissing != null)
            reviewTilemap = firstMissing.Tilemap;

        Repaint();
        SceneView.RepaintAll();
    }

    private static TilemapAudit AuditTilemap(Tilemap tilemap)
    {
        TilemapAudit audit = new TilemapAudit { Tilemap = tilemap };
        BoundsInt bounds = tilemap.cellBounds;

        foreach (Vector3Int position in bounds.allPositionsWithin)
        {
            if (!tilemap.HasTile(position))
                continue;

            audit.TileCount++;
            Tile.ColliderType colliderType = tilemap.GetColliderType(position);
            switch (colliderType)
            {
                case Tile.ColliderType.Grid:
                    audit.GridColliderCount++;
                    audit.GridCells.Add(position);
                    break;
                case Tile.ColliderType.Sprite:
                {
                    audit.SpriteColliderCount++;
                    Sprite sprite = tilemap.GetSprite(position);
                    if (sprite == null || sprite.GetPhysicsShapeCount() == 0)
                    {
                        audit.SpriteWithoutShapeCount++;
                        audit.MissingCells.Add(position);
                    }
                    else if (IsBroadPhysicsShape(sprite))
                    {
                        audit.FullBoundsShapeCells.Add(position);
                    }
                    break;
                }
                default:
                    audit.NoneColliderCount++;
                    audit.MissingCells.Add(position);
                    break;
            }
        }

        return audit;
    }

    private static string DescribeSpritePhysicsBand(Sprite sprite)
    {
        if (sprite == null || sprite.GetPhysicsShapeCount() == 0)
            return "shape=none";

        float minY = float.PositiveInfinity;
        float maxY = float.NegativeInfinity;
        float area = 0f;
        List<Vector2> points = new List<Vector2>();
        for (int shapeIndex = 0; shapeIndex < sprite.GetPhysicsShapeCount(); shapeIndex++)
        {
            points.Clear();
            sprite.GetPhysicsShape(shapeIndex, points);
            foreach (Vector2 point in points)
            {
                minY = Mathf.Min(minY, point.y);
                maxY = Mathf.Max(maxY, point.y);
            }
            area += Mathf.Abs(SignedPolygonArea(points));
        }
        float height = Mathf.Max(sprite.bounds.size.y, Mathf.Epsilon);
        float normalizedMin = (minY - sprite.bounds.min.y) / height;
        float normalizedMax = (maxY - sprite.bounds.min.y) / height;
        float boundsArea = Mathf.Max(Mathf.Abs(sprite.bounds.size.x * sprite.bounds.size.y), Mathf.Epsilon);
        return $"shapes={sprite.GetPhysicsShapeCount()}, y={normalizedMin:0.###}..{normalizedMax:0.###}, " +
               $"height={(normalizedMax - normalizedMin):0.###}, area={area / boundsArea:0.###}, " +
               $"broad={IsBroadPhysicsShape(sprite)}, elevated={IsElevatedOnlyPhysicsShape(sprite)}";
    }

    private static bool IsBroadPhysicsShape(Sprite sprite)
    {
        float boundsArea = Mathf.Abs(sprite.bounds.size.x * sprite.bounds.size.y);
        if (boundsArea <= Mathf.Epsilon)
            return false;

        float shapeArea = 0f;
        float shapeMinY = float.PositiveInfinity;
        float shapeMaxY = float.NegativeInfinity;
        List<Vector2> points = new List<Vector2>();
        int shapeCount = sprite.GetPhysicsShapeCount();
        for (int shapeIndex = 0; shapeIndex < shapeCount; shapeIndex++)
        {
            points.Clear();
            sprite.GetPhysicsShape(shapeIndex, points);
            if (points.Count < 3)
                continue;

            foreach (Vector2 point in points)
            {
                shapeMinY = Mathf.Min(shapeMinY, point.y);
                shapeMaxY = Mathf.Max(shapeMaxY, point.y);
            }

            float twiceArea = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 a = points[i];
                Vector2 b = points[(i + 1) % points.Count];
                twiceArea += a.x * b.y - b.x * a.y;
            }
            shapeArea += Mathf.Abs(twiceArea) * 0.5f;
        }

        // This is deliberately a warning, not an automatic mutation. A valid
        // solid tile may also cover its full bounds; the review overlay lets the
        // user distinguish that case from a wall sprite whose collider should
        // only follow its foot.
        float heightRatio = float.IsInfinity(shapeMinY)
            ? 0f
            : (shapeMaxY - shapeMinY) / Mathf.Max(sprite.bounds.size.y, Mathf.Epsilon);
        return shapeArea / boundsArea >= 0.85f || heightRatio >= 0.55f;
    }

    private static bool IsElevatedOnlyPhysicsShape(Sprite sprite)
    {
        if (sprite == null || sprite.GetPhysicsShapeCount() == 0 || sprite.bounds.size.y <= Mathf.Epsilon)
            return false;

        float minimumShapeY = float.PositiveInfinity;
        List<Vector2> points = new List<Vector2>();
        for (int shapeIndex = 0; shapeIndex < sprite.GetPhysicsShapeCount(); shapeIndex++)
        {
            points.Clear();
            sprite.GetPhysicsShape(shapeIndex, points);
            foreach (Vector2 point in points)
                minimumShapeY = Mathf.Min(minimumShapeY, point.y);
        }
        if (float.IsInfinity(minimumShapeY))
            return false;

        float normalizedMinimum = (minimumShapeY - sprite.bounds.min.y) / sprite.bounds.size.y;
        // Wall A5 is the authored lintel/overhead strip used above openings. The S
        // sprite in school starts at 0.445 because of one row of transparent padding,
        // while the hospital variants start above 0.45. Keep the generic threshold
        // conservative and explicitly include that known lintel family.
        return normalizedMinimum >= 0.45f ||
               (normalizedMinimum >= 0.40f &&
                sprite.name.StartsWith("Wall A5", StringComparison.OrdinalIgnoreCase));
    }

    private static HashSet<Vector3Int> FindElevatedOnlyPhysicsShapeCells(Tilemap tilemap)
    {
        HashSet<Vector3Int> result = new HashSet<Vector3Int>();
        if (tilemap == null)
            return result;
        foreach (Vector3Int cell in tilemap.cellBounds.allPositionsWithin)
        {
            if (tilemap.HasTile(cell) && IsElevatedOnlyPhysicsShape(tilemap.GetSprite(cell)))
                result.Add(cell);
        }
        return result;
    }

    private static int CountElevatedOnlyPhysicsShapeCells(Tilemap tilemap)
    {
        return FindElevatedOnlyPhysicsShapeCells(tilemap).Count;
    }

    private static Dictionary<Vector3Int, List<Vector3Int>> FindDoorSideConstraints(
        Tilemap source,
        ISet<Vector3Int> doorHeadCells)
    {
        Dictionary<Vector3Int, List<Vector3Int>> result = new Dictionary<Vector3Int, List<Vector3Int>>();
        foreach (Vector3Int doorCell in doorHeadCells)
        {
            string direction = GetSpriteDirectionSuffix(source.GetSprite(doorCell)?.name ?? string.Empty);
            Vector3Int[] offsets = direction == "W" || direction == "E"
                ? new[] { Vector3Int.left, Vector3Int.right }
                : new[] { Vector3Int.down, Vector3Int.up };
            foreach (Vector3Int offset in offsets)
            {
                // Hospital wall sprites are wider than one isometric cell, so a tile two or
                // three cells away can still project into the doorway. Trim the contiguous
                // wall run, not only the immediately adjacent cell.
                for (int distance = 1; distance <= 3; distance++)
                {
                    Vector3Int sideCell = doorCell + offset * distance;
                    if (!source.HasTile(sideCell))
                        break;
                    if (doorHeadCells.Contains(sideCell))
                        continue;
                    Sprite sideSprite = source.GetSprite(sideCell);
                    if (sideSprite == null || IsBroadPhysicsShape(sideSprite) ||
                        IsElevatedOnlyPhysicsShape(sideSprite) || source.GetColliderType(sideCell) == Tile.ColliderType.None)
                        continue;
                    if (!result.TryGetValue(sideCell, out List<Vector3Int> doors))
                    {
                        doors = new List<Vector3Int>();
                        result.Add(sideCell, doors);
                    }
                    if (!doors.Contains(doorCell))
                        doors.Add(doorCell);
                }
            }
        }
        return result;
    }

    private static PolygonCollider2D BuildDoorSideFootPatches(
        Tilemap source,
        IReadOnlyDictionary<Vector3Int, List<Vector3Int>> constraints,
        IReadOnlyDictionary<Vector3Int, List<Rect>> projectionClearances)
    {
        Transform parent = source.transform.parent;
        Transform oldPatch = parent != null ? parent.Find(DoorSidePatchPrefix + source.name) : null;
        if (oldPatch != null)
            Undo.DestroyObjectImmediate(oldPatch.gameObject);

        GameObject patchObject = new GameObject(DoorSidePatchPrefix + source.name);
        Undo.RegisterCreatedObjectUndo(patchObject, "Create door-side foot patches");
        if (parent != null)
            Undo.SetTransformParent(patchObject.transform, parent, "Parent door-side foot patches");
        patchObject.transform.localPosition = Vector3.zero;
        patchObject.transform.localRotation = Quaternion.identity;
        patchObject.transform.localScale = Vector3.one;
        patchObject.layer = source.gameObject.layer;
        PolygonCollider2D collider = Undo.AddComponent<PolygonCollider2D>(patchObject);
        collider.pathCount = 0;

        HashSet<Vector3Int> cells = new HashSet<Vector3Int>(constraints.Keys);
        cells.UnionWith(projectionClearances.Keys);
        foreach (Vector3Int cell in cells)
        {
            List<List<Vector2>> paths = GetSingleCellShapePathsWorld(source, cell);
            foreach (List<Vector2> originalPath in paths)
            {
                List<Vector2> clipped = new List<Vector2>(originalPath);
                Vector2 sideCenter = source.GetCellCenterWorld(cell);
                if (constraints.TryGetValue(cell, out List<Vector3Int> adjacentDoors))
                {
                    foreach (Vector3Int doorCell in adjacentDoors)
                    {
                        Vector2 doorCenter = source.GetCellCenterWorld(doorCell);
                        Vector2 normalTowardDoor = (doorCenter - sideCenter).normalized;
                        Vector2 boundary = (doorCenter + sideCenter) * 0.5f;
                        clipped = ClipPolygonToHalfPlane(clipped, boundary, normalTowardDoor);
                        if (clipped.Count < 3)
                            break;
                    }
                }
                if (clipped.Count < 3 || Mathf.Abs(SignedPolygonArea(clipped)) < 0.002f)
                    continue;
                List<List<Vector2>> pieces = new List<List<Vector2>> { clipped };
                if (projectionClearances.TryGetValue(cell, out List<Rect> clearances))
                {
                    foreach (Rect clearance in clearances)
                        pieces = pieces.SelectMany(piece => SubtractAxisAlignedRect(piece, clearance)).ToList();
                }
                foreach (List<Vector2> piece in pieces)
                {
                    if (piece.Count < 3 || Mathf.Abs(SignedPolygonArea(piece)) < 0.002f)
                        continue;
                    Vector2[] localPath = piece
                        .Select(point => (Vector2)patchObject.transform.InverseTransformPoint(point))
                        .ToArray();
                    int pathIndex = collider.pathCount;
                    collider.pathCount++;
                    collider.SetPath(pathIndex, localPath);
                }
            }
        }
        EditorUtility.SetDirty(collider);
        EditorUtility.SetDirty(patchObject);
        return collider;
    }

    private static Dictionary<Vector3Int, List<Rect>> FindDoorProjectionClearances(
        Tilemap source,
        ISet<Vector3Int> doorHeadCells)
    {
        Dictionary<Vector3Int, List<Rect>> result = new Dictionary<Vector3Int, List<Rect>>();
        foreach (Vector3Int doorCell in doorHeadCells)
        {
            foreach (List<Vector2> doorPath in GetSingleCellShapePathsWorld(source, doorCell))
            {
                if (doorPath.Count < 3) continue;
                float minX = doorPath.Min(point => point.x) - 0.04f;
                float maxX = doorPath.Max(point => point.x) + 0.04f;
                float minY = doorPath.Min(point => point.y) - 0.04f;
                float maxY = doorPath.Max(point => point.y) + 0.04f;
                Rect clearance = Rect.MinMaxRect(minX, minY, maxX, maxY);
                Vector2 sample = doorPath.Aggregate(Vector2.zero, (sum, point) => sum + point) / doorPath.Count;
                for (int y = -3; y <= 3; y++)
                {
                    for (int x = -3; x <= 3; x++)
                    {
                        Vector3Int candidate = doorCell + new Vector3Int(x, y, 0);
                        if (candidate == doorCell || !source.HasTile(candidate) ||
                            doorHeadCells.Contains(candidate))
                            continue;
                        Sprite sprite = source.GetSprite(candidate);
                        if (sprite == null || IsBroadPhysicsShape(sprite) || IsElevatedOnlyPhysicsShape(sprite))
                            continue;
                        if (!GetSingleCellShapePathsWorld(source, candidate)
                                .Any(path => PointInPolygon(sample, path)))
                            continue;
                        if (!result.TryGetValue(candidate, out List<Rect> clearances))
                        {
                            clearances = new List<Rect>();
                            result.Add(candidate, clearances);
                        }
                        clearances.Add(clearance);
                    }
                }
            }
        }
        return result;
    }

    private static List<List<Vector2>> SubtractAxisAlignedRect(IReadOnlyList<Vector2> polygon, Rect rect)
    {
        List<List<Vector2>> pieces = new List<List<Vector2>>();
        void AddIfValid(List<Vector2> piece)
        {
            if (piece.Count >= 3 && Mathf.Abs(SignedPolygonArea(piece)) >= 0.002f)
                pieces.Add(piece);
        }

        AddIfValid(ClipPolygonToHalfPlane(polygon, new Vector2(rect.xMin, 0f), Vector2.right));
        AddIfValid(ClipPolygonToHalfPlane(polygon, new Vector2(rect.xMax, 0f), Vector2.left));

        List<Vector2> middle = ClipPolygonToHalfPlane(polygon, new Vector2(rect.xMin, 0f), Vector2.left);
        middle = ClipPolygonToHalfPlane(middle, new Vector2(rect.xMax, 0f), Vector2.right);
        AddIfValid(ClipPolygonToHalfPlane(middle, new Vector2(0f, rect.yMin), Vector2.up));
        AddIfValid(ClipPolygonToHalfPlane(middle, new Vector2(0f, rect.yMax), Vector2.down));
        return pieces;
    }

    private static List<Vector2> ClipPolygonToHalfPlane(
        IReadOnlyList<Vector2> polygon,
        Vector2 pointOnBoundary,
        Vector2 outwardNormal)
    {
        List<Vector2> output = new List<Vector2>();
        if (polygon == null || polygon.Count == 0)
            return output;
        Vector2 previous = polygon[polygon.Count - 1];
        float previousDistance = Vector2.Dot(previous - pointOnBoundary, outwardNormal);
        bool previousInside = previousDistance <= 0.0001f;
        foreach (Vector2 current in polygon)
        {
            float currentDistance = Vector2.Dot(current - pointOnBoundary, outwardNormal);
            bool currentInside = currentDistance <= 0.0001f;
            if (currentInside != previousInside)
            {
                float denominator = previousDistance - currentDistance;
                if (Mathf.Abs(denominator) > 0.000001f)
                {
                    float t = previousDistance / denominator;
                    output.Add(Vector2.Lerp(previous, current, t));
                }
            }
            if (currentInside)
                output.Add(current);
            previous = current;
            previousDistance = currentDistance;
            previousInside = currentInside;
        }
        return output;
    }

    private static int CountBlockedElevatedDoorHeadSamples(Transform buildingRoot, Tilemap source)
    {
        if (buildingRoot == null || source == null)
            return int.MaxValue / 4;

        int blocked = 0;
        foreach (Vector3Int cell in FindElevatedOnlyPhysicsShapeCells(source))
        {
            foreach (Vector2 sample in GetSingleCellShapeCentroidsWorld(source, cell))
            {
                Physics2D.SyncTransforms();
                Collider2D[] hospitalWallHits = Physics2D.OverlapPointAll(sample).Where(collider =>
                    collider != null && collider.enabled && !collider.isTrigger &&
                    collider.transform.IsChildOf(buildingRoot)).ToArray();
                if (hospitalWallHits.Length > 0)
                {
                    blocked++;
                    List<string> sourceCellsAtSample = FindSourceCellsContainingPoint(source, cell, sample);
                    Debug.LogWarning($"[Environment Fixer] DOOR-HEAD BLOCKED {buildingRoot.name}/{cell} " +
                                     $"sample={sample}: {string.Join(", ", hospitalWallHits.Select(hit => hit.gameObject.name))}" +
                                     $" | sourceCells={string.Join(", ", sourceCellsAtSample)}" +
                                     DescribeNeighborhoods(source, new List<Vector3Int> { cell }),
                        buildingRoot);
                }
            }
        }
        return blocked;
    }

    private static List<string> FindSourceCellsContainingPoint(Tilemap source, Vector3Int aroundCell, Vector2 point)
    {
        List<string> result = new List<string>();
        for (int y = -3; y <= 3; y++)
        {
            for (int x = -3; x <= 3; x++)
            {
                Vector3Int candidate = aroundCell + new Vector3Int(x, y, 0);
                if (!source.HasTile(candidate)) continue;
                if (GetSingleCellShapePathsWorld(source, candidate).Any(path => PointInPolygon(point, path)))
                    result.Add($"{candidate}:{source.GetSprite(candidate)?.name}");
            }
        }
        return result;
    }

    private static bool PointInPolygon(Vector2 point, IReadOnlyList<Vector2> polygon)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            Vector2 a = polygon[i];
            Vector2 b = polygon[j];
            bool crosses = (a.y > point.y) != (b.y > point.y) &&
                           point.x < (b.x - a.x) * (point.y - a.y) /
                           (b.y - a.y) + a.x;
            if (crosses) inside = !inside;
        }
        return inside;
    }

    private static List<Vector2> GetSingleCellShapeCentroidsWorld(Tilemap source, Vector3Int cell)
    {
        return GetSingleCellShapePathsWorld(source, cell)
            .Where(path => path.Count > 0)
            .Select(path => path.Aggregate(Vector2.zero, (sum, point) => sum + point) / path.Count)
            .ToList();
    }

    private static List<List<Vector2>> GetSingleCellShapePathsWorld(Tilemap source, Vector3Int cell)
    {
        List<List<Vector2>> result = new List<List<Vector2>>();
        GameObject temporary = new GameObject("__TEMP_DoorHeadShapeSample",
            typeof(Tilemap), typeof(TilemapRenderer), typeof(TilemapCollider2D));
        temporary.hideFlags = HideFlags.HideAndDontSave;
        temporary.transform.SetParent(source.transform.parent, false);
        temporary.transform.localPosition = source.transform.localPosition;
        temporary.transform.localRotation = source.transform.localRotation;
        temporary.transform.localScale = source.transform.localScale;
        try
        {
            Tilemap temporaryMap = temporary.GetComponent<Tilemap>();
            temporaryMap.animationFrameRate = source.animationFrameRate;
            temporaryMap.tileAnchor = source.tileAnchor;
            temporaryMap.orientation = source.orientation;
            temporaryMap.orientationMatrix = source.orientationMatrix;
            temporaryMap.SetTile(cell, source.GetTile(cell));
            temporaryMap.SetTransformMatrix(cell, source.GetTransformMatrix(cell));
            temporary.GetComponent<TilemapRenderer>().enabled = false;
            TilemapCollider2D temporaryCollider = temporary.GetComponent<TilemapCollider2D>();
            temporaryCollider.ProcessTilemapChanges();

            PhysicsShapeGroup2D shapes = new PhysicsShapeGroup2D();
            temporaryCollider.GetShapes(shapes);
            Matrix4x4 localToWorld = shapes.localToWorldMatrix;
            List<Vector2> vertices = new List<Vector2>();
            for (int shapeIndex = 0; shapeIndex < shapes.shapeCount; shapeIndex++)
            {
                vertices.Clear();
                shapes.GetShapeVertices(shapeIndex, vertices);
                if (vertices.Count == 0) continue;
                result.Add(vertices.Select(vertex =>
                    (Vector2)localToWorld.MultiplyPoint3x4(vertex)).ToList());
            }
        }
        finally
        {
            DestroyImmediate(temporary);
        }
        return result;
    }

    private void DrawSummary()
    {
        int tileCount = tilemaps.Sum(item => item.TileCount);
        int authored = tilemaps.Sum(item => item.AuthoredColliderCount);
        int missing = tilemaps.Sum(item => item.MissingCells.Count);
        int spriteShapes = sprites.Count(item => item.PhysicsShapeCount > 0);
        int spritesWithoutCollider = sprites.Count(item => item.PhysicsShapeCount > 0 && !item.HasCollider);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Tổng quan", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            $"{tilemaps.Count} Tilemap • {tileCount:N0} tile • {authored:N0} collider đã vẽ • {missing:N0} cell cần kiểm tra");
        EditorGUILayout.LabelField(
            $"{sprites.Count} SpriteRenderer • {spriteShapes} sprite có Physics Shape • {spritesWithoutCollider} object chưa có Collider2D");

        if (GUILayout.Button("Scan A* trong Scene hiện tại"))
            ScanAstar();
    }

    private static void ScanAstar()
    {
        AstarPath astar = FindFirstObjectByType<AstarPath>();
        if (astar == null)
        {
            Debug.LogWarning("[Environment Fixer] Scene hiện tại không có AstarPath để scan.");
            return;
        }

        AstarPath.active = astar;
        astar.data.FindGraphTypes();
        if (astar.data.graphs == null || astar.data.graphs.Length == 0)
            astar.data.Awake();
        if (astar.data.graphs == null || astar.data.graphs.Length == 0)
        {
            byte[] serialized = astar.data.GetData();
            if (serialized != null && serialized.Length > 0)
            {
                astar.data.SetData((byte[])serialized.Clone());
                astar.data.Awake();
            }
        }
        if (astar.data.graphs == null || astar.data.graphs.Length == 0)
        {
            Debug.LogError("[Environment Fixer] AstarPath có serialized data nhưng không deserialize được graph trong Edit Mode. Không thể xác nhận scan.");
            return;
        }

        astar.Scan();
        int nodeCount = astar.data.graphs
            .Where(graph => graph != null)
            .Sum(graph => graph.CountNodes());
        Debug.Log($"[Environment Fixer] A* scan hoàn tất: {nodeCount:N0} node trên {astar.data.graphs.Length} graph.");
        SceneView.RepaintAll();
    }

    private void DrawTilemapSection()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("1. Tilemap Collider", EditorStyles.boldLabel);

        foreach (TilemapAudit audit in tilemaps)
        {
            if (audit.Tilemap == null)
                continue;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    audit.Included = EditorGUILayout.Toggle(audit.Included, GUILayout.Width(18));
                    EditorGUILayout.ObjectField(audit.Tilemap, typeof(Tilemap), true);
                    GUILayout.Label(audit.HasTilemapCollider ? "TilemapCollider ✓" : "Chưa có", audit.HasTilemapCollider ? goodBadge : warningBadge, GUILayout.Width(90));
                    if (GUILayout.Button("Chọn", GUILayout.Width(48)))
                        SelectAndFrame(audit.Tilemap.gameObject);
                }

                TilemapRenderer renderer = audit.Tilemap.GetComponent<TilemapRenderer>();
                string sorting = renderer == null
                    ? "Không có TilemapRenderer"
                    : $"{renderer.sortingLayerName}/{renderer.sortingOrder} • {renderer.mode}";
                EditorGUILayout.LabelField(
                    $"Tile {audit.TileCount:N0} | Sprite {audit.SpriteColliderCount:N0} | Grid {audit.GridColliderCount:N0} | " +
                    $"None {audit.NoneColliderCount:N0} | Sprite thiếu shape {audit.SpriteWithoutShapeCount:N0}");
                EditorGUILayout.LabelField("Sorting: " + sorting, EditorStyles.miniLabel);

                int reviewCount = audit.MissingCells.Count + audit.GridCells.Count + audit.FullBoundsShapeCells.Count;
                if (reviewCount > 0 && GUILayout.Button(
                        $"Review: {audit.MissingCells.Count:N0} thiếu, {audit.GridCells.Count:N0} Grid, " +
                        $"{audit.FullBoundsShapeCells.Count:N0} shape cao/rộng"))
                {
                    reviewTilemap = audit.Tilemap;
                    pickedCells.Clear();
                    showMissingCells = true;
                    SelectAndFrame(audit.Tilemap.gameObject);
                    SceneView.RepaintAll();
                }
            }
        }

        collisionLayer = EditorGUILayout.LayerField("Collision/A* Layer", collisionLayer);
        if (GUILayout.Button("Đánh dấu Tilemap có collider asset nhưng chưa có TilemapCollider2D"))
        {
            foreach (TilemapAudit audit in tilemaps)
                audit.Included = audit.AuthoredColliderCount > 0 && !audit.HasTilemapCollider;
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Thêm TilemapCollider2D cho mục đã chọn"))
                AddTilemapColliders();
            if (GUILayout.Button("Đặt Layer cho mục đã chọn"))
                ApplyCollisionLayer();
        }
    }

    private void DrawSpriteSection()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("2. Sprite môi trường (hàng rào, đèn, cây...)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Chỉ đánh dấu sẵn Sprite có Physics Shape nhưng GameObject chưa có Collider2D. " +
            "Nút bên dưới thêm PolygonCollider2D để Unity lấy đúng shape đã vẽ trong Sprite Editor.",
            MessageType.None);

        int visibleCount = 0;
        foreach (SpriteAudit audit in sprites)
        {
            if (audit.Renderer == null || (audit.PhysicsShapeCount == 0 && audit.HasCollider))
                continue;

            visibleCount++;
            using (new EditorGUILayout.HorizontalScope())
            {
                audit.Included = EditorGUILayout.Toggle(audit.Included, GUILayout.Width(18));
                EditorGUILayout.ObjectField(audit.Renderer, typeof(SpriteRenderer), true);
                GUILayout.Label($"Shape {audit.PhysicsShapeCount}", audit.PhysicsShapeCount > 0 ? goodBadge : warningBadge, GUILayout.Width(60));
                GUILayout.Label(audit.HasCollider ? "Collider ✓" : "Thiếu", audit.HasCollider ? goodBadge : warningBadge, GUILayout.Width(58));
                if (GUILayout.Button("Chọn", GUILayout.Width(48)))
                    SelectAndFrame(audit.Renderer.gameObject);
            }
        }

        if (visibleCount == 0)
            EditorGUILayout.LabelField("Không có Sprite cần xử lý.", EditorStyles.miniLabel);

        if (GUILayout.Button("Đánh dấu Sprite có Physics Shape nhưng thiếu Collider2D"))
        {
            foreach (SpriteAudit audit in sprites)
                audit.Included = audit.PhysicsShapeCount > 0 && !audit.HasCollider;
        }

        if (GUILayout.Button("Thêm PolygonCollider2D từ Physics Shape cho Sprite đã chọn"))
            AddSpritePolygonColliders();
    }

    private void DrawSortingSection()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("3. Batch Sorting có kiểm soát", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Chỉ áp dụng cho các dòng đang được tick. Hãy chia tường/đồ vật đúng nhóm trước; không dùng nút này để ép toàn bộ căn nhà vào cùng một lớp.",
            MessageType.Warning);

        string[] layerNames = SortingLayer.layers.Select(layer => layer.name).ToArray();
        if (layerNames.Length == 0)
            layerNames = new[] { "Default" };
        sortingLayerIndex = Mathf.Clamp(sortingLayerIndex, 0, layerNames.Length - 1);
        sortingLayerIndex = EditorGUILayout.Popup("Sorting Layer", sortingLayerIndex, layerNames);
        sortingOrder = EditorGUILayout.IntField("Order in Layer", sortingOrder);
        forceIndividualTilemap = EditorGUILayout.Toggle("Tilemap Mode = Individual", forceIndividualTilemap);
        forcePivotSprite = EditorGUILayout.Toggle("Sprite Sort Point = Pivot", forcePivotSprite);

        if (GUILayout.Button("Áp dụng Sorting cho mục đã chọn"))
            ApplySorting(layerNames[sortingLayerIndex]);
    }

    private void DrawPolygonPatchSection()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("4. Polygon Patch — chỉ vá phần hỏng", EditorStyles.boldLabel);
        reviewTilemap = (Tilemap)EditorGUILayout.ObjectField("Tilemap đang review", reviewTilemap, typeof(Tilemap), true);
        showMissingCells = EditorGUILayout.Toggle("Hiện cell thiếu (đỏ)", showMissingCells);
        pickMissingCells = EditorGUILayout.Toggle("Click chọn cell cảnh báo", pickMissingCells);
        EditorGUILayout.LabelField($"Đã chọn {pickedCells.Count} cell cần xử lý", EditorStyles.miniLabel);

        patchCollider = (PolygonCollider2D)EditorGUILayout.ObjectField(
            "PolygonCollider vá", patchCollider, typeof(PolygonCollider2D), true);
        snapStep = Mathf.Max(0f, EditorGUILayout.FloatField("Snap khi vẽ", snapStep));

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Tạo/Tìm Patch Object"))
                EnsurePatchCollider();
            using (new EditorGUI.DisabledScope(pickedCells.Count == 0 || reviewTilemap == null))
            {
                if (GUILayout.Button("Tạo diamond patch từ cell đã chọn"))
                    CreateDiamondPatches();
            }
        }

        using (new EditorGUI.DisabledScope(pickedCells.Count == 0 || reviewTilemap == null))
        {
            if (GUILayout.Button("Tạo collision proxy và loại các cell đã chọn"))
                CreateCollisionProxyExcludingPicked();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.backgroundColor = drawingPolygon ? new Color(1f, 0.65f, 0.25f) : Color.white;
            if (GUILayout.Button(drawingPolygon ? "Đang vẽ... Enter để chốt" : "Vẽ Polygon Path trong Scene"))
                BeginPolygonDrawing();
            GUI.backgroundColor = Color.white;

            using (new EditorGUI.DisabledScope(patchCollider == null || patchCollider.pathCount == 0))
            {
                if (GUILayout.Button("Xóa path cuối", GUILayout.Width(100)))
                    RemoveLastPatchPath();
            }
        }

        EditorGUILayout.HelpBox(
            "Trong Scene: click trái để đặt đỉnh, Enter hoặc double-click để chốt path, Backspace xóa đỉnh cuối, Esc hủy. " +
            "Đỏ = None/Sprite thiếu shape; cam = Grid full-cell; tím = Physics Shape phủ >=85% diện tích hoặc >=55% chiều cao sprite. " +
            "Collision proxy giữ visual/Tile asset nguyên vẹn, sao chép collider tốt và bỏ riêng các cell đã chọn. Sau đó dùng Polygon để vá chân tường.",
            MessageType.None);
    }

    private void AddTilemapColliders()
    {
        if (!CanEditRoot())
            return;

        int added = 0;
        foreach (TilemapAudit audit in tilemaps.Where(item => item.Included && item.Tilemap != null))
        {
            if (audit.HasTilemapCollider || audit.AuthoredColliderCount <= 0)
                continue;

            Undo.AddComponent<TilemapCollider2D>(audit.Tilemap.gameObject);
            audit.Tilemap.gameObject.layer = collisionLayer;
            EditorUtility.SetDirty(audit.Tilemap.gameObject);
            added++;
        }

        Debug.Log($"[Environment Fixer] Đã thêm {added} TilemapCollider2D. Cell None/thiếu Physics Shape vẫn được giữ lại để Polygon vá riêng.");
        Analyze();
    }

    private void ApplyCollisionLayer()
    {
        if (!CanEditRoot())
            return;

        Undo.SetCurrentGroupName("Set environment collision layer");
        int count = 0;
        foreach (TilemapAudit audit in tilemaps.Where(item => item.Included && item.Tilemap != null))
        {
            Undo.RecordObject(audit.Tilemap.gameObject, "Set collision layer");
            audit.Tilemap.gameObject.layer = collisionLayer;
            EditorUtility.SetDirty(audit.Tilemap.gameObject);
            count++;
        }

        foreach (SpriteAudit audit in sprites.Where(item => item.Included && item.Renderer != null))
        {
            Undo.RecordObject(audit.Renderer.gameObject, "Set collision layer");
            audit.Renderer.gameObject.layer = collisionLayer;
            EditorUtility.SetDirty(audit.Renderer.gameObject);
            count++;
        }

        Debug.Log($"[Environment Fixer] Đã đặt layer '{LayerMask.LayerToName(collisionLayer)}' cho {count} object.");
    }

    private void AddSpritePolygonColliders()
    {
        if (!CanEditRoot())
            return;

        int added = 0;
        foreach (SpriteAudit audit in sprites.Where(item => item.Included && item.Renderer != null))
        {
            if (audit.PhysicsShapeCount <= 0 || audit.Renderer.GetComponent<Collider2D>() != null)
                continue;

            Undo.AddComponent<PolygonCollider2D>(audit.Renderer.gameObject);
            audit.Renderer.gameObject.layer = collisionLayer;
            EditorUtility.SetDirty(audit.Renderer.gameObject);
            added++;
        }

        Debug.Log($"[Environment Fixer] Đã thêm {added} PolygonCollider2D từ Physics Shape của Sprite.");
        Analyze();
    }

    private void ApplySorting(string sortingLayerName)
    {
        if (!CanEditRoot())
            return;

        int count = 0;
        foreach (TilemapAudit audit in tilemaps.Where(item => item.Included && item.Tilemap != null))
        {
            TilemapRenderer renderer = audit.Tilemap.GetComponent<TilemapRenderer>();
            if (renderer == null)
                continue;

            Undo.RecordObject(renderer, "Apply tilemap sorting");
            renderer.sortingLayerName = sortingLayerName;
            renderer.sortingOrder = sortingOrder;
            if (forceIndividualTilemap)
                renderer.mode = TilemapRenderer.Mode.Individual;
            EditorUtility.SetDirty(renderer);
            count++;
        }

        foreach (SpriteAudit audit in sprites.Where(item => item.Included && item.Renderer != null))
        {
            Undo.RecordObject(audit.Renderer, "Apply sprite sorting");
            audit.Renderer.sortingLayerName = sortingLayerName;
            audit.Renderer.sortingOrder = sortingOrder;
            if (forcePivotSprite)
                audit.Renderer.spriteSortPoint = SpriteSortPoint.Pivot;
            EditorUtility.SetDirty(audit.Renderer);
            count++;
        }

        Debug.Log($"[Environment Fixer] Đã áp dụng sorting {sortingLayerName}/{sortingOrder} cho {count} renderer được chọn.");
        Analyze();
    }

    private PolygonCollider2D EnsurePatchCollider()
    {
        if (patchCollider != null)
            return patchCollider;
        if (!CanEditRoot())
            return null;

        Transform existing = root.transform.Find(PatchObjectName);
        GameObject patchObject;
        if (existing != null)
        {
            patchObject = existing.gameObject;
        }
        else
        {
            patchObject = new GameObject(PatchObjectName);
            Undo.RegisterCreatedObjectUndo(patchObject, "Create collider patch object");
            Undo.SetTransformParent(patchObject.transform, root.transform, "Parent collider patch object");
            patchObject.transform.localPosition = Vector3.zero;
            patchObject.transform.localRotation = Quaternion.identity;
            patchObject.transform.localScale = Vector3.one;
        }

        patchObject.layer = collisionLayer;
        patchCollider = patchObject.GetComponent<PolygonCollider2D>();
        if (patchCollider == null)
        {
            patchCollider = Undo.AddComponent<PolygonCollider2D>(patchObject);
            // PolygonCollider2D on an empty GameObject may start with a default
            // square. A patch container must never introduce that hidden blocker.
            patchCollider.pathCount = 0;
        }

        Selection.activeGameObject = patchObject;
        EditorUtility.SetDirty(patchObject);
        return patchCollider;
    }

    private bool CanEditRoot()
    {
        if (root == null)
            return false;
        if (!EditorUtility.IsPersistent(root))
            return true;

        Debug.LogError(
            "[Environment Fixer] Root hiện là prefab asset trong Project. Hãy Open Prefab (Prefab Mode) hoặc chọn prefab instance trong Scene trước khi sửa.",
            root);
        return false;
    }

    private void CreateDiamondPatches()
    {
        PolygonCollider2D polygon = EnsurePatchCollider();
        if (polygon == null || reviewTilemap == null)
            return;

        GridLayout grid = reviewTilemap.layoutGrid;
        if (grid == null)
        {
            Debug.LogError("[Environment Fixer] Tilemap không có GridLayout để đổi cell sang world.");
            return;
        }

        Undo.RecordObject(polygon, "Create missing-cell collider patches");
        int pathIndex = polygon.pathCount;
        polygon.pathCount += pickedCells.Count;

        foreach (Vector3Int cell in pickedCells)
        {
            Vector3 p0 = grid.CellToWorld(cell);
            Vector3 pRight = grid.CellToWorld(cell + Vector3Int.right);
            Vector3 pTop = grid.CellToWorld(cell + Vector3Int.right + Vector3Int.up);
            Vector3 pLeft = grid.CellToWorld(cell + Vector3Int.up);
            polygon.SetPath(pathIndex++, new[]
            {
                ToLocal2D(polygon.transform, p0),
                ToLocal2D(polygon.transform, pRight),
                ToLocal2D(polygon.transform, pTop),
                ToLocal2D(polygon.transform, pLeft)
            });
        }

        EditorUtility.SetDirty(polygon);
        Debug.Log($"[Environment Fixer] Đã tạo {pickedCells.Count} diamond patch. Hãy dùng Edit Collider để tinh chỉnh nếu cần.");
        pickedCells.Clear();
        SceneView.RepaintAll();
    }

    private void CreateCollisionProxyExcludingPicked()
    {
        if (!CanEditRoot() || reviewTilemap == null || pickedCells.Count == 0)
            return;

        Tilemap source = reviewTilemap;
        int excludedCount = pickedCells.Count;
        Tilemap proxy = BuildCollisionProxy(source, pickedCells, collisionLayer, true, out int copied);
        if (proxy == null)
            return;

        Selection.activeGameObject = proxy.gameObject;
        Debug.Log(
            $"[Environment Fixer] Đã tạo '{proxy.name}': copy {copied:N0} cell collider, loại {excludedCount:N0} cell lỗi" +
            (source.GetComponent<TilemapCollider2D>() != null ? ", và tắt TilemapCollider2D nguồn." : "."));
        pickedCells.Clear();
        Analyze();
    }

    private static Tilemap BuildCollisionProxy(
        Tilemap source,
        ISet<Vector3Int> excludedCells,
        int layer,
        bool recordUndo,
        out int copied)
    {
        copied = 0;
        if (source == null)
            return null;

        string proxyName = ProxyPrefix + source.name;
        Transform parent = source.transform.parent;
        Transform existing = parent != null ? parent.Find(proxyName) : null;
        GameObject proxyObject;
        if (existing == null)
        {
            proxyObject = new GameObject(proxyName);
            if (recordUndo)
                Undo.RegisterCreatedObjectUndo(proxyObject, "Create tilemap collision proxy");
            if (parent != null)
            {
                if (recordUndo)
                    Undo.SetTransformParent(proxyObject.transform, parent, "Parent tilemap collision proxy");
                else
                    proxyObject.transform.SetParent(parent, false);
            }
            proxyObject.transform.localPosition = source.transform.localPosition;
            proxyObject.transform.localRotation = source.transform.localRotation;
            proxyObject.transform.localScale = source.transform.localScale;
        }
        else
        {
            proxyObject = existing.gameObject;
        }

        proxyObject.layer = layer;
        Tilemap proxy = proxyObject.GetComponent<Tilemap>();
        if (proxy == null)
            proxy = recordUndo ? Undo.AddComponent<Tilemap>(proxyObject) : proxyObject.AddComponent<Tilemap>();
        TilemapRenderer proxyRenderer = proxyObject.GetComponent<TilemapRenderer>();
        if (proxyRenderer == null)
            proxyRenderer = recordUndo ? Undo.AddComponent<TilemapRenderer>(proxyObject) : proxyObject.AddComponent<TilemapRenderer>();
        TilemapCollider2D proxyCollider = proxyObject.GetComponent<TilemapCollider2D>();
        if (proxyCollider == null)
            proxyCollider = recordUndo ? Undo.AddComponent<TilemapCollider2D>(proxyObject) : proxyObject.AddComponent<TilemapCollider2D>();

        if (recordUndo)
        {
            Undo.RecordObject(proxy, "Rebuild tilemap collision proxy");
            Undo.RecordObject(proxyRenderer, "Configure tilemap collision proxy");
        }
        proxy.ClearAllTiles();
        proxy.animationFrameRate = source.animationFrameRate;
        proxy.tileAnchor = source.tileAnchor;
        proxy.orientation = source.orientation;
        proxy.orientationMatrix = source.orientationMatrix;

        foreach (Vector3Int position in source.cellBounds.allPositionsWithin)
        {
            if (!source.HasTile(position) || excludedCells.Contains(position))
                continue;
            if (source.GetColliderType(position) == Tile.ColliderType.None)
                continue;

            proxy.SetTile(position, source.GetTile(position));
            proxy.SetTransformMatrix(position, source.GetTransformMatrix(position));
            copied++;
        }

        proxyRenderer.enabled = false;
        proxyCollider.enabled = true;
        proxyCollider.ProcessTilemapChanges();

        TilemapCollider2D sourceCollider = source.GetComponent<TilemapCollider2D>();
        if (sourceCollider != null && sourceCollider.enabled)
        {
            if (recordUndo)
                Undo.RecordObject(sourceCollider, "Disable source tilemap collider");
            sourceCollider.enabled = false;
            EditorUtility.SetDirty(sourceCollider);
        }

        EditorUtility.SetDirty(proxy);
        EditorUtility.SetDirty(proxyRenderer);
        EditorUtility.SetDirty(proxyCollider);
        EditorUtility.SetDirty(proxyObject);
        return proxy;
    }

    private static bool IsScriptedSolidObject(GameObject gameObject)
    {
        string lowerName = gameObject.name.ToLowerInvariant();
        if (ContainsAny(lowerName, "bed", "giuong", "loot", "cabinet", "tu_do", "tudo"))
            return true;
        return gameObject.GetComponentsInParent<MonoBehaviour>(true)
            .Any(behaviour => behaviour != null && !(behaviour is RoofVisibility));
    }

    private static List<Vector2[]> ExtractAuthoredTilePhysicsPaths(Tilemap source)
    {
        List<Vector2[]> result = new List<Vector2[]>();
        GameObject temporary = new GameObject("__TEMP_AuthoredWallSource",
            typeof(Tilemap), typeof(TilemapRenderer), typeof(TilemapCollider2D));
        temporary.hideFlags = HideFlags.HideAndDontSave;
        temporary.transform.SetParent(source.transform.parent, false);
        temporary.transform.localPosition = source.transform.localPosition;
        temporary.transform.localRotation = source.transform.localRotation;
        temporary.transform.localScale = source.transform.localScale;

        try
        {
            Tilemap temporaryMap = temporary.GetComponent<Tilemap>();
            temporaryMap.animationFrameRate = source.animationFrameRate;
            temporaryMap.tileAnchor = source.tileAnchor;
            temporaryMap.orientation = source.orientation;
            temporaryMap.orientationMatrix = source.orientationMatrix;
            foreach (Vector3Int cell in source.cellBounds.allPositionsWithin)
            {
                if (!source.HasTile(cell)) continue;
                temporaryMap.SetTile(cell, source.GetTile(cell));
                temporaryMap.SetTransformMatrix(cell, source.GetTransformMatrix(cell));
            }
            temporaryMap.CompressBounds();
            temporary.GetComponent<TilemapRenderer>().enabled = false;

            TilemapCollider2D temporaryCollider = temporary.GetComponent<TilemapCollider2D>();
            temporaryCollider.ProcessTilemapChanges();
            PhysicsShapeGroup2D shapes = new PhysicsShapeGroup2D();
            temporaryCollider.GetShapes(shapes);
            List<Vector2> vertices = new List<Vector2>();
            for (int shapeIndex = 0; shapeIndex < shapes.shapeCount; shapeIndex++)
            {
                vertices.Clear();
                shapes.GetShapeVertices(shapeIndex, vertices);
                if (vertices.Count < 3 || Mathf.Abs(SignedPolygonArea(vertices)) < 0.003f)
                    continue;
                Vector2[] sourceLocalPath = vertices.Select(point =>
                {
                    Vector3 world = temporary.transform.TransformPoint(point);
                    return (Vector2)source.transform.InverseTransformPoint(world);
                }).ToArray();
                result.Add(sourceLocalPath);
            }
        }
        finally
        {
            DestroyImmediate(temporary);
        }
        return result;
    }

    private static Vector2[] ExtractBottomFootPathAtCell(
        Tilemap source,
        Vector3Int targetCell,
        Vector3Int donorCell,
        Transform outputTransform)
    {
        return ExtractBottomFootPathAtCell(source, targetCell, source, donorCell, outputTransform);
    }

    private static Vector2[] ExtractBottomFootPathAtCell(
        Tilemap source,
        Vector3Int targetCell,
        Tilemap donorSource,
        Vector3Int donorCell,
        Transform outputTransform)
    {
        GameObject temporary = new GameObject("__TEMP_BroadWallFootSource",
            typeof(Tilemap), typeof(TilemapRenderer), typeof(TilemapCollider2D));
        temporary.hideFlags = HideFlags.HideAndDontSave;
        temporary.transform.SetParent(source.transform.parent, false);
        temporary.transform.localPosition = source.transform.localPosition;
        temporary.transform.localRotation = source.transform.localRotation;
        temporary.transform.localScale = source.transform.localScale;

        try
        {
            Tilemap temporaryMap = temporary.GetComponent<Tilemap>();
            temporaryMap.animationFrameRate = source.animationFrameRate;
            temporaryMap.tileAnchor = source.tileAnchor;
            temporaryMap.orientation = source.orientation;
            temporaryMap.orientationMatrix = source.orientationMatrix;
            temporaryMap.SetTile(targetCell, donorSource.GetTile(donorCell));
            temporaryMap.SetTransformMatrix(targetCell, donorSource.GetTransformMatrix(donorCell));
            temporaryMap.CompressBounds();
            temporary.GetComponent<TilemapRenderer>().enabled = false;

            TilemapCollider2D temporaryCollider = temporary.GetComponent<TilemapCollider2D>();
            temporaryCollider.ProcessTilemapChanges();
            PhysicsShapeGroup2D shapes = new PhysicsShapeGroup2D();
            temporaryCollider.GetShapes(shapes);
            List<List<Vector2>> allShapes = new List<List<Vector2>>();
            List<Vector2> vertices = new List<Vector2>();
            for (int shapeIndex = 0; shapeIndex < shapes.shapeCount; shapeIndex++)
            {
                vertices.Clear();
                shapes.GetShapeVertices(shapeIndex, vertices);
                if (vertices.Count < 3) continue;
                // Collider2D.GetShapes stores the transform that maps returned local vertices
                // to world space on the PhysicsShapeGroup2D. A TilemapCollider2D can inherit
                // its Grid transform, so using temporary.transform here can double-apply the
                // hospital offset and place the repair polygons far away from the walls.
                Matrix4x4 shapeLocalToWorld = shapes.localToWorldMatrix;
                allShapes.Add(vertices.Select(point =>
                {
                    Vector3 world = shapeLocalToWorld.MultiplyPoint3x4(point);
                    return (Vector2)outputTransform.InverseTransformPoint(world);
                }).ToList());
            }
            if (allShapes.Count == 0)
                return null;

            float globalMinY = allShapes.SelectMany(shape => shape).Min(point => point.y);
            float globalMaxY = allShapes.SelectMany(shape => shape).Max(point => point.y);
            float bandHeight = Mathf.Clamp((globalMaxY - globalMinY) * 0.06f, 0.055f, 0.11f);
            List<Vector2> bottomPoints = new List<Vector2>();
            foreach (List<Vector2> shape in allShapes)
            {
                if (shape.Min(point => point.y) > globalMinY + 0.01f)
                    continue;
                List<Vector2> clipped = ClipPolygonBelow(shape, globalMinY + bandHeight);
                if (clipped.Count >= 3 && Mathf.Abs(SignedPolygonArea(clipped)) >= 0.002f)
                    bottomPoints.AddRange(clipped);
            }
            List<Vector2> hull = BuildConvexHull(bottomPoints);
            return hull.Count >= 3 && Mathf.Abs(SignedPolygonArea(hull)) >= 0.003f
                ? hull.ToArray()
                : null;
        }
        finally
        {
            DestroyImmediate(temporary);
        }
    }

    private static MergedFootprintBuild BuildMergedFootprintGroup(
        GameObject rootObject,
        string groupName,
        int layer,
        Func<Tilemap, bool> sourceFilter,
        Func<Tilemap, Sprite, bool> cellFilter,
        float bandRatio,
        float minimumBandHeight,
        float maximumBandHeight,
        bool useCellFootprintFallback)
    {
        Transform oldGroup = rootObject.transform.Find(groupName);
        if (oldGroup != null)
            Undo.DestroyObjectImmediate(oldGroup.gameObject);

        MergedFootprintBuild result = new MergedFootprintBuild();
        GameObject groupObject = new GameObject(groupName);
        Undo.RegisterCreatedObjectUndo(groupObject, $"Create {groupName}");
        Undo.SetTransformParent(groupObject.transform, rootObject.transform, $"Parent {groupName}");
        groupObject.transform.localPosition = Vector3.zero;
        groupObject.transform.localRotation = Quaternion.identity;
        groupObject.transform.localScale = Vector3.one;
        groupObject.layer = layer;
        result.GroupObject = groupObject;

        foreach (Tilemap source in rootObject.GetComponentsInChildren<Tilemap>(true))
        {
            if (source.name.StartsWith(ProxyPrefix, StringComparison.Ordinal) ||
                source.transform.IsChildOf(groupObject.transform) ||
                !sourceFilter(source))
                continue;

            List<Vector3Int> accepted = new List<Vector3Int>();
            foreach (Vector3Int cell in source.cellBounds.allPositionsWithin)
            {
                if (!source.HasTile(cell)) continue;
                Sprite sprite = source.GetSprite(cell);
                if (sprite != null && cellFilter(source, sprite))
                {
                    accepted.Add(cell);
                    result.AcceptedSprites.Add(sprite.name);
                }
                else
                {
                    result.RejectedCells++;
                }
            }
            if (accepted.Count == 0) continue;

            List<Vector2[]> sourcePaths = new List<Vector2[]>();
            foreach (List<Vector3Int> cluster in ClusterCellsByOrientation(source, accepted))
            {
                Vector2[] path = BuildMergedClusterPath(
                    rootObject, source, cluster, bandRatio, minimumBandHeight, maximumBandHeight,
                    useCellFootprintFallback);
                if (path == null || path.Length < 3) continue;
                sourcePaths.Add(path);
                result.Paths.Add(path);
                result.ClusterCount++;
                result.AcceptedCells += cluster.Count;
                if (useCellFootprintFallback)
                    result.FallbackCells += cluster.Count(cell => source.GetSprite(cell)?.GetPhysicsShapeCount() <= 0);
            }
            if (sourcePaths.Count == 0) continue;

            result.Sources.Add(source);
            GameObject colliderObject = new GameObject($"Merged_{source.name}");
            Undo.RegisterCreatedObjectUndo(colliderObject, $"Create merged footprint for {source.name}");
            Undo.SetTransformParent(colliderObject.transform, groupObject.transform, "Parent merged footprint");
            colliderObject.transform.localPosition = Vector3.zero;
            colliderObject.transform.localRotation = Quaternion.identity;
            colliderObject.transform.localScale = Vector3.one;
            colliderObject.layer = layer;
            PolygonCollider2D polygon = Undo.AddComponent<PolygonCollider2D>(colliderObject);
            polygon.pathCount = sourcePaths.Count;
            for (int pathIndex = 0; pathIndex < sourcePaths.Count; pathIndex++)
                polygon.SetPath(pathIndex, sourcePaths[pathIndex]);
        }

        if (result.ClusterCount == 0)
        {
            Undo.DestroyObjectImmediate(groupObject);
            result.GroupObject = null;
        }
        return result;
    }

    private static List<List<Vector3Int>> ClusterCellsByOrientation(
        Tilemap source,
        IReadOnlyCollection<Vector3Int> cells)
    {
        Dictionary<char, HashSet<Vector3Int>> byOrientation = cells
            .GroupBy(cell => GetSpriteOrientation(source.GetSprite(cell)?.name))
            .ToDictionary(group => group.Key, group => new HashSet<Vector3Int>(group));
        Vector3Int[] steps =
        {
            Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down
        };
        List<List<Vector3Int>> clusters = new List<List<Vector3Int>>();
        foreach (HashSet<Vector3Int> remaining in byOrientation.Values)
        {
            while (remaining.Count > 0)
            {
                Vector3Int seed = remaining.First();
                remaining.Remove(seed);
                Queue<Vector3Int> queue = new Queue<Vector3Int>();
                queue.Enqueue(seed);
                List<Vector3Int> cluster = new List<Vector3Int>();
                while (queue.Count > 0)
                {
                    Vector3Int current = queue.Dequeue();
                    cluster.Add(current);
                    foreach (Vector3Int step in steps)
                    {
                        Vector3Int neighbour = current + step;
                        if (!remaining.Remove(neighbour)) continue;
                        queue.Enqueue(neighbour);
                    }
                }
                clusters.Add(cluster);
            }
        }
        return clusters;
    }

    private static Vector2[] BuildMergedClusterPath(
        GameObject rootObject,
        Tilemap source,
        IReadOnlyCollection<Vector3Int> cells,
        float bandRatio,
        float minimumBandHeight,
        float maximumBandHeight,
        bool useCellFootprintFallback)
    {
        GameObject temporary = new GameObject("__TEMP_MergedFootprintSource",
            typeof(Tilemap), typeof(TilemapRenderer), typeof(TilemapCollider2D));
        temporary.hideFlags = HideFlags.HideAndDontSave;
        temporary.transform.SetParent(source.transform.parent, false);
        temporary.transform.localPosition = source.transform.localPosition;
        temporary.transform.localRotation = source.transform.localRotation;
        temporary.transform.localScale = source.transform.localScale;

        try
        {
            Tilemap temporaryMap = temporary.GetComponent<Tilemap>();
            temporaryMap.animationFrameRate = source.animationFrameRate;
            temporaryMap.tileAnchor = source.tileAnchor;
            temporaryMap.orientation = source.orientation;
            temporaryMap.orientationMatrix = source.orientationMatrix;
            foreach (Vector3Int cell in cells)
            {
                temporaryMap.SetTile(cell, source.GetTile(cell));
                temporaryMap.SetTransformMatrix(cell, source.GetTransformMatrix(cell));
            }
            temporaryMap.CompressBounds();
            temporary.GetComponent<TilemapRenderer>().enabled = false;

            TilemapCollider2D temporaryCollider = temporary.GetComponent<TilemapCollider2D>();
            temporaryCollider.ProcessTilemapChanges();
            PhysicsShapeGroup2D shapes = new PhysicsShapeGroup2D();
            temporaryCollider.GetShapes(shapes);
            List<Vector2> clippedPoints = new List<Vector2>();
            List<Vector2> vertices = new List<Vector2>();
            for (int shapeIndex = 0; shapeIndex < shapes.shapeCount; shapeIndex++)
            {
                vertices.Clear();
                shapes.GetShapeVertices(shapeIndex, vertices);
                if (vertices.Count < 3) continue;
                List<Vector2> rootPoints = vertices.Select(point =>
                {
                    Vector3 world = temporary.transform.TransformPoint(point);
                    return (Vector2)rootObject.transform.InverseTransformPoint(world);
                }).ToList();
                float minY = rootPoints.Min(point => point.y);
                float maxY = rootPoints.Max(point => point.y);
                float bandHeight = Mathf.Clamp(
                    (maxY - minY) * bandRatio, minimumBandHeight, maximumBandHeight);
                List<Vector2> clipped = ClipPolygonBelow(rootPoints, minY + bandHeight);
                if (clipped.Count < 3 || Mathf.Abs(SignedPolygonArea(clipped)) < 0.003f)
                    continue;
                clippedPoints.AddRange(clipped);
            }

            if (useCellFootprintFallback)
            {
                foreach (Vector3Int cell in cells)
                {
                    Sprite sprite = source.GetSprite(cell);
                    if (sprite == null || sprite.GetPhysicsShapeCount() > 0) continue;
                    Vector3Int right = cell + Vector3Int.right;
                    Vector3Int up = cell + Vector3Int.up;
                    Vector3Int rightUp = cell + Vector3Int.right + Vector3Int.up;
                    List<Vector2> cellShape = new List<Vector2>
                    {
                        ToRootPoint(rootObject, source, source.CellToLocal(cell)),
                        ToRootPoint(rootObject, source, source.CellToLocal(right)),
                        ToRootPoint(rootObject, source, source.CellToLocal(rightUp)),
                        ToRootPoint(rootObject, source, source.CellToLocal(up))
                    };
                    float minY = cellShape.Min(point => point.y);
                    float maxY = cellShape.Max(point => point.y);
                    float bandHeight = Mathf.Clamp(
                        (maxY - minY) * bandRatio, minimumBandHeight, maximumBandHeight);
                    List<Vector2> clipped = ClipPolygonBelow(cellShape, minY + bandHeight);
                    if (clipped.Count >= 3)
                        clippedPoints.AddRange(clipped);
                }
            }

            List<Vector2> hull = BuildConvexHull(clippedPoints);
            return hull.Count >= 3 && Mathf.Abs(SignedPolygonArea(hull)) >= 0.006f
                ? hull.ToArray()
                : null;
        }
        finally
        {
            DestroyImmediate(temporary);
        }
    }

    private static Vector2 ToRootPoint(GameObject rootObject, Tilemap source, Vector3 sourceLocalPoint)
    {
        Vector3 world = source.transform.TransformPoint(sourceLocalPoint);
        return rootObject.transform.InverseTransformPoint(world);
    }

    private static char GetSpriteOrientation(string spriteName)
    {
        if (string.IsNullOrWhiteSpace(spriteName)) return '?';
        for (int index = spriteName.Length - 1; index >= 0; index--)
        {
            char value = char.ToUpperInvariant(spriteName[index]);
            if (value == 'N' || value == 'S' || value == 'E' || value == 'W')
                return value;
            if (char.IsLetter(value)) break;
        }
        return '?';
    }

    private static List<Vector2> BuildConvexHull(IEnumerable<Vector2> input)
    {
        List<Vector2> points = input
            .OrderBy(point => point.x)
            .ThenBy(point => point.y)
            .ToList();
        List<Vector2> unique = new List<Vector2>();
        foreach (Vector2 point in points)
        {
            if (unique.Count == 0 || (point - unique[unique.Count - 1]).sqrMagnitude > 0.000001f)
                unique.Add(point);
        }
        if (unique.Count <= 2) return unique;

        List<Vector2> lower = new List<Vector2>();
        foreach (Vector2 point in unique)
        {
            while (lower.Count >= 2 &&
                   Cross(lower[lower.Count - 2], lower[lower.Count - 1], point) <= 0f)
                lower.RemoveAt(lower.Count - 1);
            lower.Add(point);
        }
        List<Vector2> upper = new List<Vector2>();
        for (int index = unique.Count - 1; index >= 0; index--)
        {
            Vector2 point = unique[index];
            while (upper.Count >= 2 &&
                   Cross(upper[upper.Count - 2], upper[upper.Count - 1], point) <= 0f)
                upper.RemoveAt(upper.Count - 1);
            upper.Add(point);
        }
        lower.RemoveAt(lower.Count - 1);
        upper.RemoveAt(upper.Count - 1);
        lower.AddRange(upper);
        return lower;
    }

    private static float Cross(Vector2 origin, Vector2 first, Vector2 second)
    {
        Vector2 a = first - origin;
        Vector2 b = second - origin;
        return a.x * b.y - a.y * b.x;
    }

    private static bool IsObviousRigidDecorSprite(string spriteName)
    {
        if (string.IsNullOrWhiteSpace(spriteName)) return false;
        string lower = spriteName.ToLowerInvariant();
        if (ContainsAny(lower, "debris", "corpse", "body", "mannequin", "human", "chair",
            "plant", "tree", "cart", "trolley", "painting", "rug", "carpet"))
            return false;
        if (ContainsAny(lower, "shelf", "bookcase", "bookshelf", "cabinet", "cupboard",
            "counter", "locker", "vending", "freezer"))
            return true;

        // Zombie Interior Store 9-14 are rigid counters/cabinets; 17-29 are
        // shelving, checkout desks and vending/freezer blocks. Store 1-8 and
        // 15-16 are clothes, mannequins, carts or debris and stay review-only.
        if (!lower.StartsWith("store", StringComparison.Ordinal)) return false;
        int indexStart = "store".Length;
        int indexEnd = indexStart;
        while (indexEnd < lower.Length && char.IsDigit(lower[indexEnd])) indexEnd++;
        if (indexEnd == indexStart ||
            !int.TryParse(lower.Substring(indexStart, indexEnd - indexStart), out int storeIndex))
            return false;
        return (storeIndex >= 9 && storeIndex <= 14) ||
               (storeIndex >= 17 && storeIndex <= 29);
    }

    private static List<Vector2> ClipPolygonBelow(IReadOnlyList<Vector2> polygon, float maximumY)
    {
        List<Vector2> output = polygon.ToList();
        if (output.Count == 0) return output;
        List<Vector2> input = output;
        output = new List<Vector2>();
        Vector2 previous = input[input.Count - 1];
        bool previousInside = previous.y <= maximumY;
        foreach (Vector2 current in input)
        {
            bool currentInside = current.y <= maximumY;
            if (currentInside != previousInside)
            {
                float denominator = current.y - previous.y;
                float t = Mathf.Abs(denominator) <= Mathf.Epsilon
                    ? 0f
                    : (maximumY - previous.y) / denominator;
                output.Add(Vector2.Lerp(previous, current, Mathf.Clamp01(t)));
            }
            if (currentInside) output.Add(current);
            previous = current;
            previousInside = currentInside;
        }
        return output;
    }

    private static float SignedPolygonArea(IReadOnlyList<Vector2> points)
    {
        float twiceArea = 0f;
        for (int index = 0; index < points.Count; index++)
        {
            Vector2 current = points[index];
            Vector2 next = points[(index + 1) % points.Count];
            twiceArea += current.x * next.y - next.x * current.y;
        }
        return twiceArea * 0.5f;
    }

    private static float GetMaximumPathHeight(PolygonCollider2D polygon)
    {
        float maximum = 0f;
        for (int pathIndex = 0; pathIndex < polygon.pathCount; pathIndex++)
        {
            Vector2[] path = polygon.GetPath(pathIndex);
            if (path.Length == 0) continue;
            maximum = Mathf.Max(maximum,
                path.Max(point => point.y) - path.Min(point => point.y));
        }
        return maximum;
    }

    private static float SampleColliderAgreement(
        Collider2D first,
        Collider2D second,
        out float firstCoverage,
        out float secondCoverage)
    {
        firstCoverage = 0f;
        secondCoverage = 0f;
        Bounds sampleBounds = first.bounds;
        sampleBounds.Encapsulate(second.bounds);
        const int resolution = 96;
        int insideFirst = 0;
        int insideSecond = 0;
        int insideBoth = 0;
        for (int y = 0; y < resolution; y++)
        {
            float sampleY = Mathf.Lerp(sampleBounds.min.y, sampleBounds.max.y, (y + 0.5f) / resolution);
            for (int x = 0; x < resolution; x++)
            {
                float sampleX = Mathf.Lerp(sampleBounds.min.x, sampleBounds.max.x, (x + 0.5f) / resolution);
                Vector2 point = new Vector2(sampleX, sampleY);
                bool inFirst = first.OverlapPoint(point);
                bool inSecond = second.OverlapPoint(point);
                if (inFirst) insideFirst++;
                if (inSecond) insideSecond++;
                if (inFirst && inSecond) insideBoth++;
            }
        }

        if (insideFirst < 16 || insideSecond < 16) return 0f;
        firstCoverage = (float)insideBoth / insideFirst;
        secondCoverage = (float)insideBoth / insideSecond;
        int union = insideFirst + insideSecond - insideBoth;
        return union > 0 ? (float)insideBoth / union : 0f;
    }

    private static int CountCollidableCells(Tilemap tilemap)
    {
        int count = 0;
        foreach (Vector3Int position in tilemap.cellBounds.allPositionsWithin)
        {
            if (tilemap.HasTile(position) && tilemap.GetColliderType(position) != Tile.ColliderType.None)
                count++;
        }
        return count;
    }

    private void BeginPolygonDrawing()
    {
        if (drawingPolygon)
        {
            FinishDrawingPolygon();
            return;
        }

        if (EnsurePatchCollider() == null)
            return;

        drawingPoints.Clear();
        drawingPolygon = true;
        pickMissingCells = false;
        SceneView.RepaintAll();
    }

    private void FinishDrawingPolygon()
    {
        if (!drawingPolygon)
            return;

        if (drawingPoints.Count >= 3 && patchCollider != null)
        {
            Undo.RecordObject(patchCollider, "Add polygon collider patch path");
            int pathIndex = patchCollider.pathCount;
            patchCollider.pathCount++;
            patchCollider.SetPath(pathIndex, drawingPoints);
            EditorUtility.SetDirty(patchCollider);
            Debug.Log($"[Environment Fixer] Đã thêm Polygon path {pathIndex} với {drawingPoints.Count} đỉnh.");
        }

        drawingPoints.Clear();
        drawingPolygon = false;
        SceneView.RepaintAll();
        Repaint();
    }

    private void RemoveLastPatchPath()
    {
        if (patchCollider == null || patchCollider.pathCount == 0)
            return;

        Undo.RecordObject(patchCollider, "Remove polygon collider patch path");
        patchCollider.pathCount--;
        EditorUtility.SetDirty(patchCollider);
        SceneView.RepaintAll();
    }

    private void DuringSceneGui(SceneView sceneView)
    {
        if (root == null)
            return;

        if (showMissingCells && reviewTilemap != null)
            DrawMissingCells();

        Event current = Event.current;
        if (drawingPolygon)
        {
            HandlePolygonDrawing(current);
            return;
        }

        if (pickMissingCells && reviewTilemap != null)
            HandleMissingCellPicking(current);
    }

    private void DrawMissingCells()
    {
        TilemapAudit audit = tilemaps.FirstOrDefault(item => item.Tilemap == reviewTilemap) ?? AuditTilemap(reviewTilemap);
        GridLayout grid = reviewTilemap.layoutGrid;
        if (grid == null)
            return;

        foreach (Vector3Int cell in audit.MissingCells)
        {
            Vector3[] outline = GetCellDiamondWorld(grid, cell);
            bool selected = pickedCells.Contains(cell);
            Handles.color = selected ? new Color(1f, 0.85f, 0.1f, 0.95f) : new Color(1f, 0.15f, 0.1f, 0.8f);
            Handles.DrawAAPolyLine(selected ? 5f : 3f, outline[0], outline[1], outline[2], outline[3], outline[0]);
        }


        foreach (Vector3Int cell in audit.GridCells)
        {
            Vector3[] outline = GetCellDiamondWorld(grid, cell);
            bool selected = pickedCells.Contains(cell);
            Handles.color = selected ? new Color(1f, 0.95f, 0.15f, 0.95f) : new Color(1f, 0.5f, 0.05f, 0.8f);
            Handles.DrawAAPolyLine(selected ? 5f : 3f, outline[0], outline[1], outline[2], outline[3], outline[0]);
        }


        foreach (Vector3Int cell in audit.FullBoundsShapeCells)
        {
            Vector3[] outline = GetCellDiamondWorld(grid, cell);
            bool selected = pickedCells.Contains(cell);
            Handles.color = selected ? new Color(1f, 0.3f, 1f, 1f) : new Color(0.8f, 0.15f, 0.9f, 0.85f);
            Handles.DrawAAPolyLine(selected ? 5f : 3f, outline[0], outline[1], outline[2], outline[3], outline[0]);
        }
    }

    private void HandleMissingCellPicking(Event current)
    {
        if (current.type != EventType.MouseDown || current.button != 0 || current.alt)
            return;

        Vector3 world = MouseToWorld(current.mousePosition, reviewTilemap.transform.position.z);
        Vector3Int cell = reviewTilemap.WorldToCell(world);
        TilemapAudit audit = tilemaps.FirstOrDefault(item => item.Tilemap == reviewTilemap) ?? AuditTilemap(reviewTilemap);
        if (!audit.MissingCells.Contains(cell) &&
            !audit.GridCells.Contains(cell) &&
            !audit.FullBoundsShapeCells.Contains(cell))
            return;

        if (!pickedCells.Add(cell))
            pickedCells.Remove(cell);
        current.Use();
        Repaint();
        SceneView.RepaintAll();
    }

    private void HandlePolygonDrawing(Event current)
    {
        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        if (current.type == EventType.KeyDown)
        {
            if (current.keyCode == KeyCode.Escape)
            {
                drawingPoints.Clear();
                drawingPolygon = false;
                current.Use();
                Repaint();
                SceneView.RepaintAll();
                return;
            }

            if (current.keyCode == KeyCode.Backspace)
            {
                if (drawingPoints.Count > 0)
                    drawingPoints.RemoveAt(drawingPoints.Count - 1);
                current.Use();
                SceneView.RepaintAll();
                return;
            }

            if (current.keyCode == KeyCode.Return || current.keyCode == KeyCode.KeypadEnter)
            {
                FinishDrawingPolygon();
                current.Use();
                return;
            }
        }

        float z = patchCollider != null ? patchCollider.transform.position.z : root.transform.position.z;
        Vector3 mouseWorld = MouseToWorld(current.mousePosition, z);
        Vector2 mouseLocal = patchCollider != null ? ToLocal2D(patchCollider.transform, mouseWorld) : Vector2.zero;
        mouseLocal = Snap(mouseLocal);

        if (current.type == EventType.MouseDown && current.button == 0 && !current.alt)
        {
            drawingPoints.Add(mouseLocal);
            bool finish = current.clickCount >= 2 && drawingPoints.Count >= 3;
            current.Use();
            if (finish)
                FinishDrawingPolygon();
        }

        if (current.type == EventType.Repaint && patchCollider != null)
        {
            Handles.color = new Color(1f, 0.75f, 0.1f, 1f);
            List<Vector3> worldPoints = drawingPoints
                .Select(point => patchCollider.transform.TransformPoint(point))
                .ToList();
            if (worldPoints.Count > 0)
            {
                worldPoints.Add(patchCollider.transform.TransformPoint(mouseLocal));
                Handles.DrawAAPolyLine(4f, worldPoints.ToArray());
                foreach (Vector3 point in worldPoints.Take(worldPoints.Count - 1))
                    Handles.DotHandleCap(0, point, Quaternion.identity, HandleUtility.GetHandleSize(point) * 0.04f, EventType.Repaint);
            }
        }
    }

    private Vector2 Snap(Vector2 point)
    {
        if (snapStep <= 0f)
            return point;
        return new Vector2(
            Mathf.Round(point.x / snapStep) * snapStep,
            Mathf.Round(point.y / snapStep) * snapStep);
    }

    private static Vector3 MouseToWorld(Vector2 mousePosition, float z)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
        Plane plane = new Plane(Vector3.forward, new Vector3(0f, 0f, z));
        if (plane.Raycast(ray, out float distance))
            return ray.GetPoint(distance);
        return new Vector3(ray.origin.x, ray.origin.y, z);
    }

    private static Vector3[] GetCellDiamondWorld(GridLayout grid, Vector3Int cell)
    {
        return new[]
        {
            grid.CellToWorld(cell),
            grid.CellToWorld(cell + Vector3Int.right),
            grid.CellToWorld(cell + Vector3Int.right + Vector3Int.up),
            grid.CellToWorld(cell + Vector3Int.up)
        };
    }

    private static Vector2 ToLocal2D(Transform target, Vector3 world)
    {
        Vector3 local = target.InverseTransformPoint(world);
        return new Vector2(local.x, local.y);
    }

    private static void SelectAndFrame(GameObject gameObject)
    {
        Selection.activeGameObject = gameObject;
        EditorGUIUtility.PingObject(gameObject);
        SceneView.lastActiveSceneView?.FrameSelected();
    }

}
