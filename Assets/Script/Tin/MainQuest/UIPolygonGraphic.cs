using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lightweight UI polygon whose points are normalized inside its RectTransform.
/// Useful for highlights that must follow irregular illustrated parts.
/// </summary>
[DisallowMultipleComponent]
public sealed class UIPolygonGraphic : MaskableGraphic
{
    [SerializeField] private Vector2[] normalizedPoints =
    {
        new Vector2(0.25f, 0.75f),
        new Vector2(0.75f, 0.75f),
        new Vector2(0.75f, 0.25f),
        new Vector2(0.25f, 0.25f)
    };

    public void SetNormalizedPoints(params Vector2[] points)
    {
        normalizedPoints = points;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
        if (normalizedPoints == null || normalizedPoints.Length < 3) return;

        Rect rect = rectTransform.rect;
        for (int i = 0; i < normalizedPoints.Length; i++)
        {
            Vector2 point = normalizedPoints[i];
            Vector3 position = new Vector3(
                Mathf.Lerp(rect.xMin, rect.xMax, point.x),
                Mathf.Lerp(rect.yMin, rect.yMax, point.y));
            vertexHelper.AddVert(position, color, Vector2.zero);
        }

        for (int i = 1; i < normalizedPoints.Length - 1; i++)
            vertexHelper.AddTriangle(0, i, i + 1);
    }
}
