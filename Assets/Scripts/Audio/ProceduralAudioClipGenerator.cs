using UnityEngine;

namespace RocketLauncher
{
    /// <summary>
    /// Generates AudioClips from math waveforms — no external audio files needed.
    /// All clips are created at 44100 Hz, mono.
    /// </summary>
    public static class ProceduralAudioClipGenerator
    {
        private const int SampleRate = 44100;

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

                float envelope = t < 0.02f ? t / 0.02f : Mathf.Exp(-t * 4f);

                float boomFreq = Mathf.Lerp(150f, 25f, Mathf.Sqrt(t));
                float boom = Mathf.Sin(2f * Mathf.PI * boomFreq * phase);
                boom = Mathf.Clamp(boom * 1.8f, -1f, 1f) * 0.7f;

                float rawNoise = Random.value * 2f - 1f;
                prevNoise += 0.12f * (rawNoise - prevNoise);

                float blast = rawNoise * Mathf.Exp(-t * 20f) * 0.6f;
                float rumble = prevNoise * 0.4f;

                data[i] = Mathf.Clamp((boom + blast + rumble) * envelope, -1f, 1f);
            }

            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>4 ascending sine tones (C5-E5-G5-C6) — win jingle.</summary>
        public static AudioClip CreateWinJingle()
        {
            float duration = 0.8f;
            int samples = (int)(SampleRate * duration);
            var clip = AudioClip.Create("WinJingle", samples, 1, SampleRate, false);
            var data = new float[samples];

            float[] freqs = { 523f, 659f, 784f, 1047f };
            int noteSamples = samples / freqs.Length;

            for (int n = 0; n < freqs.Length; n++)
            {
                for (int i = 0; i < noteSamples; i++)
                {
                    int idx = n * noteSamples + i;
                    if (idx >= samples) break;
                    float t = (float)i / noteSamples;
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
}
