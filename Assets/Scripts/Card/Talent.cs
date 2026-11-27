using UnityEngine;

/// <summary>
/// Base class for every talent. Talents are ScriptableObjects shared by many cards,
/// so NEVER keep per-battle state in fields: use CardRuntime.Get/SetTalentState instead.
///
/// Every number a talent uses is a public field on its asset. The description is written from those
/// fields (<see cref="DefaultDescription"/>), so changing a number changes the text everywhere
/// (Library, Collection, battle, tooltips) and in the asset itself (autoDescription ON).
/// </summary>
public abstract class Talent : ScriptableObject
{
    [Tooltip("Name shown in the game. Empty = the default name.")]
    public string talentName;

    [Tooltip("ON (recommended): the description is written from this talent's numbers and changes whenever you edit one.\n" +
             "OFF: the text below is your own and does NOT follow the numbers.")]
    public bool autoDescription = true;

    [TextArea(3, 12)]
    [Tooltip("Auto description ON: filled in for you from the numbers (edits are overwritten). OFF: your own text.")]
    public string description;

    public virtual bool IsActive => false;

    /// <summary>Name used when the asset's talentName is empty (and by the setup tool when creating assets).</summary>
    public virtual string DefaultName => GetType().Name;

    /// <summary>Full description written from this talent's fields (without the mana cost line).</summary>
    public virtual string DefaultDescription => "";

    /// <summary>
    /// The automatic description as the Library shows it: base numbers, and after each number that grows with
    /// rarity its God value, as "18% (Max 22%)". Actives end with their mana cost. This is the text stored in the asset.
    /// </summary>
    public string GeneratedDescription => ComposeIn(NumMode.WithMax, 1f);

    /// <summary>The automatic description with the base numbers only (Common rarity, no "Max").</summary>
    public string BaseDescription => ComposeIn(NumMode.Base, 1f);

    /// <summary>Library text: the automatic description with "(Max ...)", or the custom text when autoDescription is off.</summary>
    public string Description => autoDescription || string.IsNullOrEmpty(description) ? GeneratedDescription : description;

    /// <summary>A card's own text: every number that grows with rarity at that talent power (what it really does in battle).</summary>
    public string DescriptionFor(float talentPower) =>
        autoDescription || string.IsNullOrEmpty(description) ? ComposeIn(NumMode.AtPower, Mathf.Max(0.1f, talentPower)) : description;

    /// <summary>A card's own text at its rarity (see <see cref="DescriptionFor(float)"/>).</summary>
    public string DescriptionFor(CardRarity rarity) => DescriptionFor(GameRules.TalentPower(GameRules.Tier(rarity)));

    /// <summary>True when the stored text matches the numbers (always true while autoDescription is on and saved).</summary>
    public bool DescriptionInSync => autoDescription && description == GeneratedDescription;

    string Compose()
    {
        string d = DefaultDescription ?? "";
        if (this is ActiveTalent a) d += (d.Length > 0 ? " " : "") + $"Mana cost: {a.BaseCost}.";
        return d;
    }

    // ---- numbers that grow with rarity, written by the helpers below
    enum NumMode { Base, WithMax, AtPower }
    static NumMode numMode = NumMode.WithMax;
    static float numPower = 1f;

    string ComposeIn(NumMode mode, float power)
    {
        var m = numMode;
        var p = numPower;
        numMode = mode;
        numPower = power;
        try { return Compose(); }
        finally { numMode = m; numPower = p; }
    }

    static string ScaledText(float v, System.Func<float, string> fmt, bool round, float cap)
    {
        float At(float power)
        {
            float x = Mathf.Min(cap, v * power);
            return round ? Mathf.RoundToInt(x) : x;
        }
        switch (numMode)
        {
            case NumMode.AtPower: return fmt(At(numPower));
            case NumMode.WithMax:
                string b = fmt(v), m = fmt(At(GameRules.TalentPower(GameRules.MaxTier)));
                return b == m ? b : $"{b} (Max {m})";
            default: return fmt(v);
        }
    }

    public string DisplayName => string.IsNullOrEmpty(talentName) ? DefaultName : talentName;

    /// <summary>Keeps the stored description in step with the numbers (editor: runs after every Inspector change).</summary>
    protected virtual void OnValidate()
    {
        if (autoDescription)
        {
            string g = GeneratedDescription;
            if (description != g) description = g;
        }
    }

    /// <summary>Writes the generated text into the asset. Returns true if it changed.</summary>
    public bool SyncDescription()
    {
        if (!autoDescription) return false;
        string g = GeneratedDescription;
        if (description == g) return false;
        description = g;
        return true;
    }

    /// <summary>Rarity scaling of this talent for its owner: 1.0 (Common) to 1.2 (God). Every number a talent uses goes through it.</summary>
    protected static float Power(CardRuntime self) => self != null ? Mathf.Max(0.1f, self.talentPower) : 1f;
    protected static int Scaled(int value, CardRuntime self) => Mathf.RoundToInt(value * Power(self));
    protected static float Scaled(float value, CardRuntime self) => value * Power(self);

