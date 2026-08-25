#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;

public static class AK47AudioTrimmer
{
    [MenuItem("Tools/Trim AK47 Audio Files")]
    public static void TrimAK47AudioFiles()
    {
        string folder = "Assets/Resources/Sound/Weapons/AK47";
        if (!Directory.Exists(folder)) return;

        // 1. Single shot clip: Trim leading silence AND trim after 1st shot (~0.33s duration)
        bool allSucceeded = TrimClip(
            folder + "/ak47_single_raw.ogg", folder + "/ak47_single.wav", 0.012f, 0.330f);

        // 2. Full auto clip: Trim leading silence
        allSucceeded &= TrimClip(
            folder + "/ak47_auto_raw.ogg", folder + "/ak47_auto.wav", 0.012f, 0f);

        // 3. Reloading clip: Trim leading silence
        allSucceeded &= TrimClip(
            folder + "/ak47_reload_raw.ogg", folder + "/ak47_reload.wav", 0.012f, 0f);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        if (allSucceeded)
            Debug.Log("[AK47AudioTrimmer] ALL 3 AK47 AUDIO FILES TRIMMED & OPTIMIZED SUCCESSFULLY!");
        else
            Debug.LogWarning("[AK47AudioTrimmer] Some files could not be updated. See the warning above.");
    }

    private static bool TrimClip(
        string inputPath, string outputPath, float silenceThreshold, float maxDurationSec)
    {
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(inputPath);
        if (clip == null)
        {
            Debug.LogWarning($"[AK47AudioTrimmer] AudioClip not found at: {inputPath}");
            return false;
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
        try
        {
            // Unity may retain memory-mapped handles for imported audio. Release
            // them before atomically replacing the generated WAV file.
            AssetDatabase.ReleaseCachedFileHandles();
            SaveWavFile(fullOutputPath, trimmedSamples, clip.frequency, clip.channels);
        }
        catch (IOException exception)
        {
            Debug.LogWarning(
                $"[AK47AudioTrimmer] Could not update {outputPath}. " +
                $"Stop audio preview/Play Mode and run Tools > Trim AK47 Audio Files again. " +
                exception.Message);
            return false;
        }

        float newDuration = (float)(newSampleCount / clip.channels) / clip.frequency;
        Debug.Log($"[AK47AudioTrimmer] Processed {Path.GetFileName(outputPath)}: Trimmed {trimmedLeadingSec:F3}s leading silence. Original: {originalDuration:F3}s -> New: {newDuration:F3}s");
        return true;
    }

    private static void SaveWavFile(string filePath, float[] samples, int frequency, int channels)
    {
        // Keep the temporary file on the project's drive because File.Replace
        // requires source and destination to reside on the same volume.
        string temporaryDirectory = Path.Combine(
            Directory.GetParent(Application.dataPath).FullName,
            "Library", "AK47AudioTrimmerTemp");
        Directory.CreateDirectory(temporaryDirectory);
        string temporaryPath = Path.Combine(
            temporaryDirectory,
            $"{Path.GetFileNameWithoutExtension(filePath)}_{System.Guid.NewGuid():N}.wav");

        try
        {
            using (FileStream fs = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write))
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

            if (File.Exists(filePath))
                File.Replace(temporaryPath, filePath, null);
            else
                File.Move(temporaryPath, filePath);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }
}
#endif
