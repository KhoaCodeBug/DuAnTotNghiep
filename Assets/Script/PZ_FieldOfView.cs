using UnityEngine;
using System.Collections.Generic;

public class PZ_FieldOfView : MonoBehaviour
{
    public float viewRadius = 10f;
    [Range(0, 360)] public float viewAngle = 90f;

    [Header("--- Layers ---")]
    public LayerMask obstacleMask; // Layer của tường
    public LayerMask enemyMask;    // Layer của Zombie

    [Header("--- Dynamic FOV ---")]
    public float idleRadius = 10f;
    public float aimRadius = 20f;
    public float idleAngle = 120f;
    public float aimAngle = 45f;
    public float smoothTime = 0.2f;

    private float radiusVelocity;
    private float angleVelocity;

    void Update()
    {
        bool isAiming = Input.GetMouseButton(1);

        float targetRadius = isAiming ? aimRadius : idleRadius;
        float targetAngle = isAiming ? aimAngle : idleAngle;

        // SmoothDamp giúp thay đổi trị số cực mượt
        viewRadius = Mathf.SmoothDamp(viewRadius, targetRadius, ref radiusVelocity, smoothTime);
        viewAngle = Mathf.SmoothDamp(viewAngle, targetAngle, ref angleVelocity, smoothTime);
    }

    public Vector3 DirFromAngle(float angleInDegrees, bool angleIsGlobal)
    {
        if (!angleIsGlobal) angleInDegrees += transform.eulerAngles.y;
        return new Vector3(Mathf.Sin(angleInDegrees * Mathf.Deg2Rad), 0, Mathf.Cos(angleInDegrees * Mathf.Deg2Rad));
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position, viewRadius);
        Vector3 viewAngleA = DirFromAngle(-viewAngle / 2, false);
        Vector3 viewAngleB = DirFromAngle(viewAngle / 2, false);
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, transform.position + viewAngleA * viewRadius);
        Gizmos.DrawLine(transform.position, transform.position + viewAngleB * viewRadius);
    }
}