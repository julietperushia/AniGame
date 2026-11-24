# Roadmap and status

Status as of **2026-11-24** (commit `53e815d` + working tree). Update this file when a feature changes state.

## Feature status

| Area | Feature | Status | Notes |
|---|---|---|---|
| Core | Character creation and editing | ✅ Done | Name + imported or gallery portrait |
| Core | Battle engine + animated playback | ✅ Done | Seedable; 1×/2×/4× speed |
| Core | 38 talents, 9 elements, 5 status effects | ✅ Done | |
| Core | Campaign: 100 LOCs, 2,358 floors | ✅ Done (data) | Generated from `campaign.csv` |
| Upgrades | Fusion, auto merge | ✅ Done | |
| Upgrades | Stat / element / talent rerolls with preview | ✅ Done | |
| Upgrades | God Orb, dismantling, locking | ✅ Done | |
| Upgrades | 3 build slots | ✅ Done | Only the active slot fights; relay not wired (see known issues) |
| Economy | Gacha (6 packs), orb shop, 22 buffs, stamina | ✅ Done | |
| Meta | In-game Library (live rules) | ✅ Done | |
| Meta | Signed saves, backups, `.anisave` export/import | ✅ Done | |
| Polish | `UI/Fx` animation module, scene transitions, new SFX | 🟡 In progress | **Not committed yet** (2026-11-24) |
| Polish | Hover tooltips | ✅ Done | `TooltipTrigger` split into its own file in the working tree (not committed yet) |
| Art | Final UI art, frames, icons, logo | 🔴 Placeholder | Procedural placeholders; replace via `Assets/Art/UserArt/` |
| Audio | Music (menu, battle, gacha) | 🔴 Missing | Slots exist, no files |
| Quality | Automated tests | 🔴 Missing | Test Framework installed, no tests |
| Quality | Late-campaign balance pass | 🔴 Not started | See open questions |
| Tech | Keeping saves across campaign sheet changes | 🔴 Not started | Sheet edits currently reset saves |
| Release | Build / release pipeline, platforms | 🔴 Not started | TBD |

## Suggested next steps

In priority order. These are suggestions, not commitments.

1. **Commit the in-progress UI work** (the `UI/Fx/` module, the `TooltipTrigger` split, screen edits) so the docs and the
   code describe the same commit.
2. **Decide how saves survive sheet changes** before anyone has a save worth keeping. For example, key the
   campaign version to the LOC/floor structure instead of the MD5 of the whole file, and keep cards and buffs
   when it changes (the import path already does this).
3. **Late-campaign balance check** (LOC 60–100) with Simulate Balance or a dedicated scenario.
4. **Edit-mode tests** for `GameRules`, `ElementSystem`, `DamageCalculator` and seeded `BattleEngine` fights.
   These are pure C#, so they are easy to test.
5. **Decide on relay battles:** wire the 3 slots into `BattleScreen`, or remove the misleading comment.
6. **Balance-table generator** (deferred from the docs plan): a `Tools › AniGame › Export Balance Tables` menu
   item that regenerates the tables in `Docs/design/` from code, so they can't go stale.
7. **Cleanup** ([below](#cleanup)).
8. **Content and IP decision** before any public build ([GDD §11](../GDD.md#11-content-and-ip-notes)).

## Known issues

Differences found while writing these docs. Each one is checked against the code.

| # | Issue | Where | Severity |
|---|---|---|---|
| 1 | The battle screen's header says team cards fight as a relay, but only the active slot fights | `Assets/Scripts/UI/BattleScreen.cs:9` vs `:113-114` | Low (misleading comment, or a missing feature) |
| 2 | **Any byte change to `campaign.csv` resets every save** (MD5 data version) | `Assets/Editor/CampaignGenerator.cs:126`, `PlayerProfile.cs:160-166` | High once players exist |
| 3 | `Packages/manifest.json` needs a local package at `D:/exe/Antigravity-IDE-main/...` | `Packages/manifest.json` | Medium: the project won't open cleanly on another machine |
| 4 | `SampleScene` is still in the build settings but nothing uses it | `ProjectSettings/EditorBuildSettings.asset` | Low |
| 5 | Talent descriptions show base (Common) numbers on every rarity. Paralysis's failure message shows the base chance, not the scaled one | `Talent.DefaultDescription` overrides, `Talents/Active/Paralysis.cs` | Low |
| 6 | Overload DEF never appears on a campaign enemy | `Assets/Data/campaign.csv` | Low (content) |
| 7 | The save integrity key is a constant in source, so it only deters casual editing | `PlayerProfile.cs:193` | Low for single-player |

## Open design questions

| Question | Context |
|---|---|
| Who is the audience, which platforms, and is there monetisation? | GDD §9–10 are TBD |
| Is the late campaign winnable as intended? | Past ~LOC 72, enemy stat totals beat the best possible player card ([campaign.md](../design/campaign.md#difficulty-curve)) |
| Is the diamond grind intended? | Diamonds come only from gold packs: ~42,000 gold per 100 diamonds at best, so New Generation (3,000 diamonds) costs about **1.25 million gold** of Super Rare packs ([economy](../design/economy-and-stamina.md#diamond-income)) |
| Relay battles with the 3 slots? | Known issue 1 |
| Should "both fighters fall" count as a win, and a timeout as a loss? | Current behaviour (`BattleScreen.cs:209`) |
| Poison: should the miss chance stop the **poison tick** (current behaviour) or make the **target miss attacks** (closer to the Anigame wording)? | `StatusEffects.cs:82-97` |
| Should talent text show the numbers scaled for the card's rarity? | Known issue 5 |
| How should saves survive campaign sheet edits? | Known issue 2 |
| Replace the anime characters, portraits and name before any public release? | GDD §11 |
| Is the original design doc ("Thiet ke Game The Bai - He thong 1 The Nang Cap") available to add to `Docs/reference/`? | Referenced by `GameRules.cs` section numbers |

## Cleanup

Candidates. Confirm before deleting anything.

| Item | Why it can go | Size |
|---|---|---|
| `_Backup_before_*` folders (4) | Snapshots from before git history existed; they are now committed | 618 tracked files |
| Legacy pipeline: `Assets/Data/cards.csv`, `Assets/Cards/`, `Assets/CardData_Example.csv`, `CardImporter` | Replaced by `campaign.csv` + `CampaignGenerator`; used only if `campaign.csv` is missing | — |
| `SampleScene` | Unused (known issue 4) | — |
| `BattleSystem` + `BattleSystemTest` | Legacy wrapper; `BattleScreen` and the simulator use `BattleEngine` directly | 2 files |
| `anigame-simulation/` | Empty nested git repo, untracked | — |
