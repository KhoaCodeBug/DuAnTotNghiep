using UnityEngine;

public class PZ_VisibleObject : MonoBehaviour
{
    private MeshRenderer[] renderers;
    private bool isCurrentlyVisible = true;

    void Awake()
    {
        // Lấy tất cả renderer (phòng trường hợp Zombie có nhiều bộ phận)
        renderers = GetComponentsInChildren<MeshRenderer>();
    }

    private void Start()
    {
        // Đảm bảo rằng tất cả renderer đều được bật khi bắt đầu
        SetVisibility(false);
    }

    public void SetVisibility(bool visible)
    {
        if (isCurrentlyVisible == visible) return;

        isCurrentlyVisible = visible;
        foreach (var r in renderers)
        {
            r.enabled = visible;
        }
    }
}