# Cloakcraft PRD

Valheim mod: temporary cloak treatments crafted from vanilla materials. Status: v0.7.14, pre-release. Owner: Ben Shirley. Target: Valheim 1.0.17, BepInEx 5.x. Repo: github.com/RunawaygeekStudio/valheim_cloak_mod (MIT).

## 1. Problem

Cloaks are static. Players pick one per biome and forget it. Vanilla has no way to prepare a cloak for a specific trip (rain, cold, swimming, fire) without swapping to a different cloak.

## 2. Goals

| Goal | Measure |
|---|---|
| Cloaks become expedition prep | Players treat a cloak before setting out, not mid-fight |
| Uses only vanilla mechanics and materials | No new damage types, biomes or AI; every effect maps to an existing `StatusEffect` hook |
| Progression follows cloak quality | Better cloaks take better treatments and realise more of their effect |
| Fully configurable | Costs, durations, gates, names, visuals in one JSON file, no rebuild |
| Restrained, Valheim-like presentation | Game's own UI, fonts, popups and particle effects |

Non-goals (v1): new models or textures, multiplayer-visible visuals, enemy-AI effects (Veilbound, Mistwalker), stacking treatments.

## 3. Core model

```
Cloak (vanilla item) + Treatment (materials, at the Treating Bench) -> Augmented cloak
  -> one StatusEffect on the wearer (HUD icon, countdown, gameplay hooks)
  -> state stored on the cloak item (survives save, chest, tombstone, unequip)
  -> fades: continuous timer, or conditional (only ticks in rain / cold / water)
```

Rules:
- One treatment per cloak. Applying another asks to confirm, then replaces.
- Treatment **level** sets material cost and duration. Cloak **tier** sets effect strength and which levels it accepts.
- Treatments cannot be carried and reapplied in the field (v1 station UI). Meads cover that role.

## 4. Agreed tables

### 4.1 Cloak tiers

| Tier | Cloaks (prefab) | Strength multiplier | Resistance level |
|---|---|---|---|
| 1 | CapeDeerHide | 0.6 | SlightlyResistant |
| 2 | CapeTrollHide | 0.7 | SlightlyResistant |
| 3 | CapeWolf | 0.8 | Resistant |
| 4 | CapeLox, CapeLinen, CapeFeather | 0.9 | Resistant |
| 5 | CapeAsh, CapeAsksvin | 1.0 | VeryResistant |
| 6 | CapeDeepNorth (Moose Hide), CapeDeepNorthMage (Cape of the Caller) | 1.15 | VeryResistant |
| 7 | CapeOD (Cape of Odin, DLC) | 1.4 | Immune |

Unlisted shoulder items use `Default` (1). Tier 7 is the one-off "god's own cloak"; `General.UnlockCapeOfOdin` clears its DLC gate so anyone can craft and wear it (off by default).

### 4.1a Treating station

Each cloak is treated at the station its own vanilla recipe uses (`General.UseCloakStation`, default on); `CraftingStation` is the fallback. The AUGMENT tab shows at any station that treats a cloak in the pack; cloaks for another station list greyed with "Treat at the Galdr table".

| Cloak | Station | Recipe level |
|---|---|---|
| Deer Hide, Troll Hide, Wolf Fur, Lox, Linen | Workbench | 2 |
| Feather | Galdr table | 1 |
| Asksvin | Galdr table | 2 |
| Ashen | Black forge | 3 |
| Moose Hide | Black forge | 4 |
| Cape of the Caller | Galdr table | 4 |
| Cape of Odin | Workbench | 1 |

Proposed, not built: station-level gating `StationLevelBonus: [0, 0, 1, 2, 2]` (treatment level adds to the recipe level, capped at the station maximum).

### 4.2 Treatment levels

| Level (config key) | Needs tier | Duration | Second material |
|---|---|---|---|
| Simple | 1 | base | none |
| Weathered | 2 | 1.25x | none |
| Hardened | 4 | 1.5x | none |
| Storm-worn (`StormWorn`) | 5 | 1.75x | 1 Ashlands item |
| Odin's Gift (`OdinsGift`) | 6 | 2x | 1 Deep North item |

Keys are fixed (saves, icon files). Display names are config text. Legacy keys (Light/Mid/Heavy, Basic/Mid/High/Top) map on read.

### 4.3 Augmentations

