using UnityEngine;

public enum QuestRouteClueKind
{
    DeliveryInvoice,
    TransitDiagram,
    AddressNote
}

/// <summary>
/// Marks one real LootContainer as a one-time optional route clue source.
/// The normal house loot remains unchanged; opening this container also records
/// the clue in the pre-military quest journal.
/// </summary>
[DisallowMultipleComponent]
public sealed class QuestRouteClueSource : MonoBehaviour
{
    [SerializeField] private QuestRouteClueKind clueKind;
    [SerializeField] private string clueId;
    [SerializeField] private string displayName;

    private bool collected;

    public QuestRouteClueKind ClueKind => clueKind;
    public string ClueId => clueId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? GetDefaultDisplayName(clueKind) : displayName;
    public bool IsCollected => collected;

    public void Configure(QuestRouteClueKind kind)
    {
        clueKind = kind;
        clueId = QuestRouteClueItemCatalog.GetClueId(kind);
        displayName = QuestRouteClueItemCatalog.GetDisplayName(kind);
        collected = false;
    }

    public bool TryCollect(out string collectedClueId)
    {
        collectedClueId = clueId;
        if (collected || string.IsNullOrWhiteSpace(clueId))
            return false;

        collected = true;
        return true;
    }

    private static string GetDefaultDisplayName(QuestRouteClueKind kind)
    {
        switch (kind)
        {
            case QuestRouteClueKind.DeliveryInvoice: return "Hóa đơn giao hàng";
            case QuestRouteClueKind.TransitDiagram: return "Sơ đồ tuyến xe";
            default: return "Ghi chú địa chỉ";
        }
    }

#if UNITY_EDITOR
    public void EditorConfigure(QuestRouteClueKind kind, string id, string label)
    {
        clueKind = kind;
        clueId = id;
        displayName = label;
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
