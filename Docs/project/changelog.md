# Changelog

Newest first. From now on, add an entry under **Unreleased** with every meaningful change. The format is loosely
[Keep a Changelog](https://keepachangelog.com/): *Added / Changed / Fixed / Removed*.

Entries dated before the initial commit are **reconstructed** from the names of the `_Backup_before_*` folders
and from dated comments in the code. They are not a full record.

## Unreleased (working tree, 2026-11-24)

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
