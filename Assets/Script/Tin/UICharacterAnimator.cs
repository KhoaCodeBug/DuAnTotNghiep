using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Component gắn vào GameObject chứa UI Image để chạy animation nhân vật
/// bằng cách thay đổi sprite liên tục (frame-by-frame) từ sprite sheet đã slice.
/// Hỗ trợ nhiều trạng thái animation (Idle, Attack, Taunt) với chuyển đổi ngẫu nhiên.
/// </summary>
public class UICharacterAnimator : MonoBehaviour
{
    // --- Cấu hình animation ---
    private float frameRate = 10f; // Số frame mỗi giây
    private float actionInterval = 4f; // Khoảng thời gian giữa các hành động ngẫu nhiên (giây)
    private float actionIntervalVariance = 3f; // Biến thiên ngẫu nhiên (giây)

    // --- Internal state ---
    private Image targetImage;
    private Dictionary<string, Sprite[]> animationClips = new Dictionary<string, Sprite[]>();
    private string currentClip = "";
    private int currentFrame = 0;
    private float frameTimer = 0f;
    private float nextActionTimer = 0f;
    private bool isPlayingAction = false;
    private string idleClipName = "";
    private List<string> actionClipNames = new List<string>();
    private bool isInitialized = false;

    /// <summary>
    /// Khởi tạo animator với đường dẫn Resources chứa sprite sheet đã slice.
    /// Ví dụ: "CharacterPreview/Survivor1" sẽ load tất cả sprite từ folder đó.
    /// </summary>
    /// <param name="resourceFolderPath">Đường dẫn thư mục trong Resources (không cần "Resources/" prefix)</param>
    /// <param name="idleSheetName">Tên file sprite sheet Idle (không có đuôi .png)</param>
    /// <param name="actionSheetNames">Danh sách tên file sprite sheet hành động</param>
    public void Initialize(string resourceFolderPath, string idleSheetName, string[] actionSheetNames)
    {
        targetImage = GetComponent<Image>();
        if (targetImage == null)
        {
            Debug.LogWarning("[UICharacterAnimator] Không tìm thấy Image component!");
            return;
        }

        animationClips.Clear();
        actionClipNames.Clear();

        // Load Idle sprite sheet
        Sprite[] idleSprites = Resources.LoadAll<Sprite>(resourceFolderPath + "/" + idleSheetName);
        idleSprites = FilterAndSortSprites(idleSprites);
        if (idleSprites != null && idleSprites.Length > 0)
        {
            animationClips[idleSheetName] = idleSprites;
            idleClipName = idleSheetName;
        }
        else
        {
            Debug.LogWarning($"[UICharacterAnimator] Không load được idle sprites từ: {resourceFolderPath}/{idleSheetName}");
            return;
        }

        // Load các Action sprite sheets
        foreach (string actionName in actionSheetNames)
        {
            Sprite[] actionSprites = Resources.LoadAll<Sprite>(resourceFolderPath + "/" + actionName);
            actionSprites = FilterAndSortSprites(actionSprites);
            if (actionSprites != null && actionSprites.Length > 0)
            {
                animationClips[actionName] = actionSprites;
                actionClipNames.Add(actionName);
            }
        }

        // Bắt đầu chạy Idle
        PlayClip(idleClipName);
        ResetActionTimer();
        isInitialized = true;

        // Đảm bảo Image hiển thị đúng
        targetImage.preserveAspect = true;
        targetImage.type = Image.Type.Simple;
    }

