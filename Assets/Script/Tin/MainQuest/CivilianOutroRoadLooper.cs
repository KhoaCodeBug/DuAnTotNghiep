using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Builds a visual-only repeating strip from Main's authored map around the
/// civilian exit. Gameplay tilemaps, colliders and network objects are never
/// copied into the cinematic strip.
/// </summary>
[DisallowMultipleComponent]
public sealed class CivilianOutroRoadLooper : MonoBehaviour
{
    private const int ChunkCount = 3;
    private const int DefaultCellSpan = 48;
    private const float CorridorHalfWidth = 27f;
    private const float SurfaceSeamOverlap = 24f;
    private const float DetailSeamClearance = 4f;
    private const float ScrollSpeed = 7.2f;

    private readonly List<Transform> chunks = new();
    private readonly List<(SpriteRenderer renderer, bool enabled, bool forceOff)> hiddenScenery = new();
    private Grid sourceGrid;
    private GameObject loopRoot;
    private Transform carVisual;
    private Vector3 segmentVector;
    private Vector3 routeDirection;
    private Vector3 sideDirection;
    private Vector3 chunkOrigin;
    private Vector3 sourceStartWorld;
    private float segmentLength;
    private float scrollDistance;
    private bool sourceGridWasActive;

    public bool IsLooping { get; private set; }
    public Vector3 RouteDirection => routeDirection;
    public Vector3 CarAnchor { get; private set; }

    public bool Prepare(Vector2 checkpoint, Vector2 cityExit, Vector2 outroEnd, Transform targetCar)
    {
        StopLoop();
        carVisual = targetCar;
        sourceGrid = FindBestMapGrid();
        if (sourceGrid == null || carVisual == null)
        {
            Debug.LogWarning("[ROUTE A OUTRO] No usable Main Grid was found; using the moving-car fallback.", this);
            return false;
        }

        Vector2 requestedDirection = cityExit - checkpoint;
        if (requestedDirection.sqrMagnitude < 0.01f) requestedDirection = outroEnd - cityExit;
        if (requestedDirection.sqrMagnitude < 0.01f) requestedDirection = Vector2.right;

        // The loop starts at the regroup checkpoint. CityExit is deliberately
        // only a direction marker because it may sit beside Main's gray edge.
        Vector3Int originCell = sourceGrid.WorldToCell(checkpoint);
        Vector3Int targetCell = sourceGrid.WorldToCell(cityExit);
        Vector3Int cellDelta = targetCell - originCell;
        Vector3Int repeatCellDelta;
        if (Mathf.Abs(cellDelta.y) >= Mathf.Abs(cellDelta.x))
        {
            int sign = cellDelta.y != 0 ? (cellDelta.y < 0 ? -1 : 1) : (requestedDirection.y < 0f ? -1 : 1);
            repeatCellDelta = new Vector3Int(0, sign * DefaultCellSpan, 0);
        }
        else
        {
            int sign = cellDelta.x != 0 ? (cellDelta.x < 0 ? -1 : 1) : (requestedDirection.x < 0f ? -1 : 1);
            repeatCellDelta = new Vector3Int(sign * DefaultCellSpan, 0, 0);
        }

        Vector3 endWorld = sourceGrid.GetCellCenterWorld(originCell);
        Vector3 startWorld = sourceGrid.GetCellCenterWorld(originCell - repeatCellDelta);
        segmentVector = endWorld - startWorld;
        segmentVector.z = 0f;
        segmentLength = segmentVector.magnitude;
        if (segmentLength < 2f)
        {
            Debug.LogWarning("[ROUTE A OUTRO] The authored exit strip is too short to loop.", this);
            return false;
        }

        routeDirection = segmentVector / segmentLength;
        if (Vector2.Dot(routeDirection, requestedDirection.normalized) < 0f)
        {
            routeDirection = -routeDirection;
            segmentVector = -segmentVector;
            startWorld = endWorld - segmentVector;
        }
        sideDirection = new Vector3(-routeDirection.y, routeDirection.x, 0f);
        sourceStartWorld = startWorld;
        chunkOrigin = sourceGrid.transform.position;
        CarAnchor = new Vector3(checkpoint.x, checkpoint.y, carVisual.position.z);

        loopRoot = new GameObject("Civilian Outro Road Loop");
        GameObject template = BuildVisualChunk("CivilianRoadChunk_A");
        if (template == null)
        {
            Destroy(loopRoot);
            loopRoot = null;
            return false;
        }
        template.transform.SetParent(loopRoot.transform, true);
        chunks.Add(template.transform);
        for (int i = 1; i < ChunkCount; i++)
        {
            GameObject copy = Instantiate(template, loopRoot.transform);
            copy.name = $"CivilianRoadChunk_{(char)('A' + i)}";
            chunks.Add(copy.transform);
        }
        loopRoot.SetActive(false);
        return true;
    }

