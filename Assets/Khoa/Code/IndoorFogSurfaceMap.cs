using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;

/// <summary>
/// Opt-in static surface projection for one authored interior. No scene-wide discovery,
/// light, collider, gameplay visibility or network state is changed by this component.
/// The atlas maps visible sprite pixels to their footprint, preserving sprite alpha.
/// </summary>
[DisallowMultipleComponent]
public sealed class IndoorFogSurfaceMap : MonoBehaviour
{
    public Collider2D indoorVolume;
    public Tilemap[] surfaces;
    public SpriteRenderer[] spriteSurfaces;
    [Range(256, 1536)] public int atlasResolution = 1024;
    [Range(0f, 0.2f)] public float surfaceProbeInset = 0.08f;
    // Daytime Global Light is much stronger; more cover keeps unaided indoor sight
    // only modestly brighter than night instead of almost as bright as the flashlight.
    [Range(0f, 1f)] public float dayAmbientOpacity = 0.86f;
    [Range(0f, 1f)] public float nightAmbientOpacity = 0.15f;
    [Range(0f, 1f)] public float litOpacity = 0.08f;
    [Range(0f, 0.3f)] public float coneInset = 0.06f;
    [Tooltip("Flashlight intensity transition in cosine space. A wider ramp dims inward; the outer cone and wall visibility stay unchanged.")]
    [Range(0.2f, 0.6f)] public float flashlightConeFeather = 0.20f;
    [Tooltip("Fade width on the lit side of a cast-shadow edge, not distance to a wall face. Zero preserves V2 for A/B comparison.")]
    [Range(0f, 2.5f)] public float flashlightBoundaryFadeDistance = 0.65f;

    public RenderTexture Atlas { get; private set; }
    public Vector4 AtlasBounds { get; private set; }
    public int SurfaceCount { get; private set; }
    public double LastBuildMilliseconds { get; private set; }
    private bool attemptedBuild;

    private struct Surface
    {
        public Sprite sprite;
        public Matrix4x4 matrix;
        public int layer, order;
        public float depth;
    }

    public bool EnsureAtlas()
    {
        if (!isActiveAndEnabled || indoorVolume == null) return false;
        if (Atlas != null && Atlas.IsCreated()) return true;
        if (attemptedBuild) return false;
        attemptedBuild = true;
        Shader shader = Shader.Find("Hidden/IndoorFogSurfaceAtlas");
        if (shader == null || !shader.isSupported || !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf))
        {
            Debug.LogWarning("[IndoorFogSurface] Atlas unavailable; preserving legacy Fog.", this);
            return false;
        }

        var timer = System.Diagnostics.Stopwatch.StartNew();
        var entries = new List<Surface>();
        Bounds bounds = indoorVolume.bounds;
        bounds.Expand(new Vector3(4f, 5f, 0f));
        AtlasBounds = new Vector4(bounds.min.x, bounds.min.y, bounds.size.x, bounds.size.y);
        if (surfaces != null)
        foreach (Tilemap map in surfaces)
        {
            if (map == null || !map.gameObject.activeInHierarchy) continue;
            var renderer = map.GetComponent<TilemapRenderer>();
            if (renderer == null || !renderer.enabled) continue;
            // A sparse, map-wide Tilemap must never turn this local build into a map scan.
            if ((long)map.cellBounds.size.x * map.cellBounds.size.y * map.cellBounds.size.z > 10000)
            { Debug.LogWarning("[IndoorFogSurface] Tilemap too broad for the local prototype: " + map.name, this); continue; }
            foreach (Vector3Int cell in map.cellBounds.allPositionsWithin)
            {
                Sprite sprite = map.GetSprite(cell);
                if (sprite == null || map.GetColor(cell).a * map.color.a < 0.5f) continue;
                Vector3 center = map.GetCellCenterWorld(cell);
                if (!bounds.Contains(new Vector3(center.x, center.y, bounds.center.z))) continue;
                entries.Add(new Surface { sprite = sprite,
                    matrix = map.transform.localToWorldMatrix * Matrix4x4.Translate(map.GetCellCenterLocal(cell)) *
                        map.orientationMatrix * map.GetTransformMatrix(cell),
                    layer = SortingLayer.GetLayerValueFromID(renderer.sortingLayerID), order = renderer.sortingOrder, depth = center.y });
            }
        }
        if (spriteSurfaces != null)
        foreach (SpriteRenderer renderer in spriteSurfaces)
        {
            if (renderer == null || renderer.sprite == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
            entries.Add(new Surface { sprite = renderer.sprite,
                matrix = renderer.localToWorldMatrix * Matrix4x4.Scale(new Vector3(renderer.flipX ? -1f : 1f, renderer.flipY ? -1f : 1f, 1f)),
                layer = SortingLayer.GetLayerValueFromID(renderer.sortingLayerID), order = renderer.sortingOrder, depth = renderer.transform.position.y });
        }
        entries.Sort((a, b) => a.layer != b.layer ? a.layer.CompareTo(b.layer) :
            a.order != b.order ? a.order.CompareTo(b.order) : b.depth.CompareTo(a.depth));
        SurfaceCount = entries.Count;
        if (SurfaceCount == 0) return false;

        int width = Mathf.Clamp(atlasResolution, 256, 1536);
        int height = Mathf.Max(128, Mathf.RoundToInt(width * bounds.size.y / bounds.size.x));
        Atlas = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear)
        { name = "Indoor surface projection (local)", hideFlags = HideFlags.DontSave,
            filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        Atlas.Create();
        var material = new Material(shader) { hideFlags = HideFlags.DontSave };
        material.SetVector("_AtlasBounds", AtlasBounds);
        var meshes = new Dictionary<Sprite, Mesh>();
        var properties = new MaterialPropertyBlock();
        using (var commands = new CommandBuffer { name = "Bake static indoor surface projection" })
        {
            commands.SetRenderTarget(Atlas);
            commands.ClearRenderTarget(false, true, Color.clear);
            foreach (Surface entry in entries)
            {
                if (!meshes.TryGetValue(entry.sprite, out Mesh mesh))
                { mesh = CreateSurfaceMesh(entry.sprite); meshes.Add(entry.sprite, mesh); }
                if (mesh == null) continue;
                properties.Clear();
                properties.SetTexture("_MainTex", entry.sprite.texture);
                commands.DrawMesh(mesh, entry.matrix, material, 0, 0, properties);
            }
            Graphics.ExecuteCommandBuffer(commands);
        }
        foreach (Mesh mesh in meshes.Values) if (mesh != null) Destroy(mesh);
        Destroy(material);
        timer.Stop();
        LastBuildMilliseconds = timer.Elapsed.TotalMilliseconds;
        return true;
    }

