using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Creates Main's arrival tableau before HostModeSpawner chooses a point.</summary>
public static class MainArrivalStoryBootstrap
{
    private const string CarResourcePath = "Story/BrokenArrivalCar";
    private const string ArrivalAnchorName = "ViTriXeChetMay";
    // Keep the story arrival independent from the four gameplay respawn points.
    // PR #305 overwrote Main.unity and removed ViTriXeChetMay; this is the
    // marker's last known position before that scene regression.
    private static readonly Vector3 DefaultArrivalAnchorPosition =
        new Vector3(35.73f, -13.73f, -0.025169304f);
    private static readonly Vector2[] InitialSpawnOffsets =
    {
        new Vector2(-1.9f, -1.35f),
        new Vector2(1.9f, -1.35f),
        new Vector2(-1.9f, 1.35f),
        new Vector2(1.9f, 1.35f),
        new Vector2(-3.8f, 0f),
        new Vector2(3.8f, 0f),
        new Vector2(-3.2f, -2.7f),
        new Vector2(3.2f, -2.7f),
        new Vector2(-3.2f, 2.7f),
        new Vector2(3.2f, 2.7f)
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
        else
        {
            arrivalAnchorPosition = DefaultArrivalAnchorPosition;
            hasArrivalAnchor = true;
            Debug.LogWarning($"[MAIN STORY] Missing '{ArrivalAnchorName}'. Using the preserved story-arrival position.");
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

        Vector2 offset = GetInitialSpawnOffset(playerSlot);
        position = arrivalAnchorPosition + new Vector3(offset.x, offset.y, 0f);
        return true;
    }

    public static Vector2 GetInitialSpawnOffset(int playerSlot)
    {
        int safeSlot = playerSlot == int.MinValue ? 0 : Mathf.Abs(playerSlot);
        return InitialSpawnOffsets[safeSlot % InitialSpawnOffsets.Length];
    }

}