| Treatment (config key) | Effect | Timer | Base material | Simple | Weathered | Hardened | Storm-worn | Odin's Gift | Base time |
|---|---|---|---|---:|---:|---:|---|---|---:|
| Rainseal (`Rainproof`) | Rain cannot apply Wet | Rain only | Resin | 5 | 20 | 35 | 50 + 1 Asksvin Hide* | 75 + 1 Seal Pelt* | 20 min |
| Honeyward (`ToxinWard`) | Resist Poison | Continuous | Honey | 5 | 15 | 25 | 40 + 1 Vineberry Cluster* | 60 + 1 Writhan Roots* | 15 min |
| Frostthread (`Frostbound`) | Resist Frost | Cold only | Freeze Gland | 2 | 6 | 10 | 15 + 1 Morgen Sinew* | 23 + 1 Frostcore* | 20 min |
| Emberstitch (`Emberbound`) | Resist Fire | Continuous | Surtling Core | 1 | 3 | 5 | 8 + 1 Flametal Ore | 12 + 1 Embers* | 15 min |
| Lightweave (`Lightweave`) | Less fall damage (75% at tier 5), jump +10% at tier 5 | Continuous | Feathers | 5 | 20 | 35 | 50 + 1 Volture Feather | 75 + 1 Nornathread* | 15 min |
| Fleetfoot (`Fleetfoot`) | Move speed +15% at tier 5 | Continuous | Troll Hide | 1 | 3 | 5 | 8 + 1 Asksvin Hide | 12 + 1 Moose Hide* | 10 min |
| Tidescale (`Seabound`) | Swim stamina -60% at tier 5 | Swimming only | Serpent Scale | 1 | 3 | 5 | 7 + 1 Bonemaw Serpent Scale* | 11 + 1 Seal Blubber* | 20 min |
| Ironweft (`Ironbound`) | Armour +20%, stagger -20% at tier 5 | Continuous | Black Metal | 5 | 8 | 12 | 15 + 1 Flametal Ore* | 23 + 1 Bloodgold* | 10 min |

`*` proposed, not yet agreed. Second-material prefab IDs to be verified. Strength percentages scale by tier multiplier.

Conditional timers: Rainseal matches the game's own rain check exactly (wet weather, not under a roof, not in a shield dome, not swimming). Frostthread: cold or freezing environment. Tidescale: swimming.

### 4.4 Roadmap augmentations (not in v1)

Veilbound (Wraith, stealth), Mistwalker (Eitr + Wisps), Drakescale (Dragon Scale), Frostfur (Wolf Pelt), Rootbound (Ancient Bark), Fenris Step (Fenris Hair). Deferred for AI or overlap reasons.

## 5. Requirements

### 5.1 Functional (built)

| ID | Requirement | Status |
|---|---|---|
| F1 | Eight augmentations, each one `StatusEffect` subclass with HUD icon and countdown | Done |
| F2 | State on the cloak item (`m_customData`): augmentation, level, seconds left | Done |
| F3 | Effect follows the cloak: unequip removes it, re-equip restores with time left | Done |
| F4 | Conditional timers pause outside their condition; HUD shows `\|\|` when paused | Done |
| F5 | Level gated by cloak tier; refused with message, nothing consumed | Done |
| F6 | Replace asks Yes/No via the game's popup | Done |
| F7 | Expiry clears cloak state and shows "has worn off" | Done |
| F8 | Config regenerates when its version changes; old file backed up | Done |
| F9 | Console: status, reload, give, apply, clear, fx list | Done |
| F10 | Treatment items craftable at workbench (interim) | Removed in 0.6.0 |

### 5.2 Functional (next)

| ID | Requirement |
|---|---|
| F11 | Levels accept a list of materials (base + optional second); UI shows each with have/need. **Done 0.6.0**, Storm-worn extras for Emberstitch, Lightweave, Fleetfoot pending ID check |
| F12 | **AUGMENT tab** in the crafting window, on the game's own panel: three columns (cloaks in pack, treatments, levels) of vanilla-style buttons, a detail zone under each column for the selected row, summary line, Apply button. Vanilla recipe content hidden while open. **Done 0.7.9** |
| F16 | Nearby-chest materials, off by default (`UseNearbyChests`, `ChestRadius`). **Done 0.7.1** |
| F17 | Per-cloak treating station from the cloak's recipe (`UseCloakStation`). **Done 0.7.11** |
| F18 | Cape of Odin: tier 7, DLC unlock switch. **Done 0.7.12** |
| F13 | Particles ride on the status effect's `m_startEffects`, exactly as the feather cape's SlowFall does (networked, removed with the effect). Sources: `se:<status effect>`, `env:<weather>`, `prefab:<item>` (particle subtree only). Per entry: Scale, Emission, Glow (particle size), Tint, LightRange/LightIntensity (dim wisp-style point light, donor lights stripped), OnlyWhileTicking. Defaults: Lightweave se:SlowFall, Emberstitch prefab:StaffFireball, Frostthread prefab:StaffIceShards, Honeyward env:Snow green, Rainseal env:Rain. **Done 0.7.14**; Fleetfoot, Tidescale, Ironweft sources to pick |
| F14 | Tooltip line on augmented cloaks in inventory and chests |
| F15 | Multiplayer check: effect local to owner, state syncs with item; dedicated server smoke test |