    /// <summary>
    /// Lọc và sắp xếp các sprite để chỉ lấy hướng nhìn chính diện (hướng 0)
    /// của nhân vật thay vì chạy xoay vòng qua 8 hướng.
    /// </summary>
    private Sprite[] FilterAndSortSprites(Sprite[] rawSprites)
    {
        if (rawSprites == null || rawSprites.Length == 0) return rawSprites;

        string firstName = rawSprites[0].name;
        string[] parts = firstName.Split('_');

        List<Sprite> filteredList = new List<Sprite>();

        if (parts.Length >= 3)
        {
            // Format: TênSheet_Hướng_Frame (ví dụ: Idle2_0_5)
            // Lọc các sprite có hướng là "0" (nhìn chính diện)
            foreach (var sprite in rawSprites)
            {
                string[] p = sprite.name.Split('_');
                if (p.Length >= 3 && p[p.Length - 2] == "0")
                {
                    filteredList.Add(sprite);
                }
            }
            // Sắp xếp theo chỉ số frame (số cuối cùng)
            return filteredList.OrderBy(s => {
                string[] p = s.name.Split('_');
                int.TryParse(p[p.Length - 1], out int index);
                return index;
            }).ToArray();
        }
        else
        {
            // Format: TênSheet_ChỉSốFrameToànCục (ví dụ: Idle2_45)
            // Tấm sprite sheet chứa toàn bộ 8 hướng xếp liên tiếp.
            // Số frame mỗi hướng = tổng số frame / 8 hướng.
            int totalFrames = rawSprites.Length;
            int framesPerDirection = totalFrames / 8;
            if (framesPerDirection <= 0) framesPerDirection = totalFrames;

            // Sắp xếp toàn bộ trước
            var sortedRaw = rawSprites.OrderBy(s => {
                string[] p = s.name.Split('_');
                int.TryParse(p[p.Length - 1], out int index);
                return index;
            }).ToList();

            // Lấy hướng đầu tiên (hướng 0, ví dụ 15 frame đầu từ 0 đến 14)
            for (int i = 0; i < Mathf.Min(framesPerDirection, sortedRaw.Count); i++)
            {
                filteredList.Add(sortedRaw[i]);
            }
            return filteredList.ToArray();
        }
    }

    /// <summary>
    /// Dừng animation và reset trạng thái.
    /// </summary>
    public void StopAnimation()
    {
        isInitialized = false;
        currentFrame = 0;
        frameTimer = 0f;
    }

    void Update()
    {
        if (!isInitialized || targetImage == null || !animationClips.ContainsKey(currentClip)) return;

        // Cập nhật frame animation
        frameTimer += Time.unscaledDeltaTime;
        if (frameTimer >= 1f / frameRate)
        {
            frameTimer -= 1f / frameRate;
            currentFrame++;

            Sprite[] frames = animationClips[currentClip];

            if (currentFrame >= frames.Length)
            {
                // Nếu đang chạy action clip thì quay về Idle khi hết
                if (isPlayingAction)
                {
                    isPlayingAction = false;
                    PlayClip(idleClipName);
                    ResetActionTimer();
                    return;
                }
                else
                {
                    // Loop Idle
                    currentFrame = 0;
                }
            }

            targetImage.sprite = frames[currentFrame];
        }

        // Kiểm tra xem có nên chạy hành động ngẫu nhiên không
        if (!isPlayingAction && actionClipNames.Count > 0)
        {
            nextActionTimer -= Time.unscaledDeltaTime;
            if (nextActionTimer <= 0f)
            {
                // Chọn ngẫu nhiên 1 action clip
                string randomAction = actionClipNames[Random.Range(0, actionClipNames.Count)];
                isPlayingAction = true;
                PlayClip(randomAction);
            }
        }
    }

    private void PlayClip(string clipName)
    {
        if (!animationClips.ContainsKey(clipName)) return;

        currentClip = clipName;
        currentFrame = 0;
        frameTimer = 0f;

        // Hiển thị frame đầu tiên ngay lập tức
        Sprite[] frames = animationClips[clipName];
        if (frames.Length > 0 && targetImage != null)
        {
            targetImage.sprite = frames[0];
        }
    }

    private void ResetActionTimer()
    {
        nextActionTimer = actionInterval + Random.Range(-actionIntervalVariance, actionIntervalVariance);
        nextActionTimer = Mathf.Max(nextActionTimer, 2f); // Tối thiểu 2 giây
    }

    /// <summary>
    /// Trích xuất chỉ số frame từ tên sprite.
    /// Ví dụ: "Idle_Shadowless_12" → 12, "Attack1_3" → 3
    /// </summary>
    private int ExtractFrameIndex(string spriteName)
    {
        // Tìm số cuối cùng sau dấu "_"
        string[] parts = spriteName.Split('_');
        if (parts.Length > 0)
        {
            string lastPart = parts[parts.Length - 1];
            if (int.TryParse(lastPart, out int index))
                return index;
        }
        return 0;
    }
}
