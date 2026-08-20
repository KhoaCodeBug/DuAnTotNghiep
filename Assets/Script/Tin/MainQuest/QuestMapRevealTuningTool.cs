using UnityEngine;

/// <summary>
/// Play-mode tuning data for the two independent quest-map openings. The
/// runtime bridge creates this object automatically so its values can be tuned
/// while the map is visible, without recompiling scripts between adjustments.
/// </summary>
[DisallowMultipleComponent]
public sealed class QuestMapRevealTuningTool : MonoBehaviour
{
    [Header("Before Quest - opening neighborhood")]
    [SerializeField] private Vector2 beforeQuestCenter = new Vector2(0.503f, 0.339f);
    [SerializeField] private Vector2 beforeQuestSize = new Vector2(0.288f, 0.176f);

    [Header("After Quest - office opening added")]
    [SerializeField] private Vector2 afterQuestCenter = new Vector2(0.505f, 0.485f);
    [SerializeField] private Vector2 afterQuestSize = new Vector2(0.284f, 0.138f);

    public Rect BeforeQuestRect => RectFromCenterAndSize(beforeQuestCenter, beforeQuestSize);
    public Rect AfterQuestRect => RectFromCenterAndSize(afterQuestCenter, afterQuestSize);

    public int LayoutSignature
    {
        get
        {
            unchecked
            {
                int hash = beforeQuestCenter.GetHashCode();
                hash = hash * 397 ^ beforeQuestSize.GetHashCode();
                hash = hash * 397 ^ afterQuestCenter.GetHashCode();
                hash = hash * 397 ^ afterQuestSize.GetHashCode();
                return hash;
            }
        }
    }

    public void ResetToDefaults()
    {
        beforeQuestCenter = new Vector2(0.503f, 0.339f);
        beforeQuestSize = new Vector2(0.288f, 0.176f);
        afterQuestCenter = new Vector2(0.505f, 0.485f);
        afterQuestSize = new Vector2(0.284f, 0.138f);
        ClampValues();
    }

    private void OnValidate()
    {
        ClampValues();
    }

    private void ClampValues()
    {
        beforeQuestSize = ClampSize(beforeQuestSize);
        afterQuestSize = ClampSize(afterQuestSize);
        beforeQuestCenter = ClampCenter(beforeQuestCenter, beforeQuestSize);
        afterQuestCenter = ClampCenter(afterQuestCenter, afterQuestSize);
    }

    private static Vector2 ClampSize(Vector2 size)
    {
        return new Vector2(Mathf.Clamp(size.x, 0.02f, 1f), Mathf.Clamp(size.y, 0.02f, 1f));
    }

    private static Vector2 ClampCenter(Vector2 center, Vector2 size)
    {
        Vector2 half = size * 0.5f;
        return new Vector2(
            Mathf.Clamp(center.x, half.x, 1f - half.x),
            Mathf.Clamp(center.y, half.y, 1f - half.y));
    }

    private static Rect RectFromCenterAndSize(Vector2 center, Vector2 size)
    {
        return new Rect(center - size * 0.5f, size);
    }
}
