using UnityEngine.SceneManagement;

public static class SceneNames
{
    public const string MainMenu = "MainMenu";
    public const string LocationMap = "LocationMap";
    public const string Collection = "Collection"; // "Collection": builds, rerolls, fusion
    public const string Gacha = "Gacha";
    public const string Shop = "Shop";
    public const string Battle = "Battle";
    public const string Character = "Character"; // create / edit the player's character
    public const string Buffs = "Buffs";         // permanent buffs
    public const string Library = "Library";     // searchable guide
    public const string HiddenLocation = "HiddenLocation"; // 3v3 mode: 20 LOCs x 3 floors
    public const string TeamBattle = "TeamBattle";         // 3v3 battle
}

/// <summary>Data passed between scenes (which floor to fight, etc.).</summary>
public static class GameSession
{
    public static bool HasPendingBattle;
    public static int PendingLocation = 1;
    public static int PendingFloor = 1;

    /// <summary>Test battles (editor bridge "testbattle"): talent names separated by commas; null = normal.</summary>
    public static string TestPlayerTalents;
    public static string TestEnemyTalents;

    /// <summary>Location the map should open on.</summary>
    public static int SelectedLocation = 0;

    public static void StartBattle(int location, int floor)
    {
        HasPendingBattle = true;
        PendingLocation = location;
        PendingFloor = floor;
        SelectedLocation = location;
        LoadScene(SceneNames.Battle);
    }

    // ---- Hidden Location (3v3)
    public static bool HasPendingHiddenBattle;
    public static int PendingHiddenLocation = 1;
    public static int PendingHiddenFloor = 1;
    /// <summary>Hidden Location LOC the map should open on (0 = the next floor to beat).</summary>
    public static int SelectedHiddenLocation;

    public static void StartHiddenBattle(int location, int floor)
    {
        HasPendingHiddenBattle = true;
        PendingHiddenLocation = location;
        PendingHiddenFloor = floor;
        SelectedHiddenLocation = location;
        LoadScene(SceneNames.TeamBattle);
    }

    /// <summary>Loads a screen behind the animated transition (ScreenFader).</summary>
    public static void LoadScene(string sceneName)
    {
        ScreenFader.Load(sceneName);
    }
}
