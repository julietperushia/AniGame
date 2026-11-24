# Editor tools

**Source of truth:** `Assets/Editor/AniGameSetup.cs`, `Assets/Editor/CampaignGenerator.cs`,
`Assets/Editor/CardImporter.cs`, `Assets/Editor/AnimeArtGenerator*.cs`, `Assets/Editor/AniGameDevBridge.cs`,
`Assets/Editor/DebugTalents.cs`

## Tools › AniGame menu

| Menu item | What it does |
|---|---|
| **Build Everything** | Runs steps 1–5 below plus project settings. Safe to run again. Report goes to `Logs/AniGame_Report.txt` |
| Steps › 1. Repair Talent Assets | Creates a talent asset for every non-abstract `Talent` class that is missing one, and fills empty names and descriptions |
| Steps › 2. Update Cards From Legacy CSV | Legacy: `Assets/Data/cards.csv` → `Assets/Cards/` |
| Steps › 3. Build Card Database | Rebuilds `Assets/Resources/CardDatabase.asset` |
| Steps › 4. Generate UI Art | Fills `UITheme.asset` from `Assets/Art/UserArt/`, generating placeholders for anything missing |
| Steps › 5. Build Scenes | Builds all 9 scenes with `UIFactory` and writes the build settings. **Overwrites manual scene edits** |
| Campaign › Generate From Sheet | `campaign.csv` → floor cards, portraits and database ([data-pipeline.md](data-pipeline.md#generating)) |
| Campaign › Generate Without Downloading Images | Same, but doesn't download images |
| Campaign › Validate Sheet Only | Reports errors and warnings without changing anything |
| **Simulate Balance** | Seeded balance report ([below](#simulate-balance)) |
| Open Main Menu Scene | Opens `Assets/Scenes/MainMenu.unity` |
| Save Data › Delete Save | Deletes `anigame_save.json` (asks first) |
| Save Data › Refill Stamina + 10,000 Gold (cheat) | For testing |
| Save Data › Show Save File | Shows the save in Explorer |
| Save Data › Export / Import Save File… | `.anisave` transfer ([save-format.md](save-format.md#export-and-import-anisave)) |

Build Everything also enables **Run In Background** (so auto-battles keep running when the window loses focus)
and sets the product name to `AniGame` if it is still the default.

Other menus: **Tools › Card Importer** (the legacy CSV importer window) and **Tools › Debug Talents** (prints
every talent asset's name and its normalised name to the Console).

## Simulate Balance

`AniGameSetup.SimulateBalance` (`AniGameSetup.cs:481`) uses fixed seed 42, so its results are repeatable. It
reports:

1. Player card power by rarity: worst roll, roll 0, best roll, and talent bonus.
2. Simulated battles between player builds and campaign cards (see the method for the full list of scenarios).

Use it after any change to `GameRules`, talents, elements or the damage formula. There are no automated tests,
so this is the main regression check ([architecture.md](architecture.md#testing)).

## Dev Bridge

`Assets/Editor/AniGameDevBridge.cs` is an editor-only helper. It is safe to delete and is never included in a
build. It lets an **outside tool or AI assistant** drive the open editor through files:

- It writes compile results and editor errors to `Logs/AniGameBridge/bridge_log.txt` (status in
  `compile_status.txt`).
- Every 2 seconds it checks `Logs/AniGameBridge/command.txt` and runs **one** command from a fixed list.

| Command | Effect |
|---|---|
| `refresh` | Asset refresh / recompile |
| `build` | Build Everything |
| `campaign` / `campaign:nodownload` | Generate the campaign (with or without image downloads) |
| `validate` | Validate the sheet |
| `scenes`, `settings`, `database` | Run the matching setup step |
| `simulate` | Simulate Balance |
| `status` | Write status to the log |
| `tick:on` / `tick:off` | Keep Play mode advancing frames while Unity is in the background |
| `open:SceneName` | Open a scene |
| `play` / `stop` | Enter or leave Play mode |
| `shot:name`, `render:name` | Save a Game view screenshot / render the UI canvas to `Logs/AniGameBridge/<name>.png` |
| `ui:Type.Method[:arg]` | Call a UI method (testing) |
| `cheat`, `newgame` | Give resources / start a new save |
| `buff:all`, `buff:none`, `buff:<id>` | Set owned buffs |
| `character:Name`, `character:gallery:KEY` | Set the player's character |

To use it: open the project in Unity, write a command into `Logs/AniGameBridge/command.txt`, then read
`bridge_log.txt`. `Logs/` is ignored by git.