### 5.3 UI copy (agreed)

Tab: AUGMENT. Column headings: CLOAKS, TREATMENTS, LEVELS. Detail zones carry the copy below.

> Work resin, honey and hide into your cloak. One treatment at a time. It fades with use, so treat before you sail.
> **Cloak:** Choose a cloak from your pack. Finer cloaks take finer treatments.
> **Treatment:** Eight to choose from. Each is made from what its biome gives you.
> **Strength:** Simple, Weathered, Hardened, Storm-worn, Odin's Gift. More material, longer the treatment holds.
> **Greyed:** Your cloak is too humble, or you lack the materials.
> **Apply:** Materials are spent. The old treatment, if any, is washed out.

Mock: Figma "Cloakcraft Augment UI" (three columns: cloaks, treatments, levels + Apply; replace dialog; notes).

### 5.4 Visual direction

Restrained. Cloak tint per treatment (config RGBA, strength 0.35). Particles small, low emission, attached to the spine bone, emission paused with conditional timers. No MMO glow.

### 5.5 Config surface (`BepInEx/config/Cloakcraft.json`)

`General` (Enabled, DebugLogging, CraftingStation, UseCloakStation, UnlockCapeOfOdin, UseNearbyChests, ChestRadius), `CloakTiers` (1-7), `TierMultiplier`, `ResistanceByTier`, `Augmentations.<key>` (Name, Description, Material, TimerMode, Strength, JumpStrength, Levels.<key>.{Name, Cost, Extra[], DurationMinutes, MinCloakTier}), `VisualEffects` (Enabled, TintStrength, Tints, Particles.<key>.{Source, Scale, Emission, Glow, Tint, LightRange, LightIntensity, OnlyWhileTicking}). ConfigVersion 18.

## 6. Technical summary

- One Harmony patch per concern, every private member reached by reflection listed in `src/Refs.cs`; `tests/PatchTargets` fails if the game renames any
- `tests/logic`: 134 checks on config and tier logic
- No new items or prefabs: status effects only; icons embedded, overridable from `plugins/icons/`
- Known: Valheim Plus 0.10.2 breaks the console on 1.0.17, so `cloakcraft` commands are unavailable alongside it; the mod dumps the tab hierarchy to the log on first AUGMENT open instead
- Details: `docs/API-NOTES.md`, `README.md`

## 7. Decisions log

| Date | Decision |
|---|---|
| 07 Oct | Augmentation = StatusEffect subclass; state on the item, not the player |
| 07 Oct | Level buys duration, tier buys strength (not the reverse) |
| 08 Oct | Four levels then five; names Simple / Weathered / Hardened / Storm-worn / Odin's Gift |
| 08 Oct | Tier 6 for Deep North, extends up (1.15x) rather than renormalising |
| 08 Oct | Treatment names: Rainseal, Honeyward, Frostthread, Emberstitch, Lightweave, Fleetfoot, Tidescale, Ironweft |
| 08 Oct | Station-bound application via workbench tab; no carried treatment items in v1 |
| 08 Oct | Storm-worn and Odin's Gift require a second biome material |
| 08 Oct | Removed config flags nobody sets (replacement, death persistence, station level) |
| 08 Oct | Bench UI rebuilt inside the vanilla crafting panel instead of an overlay; chest sourcing added, off by default |
| 08 Oct | Row detail moved from tooltips to a detail zone under each column (tooltips unreadable on the brown panel) |
| 08 Oct | Particles via StatusEffect start effects (the feather cape's method), not cloned weather systems |
| 08 Oct | Treating station = the cloak's own recipe station; Cape of Odin kept at the top as tier 7 with an unlock switch |

## 8. Open items

| Item | Owner |
|---|---|
| Confirm `StaffFireball`, `StaffIceShards` prefab names and Extra material IDs from the log | Ben |
| Agree starred second materials; verify Deep North item IDs | Both |
| Station-level gating (`StationLevelBonus`) go / no-go | Ben |
| AUGMENT tab gold frame; needs the hierarchy dump from the log | Claude |
| Balance: cost curve vs duration (15x cost for 2x time); consider 2x / 2.5x at the top | Ben, after play |
| Particle look: Scale / Emission / Glow / light per treatment; Fleetfoot, Tidescale, Ironweft sources | Ben, in game |

## 9. Release checklist (v1.0)

- [ ] F11 to F15 done
- [ ] Section 17 test plan in `CLOAKCRAFT-PROJECT.md` passed, including save/load, death, dedicated server
- [x] Treatment items removed (0.6.0)
- [ ] README install steps verified on a clean install
- [x] LICENSE (MIT), `.gitignore` excludes `lib/`, game assemblies, BepInEx binaries
- [ ] Thunderstore manifest and icon
