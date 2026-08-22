using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Police-repair variants use separate IDs and blue-backed icons, never Route-A items.</summary>
public static class PoliceCarItemCatalog
{
    private static readonly Dictionary<ArrivalCarItemKind, ItemData> Items = new();

    public static ItemData GetOrCreate(ArrivalCarItemKind kind)
    {
        if (Items.TryGetValue(kind, out ItemData existing) && existing != null) return existing;
        ItemData item = ScriptableObject.CreateInstance<ItemData>();
        item.name = GetId(kind);
        item.itemName = GetDisplayName(kind);
        item.category = ItemCategory.QuestItem;
        item.isStackable = false;
        item.maxStack = 1;
        item.icon = CreateBlueVariantIcon(kind);
        item.hideFlags = HideFlags.DontSave;
        Items[kind] = item;
        return item;
    }

    public static bool TryLoad(string identifier, out ItemData item)
    {
        item = null;
        if (!TryGetKind(identifier, out ArrivalCarItemKind kind)) return false;
        item = GetOrCreate(kind);
        return true;
    }

    public static bool TryGetKind(ItemData item, out ArrivalCarItemKind kind)
    {
        if (item != null && (TryGetKind(item.name, out kind) || TryGetKind(item.itemName, out kind))) return true;
        kind = default;
        return false;
    }

    public static bool TryGetKind(string identifier, out ArrivalCarItemKind kind)
    {
        foreach (ArrivalCarItemKind candidate in Enum.GetValues(typeof(ArrivalCarItemKind)))
        {
            if (string.Equals(identifier, GetId(candidate), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(identifier, GetDisplayName(candidate), StringComparison.OrdinalIgnoreCase))
            {
                kind = candidate;
                return true;
            }
        }
        kind = default;
        return false;
    }

    public static string GetId(ArrivalCarItemKind kind) => "PoliceCar" + kind;

    public static string GetDisplayName(ArrivalCarItemKind kind) => kind switch
    {
        ArrivalCarItemKind.Toolbox => "Bộ dụng cụ xe tuần tra",
        ArrivalCarItemKind.Hammer => "Búa cứu hộ cảnh sát",
        ArrivalCarItemKind.FuelCan => "Can nhiên liệu dự phòng cảnh sát",
        ArrivalCarItemKind.Battery => "Ắc quy xe tuần tra",
        _ => "Lốp xe tuần tra"
    };

    private static Sprite CreateBlueVariantIcon(ArrivalCarItemKind kind)
    {
        string path = kind switch
        {
            ArrivalCarItemKind.Toolbox => "Story/CarUI/Toolbox",
            ArrivalCarItemKind.Hammer => "Story/CarUI/Hammer",
            ArrivalCarItemKind.FuelCan => "Story/CarUI/GasCan",
            ArrivalCarItemKind.Battery => "Story/CarUI/CarBattery",
            _ => "Story/CarUI/CarTire"
        };
        Texture2D source = Resources.Load<Texture2D>(path);
        if (source == null) return null;

        int width = source.width;
        int height = source.height;
        RenderTexture target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.sRGB);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        GL.Clear(true, true, new Color(0.035f, 0.22f, 0.48f, 1f));
        GL.PushMatrix();
        GL.LoadPixelMatrix(0f, width, height, 0f);
        Graphics.DrawTexture(new Rect(0f, 0f, width, height), source);
        GL.PopMatrix();

        Texture2D composite = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = GetId(kind) + "_BLUE_TEXTURE",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };
        composite.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
        composite.Apply(false, true);
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(target);

        Sprite sprite = Sprite.Create(composite, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f),
            Mathf.Max(width, height));
        sprite.name = GetId(kind) + "_BLUE_ICON";
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }
}
