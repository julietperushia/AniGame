# Save format

**Source of truth:** `Assets/Scripts/Data/PlayerProfile.cs` (`SaveData`, load/save, integrity),
`Assets/Scripts/Data/SaveTransfer.cs` (export/import), `Assets/Scripts/Data/CardInstance.cs`

## Files

All files live in `Application.persistentDataPath`. To find the folder, use **Tools › AniGame › Save Data ›
Show Save File**, or **Main Menu › SAVE DATA › OPEN SAVE FOLDER**.

| File | What |
|---|---|
| `anigame_save.json` | The save: `SaveData` written with `JsonUtility`, pretty printed, signed |
| `anigame_save.json.bak` | A copy written on every save; used if the main file fails its integrity check |
| `anigame_save_backup_<tag>_<timestamp>.json` | Created before a reset, a tamper recovery, or an import |
| `character_portrait.png` | The imported player picture |
| `pictures/*.png` | Pictures imported for single cards (EDIT CARD); a build points at one with `picture = "pic:ID"` |
| `Exports/*.anisave` | Save exports for moving to another PC |
| `image_cache/` | Portraits downloaded at runtime |

A `PlayerPrefs` key (`AniGame.SignedSave`) records that a signed save exists.

## `SaveData` fields

From `PlayerProfile.cs:11-55`:

| Group | Fields |
|---|---|
| Version | `version` (save schema, currently **2**), `dataVersion` (campaign sheet hash) |
| Character | `hasCharacter`, `characterName`, `characterSource` (`""`, `"file"`, or `"gallery:<card key>"`) |
| Currencies | `gold`, `diamonds`, `fusionOrbs`, `elementOrbs`, `talentOrbs`, `godOrbs` |
| Stamina | `stamina`, `staminaTimestamp` (Unix seconds of the last regen tick) |
| Cards | `nextUid`, `cards` (list of `CardInstance`), `slots` (3 × `BuildSlot`), `activeSlot` |
| Account | `buffs` (list of buff ids), `clearedFloors` (one count per LOC: how many floors are cleared) |
| Stats | `battlesWon`, `battlesLost`, `packsOpened`, `fusionsSucceeded`, `fusionsFailed`, `goldSpent`, `cashbackEarned` |
| Settings | `battleSpeed` (1/2/4), `sfxVolume` |
| Protection | `integrity` (HMAC of everything else) |

`CardInstance`: `uid`, `key` (always `"player"`), `rarity` (int), `build` {`hp`, `atk`, `def`, `spd`, `element`
(int), `talent` (name), `talent2`, `name` (own name, empty = the character's), `picture` (own picture: `gallery:KEY` or
`pic:ID`, empty = the character's)}, `locked`, `godBonus`. Save exports also carry the `pic:` files (`cardPictures`).

## Integrity (anti-cheat)

- On save, the JSON is signed with **HMAC-SHA256**: `integrity` is cleared, the JSON is hashed, and the hash is
  stored (`PlayerProfile.Sign`).
- On load, if the hash doesn't match:
  1. the bad file is backed up as `tampered`,
  2. the `.bak` is tried,
  3. if that also fails, a **new game starts**, and the main menu shows a warning once.
- An unsigned save (from before protection existed) is accepted **once**, then signed. After that, an unsigned
  file counts as edited.
- **Limitation:** the HMAC key is a constant in the source (`PlayerProfile.cs:193`). This stops casual editing of
  the JSON, not a determined player. Don't copy the key into docs or tickets.

## Versioning and resets

From `PlayerProfile.Load`, `PlayerProfile.cs:160-166`. The save is **replaced by a new game** (the old one is
backed up) when either:

- `version` ≠ `PlayerProfile.SaveVersion` (2), or
- `dataVersion` ≠ the current `CardDatabase.dataVersion`, which is the **MD5 of `campaign.csv`**.

So **any edit to the campaign sheet wipes every player's progress.** That is fine during development, but it
must be solved before players have real saves ([roadmap](../project/roadmap.md#open-design-questions)).

### Rules for changing the save

- Adding a field is safe: `JsonUtility` gives missing fields their default, and `Validate()` repairs null lists.
- Renaming or removing a field loses that data. Increase `SaveVersion` only if you accept a reset.
- Enum values (`rarity`, `element`) are stored as integers. **Only add new values at the end.**
- Buff ids are stored as strings. **Never rename them.**

## Export and import (`.anisave`)

`SaveTransfer`, available from the main menu's SAVE DATA panel and from **Tools › AniGame › Save Data**.

| Field | Meaning |
|---|---|
| `format`, `formatVersion` | File type check |
| `saveVersion`, `gameVersion`, `dataVersion`, `exportedAt` | Provenance |
| `characterName`, `cardCount`, `highestLocation` | Shown in the confirmation step before import |
| `save` | The full `SaveData` |
| `portraitPng` | Base64 of the imported portrait (empty for a gallery portrait) |
| `hash` | HMAC of everything above; edited files are refused |

On import, the current save is backed up first (`before_import`). If the file was made with a different
campaign sheet, it is **still accepted**: cards and buffs are kept, its `dataVersion` is updated to the current
one, and a warning says floor progress may not match. This is the one way to carry a save across a sheet change
today.

## Validation on load

`PlayerProfile.Validate` repairs common problems: null lists, cards without a build (re-rolled), `nextUid`
behind the highest uid, slots pointing at missing cards, no card in any slot (it slots the first card), no cards
at all (it gives a new starter), an invalid active slot, and stamina above max.
