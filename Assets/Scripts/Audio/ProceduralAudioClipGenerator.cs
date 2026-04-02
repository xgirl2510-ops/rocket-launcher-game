using UnityEngine;

/// <summary>
/// Generates AudioClips from math waveforms — no external audio files needed.
/// All clips are created at 44100 Hz, mono.
/// </summary>
public static class ProceduralAudioClipGenerator
{
    private const int SampleRate = 44100;

    /// <summary>Short noise burst with frequency sweep down — rocket whoosh.</summary>
    public static AudioClip CreateLaunchWhoosh()
    {
        float duration = 0.35f;
        int samples = (int)(SampleRate * duration);
        var clip = AudioClip.Create("LaunchWhoosh", samples, 1, SampleRate, false);
        var data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / samples;
            float envelope = (1f - t) * (1f - t); // fast decay
            float freq = Mathf.Lerp(800f, 100f, t); // sweep down
            float sine = Mathf.Sin(2f * Mathf.PI * freq * t * duration);
            float noise = (Random.value * 2f - 1f) * 0.6f;
            data[i] = (sine * 0.4f + noise) * envelope * 0.5f;
        }

        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>Rocket engine rumble — pure sine harmonics for gapless loop, noise on separate layer.</summary>
    public static AudioClip CreateThrustLoop()
    {
        // Duration chosen so all sine frequencies complete full cycles (LCM-friendly)
        float duration = 1.0f;
        int samples = (int)(SampleRate * duration);
        var clip = AudioClip.Create("ThrustLoop", samples, 1, SampleRate, false);
        var data = new float[samples];

        // All frequencies are integer multiples of 1 Hz (period = 1s) → seamless loop
        for (int i = 0; i < samples; i++)
        {
            float phase = (float)i / SampleRate;

            // Engine harmonics — all integer Hz so they loop perfectly at 1s
            float engine = Mathf.Sin(2f * Mathf.PI * 45f * phase) * 0.40f
                         + Mathf.Sin(2f * Mathf.PI * 90f * phase) * 0.25f
                         + Mathf.Sin(2f * Mathf.PI * 135f * phase) * 0.15f
                         + Mathf.Sin(2f * Mathf.PI * 180f * phase) * 0.08f
                         + Mathf.Sin(2f * Mathf.PI * 22f * phase) * 0.20f; // sub-bass throb

            data[i] = engine * 0.45f;
        }

        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>Heavy explosion — massive bass drop + distorted noise burst.</summary>
    public static AudioClip CreateGroundHit()
    {
        float duration = 0.8f;
        int samples = (int)(SampleRate * duration);
        var clip = AudioClip.Create("GroundExplosion", samples, 1, SampleRate, false);
        var data = new float[samples];

        float prevNoise = 0f;

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / samples;
            float phase = (float)i / SampleRate;

            // Two-stage envelope: instant attack, slow decay
            float envelope = t < 0.02f ? t / 0.02f : Mathf.Exp(-t * 4f);

            // Massive bass — starts 150Hz, drops to sub-bass 25Hz
            float boomFreq = Mathf.Lerp(150f, 25f, Mathf.Sqrt(t));
            float boom = Mathf.Sin(2f * Mathf.PI * boomFreq * phase);
            // Soft-clip distortion on the boom for punch
            boom = Mathf.Clamp(boom * 1.8f, -1f, 1f) * 0.7f;

            // Filtered rumble noise
            float rawNoise = Random.value * 2f - 1f;
            prevNoise += 0.12f * (rawNoise - prevNoise);

            // Initial blast — loud unfiltered noise, fades fast
            float blast = rawNoise * Mathf.Exp(-t * 20f) * 0.6f;
            // Rumble tail
            float rumble = prevNoise * 0.4f;

            data[i] = Mathf.Clamp((boom + blast + rumble) * envelope, -1f, 1f);
        }

        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>Big explosion with metallic shrapnel — target destroyed. Louder than ground hit.</summary>
    public static AudioClip CreateTargetHit()
    {
        float duration = 0.9f;
        int samples = (int)(SampleRate * duration);
        var clip = AudioClip.Create("TargetExplosion", samples, 1, SampleRate, false);
        var data = new float[samples];

        float prevNoise = 0f;

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / samples;
            float phase = (float)i / SampleRate;

            // Two-stage: instant attack, slower decay than ground hit
            float envelope = t < 0.015f ? t / 0.015f : Mathf.Exp(-t * 3.5f);

            // Even deeper boom
            float boomFreq = Mathf.Lerp(180f, 20f, Mathf.Sqrt(t));
            float boom = Mathf.Sin(2f * Mathf.PI * boomFreq * phase);
            boom = Mathf.Clamp(boom * 2f, -1f, 1f) * 0.8f;

            // Metallic shrapnel ring
            float ring = Mathf.Sin(2f * Mathf.PI * 600f * phase) * Mathf.Exp(-t * 7f) * 0.15f
                       + Mathf.Sin(2f * Mathf.PI * 1100f * phase) * Mathf.Exp(-t * 9f) * 0.08f;

            // Blast + rumble
            float rawNoise = Random.value * 2f - 1f;
            prevNoise += 0.15f * (rawNoise - prevNoise);
            float blast = rawNoise * Mathf.Exp(-t * 18f) * 0.7f;
            float rumble = prevNoise * 0.35f;

            data[i] = Mathf.Clamp((boom + ring + blast + rumble) * envelope, -1f, 1f);
        }

        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>3 ascending sine tones — win jingle.</summary>
    public static AudioClip CreateWinJingle()
    {
        float duration = 0.8f;
        int samples = (int)(SampleRate * duration);
        var clip = AudioClip.Create("WinJingle", samples, 1, SampleRate, false);
        var data = new float[samples];

        float[] freqs = { 523f, 659f, 784f, 1047f }; // C5, E5, G5, C6
        int noteSamples = samples / freqs.Length;

        for (int n = 0; n < freqs.Length; n++)
        {
            for (int i = 0; i < noteSamples; i++)
            {
                int idx = n * noteSamples + i;
                if (idx >= samples) break;
                float t = (float)i / noteSamples;
                // Attack-decay envelope per note
                float attack = Mathf.Min(t * 10f, 1f);
                float decay = 1f - Mathf.Pow(t, 2f);
                data[idx] = Mathf.Sin(2f * Mathf.PI * freqs[n] * t) * attack * decay * 0.4f;
            }
        }

        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>Rising pitch sine — slingshot stretch feedback.</summary>
    public static AudioClip CreateStretch()
    {
        float duration = 0.15f;
        int samples = (int)(SampleRate * duration);
        var clip = AudioClip.Create("Stretch", samples, 1, SampleRate, false);
        var data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / samples;
            float freq = Mathf.Lerp(200f, 500f, t);
            float envelope = 1f - t;
            data[i] = Mathf.Sin(2f * Mathf.PI * freq * t * duration) * envelope * 0.3f;
        }

        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>Very short sine blip — button click.</summary>
    public static AudioClip CreateClick()
    {
        float duration = 0.05f;
        int samples = (int)(SampleRate * duration);
        var clip = AudioClip.Create("Click", samples, 1, SampleRate, false);
        var data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / samples;
            float envelope = 1f - t;
            data[i] = Mathf.Sin(2f * Mathf.PI * 1000f * t * duration) * envelope * 0.35f;
        }

        clip.SetData(data, 0);
        return clip;
    }
}
