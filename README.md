# AniGame

A single-player anime card battler made in Unity. You create your own character, then take it through a
campaign of 100 locations filled with anime characters. You don't build a deck: you upgrade **one card**.
Fuse copies of your character to raise its rarity from Common to God, reroll its stats, element and talent,
and buy permanent buffs to push further.

> Full design documentation lives in [`Docs/`](Docs/README.md). Start with the [Game Design Document](Docs/GDD.md).

## Requirements

| What | Version |
|---|---|
| Unity | **6000.2.15f1** (Unity 6.2) |
| Render pipeline | URP 17.2 |
| UI | uGUI (`com.unity.ugui` 2.0) built from code by `UIFactory` |
| Input | Input System 1.16 |
| Platform | Windows (the development platform) |

**Local package warning:** `Packages/manifest.json` references `com.antigravity.ide` at a local path
(`file:D:/exe/Antigravity-IDE-main/...`). On any other machine, remove that line or point it at your own copy,
or Unity will fail to resolve packages.

## Getting started

1. Open the project folder in Unity Hub with 6000.2.15f1.
2. Run **Tools › AniGame › Build Everything**. It is safe to run again and it:
   1. repairs the talent assets,
   2. generates the campaign from `Assets/Data/campaign.csv` (and downloads portraits),
   3. builds `Assets/Resources/CardDatabase.asset`,
   4. generates the UI art and `Assets/Resources/UITheme.asset`,
   5. builds every scene and the build settings.
3. Open the main menu (**Tools › AniGame › Open Main Menu Scene**) and press **Play**.

On first launch the game opens the Character screen so you can name your hero and pick a picture.

## Folder map

| Path | Contents |
|---|---|
| `Assets/Scripts/Rules/` | **All balance numbers** (`GameRules`, `Packs`, `Buffs`) |
| `Assets/Scripts/Battle/` | Pure C# battle engine, damage formula, elements, status effects, battle log |
| `Assets/Scripts/Card/` | Card asset, rarity/element enums, runtime fighter, talent base classes |
| `Assets/Scripts/Talents/` | 25 active and 13 passive talents (one ScriptableObject class each) |
| `Assets/Scripts/Data/` | Save data and player profile, card database, save import/export, character portrait |
| `Assets/Scripts/UI/` | One screen script per scene, shared widgets, `Fx/` animation module |
| `Assets/Scripts/Audio/` | Music player, procedural sound effects |
| `Assets/Editor/` | Setup pipeline, campaign generator, card importer, art generator, Dev Bridge |
| `Assets/Data/` | `campaign.csv` (current campaign sheet), `cards.csv` (legacy sheet) |
| `Assets/Campaign/` | Generated floor cards (`LOC n/Floor k.asset`) and downloaded portraits |
| `Assets/Art/UserArt/` | Drop-in replacement art and audio (see its `README.txt`) |
| `Assets/Resources/` | `CardDatabase.asset`, `UITheme.asset` (loaded at runtime) |
| `Assets/Scenes/` | 9 game scenes (+ the unused `SampleScene`) |
| `Docs/` | Design and technical documentation |
| `_Backup_*` | Snapshots of the scripts made before big changes (13–15 Nov 2026) |

## Everyday tasks

| Task | How |
|---|---|
| Change a balance number | Edit `Assets/Scripts/Rules/GameRules.cs` (or `Packs.cs` / `Buffs.cs`). See [balance docs](Docs/design/) |
| Change the campaign | Edit `Assets/Data/campaign.csv`, then **Tools › AniGame › Campaign › Generate From Sheet**. See [data pipeline](Docs/tech/data-pipeline.md) |
| Check balance | **Tools › AniGame › Simulate Balance** (there is no automated test suite) |
| Find / reset the save | **Tools › AniGame › Save Data › Show Save File / Delete Save** |
| Give yourself gold | **Tools › AniGame › Save Data › Refill Stamina + 10,000 Gold (cheat)** |

Changing `campaign.csv` changes the data version, which **resets existing saves** (the old save is backed up).
See [save format](Docs/tech/save-format.md).
