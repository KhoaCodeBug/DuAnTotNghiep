using System.Collections.Generic;
using UnityEngine;

/// <summary>Runtime definitions for the three visible route-clue loot items.</summary>
public static class QuestRouteClueItemCatalog
{
    private static readonly Dictionary<QuestRouteClueKind, ItemData> Items = new();

    public static ItemData GetOrCreate(QuestRouteClueKind kind)
    {
        if (Items.TryGetValue(kind, out ItemData existing) && existing != null)
            return existing;

        ItemData item = ScriptableObject.CreateInstance<ItemData>();
        item.name = GetClueId(kind);
        item.itemName = GetDisplayName(kind);
        item.category = ItemCategory.QuestItem;
        item.isStackable = false;
        item.maxStack = 1;
        item.icon = CreateDocumentIcon(kind);
        item.hideFlags = HideFlags.DontSave;
        Items[kind] = item;
        return item;
    }

    public static bool TryLoad(string identifier, out ItemData item)
    {
        item = null;
        if (!TryGetKind(identifier, out QuestRouteClueKind kind)) return false;
        item = GetOrCreate(kind);
        return true;
    }

    public static bool TryGetKind(ItemData item, out QuestRouteClueKind kind)
    {
        kind = default;
        return item != null && (TryGetKind(item.name, out kind) || TryGetKind(item.itemName, out kind));
    }

    public static bool TryGetKind(string identifier, out QuestRouteClueKind kind)
    {
        for (int i = 0; i < 3; i++)
        {
            QuestRouteClueKind candidate = (QuestRouteClueKind)i;
            if (string.Equals(identifier, GetClueId(candidate), System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(identifier, GetDisplayName(candidate), System.StringComparison.OrdinalIgnoreCase))
            {
                kind = candidate;
                return true;
            }
        }
        kind = default;
        return false;
    }

    public static string GetClueId(QuestRouteClueKind kind) => kind switch
    {
        QuestRouteClueKind.DeliveryInvoice => "ROUTE_CLUE_DELIVERY_INVOICE",
        QuestRouteClueKind.TransitDiagram => "ROUTE_CLUE_TRANSIT_DIAGRAM",
        _ => "ROUTE_CLUE_ADDRESS_NOTE"
    };

    public static string GetDisplayName(QuestRouteClueKind kind) => kind switch
    {
        QuestRouteClueKind.DeliveryInvoice => "Hóa đơn giao hàng",
        QuestRouteClueKind.TransitDiagram => "Sơ đồ tuyến xe",
        _ => "Ghi chú địa chỉ"
    };

    public static string GetReadingText(QuestRouteClueKind kind) => kind switch
    {
        QuestRouteClueKind.DeliveryInvoice =>
            "Phiếu giao hàng đã ố vàng. Nơi nhận ghi: “Văn phòng dịch vụ đô thị”. " +
            "Người giao khoanh một đoạn đường phía đông khu dân cư và ghi thêm: “cổng màu tím, giao trước 17 giờ”.",
        QuestRouteClueKind.TransitDiagram =>
            "Một sơ đồ tuyến xe buýt cũ, tuyến số 04 bị gạch đỏ ở chặng cuối. " +
            "Điểm dừng cuối nằm cạnh một bãi xe bỏ hoang, trên trục đường chạy chéo qua khu nhà.",
        _ =>
            "Mảnh giấy ghi vội: “Qua ngã ba có biển cong, đi theo hàng rào sắt. " +
            "Tòa nhà hai tầng có cửa tím nằm đối diện bãi xe; lối chính đã bị chặn”."
    };

    public static string GetInferenceText(QuestRouteClueKind kind) => kind switch
    {
        QuestRouteClueKind.DeliveryInvoice => "SUY LUẬN: Văn phòng nằm về phía đông và có cổng màu tím.",
        QuestRouteClueKind.TransitDiagram => "SUY LUẬN: Có thể lần theo tuyến 04 đến bãi xe bỏ hoang.",
        _ => "SUY LUẬN: Đã có mô tả nhận dạng chính xác của tòa nhà."
    };

    public static string GetShortLabel(ItemData item)
    {
        if (!TryGetKind(item, out QuestRouteClueKind kind)) return string.Empty;
        return kind switch
        {
            QuestRouteClueKind.DeliveryInvoice => "HÓA ĐƠN",
            QuestRouteClueKind.TransitDiagram => "TUYẾN XE",
            _ => "ĐỊA CHỈ"
        };
    }

    private static Sprite CreateDocumentIcon(QuestRouteClueKind kind)
    {
        const int size = 48;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = GetClueId(kind) + "_ICON",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };
        Color32 clear = new Color32(0, 0, 0, 0);
        Color32 paper = new Color32(226, 216, 172, 255);
        Color32 edge = new Color32(68, 82, 72, 255);
        Color32 accent = kind switch
        {
            QuestRouteClueKind.DeliveryInvoice => new Color32(244, 171, 43, 255),
            QuestRouteClueKind.TransitDiagram => new Color32(70, 219, 173, 255),
            _ => new Color32(183, 91, 244, 255)
        };
        Color32[] pixels = new Color32[size * size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;
        for (int y = 4; y < 44; y++)
        for (int x = 7; x < 41; x++)
            pixels[y * size + x] = x == 7 || x == 40 || y == 4 || y == 43 ? edge : paper;
        for (int y = 31; y < 36; y++)
        for (int x = 12; x < 36; x++) pixels[y * size + x] = accent;
        for (int line = 0; line < 3; line++)
        for (int y = 11 + line * 6; y < 13 + line * 6; y++)
        for (int x = 12; x < 34 - line * 3; x++) pixels[y * size + x] = edge;
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 48f);
        sprite.name = texture.name;
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }
}
