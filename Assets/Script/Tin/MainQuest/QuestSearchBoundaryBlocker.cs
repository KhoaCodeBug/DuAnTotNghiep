using UnityEngine;

/// <summary>Explains the temporary solid edge of the opening quest district.</summary>
[DisallowMultipleComponent]
public sealed class QuestSearchBoundaryBlocker : MonoBehaviour
{
    private float nextWarningTime;

    private void OnCollisionEnter2D(Collision2D collision) => WarnIfPlayer(collision.collider);
    private void OnCollisionStay2D(Collision2D collision) => WarnIfPlayer(collision.collider);

    private void WarnIfPlayer(Collider2D other)
    {
        if (Time.unscaledTime < nextWarningTime || other.GetComponentInParent<PlayerMovement>() == null) return;
        nextWarningTime = Time.unscaledTime + 2.5f;
        AutoChatManager.Instance?.AddMessage("GIỚI HẠN KHU VỰC",
            "Phía ngoài chưa an toàn. Hãy tìm manh mối trong khu được đánh dấu trên bản đồ.");
    }
}
