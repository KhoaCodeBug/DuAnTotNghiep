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

    [Header("Hospital - unlocked after the first three clues")]
    [SerializeField] private Vector2 afterQuestCenter = new Vector2(0.505f, 0.485f);
    [SerializeField] private Vector2 afterQuestSize = new Vector2(0.284f, 0.138f);

    [Header("Military - unlocked after the Hospital Radio")]
    [SerializeField] private Vector2 militaryCenter = new Vector2(0.72f, 0.72f);
    [SerializeField] private Vector2 militarySize = new Vector2(0.16f, 0.16f);
    [SerializeField] private Vector2 militaryMarkerPosition = new Vector2(0.72f, 0.72f);

    [Header("Countryside route - unlocked by ManhMoi3")]
    [SerializeField] private Vector2 countrysideCenter = new Vector2(0.84f, 0.48f);
    [SerializeField] private Vector2 countrysideSize = new Vector2(0.18f, 0.22f);

    private bool militaryPositionInitialized;

    public Rect BeforeQuestRect => RectFromCenterAndSize(beforeQuestCenter, beforeQuestSize);
    public Rect AfterQuestRect => RectFromCenterAndSize(afterQuestCenter, afterQuestSize);
    public Rect MilitaryRect => RectFromCenterAndSize(militaryCenter, militarySize);
    public Rect CountrysideRect => RectFromCenterAndSize(countrysideCenter, countrysideSize);
    public Vector2 MilitaryMarkerPosition => militaryMarkerPosition;

    public void InitializeRuntimeMilitaryPosition(Vector2 normalizedPosition)
    {
        if (militaryPositionInitialized) return;
        militaryPositionInitialized = true;
        militaryMarkerPosition = ClampPoint(normalizedPosition);
        militaryCenter = militaryMarkerPosition;
    }

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
                hash = hash * 397 ^ militaryCenter.GetHashCode();
                hash = hash * 397 ^ militarySize.GetHashCode();
                hash = hash * 397 ^ militaryMarkerPosition.GetHashCode();
                hash = hash * 397 ^ countrysideCenter.GetHashCode();
                hash = hash * 397 ^ countrysideSize.GetHashCode();
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
        militaryCenter = militaryMarkerPosition;
        militarySize = new Vector2(0.16f, 0.16f);
        countrysideCenter = new Vector2(0.84f, 0.48f);
        countrysideSize = new Vector2(0.18f, 0.22f);
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
        militarySize = ClampSize(militarySize);
        countrysideSize = ClampSize(countrysideSize);
        beforeQuestCenter = ClampCenter(beforeQuestCenter, beforeQuestSize);
        afterQuestCenter = ClampCenter(afterQuestCenter, afterQuestSize);
        militaryCenter = ClampCenter(militaryCenter, militarySize);
        countrysideCenter = ClampCenter(countrysideCenter, countrysideSize);
        militaryMarkerPosition = ClampPoint(militaryMarkerPosition);
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

    private static Vector2 ClampPoint(Vector2 point) => new Vector2(
        Mathf.Clamp01(point.x), Mathf.Clamp01(point.y));

    private static Rect RectFromCenterAndSize(Vector2 center, Vector2 size)
    {
        return new Rect(center - size * 0.5f, size);
    }
}
