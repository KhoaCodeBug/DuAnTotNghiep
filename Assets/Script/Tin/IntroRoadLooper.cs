using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Builds a visual-only strip from the existing Intro tilemaps and recycles
/// three copies outside the camera, creating an endless-road trailer shot.
/// The gameplay Grid is never moved or duplicated with colliders/AI.
/// </summary>
public sealed class IntroRoadLooper : MonoBehaviour
{
    [SerializeField] private Transform gridRoot;
    [SerializeField] private Transform car;
    [SerializeField] private Transform routeStart;
    [SerializeField] private Transform routeEnd;
    [SerializeField, Range(3, 5)] private int chunkCount = 3;
    [SerializeField, Min(8)] private int loopCellSpan = 70;
    [SerializeField, Min(4f)] private float corridorHalfWidth = 26f;
    [SerializeField, Min(0f)] private float seamOverlap = 4f;
    [SerializeField, Min(4f)] private float surfaceSeamOverlap = 28f;
    [SerializeField, Min(0f)] private float detailSeamClearance = 6f;
    [SerializeField, Min(0.1f)] private float scrollSpeed = 7.2f;
    [SerializeField, Min(0f)] private float carSwayDistance = 0.018f;
    [SerializeField, Min(1f)] private float exitDistance = 28f;

    private readonly List<Transform> chunks = new List<Transform>();
    [SerializeField, HideInInspector] private GameObject loopRoot;
    private Vector3 routeVector;
    private Vector3 segmentVector;
    private Vector3 routeDirection;
    private Vector3 sideDirection;
    private Vector3 chunkOrigin;
    private Vector3 carAnchor;
    private Vector3 exitStartPosition;
    private float routeLength;
    private float segmentLength;
    private float scrollDistance;
    private float exitElapsed;
    private float exitDuration;
    private bool prepared;
    private bool looping;
    private bool exiting;

    public bool IsExitComplete { get; private set; }
    public GameObject LoopRoot => loopRoot;

    public bool Prepare()
    {
        if (prepared) return true;

        gridRoot ??= GameObject.Find("Grid")?.transform;
        car ??= FindFirstObjectByType<IntroCarDriveSetup>()?.transform;
        routeStart ??= GameObject.Find("CarStart")?.transform;
        routeEnd ??= GameObject.Find("CarStop")?.transform;

        if (gridRoot == null || car == null || routeStart == null || routeEnd == null)
        {
            Debug.LogError("IntroRoadLooper needs Grid, Intro car, CarStart and CarStop.", this);
            return false;
        }

        Grid sourceGrid = gridRoot.GetComponent<Grid>();
        if (sourceGrid == null)
        {
            Debug.LogError("Intro Grid root has no Grid component.", this);
            return false;
        }

        // Use an exact Grid-cell delta for the repeating piece. An arbitrary
        // world-space delta leaves a thin crack between isometric Tilemaps.
        Vector3Int startCell = sourceGrid.WorldToCell(routeStart.position);
        Vector3Int endCell = sourceGrid.WorldToCell(routeEnd.position);
        Vector3Int fullCellDelta = endCell - startCell;
        Vector3Int repeatCellDelta;
        if (Mathf.Abs(fullCellDelta.y) >= Mathf.Abs(fullCellDelta.x))
            repeatCellDelta = new Vector3Int(0, fullCellDelta.y < 0 ? -loopCellSpan : loopCellSpan, 0);
        else
            repeatCellDelta = new Vector3Int(fullCellDelta.x < 0 ? -loopCellSpan : loopCellSpan, 0, 0);

        // Only bake the straight road section selected for the trailer. The
        // rest of CarStart -> CarStop belongs to the later trouble sequence.
        routeVector = sourceGrid.GetCellCenterWorld(startCell + repeatCellDelta)
            - sourceGrid.GetCellCenterWorld(startCell);
        routeVector.z = 0f;
        routeLength = routeVector.magnitude;
        if (routeLength < 1f)
        {
            Debug.LogError("IntroRoadLooper route is too short.", this);
            return false;
        }

        routeDirection = routeVector / routeLength;
        segmentLength = routeLength;
        segmentVector = routeVector;
        sideDirection = new Vector3(-routeDirection.y, routeDirection.x, 0f);
        chunkOrigin = gridRoot.position;
        carAnchor = routeStart.position;

        if (loopRoot != null && loopRoot.transform.childCount >= chunkCount)
        {
            chunks.Clear();
            for (int i = 0; i < loopRoot.transform.childCount; i++)
                chunks.Add(loopRoot.transform.GetChild(i));
            loopRoot.SetActive(false);
            prepared = true;
            return true;
        }

        loopRoot = new GameObject("TrailerLoopRoot");
        GameObject template = BuildVisualChunk("RoadChunk_A");
        if (template == null)
        {
            DisposeObject(loopRoot);
            loopRoot = null;
            return false;
        }

        template.transform.SetParent(loopRoot.transform, true);
        chunks.Add(template.transform);
        for (int i = 1; i < chunkCount; i++)
        {
            GameObject copy = Instantiate(template, loopRoot.transform);
            copy.name = $"RoadChunk_{(char)('A' + i)}";
            chunks.Add(copy.transform);
        }

        loopRoot.SetActive(false);
        prepared = true;
        return true;
    }

