using UnityEngine;
using System.Collections.Generic;

public class FieldOfViewMesh : MonoBehaviour
{
    private Mesh mesh;
    public PZ_FieldOfView fov;

    [Tooltip("Cố định số lượng tia để chống rung Mesh")]
    public int stepCount = 100;

    private List<PZ_VisibleObject> currentlyVisibleEnemies = new List<PZ_VisibleObject>();

    void Start()
    {
        mesh = new Mesh();
        mesh.name = "FOV Mesh";
        GetComponent<MeshFilter>().mesh = mesh;
    }

    // Xử lý ẩn hiện trong Update
    private void Update()
    {
        FindVisibleTargets();
    }

    // Vẽ Mesh trong LateUpdate để tránh giật lag khi Player di chuyển
    void LateUpdate()
    {
        DrawMesh();
    }

    void DrawMesh()
    {
        Vector3 origin = fov.transform.position;
        origin.y += 0.05f;

        float stepAngleSize = 360f / stepCount;
        List<Vector3> viewPoints = new List<Vector3>();

        float minPlayerRadius = 0.8f;
        float wallClimbHeight = 2.0f;

        for (int i = 0; i <= stepCount; i++)
        {
            float angle = fov.transform.eulerAngles.y + stepAngleSize * i;
            Vector3 dir = fov.DirFromAngle(angle, true);

            Vector3 rayOrigin = fov.transform.position + Vector3.up * 0.8f;
            float angleToForward = Vector3.Angle(fov.transform.forward, dir);

            // Quầng sáng quanh chân 3m, còn lại theo FOV
            float currentMaxDist = 3.0f;
            if (angleToForward < fov.viewAngle / 2f) currentMaxDist = fov.viewRadius;

            RaycastHit hit;
            Vector3 worldPoint;

            if (Physics.Raycast(rayOrigin, dir, out hit, currentMaxDist, fov.obstacleMask))
            {
                float finalDist = Mathf.Max(hit.distance, minPlayerRadius);
                worldPoint = origin + dir * finalDist;
                worldPoint.y = origin.y + wallClimbHeight;
            }
            else
            {
                worldPoint = origin + dir * currentMaxDist;
                worldPoint.y = origin.y;
            }

            viewPoints.Add(transform.InverseTransformPoint(worldPoint));
        }

        int vertexCount = viewPoints.Count + 1;
        Vector3[] vertices = new Vector3[vertexCount];
        int[] triangles = new int[(vertexCount - 2) * 3];

        vertices[0] = new Vector3(0, 0.05f, 0); // Tâm ở chân

        for (int i = 0; i < vertexCount - 1; i++)
        {
            vertices[i + 1] = viewPoints[i];
            if (i < vertexCount - 2)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
    }

    void FindVisibleTargets()
    {
        // Luôn quét bằng aimRadius (bán kính lớn nhất) để không bị sót mục tiêu khi thu nhỏ FOV
        float scanRadius = fov.aimRadius;
        Collider[] targetsInRadius = Physics.OverlapSphere(fov.transform.position, scanRadius, fov.enemyMask);

        List<PZ_VisibleObject> enemiesFoundThisFrame = new List<PZ_VisibleObject>();

        for (int i = 0; i < targetsInRadius.Length; i++)
        {
            Transform target = targetsInRadius[i].transform;
            PZ_VisibleObject visibleScript = target.GetComponent<PZ_VisibleObject>();
            if (visibleScript == null) continue;

            Vector3 dirToTarget = (target.position - fov.transform.position).normalized;
            float distToTarget = Vector3.Distance(fov.transform.position, target.position);

            bool canSee = false;

            if (distToTarget <= 3.0f) // Gần chân
            {
                canSee = true;
            }
            else if (Vector3.Angle(fov.transform.forward, dirToTarget) < fov.viewAngle / 2f)
            {
                Vector3 startRay = fov.transform.position + Vector3.up * 1.0f;
                Vector3 endRay = target.position + Vector3.up * 1.0f;

                if (!Physics.Linecast(startRay, endRay, fov.obstacleMask))
                {
                    canSee = true;
                }
            }

            if (canSee)
            {
                visibleScript.SetVisibility(true);
                enemiesFoundThisFrame.Add(visibleScript);
            }
            else
            {
                visibleScript.SetVisibility(false);
            }
        }

        // Tắt những con không còn nằm trong tầm nhìn nữa
        foreach (var oldEnemy in currentlyVisibleEnemies)
        {
            if (oldEnemy != null && !enemiesFoundThisFrame.Contains(oldEnemy))
            {
                oldEnemy.SetVisibility(false);
            }
        }
        currentlyVisibleEnemies = enemiesFoundThisFrame;
    }
}