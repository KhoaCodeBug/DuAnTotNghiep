using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Converts the real isometric Main-scene Tilemaps into a flat, cell-accurate
/// Project-Zomboid-style map. No gameplay camera pixels are used: every output
/// pixel represents an occupied cell in the shared Map Grid.
/// </summary>
public static class ProjectZomboidMapRasterizer
{
    public sealed class Result
    {
        public Texture2D Texture;
        public Grid Grid;
        public Vector2Int MinCell;
        public Vector2Int Size;

        public Vector2 WorldToNormalized(Vector3 worldPosition)
        {
            Vector3Int cell = Grid.WorldToCell(worldPosition);
            return new Vector2(
                Mathf.InverseLerp(MinCell.x, MinCell.x + Size.x - 1, cell.x),
                Mathf.InverseLerp(MinCell.y, MinCell.y + Size.y - 1, cell.y));
        }

        public Vector3 NormalizedToWorld(Vector2 normalizedPosition)
        {
            Vector3 cellPosition = new Vector3(
                Mathf.Lerp(MinCell.x, MinCell.x + Size.x - 1, normalizedPosition.x),
                Mathf.Lerp(MinCell.y, MinCell.y + Size.y - 1, normalizedPosition.y),
                0f);
            Vector3 localPosition = Grid.CellToLocalInterpolated(cellPosition);
            return Grid.transform.TransformPoint(localPosition);
        }
    }

    private static readonly Color32 Outside = new Color32(61, 105, 48, 255);
    private static readonly Color32 Forest = new Color32(91, 128, 72, 255);
    private static readonly Color32 Grass = new Color32(116, 151, 79, 255);
    private static readonly Color32 Land = new Color32(218, 211, 171, 255);
    private static readonly Color32 UpperLand = new Color32(202, 195, 157, 255);
    private static readonly Color32 Asphalt = new Color32(105, 108, 104, 255);
    private static readonly Color32 RoadEdge = new Color32(78, 82, 79, 255);
    private static readonly Color32 Pavement = new Color32(184, 181, 164, 255);
    private static readonly Color32 DevelopedLot = new Color32(198, 192, 171, 255);
    private static readonly Color32 DirtRoad = new Color32(151, 126, 88, 255);
    private static readonly Color32 Residential = new Color32(205, 145, 72, 255);
    private static readonly Color32 Commercial = new Color32(224, 190, 54, 255);
    private static readonly Color32 Medical = new Color32(116, 105, 178, 255);
    private static readonly Color32 Community = new Color32(92, 155, 84, 255);
    private static readonly Color32 Emergency = new Color32(185, 75, 61, 255);

