using UnityEngine;

public enum QuestLocationType
{
    ResidentialHouse,
    PurpleOffice
}

/// <summary>
/// Stable, human-readable identity for a quest location placed in a scene.
/// The ID belongs to the scene instance, not to the prefab asset, so two houses
/// created from the same prefab can still be counted as two different houses.
/// </summary>
[DisallowMultipleComponent]
public sealed class QuestLocationIdentity : MonoBehaviour
{
    [SerializeField] private QuestLocationType locationType = QuestLocationType.ResidentialHouse;
    [SerializeField] private string locationId;
    [SerializeField] private string displayName;

    public QuestLocationType LocationType => locationType;
    public string LocationId => locationId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? gameObject.name : displayName;
    public bool HasValidId => !string.IsNullOrWhiteSpace(locationId);

    /// <summary>
    /// Lets a LootContainer or an investigation point resolve the location that owns it.
    /// </summary>
    public static bool TryResolve(Component child, out QuestLocationIdentity identity)
    {
        identity = child == null ? null : child.GetComponentInParent<QuestLocationIdentity>(true);
        return identity != null && identity.HasValidId;
    }

#if UNITY_EDITOR
    public void EditorSetIdentity(QuestLocationType type, string id, string label)
    {
        locationType = type;
        locationId = id;
        displayName = label;
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
