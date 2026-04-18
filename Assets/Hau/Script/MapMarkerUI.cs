using UnityEngine;
using Fusion;

public class MinimapFollow : MonoBehaviour
{
    private Transform localPlayer;

    void Update()
    {
        // luôn cố tìm nếu chưa có
        if (localPlayer == null)
        {
            foreach (var obj in FindObjectsOfType<NetworkObject>())
            {
                if (obj.HasInputAuthority)
                {
                    localPlayer = obj.transform;
                    Debug.Log("Found Local Player: " + obj.name);
                    break;
                }
            }
        }

        if (localPlayer == null) return;

        // 👇 follow player (2D đúng trục)
        Vector3 pos = localPlayer.position;
        transform.position = new Vector3(pos.x, pos.y, -10f);
    }
}