    // ---- description helpers
    /// <summary>0.15 -> "15%", 0.075 -> "7.5%".</summary>
    protected static string P(float fraction) => $"{fraction * 100f:0.##}%";
    /// <summary>"1 turn" / "3 turns".</summary>
    protected static string N(int n, string word) => n == 1 ? $"1 {word}" : $"{n} {word}s";

    // Numbers that GROW WITH RARITY go through these, so the Library shows "18% (Max 22%)" and a card shows
    // its own value. Use the one that matches how the code scales the number (Scaled(int) rounds, Scaled(float) doesn't).
    /// <summary>Whole-number % from Scaled(int): "18%" / "18% (Max 22%)".</summary>
    protected static string Pct(int v) => ScaledText(v, x => $"{x:0.##}%", true, float.MaxValue);
    /// <summary>Fraction shown as % from Scaled(float): 0.15 -> "15%" / "15% (Max 18%)". cap = the code's upper limit.</summary>
    protected static string PctF(float fraction, float cap = float.MaxValue) => ScaledText(fraction, x => $"{x * 100f:0.##}%", false, cap);
    /// <summary>Plain whole number from Scaled(int): "17" / "17 (Max 20)".</summary>
    protected static string Num(int v) => ScaledText(v, x => $"{x:0.##}", true, float.MaxValue);
    /// <summary>A scaled float rounded to a whole number: "60" / "60 (Max 72)".</summary>
    protected static string NumR(float v) => ScaledText(v, x => $"{x:0}", true, float.MaxValue);
    /// <summary>A scaled float: "3" / "3 (Max 3.6)".</summary>
    protected static string Dec(float v) => ScaledText(v, x => $"{x:0.##}", false, float.MaxValue);
    /// <summary>A scaled multiplier: "2x" / "2x (Max 2.4x)".</summary>
    protected static string Mul(float v) => ScaledText(v, x => $"{x:0.##}x", false, float.MaxValue);
    /// <summary>Turns from Scaled(int): "3 turns" / "3 turns (Max 4 turns)".</summary>
    protected static string TurnsS(int v) => ScaledText(v, x => Mathf.RoundToInt(x) == 1 ? "1 turn" : $"{x:0} turns", true, float.MaxValue);

    // Called when battle starts
    public virtual void OnBattleStart(CardRuntime self, CardRuntime enemy) { }

    /// <summary>The card got this talent during the battle from another talent (copied by Mimic, drawn by Gamble...).</summary>
    public virtual void OnLearnedInBattle(CardRuntime self, CardRuntime enemy) { }

    // Called before attack
    public virtual void OnBeforeAttack(CardRuntime self, CardRuntime enemy) { }

    // Called after attack
    public virtual void OnAfterAttack(CardRuntime self, CardRuntime enemy) { }

    // Called when taking damage from a normal attack
    public virtual void OnTakeDamage(CardRuntime self, ref int damage) { }

    // Called every turn (after status effects, before acting)
    public virtual void OnTurnStart(CardRuntime self, CardRuntime enemy) { }

    /// <summary>After every normal attack in the battle, by either fighter.</summary>
    public virtual void OnAnyAttack(CardRuntime self, CardRuntime enemy, CardRuntime attacker) { }

    /// <summary>After the enemy casts an active talent.</summary>
    public virtual void OnEnemySkill(CardRuntime self, CardRuntime enemy) { }

    /// <summary>This card evaded a normal attack.</summary>
    public virtual void OnEvade(CardRuntime self, CardRuntime attacker) { }

    /// <summary>This card's normal attack was evaded or missed.</summary>
    public virtual void OnAttackMissed(CardRuntime self, CardRuntime enemy) { }

    /// <summary>A normal attack hit this card (after the damage was taken).</summary>
    public virtual void OnHitTaken(CardRuntime self, CardRuntime attacker, int damage, bool crit) { }

    /// <summary>Category shown in the Library filter.</summary>
    public TalentCategory Category => TalentCatalog.CategoryOf(this);
}

public abstract class PassiveTalent : Talent
{
    public override bool IsActive => false;
}

public abstract class ActiveTalent : Talent
{
    public override bool IsActive => true;

    [Tooltip("Mana cost. 1v1: the talent fires when the mana bar is full, so the real cost is max(manaCost, max Mana). 3v3: the team pays exactly this from its shared pool.")]
    public int manaCost = 100;

    /// <summary>Anigame rule: an active talent fires when the card's mana bar is full.</summary>
    public virtual int GetManaCost(CardRuntime self) => Mathf.Max(manaCost, self.maxMana);

    public abstract void Activate(CardRuntime self, CardRuntime enemy);

    /// <summary>Mana this talent says it costs. 3v3 teams pay exactly this from the shared pool.</summary>
    public virtual int BaseCost => manaCost;

    /// <summary>Extra condition to cast (checked before mana is spent). Default: always.</summary>
    public virtual bool CanActivate(CardRuntime self, CardRuntime enemy) => true;
}
