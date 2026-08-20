using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Creates Main's arrival tableau before HostModeSpawner chooses a point.</summary>
public static class MainArrivalStoryBootstrap
{
    private const string CarResourcePath = "Story/BrokenArrivalCar";
    private const string ArrivalAnchorName = "ViTriXeChetMay";
    private static readonly Vector2[] InitialSpawnOffsets =
    {
        new Vector2(-1.9f, -1.35f),
        new Vector2(1.9f, -1.35f),
        new Vector2(-1.9f, 1.35f),
        new Vector2(1.9f, 1.35f)
    };

    private static int configuredSpawnerInstanceId;
    private static Vector3 arrivalAnchorPosition;
    private static bool hasArrivalAnchor;

    public static void EnsureMainSceneSetup(HostModeSpawner spawner)
    {
        if (spawner == null || SceneManager.GetActiveScene().name != "Main") return;
        int spawnerInstanceId = spawner.GetInstanceID();
        if (configuredSpawnerInstanceId == spawnerInstanceId && BrokenArrivalCar.Instance != null) return;

        Transform anchorTransform = GameObject.Find(ArrivalAnchorName)?.transform;
        if (anchorTransform != null)
        {
            arrivalAnchorPosition = anchorTransform.position;
            hasArrivalAnchor = true;
        }
        else if (!TryGetFallbackAnchor(spawner.spawnPoints, out arrivalAnchorPosition))
        {
            Debug.LogError($"[MAIN STORY] Missing '{ArrivalAnchorName}' and no fallback spawn point exists.");
            return;
        }
        else
        {
            hasArrivalAnchor = true;
            Debug.LogWarning($"[MAIN STORY] Missing '{ArrivalAnchorName}'. Using the old spawn-point centre as fallback.");
        }

        BrokenArrivalCar car = BrokenArrivalCar.Instance;
        if (car == null)
        {
            GameObject prefab = Resources.Load<GameObject>(CarResourcePath);
            if (prefab == null)
            {
                Debug.LogError($"[MAIN STORY] Missing Resources/{CarResourcePath}.prefab.");
                return;
            }
            GameObject carObject = Object.Instantiate(prefab, arrivalAnchorPosition,
                Quaternion.Euler(0f, 0f, 4.218f));
            carObject.name = "Broken Arrival Car (from Intro)";
            car = carObject.GetComponent<BrokenArrivalCar>();
        }

        if (car != null)
        {
            car.transform.position = arrivalAnchorPosition;
            configuredSpawnerInstanceId = spawnerInstanceId;
        }
    }

    public static bool TryGetInitialSpawnPose(int playerSlot, out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;
        if (!hasArrivalAnchor || SceneManager.GetActiveScene().name != "Main") return false;

        Vector2 offset = InitialSpawnOffsets[Mathf.Abs(playerSlot) % InitialSpawnOffsets.Length];
        position = arrivalAnchorPosition + new Vector3(offset.x, offset.y, 0f);
        return true;
    }

    private static bool TryGetFallbackAnchor(Transform[] points, out Vector3 anchor)
    {
        anchor = Vector3.zero;
        if (points == null || points.Length == 0) return false;

        int count = 0;
        for (int i = 0; i < points.Length; i++)
        {
            if (points[i] == null) continue;
            anchor += points[i].position;
            count++;
        }

        if (count == 0) return false;
        anchor /= count;
        return true;
    }
}
