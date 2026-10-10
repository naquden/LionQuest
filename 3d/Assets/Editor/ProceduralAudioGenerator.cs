using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Writes placeholder SFX/music as real 16-bit PCM WAV files, imported as AudioClip assets.
///
/// Unity's runtime API has no WAV encoder (unlike Texture2D.EncodeToPNG for UiTheme's procedural
/// sprites) — AudioClip.SetData only fills an in-memory clip, and an in-memory-only clip cannot be
/// serialized as a project asset for a prefab/generator to reference. Writing the WAV bytes by hand
/// (a fixed 44-byte header + 16-bit little-endian PCM samples) is the only way to get something
/// AssetDatabase.ImportAsset can turn into a real .asset-backed AudioClip.
///
/// Every clip here is a short synthesized tone (sine/noise envelopes), not real sound design — swap
/// the generated .wav files for authored SFX/music later; nothing downstream cares how the AudioClip
/// was produced.
/// </summary>
public static class ProceduralAudioGenerator
{
    const string AudioDir = "Assets/Audio/Generated";
    const int SampleRate = 44100;

    [MenuItem("LionQuest/Generate/Placeholder Audio")]
    public static void Generate()
    {
        UiTheme.EnsureFolder(AudioDir);

        WriteTone("SFX_Hit", DecayedTone(220f, 0.12f, 0.9f, waveform: Square));
        WriteTone("SFX_EnemyDeath", DecayedTone(110f, 0.35f, 0.8f, waveform: Sawtooth));
        WriteTone("SFX_PlayerDeath", DecayedTone(80f, 0.6f, 0.9f, waveform: Sawtooth));
        WriteTone("SFX_SkillCast", RisingTone(300f, 700f, 0.25f, 0.7f));
        WriteTone("SFX_Pickup", RisingTone(500f, 1000f, 0.18f, 0.6f));
        WriteTone("SFX_UIClick", DecayedTone(600f, 0.06f, 0.5f, waveform: Sine));
        WriteTone("SFX_Victory", Chord(new[] { 440f, 554f, 659f }, 1.2f, 0.8f));
        WriteTone("SFX_GameOver", DecayedTone(150f, 1.0f, 0.8f, waveform: Sawtooth));

        WriteTone("Music_Menu", AmbientLoop(220f, 8f, 0.35f));
        WriteTone("Music_Gameplay", AmbientLoop(146f, 8f, 0.4f));

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"ProceduralAudioGenerator: wrote placeholder clips to {AudioDir}.");
    }

    public static AudioClip Load(string clipName) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioDir}/{clipName}.wav");

    // ------------------------------------------------------------------
    // Waveforms
    // ------------------------------------------------------------------

    private delegate float Waveform(float phase01);

    private static float Sine(float phase01) => Mathf.Sin(phase01 * Mathf.PI * 2f);
    private static float Square(float phase01) => phase01 < 0.5f ? 1f : -1f;
    private static float Sawtooth(float phase01) => phase01 * 2f - 1f;

    /// <summary>A single tone that starts at peakVolume and decays to silence over durationSeconds —
    /// the shape of an impact/hit sound.</summary>
    private static float[] DecayedTone(float frequency, float durationSeconds, float peakVolume, Waveform waveform)
    {
        int sampleCount = Mathf.Max(1, Mathf.RoundToInt(durationSeconds * SampleRate));
        var samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / SampleRate;
            float phase01 = (t * frequency) % 1f;
            float envelope = 1f - (float)i / sampleCount; // linear decay to 0.
            samples[i] = waveform(phase01) * envelope * peakVolume;
        }
        return samples;
    }

    /// <summary>Frequency sweeps from startFrequency to endFrequency over durationSeconds, with a
    /// short attack/release envelope — the shape of a cast/pickup chime.</summary>
    private static float[] RisingTone(float startFrequency, float endFrequency, float durationSeconds, float peakVolume)
    {
        int sampleCount = Mathf.Max(1, Mathf.RoundToInt(durationSeconds * SampleRate));
        var samples = new float[sampleCount];
        float phase = 0f;
        for (int i = 0; i < sampleCount; i++)
        {
            float t01 = (float)i / sampleCount;
            float frequency = Mathf.Lerp(startFrequency, endFrequency, t01);
            phase += frequency / SampleRate;
            phase %= 1f;

            float envelope = Mathf.Sin(t01 * Mathf.PI); // fades in and out — 0 at both ends, peak at the middle.
            samples[i] = Sine(phase) * envelope * peakVolume;
        }
        return samples;
    }

    /// <summary>Sum of several DecayedTone sine waves at once — a simple major-chord "ta-da" for
    /// victory rather than a single flat tone.</summary>
    private static float[] Chord(float[] frequencies, float durationSeconds, float peakVolume)
    {
        int sampleCount = Mathf.Max(1, Mathf.RoundToInt(durationSeconds * SampleRate));
        var samples = new float[sampleCount];
        float perNoteVolume = peakVolume / frequencies.Length;
        foreach (float frequency in frequencies)
        {
            float[] note = DecayedTone(frequency, durationSeconds, perNoteVolume, Sine);
            for (int i = 0; i < sampleCount; i++) samples[i] += note[i];
        }
        return samples;
    }

    /// <summary>A low sustained tone plus its perfect fifth, looping cleanly (an integer number of
    /// full cycles fits the clip length so AudioSource.loop does not click at the seam) — a minimal
    /// ambient drone, not a composed track.</summary>
    private static float[] AmbientLoop(float baseFrequency, float durationSeconds, float peakVolume)
    {
        // Snap the duration so both the root and its fifth complete a whole number of cycles —
        // otherwise the waveform's value at sample 0 and at the last sample differ and looping clicks.
        float fifthFrequency = baseFrequency * 1.5f;
        int rootCycles = Mathf.Max(1, Mathf.RoundToInt(durationSeconds * baseFrequency));
        float snappedDuration = rootCycles / baseFrequency;

        int sampleCount = Mathf.Max(1, Mathf.RoundToInt(snappedDuration * SampleRate));
        var samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / SampleRate;
            float root = Sine((t * baseFrequency) % 1f);
            float fifth = Sine((t * fifthFrequency) % 1f);
            samples[i] = (root * 0.7f + fifth * 0.3f) * peakVolume;
        }
        return samples;
    }

    // ------------------------------------------------------------------
    // WAV encoding
    // ------------------------------------------------------------------

    private static void WriteTone(string clipName, float[] samples)
    {
        string path = $"{AudioDir}/{clipName}.wav";
        byte[] wavBytes = EncodeWav(samples, SampleRate);
        File.WriteAllBytes(path, wavBytes);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
    }

    /// <summary>Minimal mono 16-bit PCM WAV: the standard 44-byte RIFF/WAVE/fmt/data header
    /// (see the canonical WAV spec) followed by little-endian 16-bit samples.</summary>
    private static byte[] EncodeWav(float[] samples, int sampleRate)
    {
        const int bitsPerSample = 16;
        const int channels = 1;
        int byteRate = sampleRate * channels * bitsPerSample / 8;
        int blockAlign = channels * bitsPerSample / 8;
        int dataSize = samples.Length * blockAlign;

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        // Raw ASCII bytes, not writer.Write(char[]) — BinaryWriter's char overload runs through the
        // stream's text encoding, which is the wrong tool for a fixed binary tag and not guaranteed
        // to produce exactly 4 bytes for every encoding.
        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataSize);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));

        writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16); // fmt chunk size
        writer.Write((short)1); // PCM
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write((short)blockAlign);
        writer.Write((short)bitsPerSample);

        writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        writer.Write(dataSize);
        foreach (float sample in samples)
        {
            short pcm = (short)Mathf.Clamp(Mathf.RoundToInt(sample * short.MaxValue), short.MinValue, short.MaxValue);
            writer.Write(pcm);
        }

        return stream.ToArray();
    }
}
