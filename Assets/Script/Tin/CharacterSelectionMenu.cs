using UnityEngine;

public class CharacterSelectionMenu : MonoBehaviour
{
    [Header("--- HỆ THỐNG ---")]
    public GameObject fusionManager;

    [Header("--- UI PREVIEW ---")]
    public GameObject[] previewImages;

    private string[] characterNames = { "Kẻ Sống Sót: Vô Danh", "Kẻ Sống Sót: Bóng Ma" };

    private string[] characterStats = {
        "<color=#ff5555>KỸ NĂNG: CƠN ĐIÊN LÂM CHUNG</color>\nBản năng sinh tồn tột độ. Khi hạ sát 5 thực thể đột biến, lượng adrenaline kích phát vượt giới hạn cơ thể. Xóa bỏ hoàn toàn độ giật và không tiêu hao đạn dược trong vài giây.\n<color=#aaaaaa>[Thời gian hồi phục: 50s]</color>",
        "<color=#55ffff>KỸ NĂNG: BÓNG ĐÊM TĨNH LẶNG</color>\nSinh ra để lẩn khuất. Khi hạ thấp trọng tâm , nhịp tim và hơi thở đồng bộ với môi trường xung quanh. Đánh lừa hoàn toàn giác quan của lũ thây ma trong 5 giây.\n<color=#aaaaaa>[Thời gian hồi phục: 30s]</color>"
    };

    public static int LocalSelectedCharacterID = 0;
    public static string FinalSelectedName = "";

    private int previewID = 0;
    private string inputName = "";
    private bool isSelectionDone = false;

    // 🔥 ĐÃ FIX: Bóp chiều cao khung từ 920 xuống 780 cho gọn gàng
    private float boxWidth = 800;
    private float boxHeight = 780;
    private float startX;
    private float startY;

    void Start()
    {
        if (fusionManager != null) fusionManager.SetActive(false);
        inputName = PlayerPrefs.GetString("MyPlayerName", "Survivor_" + Random.Range(100, 999));

        startX = (1920 - boxWidth) / 2f;
        startY = 150f; // Kéo khung xuống một chút để cách xa cái Tiêu đề game

        ForceImagePosition();
        UpdatePreview();
    }

    void UpdatePreview()
    {
        if (previewImages == null) return;
        for (int i = 0; i < previewImages.Length; i++)
        {
            if (previewImages[i] != null) previewImages[i].SetActive(i == previewID);
        }
    }

    void ForceImagePosition()
    {
        if (previewImages == null) return;

        float scaleX = Screen.width / 1920f;
        float scaleY = Screen.height / 1080f;

        // 🔥 ĐÃ FIX: Kéo ảnh lên vị trí Y = 360 (Gần với Tên nhân vật hơn)
        float imageCenterX = 1920f / 2f * scaleX;
        float imageCenterY = (Screen.height) - (360f * scaleY);

        for (int i = 0; i < previewImages.Length; i++)
        {
            if (previewImages[i] != null)
            {
                RectTransform rectT = previewImages[i].GetComponent<RectTransform>();
                if (rectT != null)
                {
                    rectT.anchorMin = Vector2.zero;
                    rectT.anchorMax = Vector2.zero;
                    rectT.anchoredPosition = new Vector2(imageCenterX, imageCenterY);
                    rectT.sizeDelta = new Vector2(500 * scaleX, 500 * scaleY);
                }
            }
        }
    }

    void OnGUI()
    {
        if (isSelectionDone) return;

        Vector3 scale = new Vector3(Screen.width / 1920f, Screen.height / 1080f, 1f);
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, scale);