    public void ClearPreparedChunksReference()
    {
        prepared = false;
        chunks.Clear();
        loopRoot = null;
    }

    public void BeginLoop()
    {
        if (!Prepare()) return;

        gridRoot.gameObject.SetActive(false);
        loopRoot.SetActive(true);
        carAnchor = routeStart.position;
        car.position = WithCarZ(carAnchor);
        scrollDistance = 0f;
        exitElapsed = 0f;
        exiting = false;
        looping = true;
        IsExitComplete = false;
        UpdateChunkPositions();
    }

    public void BeginExitShot(float duration)
    {
        if (!looping || exiting) return;
        exiting = true;
        exitElapsed = 0f;
        exitDuration = Mathf.Max(0.1f, duration);
        exitStartPosition = car.position;
        IsExitComplete = false;
    }

    public void SwitchToTroubleRoad()
    {
        looping = false;
        exiting = false;
        if (loopRoot != null) loopRoot.SetActive(false);
        if (gridRoot != null) gridRoot.gameObject.SetActive(true);
        if (car != null && routeStart != null) car.position = WithCarZ(routeStart.position);
    }

    private void Update()
    {
        if (!looping) return;

        float delta = Time.unscaledDeltaTime;
        float speedMultiplier = exiting
            ? Mathf.Lerp(1f, 1.65f, Mathf.Clamp01(exitElapsed / Mathf.Max(0.1f, exitDuration)))
            : 1f;
        scrollDistance += scrollSpeed * speedMultiplier * delta;
        UpdateChunkPositions();

        if (!exiting)
        {
            float sway = Mathf.Sin(Time.unscaledTime * 7f) * carSwayDistance;
            car.position = WithCarZ(carAnchor + sideDirection * sway);
            return;
        }

        exitElapsed += delta;
        float t = Mathf.Clamp01(exitElapsed / exitDuration);
        float eased = t * t * t;
        car.position = WithCarZ(Vector3.LerpUnclamped(exitStartPosition,
            exitStartPosition + routeDirection * exitDistance, eased));

        if (t >= 1f) IsExitComplete = true;
    }

    private void UpdateChunkPositions()
    {
        // Keep the active strip surrounded by one neighbour on each side.
        // A 0..length phase puts all recycled pieces on the same side near
        // the wrap point, briefly exposing the camera clear colour.
        float phase = Mathf.Repeat(scrollDistance + segmentLength * 0.5f, segmentLength)
            - segmentLength * 0.5f;
        float centreIndex = (chunks.Count - 1) * 0.5f;
        for (int i = 0; i < chunks.Count; i++)
            chunks[i].position = chunkOrigin + segmentVector * (i - centreIndex) - routeDirection * phase;
    }

