# Changelog

Newest first. From now on, add an entry under **Unreleased** with every meaningful change. The format is loosely
[Keep a Changelog](https://keepachangelog.com/): *Added / Changed / Fixed / Removed*.

Entries dated before the initial commit are **reconstructed** from the names of the `_Backup_before_*` folders
and from dated comments in the code. They are not a full record.

## Unreleased (working tree, 2026-11-26)

### Added
- **Boss** (main menu, unlocked with 1 God card): one location with many gates fought in order; your 3 build slots
  against one boss that acts 3 times a round. Bosses give God Orbs, materials and gold (replays 25%). Test Mode >
  BOSS EDITOR adds/orders gates, picks each boss's character (name and picture) and sets its stats, talents and
  rewards (`Assets/Resources/Config/bosses.json`). Bridge `bosssim` tests every gate.
- **Minigames** (main menu): Flappy Hero, Anime Quiz, Lucky 777, Roulette, Higher or Lower and Memory Match, with one
  shared 5-minute cooldown. Test Mode > MINIGAME PRIZES sets the cooldown and every prize line
  (`Assets/Resources/Config/minigames.json`).
- **Hidden Location** (3v3 mode, 20 LOCs × 3 floors, unlocked with 3 Mythic+ cards) with its own screens
  (`HiddenLocationScreen`, `TeamBattleScreen`), AniList-based enemy data and a Hidden Location simulator.
- **1v1 Talent Matrix** (every talent vs every talent) in Test Mode and *Tools > AniGame*; dev bridge commands
  `talentmatrix`, `matrixtry`, `floorcheck`, `hiddensim`, `hiddengen`, `hiddentest`.
- **Healing fatigue:** after round 10 all healing received drops 5% per round (`GameBalance.asset`).
- Barriers are drawn on the HP bars (light-blue part + "+N").
- `Docs/talents.csv`: every talent with its description and numbers.
- **Floor simulator** (`Battle/FloorSim.cs`, bridge `floorsim[:battlesPerFloor][|fromLoc-toLoc][|max]`): every talent alone
  fights every campaign floor of a LOC range on the 1v1 matrix card (or a max God card with `max`), in the background.
  CSVs in `Logs/AniGameBridge/FloorSim` (summary, by LOC, floors, lost floors, worst-floor logs).
- **Card editor:** clicking a card's name in the collection opens EDIT CARD: its own name *and* picture (anime gallery
  or a PNG/JPG from the PC, path or URL). Each saved copy keeps its own; the Character screen only changes what a
  card hasn't been given itself. Imported card pictures are stored in `persistentDataPath/pictures` and travel in
  save exports.
- **Card Pack prizes:** a minigame prize can be a Common, Rare, Super Rare or Ultra Rare Pack, opened for free at once
  (the result shows what came out). The default prize tables now hand out packs.
- **Boss editor pictures:** CHANGE PICTURE gives a boss its own picture (gallery or imported, saved in
  `Assets/Resources/Config/Pictures`), and + ADD CHARACTER in the character picker makes a reusable boss character
  (name, anime, element, picture), stored in `bosses.json`.
- Battle log: normal attacks and element-scaled skills say how well they hit — *SUPER EFFECTIVE!* (+25%),
  *slightly effective* (+12.5%), *not very effective* (−12.5%), *barely effective...* (−25%) — with the multiplier.
  Neutral matchups add nothing.

### Changed
- Talents: Blaze / Poison ticks can miss and a miss removes every stack; Leech heals only the damage dealt;
  Burst Barrier halves Mana Regen while up; Overload keeps decaying below base; Death's Dance hits clear 10%;
  Dexterity Drive gains SPD each cast; Sleep isn't broken by attacks (−30% hits, caster Mana Regen −50%);
  Celestial Blessing fires at its own cost; Rot gains ATK from the healing it stops; **Silence is now a passive**
  (stops the enemy's Mana Regen after it casts); Nothingness punishes every copy; Hex gains the ATK/DEF gap;
  Blackhole takes 30% of what it swallows and halves its owner's gains.
- Talents: Regeneration ticks can fail (10% +10% per extra stack) and lose a stack; Slayer uses the enemy's current
  HP; Counter Stance can evade and counter skill damage; Assassinate replaces that turn's normal attack; Frostbite's
  Freeze chance grows as the enemy's SPD drops (up to 60%).
- Player-facing text no longer mentions test or balance settings outside Settings and Test Mode.
- [talents.md](../design/talents.md) and [combat.md](../design/combat.md) rewritten for the current 74 talents.

### Fixed
- A card with several actives no longer gets stuck when one of them can't be cast any more (Soul Swap).
- Burst Barrier: damage beyond the barrier goes to the HP before the explosion, and the battle screen says
  "BLOCKED" only when the whole hit is held.

## Earlier unreleased work (2026-11-24)

### Added
- `Assets/Scripts/UI/Fx/` animation module: `UIFx` effects (sparks, confetti, slashes, flashes, shakes, slow
  motion…), `ScreenFader` scene transitions, `ScreenIntroRunner` panel intros, and the `FxBob`, `FxBreathe`,
  `FxSpin`, `FxShaker`, `FxTween`, `FxParticles` components.
- `TooltipTrigger.cs`: the hover-tooltip component, moved out of `UITooltip.cs` into its own file.
- New synthesised sound effects: `whoosh`, `swoosh`, `impact`, `slash`, `charge`, `sparkle`, `fail`, `coin`.
- Project documentation: `README.md`, `CLAUDE.md`, and `Docs/` (GDD, design specs, screens, art, technical
  docs, roadmap, this changelog).

### Changed
- Large battle screen update (~400 lines) and card view update (~220 lines) for the new effects.
- Collection, Gacha, Buffs, Shop, Main Menu, Location Map, Character and Library screens updated for effects
  and tooltips. All scenes rebuilt.
- `GameSession.LoadScene` now goes through `ScreenFader` instead of loading scenes directly.
- Dev Bridge: more commands for testing.

## 2026-11-18: Initial commit (`53e815d`)

The first git commit. It contains the whole game as described in these docs, including the four backup
folders below.

## 2026-11-15: Permanent buffs, stamina rules, single-character rule, UI pass 2

Reconstructed from `_Backup_before_buffs_2026-11-15`, `_Backup_before_ui2_2026-11-15`, and comments dated
15 Nov 2026.

- Added the 22 permanent buffs and their mixed gold/diamond prices (`Buffs.cs:18`).
- Added stamina: max 80, 5 per fight, +1 every 5 minutes, +2 on a first clear, refill when a location
  unlocks (`GameRules.cs:70`).
- Changed: the player's own character became the only character you can collect (`PlayerProfile.cs:17`).
- A second UI pass ("ui2").

## 2026-11-14: Single upgradable card system

Reconstructed from `_Backup_before_single_card_2026-11-14`.

- Moved to the "1 upgradable card" design: one base of 100 for every stat, rarity multipliers, rolls,
  fusion with fodder, rerolls.

## 2026-11-13: Before AI-assisted development

Reconstructed from `_Backup_before_claude_2026-11-13`: the scripts and `SampleScene` as they were before
AI-assisted work began.
