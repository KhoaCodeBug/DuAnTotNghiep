using UnityEngine;

public class MapIconFollow : MonoBehaviour
{
    public Transform player;

    void Update()
    {
        transform.position = new Vector3(
            player.position.x,
            player.position.y,
            -1 // luôn nằm trước map
        );
    }
}