    private static Mesh CreateSurfaceMesh(Sprite sprite)
    {
        if (sprite.packed && sprite.packingRotation != SpritePackingRotation.None) return null;
        Vector2[] original = sprite.vertices;
        Vector2[] originalUv = sprite.uv;
        if (original.Length < 3) return null;
        Vector2 min = original[0], max = original[0], uvMin = originalUv[0], uvMax = originalUv[0];
        for (int i = 1; i < original.Length; i++)
        { min = Vector2.Min(min, original[i]); max = Vector2.Max(max, original[i]); uvMin = Vector2.Min(uvMin, originalUv[i]); uvMax = Vector2.Max(uvMax, originalUv[i]); }
        var shapes = new List<Vector2[]>();
        var points = new List<Vector2>();
        for (int i = 0; i < sprite.GetPhysicsShapeCount(); i++)
        { sprite.GetPhysicsShape(i, points); shapes.Add(points.ToArray()); }
        const int columns = 32;
        var vertices = new Vector3[(columns + 1) * 2];
        var uv = new Vector2[vertices.Length];
        var feet = new Vector2[vertices.Length];
        var indices = new int[columns * 6];
        for (int c = 0; c <= columns; c++)
        {
            float t = c / (float)columns;
            float x = Mathf.Lerp(min.x, max.x, t);
            float foot = FootY(shapes, x);
            for (int row = 0; row < 2; row++)
            {
                int index = c * 2 + row;
                vertices[index] = new Vector3(x, row == 0 ? min.y : max.y, 0f);
                uv[index] = new Vector2(Mathf.Lerp(uvMin.x, uvMax.x, t), row == 0 ? uvMin.y : uvMax.y);
                feet[index] = new Vector2(x, foot);
            }
            if (c == columns) continue;
            int start = c * 6, v = c * 2;
            indices[start] = v; indices[start + 1] = v + 1; indices[start + 2] = v + 2;
            indices[start + 3] = v + 2; indices[start + 4] = v + 1; indices[start + 5] = v + 3;
        }
        var mesh = new Mesh { name = "Surface footprint " + sprite.name, hideFlags = HideFlags.DontSave };
        mesh.vertices = vertices; mesh.uv = uv; mesh.uv2 = feet; mesh.triangles = indices;
        mesh.RecalculateBounds();
        return mesh;
    }

    private static float FootY(List<Vector2[]> shapes, float x)
    {
        float lowest = float.PositiveInfinity;
        foreach (Vector2[] shape in shapes)
        for (int i = 0; i < shape.Length; i++)
        {
            Vector2 a = shape[i], b = shape[(i + 1) % shape.Length];
            if (Mathf.Abs(a.x - b.x) < 0.00001f || x < Mathf.Min(a.x, b.x) || x > Mathf.Max(a.x, b.x)) continue;
            lowest = Mathf.Min(lowest, Mathf.Lerp(a.y, b.y, (x - a.x) / (b.x - a.x)));
        }
        // Wall decorations use the cell's ground anchor, not the bottom of a floating painting.
        return float.IsPositiveInfinity(lowest) ? 0f : Mathf.Min(lowest, 0.12f);
    }

    private void OnDisable()
    {
        if (Atlas != null) { Atlas.Release(); Destroy(Atlas); Atlas = null; }
        attemptedBuild = false;
    }
}
