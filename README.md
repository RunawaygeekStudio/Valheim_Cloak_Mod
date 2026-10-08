# Cloakcraft

Valheim 1.0.17 mod (BepInEx 5). Treat your cloak with vanilla materials for a temporary effect: rain, poison, frost, fire, falls, speed, swimming, armour. Requirements: `docs/PRD.md`. Original design: `CLOAKCRAFT-PROJECT.md`. Verified game API: `docs/API-NOTES.md`.

**Status: v0.7.14, pre-release. Works in game; balance and multiplayer untested.**

## Install

| Step | Action |
|---|---|
| 1 | BepInEx 5.x installed in Valheim (you have it) |
| 2 | Copy `build/Cloakcraft.dll` to `Valheim/BepInEx/plugins/` |
| 3 | Launch. First run writes `BepInEx/config/Cloakcraft.json` from the built-in defaults |

Log line on success: `Cloakcraft 0.7.14 loaded; 8 augmentations enabled`.

## Play

| Action | How |
|---|---|
| Treat a cloak | At the station the cloak is made at (workbench, Galdr table, black forge) open the crafting window and pick the **AUGMENT** tab. Three columns: cloaks in your pack, treatments, levels. Select a row and its detail (tier, effect, cost, duration) shows under the column. Greyed: wrong station, cloak class too low, or materials missing. Apply |
| Chests | Off by default. `General.UseNearbyChests: true` lets the bench count and take materials from chests you can open within `ChestRadius` metres |
| Levels | Simple fits any cloak; Weathered, Hardened, Storm-worn and Odin's Gift need a tier 2, 4, 5 or 6 cloak (`CloakTiers`). Storm-worn and Odin's Gift also take a second material (`Extra`) |
| Cape of Odin | Tier 7: strongest effects, resistances become Immune. `General.UnlockCapeOfOdin: true` removes the DLC gate |
| Particles | Each treatment borrows a game effect (feather cape feathers, fire staff embers, ice staff frost, coloured snow, rain) with a dim wisp-like glow. All tunable in `VisualEffects.Particles` |
| See it | Status-effect icon top right with countdown. `\|\|` prefix = conditional timer paused |
| Replace | Using another treatment on an augmented cloak asks to confirm |
| Remove | Expires on its own. Or console `cloakcraft clear` |

State lives on the cloak item, so it survives save/load, chests, tombstones and unequipping.

## Console (F5)

| Command | Needs devcommands | Does |
|---|---|---|
| `cloakcraft status` | no | Equipped cloak, tier, active augmentation, timer state |
| `cloakcraft reload` | no | Re-read `Cloakcraft.json` |
| `cloakcraft apply <aug> <level> [minutes]` | yes | Apply without an item, optional custom duration (test expiry fast) |
| `cloakcraft clear` | no | Strip the augmentation from the equipped cloak |
| `cloakcraft fx list <text>` | no | List particle sources (`se:`, `env:`, `prefab:`) whose name contains text, for `VisualEffects.Particles` |
| `cloakcraft ui` | no | Write the crafting tab hierarchy to the log (for matching tab styling) |
| `cloakcraft items <text>` | no | List item prefab names containing text, for `Material` and `Extra` |

`<aug>` (config keys): Rainproof, ToxinWard, Frostbound, Emberbound, Lightweave, Fleetfoot, Seabound, Ironbound. In game they display as Rainseal, Honeyward, Frostthread, Emberstitch, Lightweave, Fleetfoot, Tidescale, Ironweft (set by `Name` in the config). `<level>`: Simple, Weathered, Hardened, StormWorn, OdinsGift (config keys; display names are set in the config).

## Config (`BepInEx/config/Cloakcraft.json`)