    public void BeginLoop()
    {
        if (loopRoot == null || sourceGrid == null || chunks.Count == 0) return;
        sourceGridWasActive = sourceGrid.gameObject.activeSelf;
        sourceGrid.gameObject.SetActive(false);
        for (int i = 0; i < hiddenScenery.Count; i++)
        {
            SpriteRenderer renderer = hiddenScenery[i].renderer;
            if (renderer == null) continue;
            renderer.forceRenderingOff = true;
            renderer.enabled = false;
        }
        loopRoot.SetActive(true);
        scrollDistance = 0f;
        carVisual.position = CarAnchor;
        IsLooping = true;
        UpdateChunkPositions();
    }

    private void Update()
    {
        if (!IsLooping) return;
        scrollDistance += ScrollSpeed * Time.unscaledDeltaTime;
        UpdateChunkPositions();
        if (carVisual != null)
        {
            float sway = Mathf.Sin(Time.unscaledTime * 6.5f) * 0.025f;
            carVisual.position = CarAnchor + sideDirection * sway;
        }
    }

    public void StopLoop()
    {
        IsLooping = false;
        if (sourceGrid != null && sourceGridWasActive)
            sourceGrid.gameObject.SetActive(true);
        sourceGridWasActive = false;
        for (int i = 0; i < hiddenScenery.Count; i++)
        {
            var state = hiddenScenery[i];
            if (state.renderer == null) continue;
            state.renderer.forceRenderingOff = state.forceOff;
            state.renderer.enabled = state.enabled;
        }
        hiddenScenery.Clear();
        chunks.Clear();
        if (loopRoot != null) Destroy(loopRoot);
        loopRoot = null;
        sourceGrid = null;
        carVisual = null;
    }

    private void UpdateChunkPositions()
    {
        float phase = Mathf.Repeat(scrollDistance + segmentLength * 0.5f, segmentLength)
                      - segmentLength * 0.5f;
        float centreIndex = (chunks.Count - 1) * 0.5f;
        for (int i = 0; i < chunks.Count; i++)
            chunks[i].position = chunkOrigin + segmentVector * (i - centreIndex) - routeDirection * phase;
    }

    private GameObject BuildVisualChunk(string chunkName)
    {
        GameObject chunk = new GameObject(chunkName);
        GameObject visualGridObject = new GameObject("VisualGrid", typeof(Grid));
        visualGridObject.transform.SetParent(chunk.transform, false);
        visualGridObject.layer = sourceGrid.gameObject.layer;
        Grid visualGrid = visualGridObject.GetComponent<Grid>();
        visualGrid.cellSize = sourceGrid.cellSize;
        visualGrid.cellGap = sourceGrid.cellGap;
        visualGrid.cellLayout = sourceGrid.cellLayout;
        visualGrid.cellSwizzle = sourceGrid.cellSwizzle;

        int copiedTileCount = 0;
        Tilemap[] tilemaps = sourceGrid.GetComponentsInChildren<Tilemap>(true);
        for (int i = 0; i < tilemaps.Length; i++)
        {
            Tilemap source = tilemaps[i];
            TilemapRenderer sourceRenderer = source.GetComponent<TilemapRenderer>();
            if (sourceRenderer == null) continue;

            GameObject layer = new GameObject(source.name, typeof(Tilemap), typeof(TilemapRenderer));
            layer.transform.SetParent(visualGridObject.transform, false);
            layer.layer = source.gameObject.layer;
            layer.transform.localPosition = sourceGrid.transform.InverseTransformPoint(source.transform.position);
            layer.transform.localRotation = Quaternion.Inverse(sourceGrid.transform.rotation) * source.transform.rotation;
            layer.transform.localScale = source.transform.lossyScale;
            layer.SetActive(source.gameObject.activeInHierarchy);

            Tilemap destination = layer.GetComponent<Tilemap>();
            destination.color = source.color;
            destination.tileAnchor = source.tileAnchor;
            destination.orientation = source.orientation;
            destination.orientationMatrix = source.orientationMatrix;
            destination.animationFrameRate = source.animationFrameRate;
            TilemapRenderer destinationRenderer = layer.GetComponent<TilemapRenderer>();
            destinationRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
            destinationRenderer.sortingOrder = sourceRenderer.sortingOrder;
            destinationRenderer.mode = sourceRenderer.mode;
            destinationRenderer.sortOrder = sourceRenderer.sortOrder;
            destinationRenderer.sharedMaterial = sourceRenderer.sharedMaterial;

            foreach (Vector3Int cell in source.cellBounds.allPositionsWithin)
            {
                TileBase tile = source.GetTile(cell);
                if (tile == null) continue;
                Vector3 world = source.GetCellCenterWorld(cell);
                if (!InsideCorridor(world, source.name, out _)) continue;
                destination.SetTile(cell, tile);
                destination.SetTransformMatrix(cell, source.GetTransformMatrix(cell));
                destination.SetColor(cell, source.GetColor(cell));
                destination.SetTileFlags(cell, source.GetTileFlags(cell));
                copiedTileCount++;
            }
            destination.CompressBounds();
            if (destination.GetUsedTilesCount() == 0) Destroy(layer);
        }

        CopyStaticScenery(chunk.transform);
        if (copiedTileCount > 0) return chunk;
        Debug.LogWarning("[ROUTE A OUTRO] No tiles were found around CivilianCityExit.", this);
        Destroy(chunk);
        return null;
    }

