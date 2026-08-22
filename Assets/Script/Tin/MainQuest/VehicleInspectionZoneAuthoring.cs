using UnityEngine;

/// <summary>Keeps runtime vehicle interaction polygons visible and editable in Main's EditMode.</summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(PolygonCollider2D))]
public sealed class VehicleInspectionZoneAuthoring : MonoBehaviour
{
    [SerializeField] private Color sceneColor = new Color(0.2f, 1f, 0.35f, 0.85f);
    [SerializeField] private string sceneLabel = "VÙNG KIỂM TRA XE";

    private PolygonCollider2D polygon;

    private void OnEnable() => ConfigureCollider();
    private void OnValidate() => ConfigureCollider();

    private void ConfigureCollider()
    {
        if (polygon == null) polygon = GetComponent<PolygonCollider2D>();
        if (polygon == null) return;
        polygon.isTrigger = true;
        polygon.enabled = true;
    }

    private void OnDrawGizmos()
    {
        ConfigureCollider();
        if (polygon == null || polygon.pathCount == 0) return;
        Vector2[] points = polygon.GetPath(0);
        if (points.Length < 2) return;

        Gizmos.color = sceneColor;
        for (int i = 0; i < points.Length; i++)
        {
            Vector3 from = polygon.transform.TransformPoint(points[i] + polygon.offset);
            Vector3 to = polygon.transform.TransformPoint(points[(i + 1) % points.Length] + polygon.offset);
            Gizmos.DrawLine(from, to);
            Gizmos.DrawSphere(from, 0.075f);
        }

#if UNITY_EDITOR
        UnityEditor.Handles.color = sceneColor;
        UnityEditor.Handles.Label(polygon.bounds.center, sceneLabel);
#endif
    }
}