    public static Result Build(GameObject mapRoot)
    {
        if (mapRoot == null)
            return null;

        Grid rootGrid = mapRoot.GetComponent<Grid>();
        if (rootGrid == null)
            rootGrid = mapRoot.GetComponentInChildren<Grid>(true);
        if (rootGrid == null)
            return null;

        // Some authored building prefabs (for example the orange apartment
        // block) are scene-root siblings instead of children of `Map`. Include
        // every real Tilemap in the same scene so the cartographic result does
        // not silently omit those locations.
        Tilemap[] sceneMaps = UnityEngine.Object.FindObjectsByType<Tilemap>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        var mapList = new List<Tilemap>();
        for (int i = 0; i < sceneMaps.Length; i++)
        {
            Tilemap tilemap = sceneMaps[i];
            if (tilemap.gameObject.scene != mapRoot.scene)
                continue;
            string rootName = tilemap.transform.root.name;
            if (rootName.StartsWith("__ColliderProxy", StringComparison.OrdinalIgnoreCase) ||
                tilemap.name.StartsWith("__ColliderProxy", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(tilemap.name, "Test", StringComparison.OrdinalIgnoreCase))
                continue;
            mapList.Add(tilemap);
        }
        Tilemap[] allMaps = mapList.ToArray();
        Vector2Int min = new Vector2Int(int.MaxValue, int.MaxValue);
        Vector2Int max = new Vector2Int(int.MinValue, int.MinValue);
        // Use the complete playable-map extent. Forest/tree layers are part of
        // the extent even though their sprites are simplified below; otherwise
        // the map would stop at the last building and hide large parts of Main.
        for (int i = 0; i < allMaps.Length; i++)
        {
            if (!IsMapExtentLayer(allMaps[i].name))
                continue;
            VisitOccupiedCells(allMaps[i], rootGrid, (cell, tile) =>
            {
                min = Vector2Int.Min(min, new Vector2Int(cell.x, cell.y));
                max = Vector2Int.Max(max, new Vector2Int(cell.x, cell.y));
            });
        }

        // Fallback for a custom scene without the standard Main layer names.
        if (min.x == int.MaxValue)
        {
            for (int i = 0; i < allMaps.Length; i++)
            {
                if (!IsBuildingFootprintLayer(allMaps[i].name))
                    continue;
                VisitOccupiedCells(allMaps[i], rootGrid, (cell, tile) =>
                {
                    min = Vector2Int.Min(min, new Vector2Int(cell.x, cell.y));
                    max = Vector2Int.Max(max, new Vector2Int(cell.x, cell.y));
                });
            }
        }

        if (min.x == int.MaxValue)
            return null;

        const int padding = 8;
        min -= Vector2Int.one * padding;
        max += Vector2Int.one * padding;
        int width = max.x - min.x + 1;
        int height = max.y - min.y + 1;
        if (width <= 0 || height <= 0 || width > 2048 || height > 2048)
            return null;

        var pixels = new Color32[width * height];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Outside;

        var roadMask = new bool[pixels.Length];

        // Forest is deliberately only a flat cartographic mass. Roads and
        // developed ground are painted over it, matching the readable style of
        // Project Zomboid's town maps without copying gameplay-camera pixels.
        PaintNamedLayer(allMaps, rootGrid, min, width, height, pixels, "cayvahangrao", Forest);
        PaintNamedLayer(allMaps, rootGrid, min, width, height, pixels, "lacayrac", Forest);

        // Respect the real TilemapRenderer order. More importantly, classify
        // every placed tile instead of assigning one color to a whole layer:
        // asphalt, paving, dirt and grass can all coexist inside `matdat`.
        PaintGroundLayer(allMaps, rootGrid, min, width, height, pixels, roadMask, "matdat");
        PaintGroundLayer(allMaps, rootGrid, min, width, height, pixels, roadMask, "matdattren");
        PaintGroundLayer(allMaps, rootGrid, min, width, height, pixels, roadMask, "duongdat");
        PaintNamedLayer(allMaps, rootGrid, min, width, height, pixels, "thamco", Grass);
        PaintNamedLayer(allMaps, rootGrid, min, width, height, pixels, "cobam", Grass);
        OutlineMask(roadMask, pixels, width, height, RoadEdge);

        var buildingMask = new bool[pixels.Length];
        var buildingColors = new Color32[pixels.Length];
        for (int i = 0; i < allMaps.Length; i++)
        {
            Tilemap tilemap = allMaps[i];
            if (!IsBuildingFootprintLayer(tilemap.name))
                continue;

            Color32 color = GetBuildingColor(tilemap.transform);
            VisitOccupiedCells(tilemap, rootGrid, (cell, tile) =>
            {
                int index = ToIndex(cell, min, width, height);
                if (index < 0) return;
                buildingMask[index] = true;
                buildingColors[index] = color;
            });
        }

        // Draw a dark one-cell outline while preserving the exact footprint.
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                if (!buildingMask[index]) continue;
                bool boundary = x == 0 || y == 0 || x == width - 1 || y == height - 1 ||
                    !buildingMask[index - 1] || !buildingMask[index + 1] ||
                    !buildingMask[index - width] || !buildingMask[index + width];
                Color32 fill = buildingColors[index];
                pixels[index] = boundary
                    ? new Color32((byte)(fill.r * 0.68f), (byte)(fill.g * 0.68f), (byte)(fill.b * 0.68f), 255)
                    : fill;
            }
        }

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "ProjectZomboid_Main_Map",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixels32(pixels);
        texture.Apply(false, false);

