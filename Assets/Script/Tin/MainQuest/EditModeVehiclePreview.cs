using UnityEngine;

/// <summary>Shows an authoring-only vehicle sprite in EditMode and hides it during gameplay.</summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class EditModeVehiclePreview : MonoBehaviour
{
    private SpriteRenderer previewRenderer;

    private void OnEnable() => RefreshVisibility();
    private void Update() => RefreshVisibility();

    private void RefreshVisibility()
    {
        if (previewRenderer == null) previewRenderer = GetComponent<SpriteRenderer>();
        if (previewRenderer != null) previewRenderer.enabled = !Application.isPlaying;
    }
}
