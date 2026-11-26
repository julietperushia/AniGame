using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Background music from UITheme (menuMusic / battleMusic / gachaMusic, provided in Assets/Art/UserArt/Audio).
/// Created automatically, survives scene loads and cross-fades when the track changes. Silent when no clip is set.
/// </summary>
public class MusicPlayer : MonoBehaviour
{
    const float FadeSeconds = 0.8f;
    const float MusicVolume = 0.45f;

    AudioSource a, b;
    AudioSource current;
    static MusicPlayer instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (instance != null) return;
        var go = new GameObject("MusicPlayer");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<MusicPlayer>();
    }

    void Awake()
    {
        a = gameObject.AddComponent<AudioSource>();
        b = gameObject.AddComponent<AudioSource>();
        foreach (var s in new[] { a, b }) { s.loop = true; s.playOnAwake = false; s.volume = 0f; }
        current = a;
        SceneManager.activeSceneChanged += (_, scene) => PlayFor(scene.name);
        PlayFor(SceneManager.GetActiveScene().name);
    }

    void PlayFor(string scene)
    {
        var theme = UITheme.Instance;
        if (theme == null) return;
        AudioClip clip = scene == SceneNames.Battle ? theme.battleMusic : scene == SceneNames.Gacha ? theme.gachaMusic : theme.menuMusic;
        if (clip == null) clip = theme.menuMusic;
        if (current.clip == clip && current.isPlaying) return;
        var next = current == a ? b : a;
        next.clip = clip;
        next.volume = 0f;
        if (clip != null) next.Play();
        current = next;
    }

    void Update()
    {
        float target = MusicVolume * Mathf.Clamp(PlayerProfile.Data.musicVolume / 0.7f, 0f, 1.43f); // Settings > Music (70% = normal)
        float step = Time.unscaledDeltaTime / FadeSeconds;
        var other = current == a ? b : a;
        current.volume = Mathf.MoveTowards(current.volume, current.clip != null ? target : 0f, step * MusicVolume);
        other.volume = Mathf.MoveTowards(other.volume, 0f, step * MusicVolume);
        if (other.isPlaying && other.volume <= 0f) other.Stop();
    }
}
