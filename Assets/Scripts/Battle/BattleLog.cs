using System.Collections.Generic;
using UnityEngine;

public enum BattleEventType
{
    BattleStart,
    RoundStart,
    TurnStart,
    NormalAttack,
    Evaded,
    Damage,
    Heal,
    SkillActivated,
    PassiveTriggered,
    Status,
    TurnSkipped,
    Info,
    BattleEnd
}

/// <summary>
/// One thing that happened in battle. The engine runs a whole turn instantly and records events;
/// the UI then replays them one by one with animations. Each event stores an HP/mana snapshot of
/// both fighters taken right after it happened, so bars stay in sync with the popups.
/// </summary>
public class BattleEvent
{
    public BattleEventType type;
    public CardRuntime actor;
    public CardRuntime target;
    public int amount;
    public DamageKind kind;
    public bool crit;
    public float elementMultiplier = 1f;
    public Talent skill;   // SkillActivated: the talent that was cast
    public bool miss;      // Evaded: a miss (Blind) rather than a dodge
    public string tag;     // optional animation hint (charge, release, haste, counter, hypno)
    public string text;
    public int round;
    /// <summary>3v3: the card of the acting team that did this (null in 1v1).</summary>
    public TeamMember member;

    public int playerHp, playerMaxHp, playerMana, playerMaxMana;
    /// <summary>Burst Barrier shields at the time of the event (drawn on the HP bars).</summary>
    public int playerShield, enemyShield;
    public int enemyHp, enemyMaxHp, enemyMana, enemyMaxMana;
}

/// <summary>
/// Static sink used by CardRuntime, StatusEffects and talents to report what happened.
/// While a BattleEngine is running a step it is "attached" and events are recorded;
/// otherwise messages just go to the Unity console.
/// </summary>
public static class BattleLog
{
    /// <summary>Mirror battle messages to the Unity console.</summary>
    public static bool MirrorToConsole = false;

    static BattleEngine current;
    public static BattleEngine Current => current;

    internal static void Attach(BattleEngine engine) { current = engine; }
    internal static void Detach(BattleEngine engine) { if (current == engine) current = null; }

    public static void Damaged(CardRuntime target, int amount, DamageKind kind)
    {
        if (current == null) return;
        var e = current.Emit(BattleEventType.Damage, current.CurrentActor, target);
        e.amount = amount;
        e.kind = kind;
        e.crit = kind == DamageKind.Critical;
        e.text = $"{target.DisplayName} took {amount} {KindLabel(kind)}damage";
        Mirror(e.text);
    }

    public static void Healed(CardRuntime target, int amount)
    {
        if (amount <= 0 || current == null) return;
        var e = current.Emit(BattleEventType.Heal, current.CurrentActor, target);
        e.amount = amount;
        e.text = $"<color=#5DFFB0>{target.DisplayName} healed {amount} HP</color>";
        Mirror(e.text);
    }

    /// <summary>General message (replacement for Debug.Log inside talents).</summary>
    public static void Info(string message)
    {
        if (current == null) { if (MirrorToConsole) Debug.Log(message); return; }
        var e = current.Emit(BattleEventType.Info, current.CurrentActor, null);
        e.text = message;
        Mirror(message);
    }

    /// <summary>A passive talent triggered (UI shows a small banner over the owner).</summary>
    public static void Passive(CardRuntime owner, string message)
    {
        if (current == null) { if (MirrorToConsole) Debug.Log(message); return; }
        var e = current.Emit(BattleEventType.PassiveTriggered, owner, null);
        e.text = message;
        Mirror(message);
    }

    /// <summary>A status effect was applied / ticked / expired.</summary>
    public static void Status(CardRuntime target, string message)
    {
        if (current == null) { if (MirrorToConsole) Debug.Log(message); return; }
        var e = current.Emit(BattleEventType.Status, current.CurrentActor, target);
        e.text = message;
        Mirror(message);
    }

    static string KindLabel(DamageKind kind)
    {
        switch (kind)
        {
            case DamageKind.Critical: return "critical ";
            case DamageKind.True: return "true ";
            case DamageKind.Burn: return "burn ";
            case DamageKind.Poison: return "poison ";
            case DamageKind.Reflect: return "reflected ";
            case DamageKind.Recoil: return "recoil ";
            case DamageKind.Skill: return "skill ";
            default: return "";
        }
    }

    static void Mirror(string message)
    {
        if (MirrorToConsole) Debug.Log(message);
    }
}

/// <summary>
/// Random source for battles. Seedable so a battle can be replayed / simulated deterministically.
/// Talents must use this instead of UnityEngine.Random.
/// </summary>
public static class BattleRandom
{
    static System.Random rng = new System.Random();

    public static void Seed(int seed)
    {
        rng = seed == 0 ? new System.Random() : new System.Random(seed);
    }

    /// <summary>Integer in [minInclusive, maxExclusive).</summary>
    public static int Range(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive) return minInclusive;
        return rng.Next(minInclusive, maxExclusive);
    }

    public static float Value => (float)rng.NextDouble();

    /// <summary>True with the given percent chance (0-100), fractional percents allowed.</summary>
    public static bool Chance(float percent)
    {
        if (percent <= 0f) return false;
        if (percent >= 100f) return true;
        return rng.NextDouble() * 100.0 < percent;
    }

    /// <summary>True with the given percent chance (0-100).</summary>
    public static bool Chance(int percent)
    {
        if (percent <= 0) return false;
        if (percent >= 100) return true;
        return rng.Next(0, 100) < percent;
    }
}
