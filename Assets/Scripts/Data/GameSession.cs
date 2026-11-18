using UnityEngine.SceneManagement;

public static class SceneNames
{
    public const string MainMenu = "MainMenu";
    public const string LocationMap = "LocationMap";
    public const string Collection = "Collection"; // "My Cards": builds, rerolls, fusion
    public const string Gacha = "Gacha";
    public const string Shop = "Shop";
    public const string Battle = "Battle";
    public const string Character = "Character"; // create / edit the player's character
    public const string Buffs = "Buffs";         // permanent buffs
    public const string Library = "Library";     // searchable guide
}

/// <summary>Data passed between scenes (which floor to fight, etc.).</summary>
public static class GameSession
{
    public static bool HasPendingBattle;
    public static int PendingLocation = 1;
    public static int PendingFloor = 1;

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

    public static void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }
}
