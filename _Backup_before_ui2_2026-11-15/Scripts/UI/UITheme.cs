using UnityEngine;

/// <summary>
/// All UI art in one asset (Assets/Resources/UITheme.asset), filled by Tools > AniGame > Build Everything.
/// Every slot gets a generated placeholder; drop your own files into Assets/Art/UserArt/... (see the asset list)
/// and re-run "Generate UI Art" to replace them.
/// </summary>
[CreateAssetMenu(fileName = "UITheme", menuName = "AniGame/UI Theme")]
public class UITheme : ScriptableObject
{
    [Header("Basic shapes")]
    public Sprite rounded;      // 9-sliced rounded rectangle (panels, buttons, bars)
    public Sprite roundedSmall; // tighter corners (chips)
    public Sprite circle;
    public Sprite glow;         // soft radial glow
    public Sprite gradient;     // vertical white -> transparent
    public Sprite vignette;     // dark edges

    [Header("Anime background effects")]
    public Sprite sakuraPetal;
    public Sprite sparkle;
    public Sprite halftone;     // tiling dot pattern
    public Sprite lightRays;
    public Sprite speedLines;

    [Header("Card frames (index = rarity: Common ... God)")]
    public Sprite[] rarityFrames = new Sprite[9];
    public Sprite frameShine;   // diagonal light band swept across high-rarity cards

    [Header("Icons")]
    public Sprite[] elementIcons = new Sprite[9]; // index = (int)CardElement
    public Sprite goldIcon;
    public Sprite diamondIcon;
    public Sprite fusionOrbIcon;
    public Sprite elementOrbIcon;
    public Sprite talentOrbIcon;
    public Sprite godOrbIcon;
    public Sprite arrow;        // right-pointing arrow (library charts)

    [Header("Talent + buff icons (names / ids parallel to the sprite arrays)")]
    public string[] talentIconNames = new string[0];
    public Sprite[] talentIcons = new Sprite[0];
    public string[] buffIconIds = new string[0];
    public Sprite[] buffIcons = new Sprite[0];
    public Sprite staminaIcon;
    public Sprite lockIcon;

    [Header("Screen backgrounds (optional, your art)")]
    public Sprite menuBackground;
    public Sprite mapBackground;
    public Sprite battleBackground;
    public Sprite cardsBackground;
    public Sprite gachaBackground;
    public Sprite shopBackground;
    public Sprite logo;

    [Header("Gacha pack art (optional; order = Packs.All)")]
    public Sprite[] packArt = new Sprite[6];

    [Header("Music (optional)")]
    public AudioClip menuMusic;
    public AudioClip battleMusic;
    public AudioClip gachaMusic;

    static UITheme instance;

    public static UITheme Instance
    {
        get
        {
            if (instance == null) instance = Resources.Load<UITheme>("UITheme");
            return instance;
        }
    }

    public Sprite Frame(CardRarity rarity)
    {
        int i = (int)rarity;
        return rarityFrames != null && i >= 0 && i < rarityFrames.Length ? rarityFrames[i] : null;
    }

    public Sprite ElementIcon(CardElement element)
    {
        int i = (int)element;
        return elementIcons != null && i >= 0 && i < elementIcons.Length ? elementIcons[i] : null;
    }

    public Sprite OrbIcon(OrbType type) => type == OrbType.Fusion ? fusionOrbIcon : type == OrbType.Element ? elementOrbIcon : type == OrbType.Talent ? talentOrbIcon : godOrbIcon;

    public static string IconKey(string name) =>
        string.IsNullOrEmpty(name) ? "" : name.Replace(" ", "").Replace("-", "").Replace("_", "").Replace("'", "").ToLowerInvariant();

    public Sprite TalentIcon(string talentName)
    {
        string key = IconKey(talentName);
        if (key.Length == 0 || talentIconNames == null || talentIcons == null) return null;
        for (int i = 0; i < talentIconNames.Length && i < talentIcons.Length; i++)
            if (talentIconNames[i] == key) return talentIcons[i];
        return null;
    }

    public Sprite TalentIcon(Talent talent) => talent != null ? TalentIcon(talent.DisplayName) : null;

    public Sprite BuffIcon(string id)
    {
        if (string.IsNullOrEmpty(id) || buffIconIds == null || buffIcons == null) return null;
        for (int i = 0; i < buffIconIds.Length && i < buffIcons.Length; i++)
            if (buffIconIds[i] == id) return buffIcons[i];
        return null;
    }

    public Sprite PackArt(int index) => packArt != null && index >= 0 && index < packArt.Length ? packArt[index] : null;

    public Sprite Background(string screen)
    {
        switch (screen)
        {
            case "menu": return menuBackground;
            case "map": return mapBackground;
            case "battle": return battleBackground;
            case "cards": return cardsBackground;
            case "gacha": return gachaBackground;
            case "shop": return shopBackground;
        }
        return null;
    }
}
