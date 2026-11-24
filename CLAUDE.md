# CLAUDE.md

Guidance for AI assistants working in this repo. The full documentation is in `Docs/` (start at
`Docs/README.md`).

## Project

AniGame: a single-player anime card battler in **Unity 6000.2.15f1** (URP, uGUI, Input System). C#, no
assembly definitions, no namespaces. The player upgrades one card (their own character) through fusion and
rerolls, and fights a 100-location campaign in automatic 1v1 battles.

## Rules the code must keep

- **Balance numbers live only in `Assets/Scripts/Rules/`** (`GameRules.cs`, `Packs.cs`, `Buffs.cs`) and in
  `Assets/Scripts/Battle/ElementSystem.cs`. Never hard-code a balance number in UI or talent code. The in-game
  Library reads these files live.
- **Enums are append-only** (`CardElement`, `CardRarity`, …). They are saved as integers.
- **Buff ids are never renamed.** They are saved as strings.
- **Talents are shared ScriptableObjects.** Never keep per-battle state in fields. Use
  `CardRuntime.GetTalentState/SetTalentState`. Put talent numbers through `Scaled()`.
- **All HP changes go through** `CardRuntime.TakeDamage / TakeTrueDamage / Heal`, so the battle log and UI see
  them.
- **Only `PlayerProfile` changes `SaveData`.** Action methods return `null` on success or an error sentence for
  the UI, and call `Save()` afterwards.
- **Scenes are generated** by `AniGameSetup.BuildScenes` via `UIFactory`. Change the builder or the screen
  script, never the `.unity` file by hand, because a rebuild overwrites it.
- **Editing `Assets/Data/campaign.csv` resets every player save** (its MD5 is the data version). Say so before
  changing it.
- Text uses legacy `UnityEngine.UI.Text`, not TextMeshPro, so icons must be sprites, not emoji.

## Verifying changes

There are no automated tests. To verify:

- **Compile:** Unity must be open. The Dev Bridge (`Assets/Editor/AniGameDevBridge.cs`) writes compile results to
  `Logs/AniGameBridge/bridge_log.txt` and `compile_status.txt`. Write `refresh` into
  `Logs/AniGameBridge/command.txt` to trigger a recompile.
- **Balance:** the `simulate` bridge command, or **Tools › AniGame › Simulate Balance**.
- **Screens:** `open:SceneName`, `play`, `shot:name` (screenshot written to `Logs/AniGameBridge/`), `stop`.
- Other bridge commands: `Docs/tech/editor-tools.md`.

## Docs

When you change a rule, a number, a talent, a buff, a pack or the save format, update the matching file in
`Docs/design/` or `Docs/tech/`, and add a line to `Docs/project/changelog.md` under *Unreleased*. The code
is the source of truth: if a doc disagrees with it, fix the doc.

## Don't

- Don't commit `Library/`, `Temp/`, `Logs/`, `UserSettings/` (already in `.gitignore`).
- Don't print or copy the save HMAC key from `PlayerProfile.cs` into docs, logs or messages.
- Don't delete the `_Backup_*` folders, the legacy CSV pipeline or `anigame-simulation/` without asking (see
  `Docs/project/roadmap.md#cleanup`).
