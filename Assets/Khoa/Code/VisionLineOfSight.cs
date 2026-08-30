using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared 2D visibility rules for gameplay LOS and the local FOW renderer.
/// The visual fan and PlayerVision must agree on which colliders are opaque.
/// </summary>
public static class VisionLineOfSight
{
    public static bool IsPassThrough(Collider2D collider)
    {
        return collider != null &&
               collider.GetComponentInParent<MilitaryGateVisionPassThrough>() != null;
    }

    public static bool IsBlocking(Collider2D collider, Transform scope = null)
    {
        if (collider == null || IsPassThrough(collider))
            return false;

        if (scope == null)
            return true;

        Transform colliderTransform = collider.transform;
        return colliderTransform == scope || colliderTransform.IsChildOf(scope);
    }

    public static bool IsBlocked(Vector2 origin, Vector2 direction, float distance,
        ContactFilter2D filter, RaycastHit2D[] hits)
    {
        if (distance <= 0.001f || hits == null || hits.Length == 0)
            return false;

        int hitCount = Physics2D.Raycast(origin, direction, filter, hits, distance);
        for (int i = 0; i < hitCount; i++)
        {
            if (IsBlocking(hits[i].collider))
                return true;
        }

        return false;
    }

    public static float FindNearestBlockingDistance(Vector2 origin, Vector2 direction,
        float maxDistance, ContactFilter2D filter, List<RaycastHit2D> hits,
        Transform scope = null)
    {
        float safeMaxDistance = Mathf.Max(0.001f, maxDistance);
        if (hits == null)
            return safeMaxDistance;

        Physics2D.Raycast(origin, direction, filter, hits, safeMaxDistance);
        float nearest = safeMaxDistance;
        for (int i = 0; i < hits.Count; i++)
        {
            Collider2D collider = hits[i].collider;
            if (!IsBlocking(collider, scope))
                continue;

            nearest = Mathf.Min(nearest, Mathf.Max(0f, hits[i].distance));
        }

        return nearest;
    }
}