    private GameObject BuildVisualChunk(string chunkName)
    {
        Grid sourceGrid = gridRoot.GetComponent<Grid>();
        if (sourceGrid == null)
        {
            Debug.LogError("Intro Grid root has no Grid component.", this);
            return null;
        }

        GameObject chunk = new GameObject(chunkName);
        GameObject visualGridObject = new GameObject("VisualGrid", typeof(Grid));
        visualGridObject.transform.SetParent(chunk.transform, false);
        Grid visualGrid = visualGridObject.GetComponent<Grid>();
        visualGrid.cellSize = sourceGrid.cellSize;
        visualGrid.cellGap = sourceGrid.cellGap;
        visualGrid.cellLayout = sourceGrid.cellLayout;
        visualGrid.cellSwizzle = sourceGrid.cellSwizzle;

        int copiedTileCount = 0;
        Tilemap[] sourceTilemaps = gridRoot.GetComponentsInChildren<Tilemap>(true);
        foreach (Tilemap source in sourceTilemaps)
        {
            TilemapRenderer sourceRenderer = source.GetComponent<TilemapRenderer>();
            if (sourceRenderer == null) continue;

            GameObject layer = new GameObject(source.name, typeof(Tilemap), typeof(TilemapRenderer));
            layer.transform.SetParent(visualGridObject.transform, false);
            layer.transform.localPosition = gridRoot.InverseTransformPoint(source.transform.position);
            layer.transform.localRotation = Quaternion.Inverse(gridRoot.rotation) * source.transform.rotation;
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
                Vector3 fromStart = world - routeStart.position;
                float alongRoute = Vector3.Dot(fromStart, routeDirection);
                float sideDistance = Mathf.Abs(Vector3.Dot(fromStart, sideDirection));
                bool continuousSurface = IsContinuousSurfaceLayer(source.name);
                float longitudinalPadding = continuousSurface ? surfaceSeamOverlap : seamOverlap;
                // Keep one complete road strip and a small overlap at both
                // cell-aligned ends. The overlap covers sprite overhangs and
                // prevents the scene clear colour from ever showing at a seam.
                if (alongRoute < -longitudinalPadding || alongRoute > routeLength + longitudinalPadding || sideDistance > corridorHalfWidth)
                    continue;

                // Terrain must reach across both ends. Decorative layers use
                // an inset so roofs, walls and trees are never cut into small
                // floating sprite fragments at the repeating seam.
                if (!continuousSurface
                    && (alongRoute < detailSeamClearance
                        || alongRoute > routeLength - detailSeamClearance
                        || sideDistance > corridorHalfWidth - detailSeamClearance))
                    continue;

                destination.SetTile(cell, tile);
                destination.SetTransformMatrix(cell, source.GetTransformMatrix(cell));
                destination.SetColor(cell, source.GetColor(cell));
                destination.SetTileFlags(cell, source.GetTileFlags(cell));
                copiedTileCount++;
            }

            destination.CompressBounds();
            if (destination.GetUsedTilesCount() == 0) DisposeObject(layer);
        }

        if (copiedTileCount == 0)
        {
            Debug.LogError("IntroRoadLooper could not copy tiles from the route corridor.", this);
            DisposeObject(chunk);
            return null;
        }

        return chunk;
    }

    private Vector3 WithCarZ(Vector3 position)
    {
        position.z = car.position.z;
        return position;
    }

    private static bool IsContinuousSurfaceLayer(string layerName)
    {
        string normalized = layerName.ToLowerInvariant();
        return normalized == "tilemap"
            || normalized.Contains("matdat")
            || normalized.Contains("thamco");
    }

    private static void DisposeObject(Object target)
    {
        if (target == null) return;
        if (Application.isPlaying) Destroy(target);
        else DestroyImmediate(target);
    }
}
