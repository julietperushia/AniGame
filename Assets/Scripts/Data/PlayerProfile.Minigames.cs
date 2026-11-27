using UnityEngine;

/// <summary>Minigames: one shared cooldown (it starts when a game starts) and prizes.</summary>
public static partial class PlayerProfile
{
    public static int CooldownSeconds => Mathf.Max(0, MinigameConfig.Current.cooldownMinutes) * 60;

    /// <summary>Seconds until the next minigame can start (0 = ready).</summary>
    public static int MinigameCooldownLeft => (int)System.Math.Min(int.MaxValue, System.Math.Max(0L, Data.minigameReadyAt - Now));

    public static bool MinigameReady => MinigameCooldownLeft <= 0;

    /// <summary>Starts the shared cooldown (called when a game starts, so quitting doesn't skip it).</summary>
    public static void StartMinigameCooldown()
    {
        Data.minigameReadyAt = Now + CooldownSeconds;
        Data.minigamesPlayed++;
        Save();
    }

    /// <summary>Test Mode: play again right away.</summary>
    public static void ResetMinigameCooldown()
    {
        Data.minigameReadyAt = 0;
        Save();
    }

    /// <summary>Pays the best prize line reached with this score. Returns the line (null = no prize) and what was given.</summary>
    public static (PrizeTier tier, Prize given) FinishMinigame(string id, int score)
    {
        var tier = MinigameConfig.Current.TierFor(id, score);
        if (tier == null || tier.prize == null || tier.prize.amount <= 0) return (tier, null);
        return (tier, GivePrize(tier.prize));
    }

    /// <summary>Roulette: pays the prize of the segment it landed on.</summary>
    public static Prize GiveSegmentPrize(string id, int index)
    {
        var g = MinigameConfig.Current.Find(id);
        if (g == null || index < 0 || index >= g.tiers.Count) return null;
        return GivePrize(g.tiers[index].prize);
    }

    public static string CooldownText(int seconds) => seconds <= 0 ? "READY" : $"{seconds / 60}:{seconds % 60:00}";
}
