#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;

[InitializeOnLoad]
public static class AK47AudioTrimmer
{
    static AK47AudioTrimmer()
    {
        EditorApplication.delayCall += TrimAK47AudioFiles;
    }

    [MenuItem("Tools/Trim AK47 Audio Files")]
    public static void TrimAK47AudioFiles()
    {
        string folder = "Assets/Resources/Sound/Weapons/AK47";
        if (!Directory.Exists(folder)) return;

        // 1. Single shot clip: Trim leading silence AND trim after 1st shot (~0.33s duration)
        TrimClip(folder + "/ak47_single_raw.ogg", folder + "/ak47_single.wav", 0.012f, 0.330f);

        // 2. Full auto clip: Trim leading silence
        TrimClip(folder + "/ak47_auto_raw.ogg", folder + "/ak47_auto.wav", 0.012f, 0f);

        // 3. Reloading clip: Trim leading silence
        TrimClip(folder + "/ak47_reload_raw.ogg", folder + "/ak47_reload.wav", 0.012f, 0f);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[AK47AudioTrimmer] ALL 3 AK47 AUDIO FILES TRIMMED & OPTIMIZED SUCCESSFULLY!");
    }

    private static void TrimClip(string inputPath, string outputPath, float silenceThreshold, float maxDurationSec)
    {
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(inputPath);
        if (clip == null)
        {
            Debug.LogWarning($"[AK47AudioTrimmer] AudioClip not found at: {inputPath}");
            return;
        }

        float[] samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);

        int startSample = 0;
        int endSample = samples.Length;

        // 1. Strip leading silence (0s delay)
        for (int i = 0; i < samples.Length; i++)
        {
            if (Mathf.Abs(samples[i]) >= silenceThreshold)
            {
                startSample = i;
                break;
            }
        }

        startSample = (startSample / clip.channels) * clip.channels;

        // 2. If maxDurationSec is specified, cap duration (e.g. 0.33s for single shot)
        if (maxDurationSec > 0f)
        {
            int maxSamples = Mathf.FloorToInt(maxDurationSec * clip.frequency * clip.channels);
            maxSamples = (maxSamples / clip.channels) * clip.channels;
            if (startSample + maxSamples < endSample)
            {
                endSample = startSample + maxSamples;
            }
        }

        float trimmedLeadingSec = (float)startSample / (clip.frequency * clip.channels);
        float originalDuration = clip.length;

        int newSampleCount = endSample - startSample;
        float[] trimmedSamples = new float[newSampleCount];
        System.Array.Copy(samples, startSample, trimmedSamples, 0, newSampleCount);

        // Apply a gentle 5ms fade-out at the end to prevent pop/click
        int fadeSamples = Mathf.Min(Mathf.FloorToInt(0.005f * clip.frequency * clip.channels), newSampleCount / 2);
        for (int i = 0; i < fadeSamples; i++)
        {
            float idx = newSampleCount - 1 - i;
            float factor = (float)i / fadeSamples;
            trimmedSamples[(int)idx] *= factor;
        }

        string fullOutputPath = Path.Combine(Application.dataPath, outputPath.Replace("Assets/", ""));
        SaveWavFile(fullOutputPath, trimmedSamples, clip.frequency, clip.channels);

        float newDuration = (float)(newSampleCount / clip.channels) / clip.frequency;
        Debug.Log($"[AK47AudioTrimmer] Processed {Path.GetFileName(outputPath)}: Trimmed {trimmedLeadingSec:F3}s leading silence. Original: {originalDuration:F3}s -> New: {newDuration:F3}s");
    }

    private static void SaveWavFile(string filePath, float[] samples, int frequency, int channels)
    {
        using (FileStream fs = new FileStream(filePath, FileMode.Create))
        using (BinaryWriter writer = new BinaryWriter(fs))
        {
            int sampleCount = samples.Length;
            int byteRate = frequency * channels * 2;

            // RIFF header
            writer.Write(new char[4] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + sampleCount * 2);
            writer.Write(new char[4] { 'W', 'A', 'V', 'E' });

            // fmt subchunk
            writer.Write(new char[4] { 'f', 'm', 't', ' ' });
            writer.Write(16);
            writer.Write((short)1); // PCM
            writer.Write((short)channels);
            writer.Write(frequency);
            writer.Write(byteRate);
            writer.Write((short)(channels * 2));
            writer.Write((short)16);

            // data subchunk
            writer.Write(new char[4] { 'd', 'a', 't', 'a' });
            writer.Write(sampleCount * 2);

            for (int i = 0; i < sampleCount; i++)
            {
                short intSample = (short)Mathf.Clamp(samples[i] * 32767f, -32768f, 32767f);
                writer.Write(intSample);
            }
        }
    }
}
#endif
