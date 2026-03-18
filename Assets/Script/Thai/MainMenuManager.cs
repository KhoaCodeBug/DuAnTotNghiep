using UnityEngine;
using UnityEngine.SceneManagement; // Thư viện để chuyển cảnh

public class MainMenuManager : MonoBehaviour
{
    public void PlayGame()
    {
        // "KhoaTest" phải trùng khớp hoàn toàn với tên Scene của bạn
        SceneManager.LoadScene("KhoaTest");
    }

    public void OpenSettings()
    {
        Debug.Log("Mở cài đặt...");
        // Bạn có thể thêm code mở Panel Setting ở đây sau
    }
}