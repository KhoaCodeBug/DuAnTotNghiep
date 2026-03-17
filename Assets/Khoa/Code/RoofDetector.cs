using SmallScaleInc.ZombieRural;
using UnityEngine;

public class RoofDetector : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        RoofVisibility roof = other.GetComponentInParent<RoofVisibility>();

        if (roof != null)
        {
            roof.EnterRoof();
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        RoofVisibility roof = other.GetComponentInParent<RoofVisibility>();

        if (roof != null)
        {
            roof.ExitRoof();
        }
    }
}