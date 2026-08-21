using UnityEngine;

/// <summary>
/// Canonical clockwise direction order shared by the sedan controller and its
/// sprite array: N, NE, E, SE, S, SW, W, NW.
/// </summary>
public static class EightWayDirection
{
    public const int Count = 8;
    public const float DegreesPerDirection = 360f / Count;

    private static readonly string[] Labels =
    {
        "N", "NE", "E", "SE", "S", "SW", "W", "NW"
    };

    public static int NormalizeIndex(int index) =>
        ((index % Count) + Count) % Count;

    public static int HeadingDegreesToIndex(float headingDegrees) =>
        NormalizeIndex(Mathf.RoundToInt(Mathf.Repeat(headingDegrees, 360f) / DegreesPerDirection));

    public static float IndexToHeadingDegrees(int index) =>
        NormalizeIndex(index) * DegreesPerDirection;

    public static Vector2 IndexToLogicalDirection(int index)
    {
        float radians = IndexToHeadingDegrees(index) * Mathf.Deg2Rad;
        return new Vector2(Mathf.Sin(radians), Mathf.Cos(radians));
    }

    public static Vector2 IndexToIsometricDirection(
        int index,
        float verticalScale = IsometricMovementProjection.DefaultVerticalScale) =>
        IsometricMovementProjection.ProjectDirection(IndexToLogicalDirection(index), verticalScale);

    public static string IndexToLabel(int index) => Labels[NormalizeIndex(index)];
}
