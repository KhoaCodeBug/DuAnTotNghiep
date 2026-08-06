using UnityEngine;

/// <summary>
/// Marks a trigger collider as an indoor space for local fog-of-war rendering.
/// It intentionally has no network state and does not alter collision or AI.
/// </summary>
[DisallowMultipleComponent]
public sealed class IndoorVisionArea : MonoBehaviour
{
}
