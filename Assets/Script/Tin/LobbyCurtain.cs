using UnityEngine;

public class LobbyCurtain : MonoBehaviour
{
    // Biến static để gọi từ mọi nơi mà không cần Find
    public static LobbyCurtain Instance;

    void Awake()
    {
        // Setup Singleton đơn giản
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject); // Chống đẻ ra 2 tấm màn
        }
    }

    public void HideCurtain()
    {
        // Tắt Canvas chứa tấm màn này đi
        gameObject.SetActive(false);
        Debug.Log("🎭 ĐÃ KÉO MÀN! CHÀO MỪNG ĐẾN VỚI THẾ GIỚI ZOMBIE!");
    }
}