        return new Result
        {
            Texture = texture,
            Grid = rootGrid,
            MinCell = min,
            Size = new Vector2Int(width, height)
        };
    }

    private static void PaintNamedLayer(Tilemap[] maps, Grid grid, Vector2Int min, int width, int height,
        Color32[] pixels, string exactName, Color32 color)
    {
        for (int i = 0; i < maps.Length; i++)
        {
            if (!string.Equals(maps[i].name, exactName, StringComparison.OrdinalIgnoreCase))
                continue;
            VisitOccupiedCells(maps[i], grid, (cell, tile) =>
            {
                int index = ToIndex(cell, min, width, height);
                if (index >= 0) pixels[index] = color;
            });
        }
    }

    private static void PaintGroundLayer(Tilemap[] maps, Grid grid, Vector2Int min, int width, int height,
        Color32[] pixels, bool[] roadMask, string exactName)
    {
        for (int i = 0; i < maps.Length; i++)
        {
            if (!string.Equals(maps[i].name, exactName, StringComparison.OrdinalIgnoreCase))
                continue;

            VisitOccupiedCells(maps[i], grid, (cell, tile) =>
            {
                int index = ToIndex(cell, min, width, height);
                if (index < 0) return;
                Color32 color = GetGroundColor(tile, exactName);
                pixels[index] = color;
                roadMask[index] = SameRgb(color, Asphalt);
            });
        }
    }

    private static Color32 GetGroundColor(TileBase tile, string layerName)
    {
        string tileName = tile == null ? string.Empty : tile.name.ToLowerInvariant();
        Tile concrete = tile as Tile;
        string textureName = concrete != null && concrete.sprite != null && concrete.sprite.texture != null
            ? concrete.sprite.texture.name.ToLowerInvariant()
            : string.Empty;

        // ZombieCity's B1_*_0 set is the dark asphalt road set; A1_*_0 is
        // its paved sidewalk set. These names are stable in the imported pack.
        if (tileName.StartsWith("ground b1_") && tileName.EndsWith("_0"))
            return Asphalt;
        if (tileName.StartsWith("ground a1_") && tileName.EndsWith("_0"))
            return Pavement;

        bool ruralAtlas = textureName.Contains("rural zombie atlas");
        if (ruralAtlas)
        {
            if (tileName.StartsWith("ground g"))
                return Grass;
            return string.Equals(layerName, "duongdat", StringComparison.OrdinalIgnoreCase)
                ? DirtRoad
                : Land;
        }

        if (string.Equals(layerName, "duongdat", StringComparison.OrdinalIgnoreCase))
            return DirtRoad;
        if (string.Equals(layerName, "matdattren", StringComparison.OrdinalIgnoreCase))
            return UpperLand;

        // Interior-pack ground tiles are used for concrete yards, parking
        // areas and developed lots in Main. Keeping them slightly darker than
        // the terrain makes the real street blocks visible on the flat map.
        return DevelopedLot;
    }

    private static void OutlineMask(bool[] mask, Color32[] pixels, int width, int height, Color32 edgeColor)
    {
        var edge = new bool[mask.Length];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                if (!mask[index]) continue;
                edge[index] = x == 0 || y == 0 || x == width - 1 || y == height - 1 ||
                    !mask[index - 1] || !mask[index + 1] || !mask[index - width] || !mask[index + width];
            }
        }

        for (int i = 0; i < edge.Length; i++)
            if (edge[i]) pixels[i] = edgeColor;
    }

    private static bool SameRgb(Color32 a, Color32 b)
    {
        return a.r == b.r && a.g == b.g && a.b == b.b;
    }

    private static bool IsBuildingFootprintLayer(string layerName)
    {
        string value = layerName.ToLowerInvariant().Replace(" ", string.Empty);
        return value.Contains("nennha") || value.Contains("nenlau") || value.Contains("nocnha") ||
               value.Contains("maingo") || value.Contains("noctruong");
    }

    private static bool IsMapExtentLayer(string layerName)
    {
        string value = layerName.ToLowerInvariant().Replace(" ", string.Empty);
        return value == "matdat" || value == "matdattren" || value == "duongdat" ||
               value == "thamco" || value == "cobam" || value == "lacayrac" ||
               value == "cayvahangrao" || IsBuildingFootprintLayer(layerName);
    }

    private static Color32 GetBuildingColor(Transform transform)
    {
        string path = string.Empty;
        Transform current = transform;
        while (current != null)
        {
            path += "/" + current.name.ToLowerInvariant();
            current = current.parent;
        }

        if (path.Contains("benhvien") || path.Contains("medical")) return Medical;
        if (path.Contains("truonghoc") || path.Contains("community")) return Community;
        if (path.Contains("cuuhoa") || path.Contains("police")) return Emergency;
        if (path.Contains("sieuthi") || path.Contains("cuahang") || path.Contains("quanan") ||
            path.Contains("quannuoc")) return Commercial;
        return Residential;
    }

    private static int ToIndex(Vector3Int cell, Vector2Int min, int width, int height)
    {
        int x = cell.x - min.x;
        int y = cell.y - min.y;
        return x < 0 || y < 0 || x >= width || y >= height ? -1 : y * width + x;
    }

    private static void VisitOccupiedCells(Tilemap tilemap, Grid rootGrid, Action<Vector3Int, TileBase> visitor)
    {
        BoundsInt bounds = tilemap.cellBounds;
        TileBase[] tiles = tilemap.GetTilesBlock(bounds);
        int index = 0;
        for (int z = bounds.zMin; z < bounds.zMax; z++)
        {
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                for (int x = bounds.xMin; x < bounds.xMax; x++, index++)
                {
                    if (tiles[index] == null) continue;
                    Vector3 world = tilemap.GetCellCenterWorld(new Vector3Int(x, y, z));
                    visitor(rootGrid.WorldToCell(world), tiles[index]);
                }
            }
        }
    }
}