        Color titleColor = new Color(0.8f, 0.1f, 0.1f);
        GUIStyle gameTitleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 55,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = titleColor },
            hover = { textColor = titleColor },
            active = { textColor = titleColor }
        };

        Color nameColor = Color.yellow;
        GUIStyle nameStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 42,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = nameColor },
            hover = { textColor = nameColor },
            active = { textColor = nameColor }
        };

        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
        GUIStyle statsStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, wordWrap = true, alignment = TextAnchor.UpperCenter, richText = true };
        GUIStyle arrowBtnStyle = new GUIStyle(GUI.skin.button) { fontSize = 35, fontStyle = FontStyle.Bold };
        GUIStyle inputStyle = new GUIStyle(GUI.skin.textField) { fontSize = 26, alignment = TextAnchor.MiddleCenter };

        // TIÊU ĐỀ
        GUI.Label(new Rect(0, 40, 1920, 80), "FRAGMENTS OF SURVIVAL", gameTitleStyle);

        // KHUNG CHÍNH (Đã được thu gọn)
        GUI.Box(new Rect(startX, startY, boxWidth, boxHeight), "", boxStyle);

        Vector2 mousePos = Event.current.mousePosition;

        // ==========================================
        // KHU VỰC 1: TÊN NHÂN VẬT
        // ==========================================
        GUI.Label(new Rect(startX, startY + 0, boxWidth, 60), characterNames[previewID], nameStyle);


        // ==========================================
        // KHU VỰC 2: MŨI TÊN (Kéo lên cho vừa với hình mới)
        // ==========================================
        float arrowY = startY + 180; // Dịch lên
        Rect leftArrowRect = new Rect(startX + 100, arrowY, 60, 60);
        Rect rightArrowRect = new Rect(startX + boxWidth - 160, arrowY, 60, 60);

        GUI.color = leftArrowRect.Contains(mousePos) ? Color.yellow : Color.white;
        if (GUI.Button(leftArrowRect, "<", arrowBtnStyle))
        {
            previewID--;
            if (previewID < 0) previewID = characterNames.Length - 1;
            UpdatePreview();
        }

        GUI.color = rightArrowRect.Contains(mousePos) ? Color.yellow : Color.white;
        if (GUI.Button(rightArrowRect, ">", arrowBtnStyle))
        {
            previewID++;
            if (previewID >= characterNames.Length) previewID = 0;
            UpdatePreview();
        }
        GUI.color = Color.white;


        // ==========================================
        // KHU VỰC 3: CHỈ SỐ KỸ NĂNG (Kéo xích lên sát hình)
        // ==========================================
        GUI.Label(new Rect(startX + 50, startY + 380, boxWidth - 100, 150), characterStats[previewID], statsStyle);


        // ==========================================
        // KHU VỰC 4: NHẬP TÊN & VÀO GAME (Kéo lên sát cụm Kỹ năng)
        // ==========================================
        float inputWidth = 350;
        float inputX = startX + (boxWidth - inputWidth) / 2f;

        GUI.Label(new Rect(startX + 50, startY + 540, boxWidth - 100, 30), "ĐỊNH DANH KẺ SỐNG SÓT:", new GUIStyle(statsStyle) { fontSize = 18 });
        inputName = GUI.TextField(new Rect(inputX, startY + 580, inputWidth, 45), inputName, 15, inputStyle);

        Rect confirmRect = new Rect(startX + 120, startY + 660, boxWidth - 240, 65);
        GUI.color = confirmRect.Contains(mousePos) ? new Color(1f, 0.4f, 0.4f) : Color.white;

        if (GUI.Button(confirmRect, "TIẾN VÀO VÙNG ĐẤT CHẾT", new GUIStyle(GUI.skin.button) { fontSize = 26, fontStyle = FontStyle.Bold }))
        {
            ConfirmSelection();
        }
        GUI.color = Color.white;
    }

    private void ConfirmSelection()
    {
        LocalSelectedCharacterID = previewID;
        FinalSelectedName = inputName;
        PlayerPrefs.SetString("MyPlayerName", inputName);
        PlayerPrefs.Save();
        isSelectionDone = true;

        if (previewImages != null && previewImages.Length > previewID && previewImages[previewID] != null)
        {
            previewImages[previewID].SetActive(false);
        }

        // ❌ XÓA DÒNG NÀY ĐI (Chưa cho kéo màn vội)
        // if (LobbyCurtain.Instance != null) LobbyCurtain.Instance.HideCurtain();

        // Chỉ bật Fusion lên thôi (Bảng Host/Client sẽ hiện đè lên nền đen)
        if (fusionManager != null) fusionManager.SetActive(true);
    }
}