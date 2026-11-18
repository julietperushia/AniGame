# [ANIGAME]

> A fan-made, non-commercial card game built with **Unity**, inspired by the Discord game **Anigame**.

---

## ⚠️ Disclaimer

**This project was created purely for entertainment and personal learning.**

- Its gameplay is **inspired by** **Anigame**, a card game on Discord. This project is **not affiliated with, endorsed by, sponsored by, or connected to Anigame** or its developers in any way.
- This project is a standalone Unity game. It is **not** a Discord bot and does not run on Discord.
- All trademarks, names and references to Anigame belong to their respective owners.
- This project is **strictly non-commercial**. It does **not** sell, trade or accept real money for any in-game items, currency, cards or accounts, and contains no in-app purchases or ads.
- In-game currencies (Gold, Diamonds) and items **have no real-world value** and cannot be bought, sold or exchanged for real money or goods.

See [DISCLAIMER.md](DISCLAIMER.md) for the full statement.

---

## 📖 About

Instead of collecting many characters, each player builds and upgrades **one single card** throughout the game. Players climb floors to earn resources, open packs, fuse cards to raise rarity, and tune their build with stats, elements and talents.

### Core features

| System | Summary |
| --- | --- |
| **Rarity** | 9 tiers: Common → Uncommon → Rare → Super Rare → Ultra Rare → Epic → Legendary → Mythic → God. Each tier adds +5% stats (additive, capped at +40%). |
| **Stats** | HP, ATK, DEF, SPD. Each stat rolls independently within ±5 (Common), widening by 2 per tier up to ±21 (God). Rerolled with Gold. |
| **Fusion** | 1 main card + 2 fodder cards of the same rarity + Fusion Orbs. A failed fusion loses the 2 fodder cards; the main card is kept. Success rates decrease at higher tiers. |
| **Elements** | Fire > Tree > Rock > Lightning > Water > Fire, Dark ⇄ Light, plus Neutral and Non-Neutral. Rerolled with Element Orbs. |
| **Talents** | One passive per card, scaling with rarity. Rerolled with Talent Orbs. |
| **Gacha** | Gold packs and Diamond packs. Each pack gives 5 items, with one guaranteed card of the pack's rarity. Diamonds drop only from Gold packs. |
| **Build slots** | Save up to 3 builds and pick one before each fight. |
| **Card lock** | Locked cards cannot be used as fusion fodder. |

---

## 🛠️ Requirements

- **Unity** 6000.2.15f1 (installed via Unity Hub)
- Target platform(s): Windows

---

## 🚀 Getting Started

1. Clone the repository:
   ```bash
   git clone https://github.com/julietperushia/AniGame.git
   ```
2. Open **Unity Hub** → **Add** → select the cloned folder.
3. Open the project with Unity [UNITY_VERSION].
4. Open the main scene at `[Assets/Scenes/MainScene.unity]`.
5. Press **Play** to run in the Editor.

### Building

1. Go to **File → Build Settings**.
2. Choose your target platform and click **Switch Platform** if needed.
3. Click **Build** and choose an output folder.

---

## 📁 Project Structure

```
Assets/
├── Scenes/        # Game scenes
├── Scripts/       # C# gameplay code
├── Prefabs/       # Reusable game objects
├── Art/           # Sprites, UI and other visuals
└── Data/          # ScriptableObjects (rarity, talents, packs, floors)
```

*(Adjust to match your actual folder layout.)*

---

## 📜 Legal

- [Terms of Use](TERMS_OF_USE.md)
- [Privacy Policy](PRIVACY_POLICY.md)
- [Disclaimer](DISCLAIMER.md)
- [License](LICENSE.md): non-commercial use only

---

## 🤝 Contributing

Contributions, bug reports and suggestions are welcome. Please open an issue or pull request. By contributing, you agree that your contributions fall under this project's [License](LICENSE).

---

## 📬 Contact

For questions, takedown requests or concerns: **[CONTACT]**

If you represent Anigame and have any concerns about this project, please reach out and they will be addressed promptly.
