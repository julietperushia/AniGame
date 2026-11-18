using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Procedural sound effects (no audio files needed). Clips are synthesised once on first use.
/// Replace with real AudioClips later by assigning them in <see cref="overrides"/>.
/// Ids: click, swing, hit, crit, miss, heal, skill, buff, burn, poison, stun, win, lose.
/// </summary>
public class SfxSynth : MonoBehaviour
{
    const int SampleRate = 44100;

    [System.Serializable]
    public class ClipOverride
    {
        public string id;
        public AudioClip clip;
    }

    public List<ClipOverride> overrides = new List<ClipOverride>();

    static SfxSynth instance;
    AudioSource source;
    readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();

    public static float Volume = 0.7f;

    static SfxSynth Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<SfxSynth>();
                if (instance == null)
                {
                    var go = new GameObject("SFX");
                    instance = go.AddComponent<SfxSynth>();
                }
            }
            return instance;
        }
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        foreach (var o in overrides)
            if (o != null && o.clip != null && !string.IsNullOrEmpty(o.id)) clips[o.id] = o.clip;
    }

    public static void Play(string id, float volume = 1f, float pitch = 1f)
    {
        if (!Application.isPlaying || Volume <= 0f) return;
        var s = Instance;
        if (s == null || s.source == null) return;
        var clip = s.GetClip(id);
        if (clip == null) return;
        s.source.pitch = pitch;
        s.source.PlayOneShot(clip, Mathf.Clamp01(volume * Volume));
    }

    AudioClip GetClip(string id)
    {
        if (clips.TryGetValue(id, out AudioClip clip)) return clip;
        float[] data = Generate(id);
        if (data == null) return null;
        clip = AudioClip.Create("sfx_" + id, data.Length, 1, SampleRate, false);
        clip.SetData(data, 0);
        clips[id] = clip;
        return clip;
    }

    // ---------------------------------------------------------------- synthesis

    static float[] Generate(string id)
    {
        switch (id)
        {
            case "click": return Tone(0.05f, t => Sine(t, 1400f) * Env(t, 0.05f, 0.002f));
            case "swing": return Tone(0.16f, t => Noise() * Env(t, 0.16f, 0.03f) * 0.35f * Mathf.Sin(Mathf.PI * t / 0.16f));
            case "hit": return Tone(0.18f, t => (Noise() * 0.6f + Sine(t, Mathf.Lerp(160f, 60f, t / 0.18f)) * 0.8f) * Env(t, 0.18f, 0.002f));
            case "crit": return Tone(0.32f, t => (Noise() * 0.5f + Sine(t, Mathf.Lerp(220f, 70f, t / 0.32f)) * 0.8f + Sine(t, 1320f) * 0.35f) * Env(t, 0.32f, 0.002f));
            case "miss": return Tone(0.22f, t => Noise() * 0.3f * Mathf.Sin(Mathf.PI * t / 0.22f) * (0.5f + 0.5f * Sine(t, 30f)));
            case "heal": return Arpeggio(new[] { 523.25f, 659.25f, 783.99f }, 0.09f, 0.35f);
            case "buff": return Arpeggio(new[] { 440f, 587.33f }, 0.07f, 0.25f);
            case "skill": return Tone(0.55f, t => (Sine(t, Mathf.Lerp(300f, 1200f, t / 0.55f) + 8f * Mathf.Sin(t * 40f)) * 0.6f + Sine(t, 600f) * 0.2f) * Env(t, 0.55f, 0.02f));
            case "burn": return Tone(0.3f, t => Noise() * 0.45f * Env(t, 0.3f, 0.01f) * (0.6f + 0.4f * Sine(t, 18f)));
            case "poison": return Tone(0.3f, t => Sine(t, 180f + 40f * Mathf.Sin(t * 50f)) * 0.5f * Env(t, 0.3f, 0.01f));
            case "stun": return Tone(0.35f, t => Square(t, 880f - 400f * t) * 0.25f * Env(t, 0.35f, 0.005f));
            case "win": return Arpeggio(new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.12f, 0.6f);
            case "lose": return Arpeggio(new[] { 392f, 349.23f, 311.13f, 261.63f }, 0.16f, 0.7f);
        }
        return null;
    }

    delegate float Wave(float t);

    static float[] Tone(float seconds, Wave wave)
    {
        int n = Mathf.CeilToInt(seconds * SampleRate);
        var data = new float[n];
        for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(wave((float)i / SampleRate), -1f, 1f) * 0.8f;
        return data;
    }

    static float[] Arpeggio(float[] notes, float step, float tail)
    {
        float total = step * (notes.Length - 1) + tail;
        return Tone(total, t =>
        {
            float v = 0f;
            for (int i = 0; i < notes.Length; i++)
            {
                float local = t - i * step;
                if (local < 0f) continue;
                v += (Sine(local, notes[i]) * 0.7f + Sine(local, notes[i] * 2f) * 0.15f) * Env(local, tail, 0.005f);
            }
            return v * 0.45f;
        });
    }

    static readonly System.Random noise = new System.Random(1234);
    static float Noise() => (float)(noise.NextDouble() * 2.0 - 1.0);
    static float Sine(float t, float freq) => Mathf.Sin(2f * Mathf.PI * freq * t);
    static float Square(float t, float freq) => Sine(t, freq) >= 0f ? 1f : -1f;

    /// <summary>Attack/decay envelope.</summary>
    static float Env(float t, float length, float attack)
    {
        if (t < attack) return t / attack;
        float k = Mathf.Clamp01((t - attack) / Mathf.Max(0.0001f, length - attack));
        return (1f - k) * (1f - k);
    }
}
