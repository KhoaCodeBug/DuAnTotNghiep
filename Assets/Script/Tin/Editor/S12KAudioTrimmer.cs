using UnityEditor;
using UnityEngine;
using System.IO;

public static class S12KAudioTrimmer
{
    [MenuItem("Tools/Trim S12K Audio")]
    public static void TrimS12KAudio()
    {
        string ogaPath = "Assets/Resources/Sound/Weapons/S12K/S12K_Single.ogg";
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ogaPath);
        if (clip == null)
        {
            Debug.LogError("[S12KAudioTrimmer] Không tìm thấy file âm thanh tại: " + ogaPath);
            return;
        }

        int channels = clip.channels;
        int frequency = clip.frequency;
        float[] samples = new float[clip.samples * channels];
        clip.GetData(samples, 0);

        // Tìm điểm bắt đầu tiếng nổ (Peak Amplitude)
        float threshold = 0.05f;
        int startSample = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            if (Mathf.Abs(samples[i]) > threshold)
            {
                // Lùi lại khoảng 0.02s để giữ trọn vẹn âm đầu
                startSample = Mathf.Max(0, i - (int)(frequency * channels * 0.02f));
                break;
            }
        }

        // Lấy độ dài 0.6 giây cho đúng 1 phát bắn súng Shotgun gọn lọt
        int desiredSamples = (int)(frequency * channels * 0.6f);
        int endSample = Mathf.Min(samples.Length, startSample + desiredSamples);

        int trimmedSampleCount = endSample - startSample;
        float[] trimmedSamples = new float[trimmedSampleCount];
        System.Array.Copy(samples, startSample, trimmedSamples, 0, trimmedSampleCount);

        // Tạo thư mục nếu chưa có
        string dirPath = Path.Combine(Application.dataPath, "Resources/Sound/Weapons/S12K");
        if (!Directory.Exists(dirPath))
        {
            Directory.CreateDirectory(dirPath);
        }

        string fullWavPath = Path.Combine(dirPath, "s12k_single.wav");
        SaveWav(fullWavPath, trimmedSamples, channels, frequency);

        AssetDatabase.Refresh();

        string relativeWavPath = "Assets/Resources/Sound/Weapons/S12K/s12k_single.wav";
        Debug.Log($"[S12KAudioTrimmer] Đã cắt thành công 1 phát bắn S12K vào: {relativeWavPath}");

        // Tự động gán vào S12K.asset
        ItemData s12kItem = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Resources/Items/S12K.asset");
        AudioClip newWav = AssetDatabase.LoadAssetAtPath<AudioClip>(relativeWavPath);
        if (s12kItem != null && newWav != null)
        {
            s12kItem.customSingleShootSFX = newWav;
            EditorUtility.SetDirty(s12kItem);
            AssetDatabase.SaveAssets();
            Debug.Log("[S12KAudioTrimmer] Đã tự động gán s12k_single.wav vào S12K.asset!");
        }
    }

    private static void SaveWav(string filepath, float[] samples, int channels, int frequency)
    {
        using (FileStream fs = new FileStream(filepath, FileMode.Create))
        using (BinaryWriter writer = new BinaryWriter(fs))
        {
            int sampleCount = samples.Length;
            int byteCount = sampleCount * 2; // 16-bit PCM

            // RIFF header
            writer.Write(new char[4] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + byteCount);
            writer.Write(new char[4] { 'W', 'A', 'V', 'E' });

            // fmt subchunk
            writer.Write(new char[4] { 'f', 'm', 't', ' ' });
            writer.Write(16); // Subchunk1Size
            writer.Write((short)1); // AudioFormat 1 = PCM
            writer.Write((short)channels);
            writer.Write(frequency);
            writer.Write(frequency * channels * 2); // ByteRate
            writer.Write((short)(channels * 2)); // BlockAlign
            writer.Write((short)16); // BitsPerSample

            // data subchunk
            writer.Write(new char[4] { 'd', 'a', 't', 'a' });
            writer.Write(byteCount);

            for (int i = 0; i < sampleCount; i++)
            {
                short sampleInt = (short)Mathf.Clamp(samples[i] * 32767f, -32768f, 32767f);
                writer.Write(sampleInt);
            }
        }
    }
}
