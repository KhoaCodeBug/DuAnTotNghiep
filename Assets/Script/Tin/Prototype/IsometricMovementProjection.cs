using UnityEngine;

/// <summary>
/// Converts screen-aligned movement input to the 2:1 isometric world basis
/// used by Main's Grid. Cardinal input stays cardinal; diagonal input follows
/// the visible road axes instead of the Euclidean 45-degree diagonals.
/// </summary>
public static class IsometricMovementProjection
{
    public const float DefaultVerticalScale = 0.5f;

    public static Vector2 ProjectInput(
        Vector2 input,
        float verticalScale = DefaultVerticalScale)
    {
        float magnitude = Mathf.Clamp01(input.magnitude);
        if (magnitude <= Mathf.Epsilon) return Vector2.zero;

        float safeVerticalScale = Mathf.Max(0.01f, verticalScale);
        Vector2 projected = new(input.x, input.y * safeVerticalScale);
        if (projected.sqrMagnitude <= Mathf.Epsilon) return Vector2.zero;

        return projected.normalized * magnitude;
    }

    public static Vector2 ProjectDirection(
        Vector2 direction,
        float verticalScale = DefaultVerticalScale)
    {
        if (direction.sqrMagnitude <= Mathf.Epsilon) return Vector2.zero;
        return ProjectInput(direction.normalized, verticalScale).normalized;
    }
}
