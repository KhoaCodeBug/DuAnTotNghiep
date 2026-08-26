using System.Collections.Generic;
using UnityEngine;

/// <summary>Authored Main.unity anchor for one Route B repair-loot container.</summary>
public sealed class MilitaryRepairLootMarker : MonoBehaviour
{
    [SerializeField, Min(1)] private int stableId = 1;

    public int StableId => stableId;

    public static void GetOrderedMarkers(List<MilitaryRepairLootMarker> result)
    {
        result.Clear();
        result.AddRange(FindObjectsByType<MilitaryRepairLootMarker>(
            FindObjectsInactive.Include, FindObjectsSortMode.None));
        result.RemoveAll(marker => marker == null || !marker.gameObject.scene.IsValid());
        result.Sort((left, right) => left.stableId.CompareTo(right.stableId));
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.95f, 0.12f, 0.12f, 0.9f);
        Gizmos.DrawWireCube(transform.position, new Vector3(0.9f, 1.15f, 0f));
    }
}
