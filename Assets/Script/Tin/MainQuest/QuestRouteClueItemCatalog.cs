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
        QuestRouteClueKind.DeliveryInvoice => "Phiếu điều chuyển vật tư",
        QuestRouteClueKind.TransitDiagram => "Thông báo đổi tuyến sơ tán",
        _ => "Ghi chú của nhân viên trực"
    };

    public static string GetReadingText(QuestRouteClueKind kind) => kind switch
    {
        QuestRouteClueKind.DeliveryInvoice =>
            "Phiếu điều chuyển khẩn: “Toàn bộ thuốc, nhiên liệu dự phòng và dụng cụ sửa chữa còn lại " +
            "được chuyển về Văn phòng Điều phối Khu Dân Cư trước 18:00. Không giao trực tiếp tại điểm sơ tán.” " +
            "Người giao khoanh một đoạn đường phía đông và ghi thêm: “cổng màu tím”.",
        QuestRouteClueKind.TransitDiagram =>
            "Thông báo vận hành: “Tuyến sơ tán dân sự đã bị hủy. Các chuyến xe còn hoạt động phải chuyển " +
            "qua trạm kiểm soát khu quân sự. Sơ đồ tuyến mới được lưu tại Văn phòng Điều phối.”",
        _ =>
            "Mảnh giấy viết vội: “Tôi đã khóa hồ sơ tuyến cuối trong tủ lưu trữ. Chìa khóa vẫn ở bàn điều phối; " +
            "bản ghi liên lạc còn trong radio. Nếu không còn ai quay lại, hãy mang bản đồ đến trạm quân sự.”"
    };

    public static string GetInferenceText(QuestRouteClueKind kind) => kind switch
    {
        QuestRouteClueKind.DeliveryInvoice => "SUY LUẬN: Nhiên liệu và dụng cụ sửa xe từng được tập kết tại văn phòng cổng tím.",
        QuestRouteClueKind.TransitDiagram => "SUY LUẬN: Muốn đến điểm sơ tán phải tìm sơ đồ tuyến nằm trong văn phòng.",
        _ => "SUY LUẬN: Trong văn phòng cần kiểm tra bàn điều phối, radio rồi tủ lưu trữ."
    };

    public static string GetShortLabel(ItemData item)
    {
        if (!TryGetKind(item, out QuestRouteClueKind kind)) return string.Empty;
        return kind switch
        {
            QuestRouteClueKind.DeliveryInvoice => "VẬT TƯ",
            QuestRouteClueKind.TransitDiagram => "SƠ TÁN",
            _ => "NHÂN VIÊN TRỰC"
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