| Section | Controls |
|---|---|
| `General` | Enable, debug log, fallback crafting station, `UseCloakStation` (treat at the cloak's own station), `UnlockCapeOfOdin`, nearby chests (off by default) |
| `CloakTiers` | Cloak prefab name to tier 1-7 (6 = Deep North, 7 = Cape of Odin). Unlisted cloaks use `Default` |
| `TierMultiplier` | Effect strength realised per tier (numeric effects) |
| `ResistanceByTier` | Resistance level per tier (Toxin Ward, Frostbound, Emberbound) |
| `Augmentations.<Id>` | `Material` prefab, `TimerMode`, `Strength`, `Levels.{Simple,Weathered,Hardened,StormWorn,OdinsGift}.{Name,Cost,DurationMinutes,MinCloakTier,Extra[]}` |
| `VisualEffects` | Cloak tint per augmentation; `Particles.<Id>`: `Source` (`se:SlowFall`, `env:Snow`, `prefab:StaffFireball`), `Scale`, `Emission`, `Glow`, `Tint`, `LightRange`, `LightIntensity`, `OnlyWhileTicking` |

Known: Valheim Plus 0.10.2 breaks the game console on 1.0.17, so the `cloakcraft` commands don't work alongside it.

Delete the file to regenerate defaults. When the format changes, the mod backs up the old file as `Cloakcraft.json.vN.bak` and writes fresh defaults. Costs, durations and strengths apply to treatments applied after a reload.

## Build

```
# once: copy these into lib/
#   Valheim/valheim_Data/Managed/{assembly_valheim,assembly_utils,assembly_guiutils,Newtonsoft.Json,UnityEngine,UnityEngine.CoreModule,UnityEngine.UI}.dll
#   Valheim/BepInEx/core/{BepInEx,0Harmony}.dll
dotnet build -c Release                       # -> build/Cloakcraft.dll
dotnet run --project tests -- build/Cloakcraft.dll lib   # every Harmony target and reflected member still exists (run after game updates)
dotnet run --project tests/logic                         # config, tier, class and legacy-level logic against the built DLL
```

Needs .NET SDK 8. Target is netstandard2.1 (Valheim runs Unity 6). `lib/` is gitignored; never commit game DLLs. Also copy `UnityEngine.ImageConversionModule.dll` into `lib/`.

## Layout

| Path | What |
|---|---|
| `src/Plugin.cs` | Entry point, console commands |
| `src/Config.cs` | JSON schema and loader |
| `src/Augmentations.cs` | Catalogue: which effect each augmentation wires to |
| `src/CloakState.cs` | Read/write state in the cloak's `m_customData` |
| `src/SE_Cloakcraft.cs` | The status effect: timer, gameplay hooks, HUD text |
| `src/Items.cs` | Status-effect registration, localisation |
| `src/BenchUI.cs` | Augment tab: three columns on the vanilla crafting panel |
| `src/Materials.cs` | Material counting and removal, inventory plus optional nearby chests |
| `src/AugmentationManager.cs` | Apply, replace, per-frame sync between cloak and effect |
| `src/Visuals.cs` | Cloak tint |
| `src/Fx.cs` | Particles via the status effect's start effects (sources: status effects, weather, item prefabs) plus a dim glow light |
| `src/Icons.cs` | Loads item and HUD icons (embedded PNGs, overridable) |
| `assets/icons/` | 8 augmentation icons (HUD and bench) generated by `tools/make_icons.py`; per-level variants kept but not embedded |
| `src/Patches.cs` | All Harmony patches |
| `tests/` | Patch-target checker; `tests/logic` logic self-check |
| `src/Refs.cs` | Every private game member reached by reflection |

## Icons

Generated by `python3 tools/make_icons.py` (needs Pillow) into `assets/icons/`, then embedded at build. To use painted art without rebuilding: make a folder `icons` next to `Cloakcraft.dll` in `BepInEx/plugins` and drop in PNGs named `<Aug>_<Level>.png` (item, level key = Simple/Weathered/Hardened/StormWorn/OdinsGift) or `<Aug>.png` (HUD). 128x128, transparent background.

## Known limits (v0.2)

- Treatment items use the material's model when dropped; icons are the generated set
- Particles: Emberstitch, Honeyward, Rainseal, Frostthread; Lightweave, Fleetfoot, Tidescale, Ironweft are tint-only until sources are chosen
- Storm-worn extra materials use guessed prefab names (FlametalOreNew, VoltureFeather, AsksvinSkin); a wrong name shows as "Unknown material" in the bench, fix it in the config after `cloakcraft items <text>`
- Other players don't see your tint (effect itself is local, state syncs with the item)
- Untested in game: see `CLOAKCRAFT-PROJECT.md` section 17 for the test plan
