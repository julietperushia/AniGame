using UnityEngine;

/// <summary>
/// Player settings that are applied when the game starts (Settings panel in the main menu).
/// Sound volumes live in the save (exported with it); quality and full screen are per PC (PlayerPrefs).
/// </summary>
public static class GameSettings
{
    public const string QualityKey = "AniGame.Quality";
    public const string FullScreenKey = "AniGame.FullScreen";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ApplyOnStart()
    {
        int q = PlayerPrefs.GetInt(QualityKey, -1);
        if (q >= 0 && q < QualitySettings.names.Length && q != QualitySettings.GetQualityLevel()) QualitySettings.SetQualityLevel(q, true);
        if (!Application.isEditor && PlayerPrefs.HasKey(FullScreenKey)) Screen.fullScreen = PlayerPrefs.GetInt(FullScreenKey, 1) == 1;
    }

    public static float SfxVolume
    {
        get => PlayerProfile.Data.sfxVolume;
        set { PlayerProfile.Data.sfxVolume = Mathf.Clamp01(value); SfxSynth.Volume = PlayerProfile.Data.sfxVolume; }
    }

    public static float MusicVolume
    {
        get => PlayerProfile.Data.musicVolume;
        set => PlayerProfile.Data.musicVolume = Mathf.Clamp01(value);
    }

    public static int Quality
    {
        get => QualitySettings.GetQualityLevel();
        set
        {
            int q = Mathf.Clamp(value, 0, QualitySettings.names.Length - 1);
            QualitySettings.SetQualityLevel(q, true);
            PlayerPrefs.SetInt(QualityKey, q);
            PlayerPrefs.Save();
        }
    }

    public static bool FullScreen
    {
        get => Screen.fullScreen;
        set
        {
            Screen.fullScreen = value;
            PlayerPrefs.SetInt(FullScreenKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