    private void CopyStaticScenery(Transform chunk)
    {
        SpriteRenderer[] renderers = FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer source = renderers[i];
            if (source == null || !source.enabled || source.sprite == null ||
                source.GetComponentInParent<Tilemap>() != null ||
                source.GetComponentInParent<NetworkObject>() != null ||
                source.transform.IsChildOf(carVisual) ||
                source.GetComponentInParent<PlayerMovement>() != null ||
                source.GetComponentInParent<VehicleControllerFusion>() != null ||
                source.GetComponentInParent<ZombieAIKhoaRebuilt>() != null ||
                source.GetComponentInParent<ZOmbieAI_Khoa>() != null ||
                source.GetComponentInParent<ZombieHealth>() != null ||
                source.gameObject.name.Contains("Marker") ||
                source.gameObject.name.Contains("Outro"))
                continue;
            if (!InsideCorridor(source.transform.position, "detail", out _)) continue;

            GameObject copyObject = new GameObject(source.gameObject.name + " (Outro)");
            copyObject.transform.SetParent(chunk, false);
            copyObject.transform.localPosition = sourceGrid.transform.InverseTransformPoint(source.transform.position);
            copyObject.transform.localRotation = Quaternion.Inverse(sourceGrid.transform.rotation) * source.transform.rotation;
            copyObject.transform.localScale = source.transform.lossyScale;
            copyObject.layer = source.gameObject.layer;
            SpriteRenderer copy = copyObject.AddComponent<SpriteRenderer>();
            copy.sprite = source.sprite;
            copy.color = source.color;
            copy.flipX = source.flipX;
            copy.flipY = source.flipY;
            copy.drawMode = source.drawMode;
            copy.size = source.size;
            copy.maskInteraction = source.maskInteraction;
            copy.sortingLayerID = source.sortingLayerID;
            copy.sortingOrder = source.sortingOrder;
            copy.sharedMaterial = source.sharedMaterial;
            hiddenScenery.Add((source, source.enabled, source.forceRenderingOff));
        }
    }

    private bool InsideCorridor(Vector3 world, string layerName, out float alongRoute)
    {
        Vector3 fromStart = world - sourceStartWorld;
        alongRoute = Vector3.Dot(fromStart, routeDirection);
        float sideDistance = Mathf.Abs(Vector3.Dot(fromStart, sideDirection));
        bool surface = IsContinuousSurfaceLayer(layerName);
        float padding = surface ? SurfaceSeamOverlap : 0f;
        if (alongRoute < -padding || alongRoute > segmentLength + padding ||
            sideDistance > CorridorHalfWidth)
            return false;
        return surface || (alongRoute >= DetailSeamClearance &&
                           alongRoute <= segmentLength - DetailSeamClearance &&
                           sideDistance <= CorridorHalfWidth - DetailSeamClearance);
    }

    private static Grid FindBestMapGrid()
    {
        Grid[] grids = FindObjectsByType<Grid>(FindObjectsSortMode.None);
        Grid best = null;
        int bestTilemapCount = 0;
        for (int i = 0; i < grids.Length; i++)
        {
            if (grids[i] == null || !grids[i].gameObject.activeInHierarchy) continue;
            int tilemapCount = grids[i].GetComponentsInChildren<Tilemap>(true).Length;
            if (tilemapCount <= bestTilemapCount) continue;
            best = grids[i];
            bestTilemapCount = tilemapCount;
        }
        return best;
    }

    private static bool IsContinuousSurfaceLayer(string layerName)
    {
        string normalized = layerName.ToLowerInvariant();
        return normalized == "tilemap" || normalized.Contains("matdat") ||
               normalized.Contains("thamco") || normalized.Contains("road") ||
               normalized.Contains("duong");
    }

    private void OnDestroy() => StopLoop();
}
