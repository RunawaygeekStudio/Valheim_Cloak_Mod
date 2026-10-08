# Cloakcraft --- Valheim Cloak Augmentation Mod

**Project status:** v0.2 built (compiles, not yet game-tested)\
**Target:** Valheim 1.0.16\
**Mod loader:** BepInEx 5.x\
**Mod ID:** `BenShirley.Cloakcraft`\
**Current version:** `0.6.0`

![Cloakcraft promotional artwork](assets/cloakcraft_promo.png)

## 1. Concept

Cloakcraft adds a temporary **cloak augmentation system** to Valheim.
Players keep existing vanilla cloaks, craft consumable treatments from
vanilla materials, and apply one treatment to an equipped cloak. The
treatment temporarily modifies an existing Valheim mechanic.

> **Material defines the augmentation. Cloak tier defines its potential
> strength. Treatment cost defines the investment. The environment
> determines when some treatments are consumed.**

## 2. MVP design principles

1.  Existing vanilla cloaks are augmented rather than replaced.
2.  Augmentations are consumable treatment items.
3.  Only one augmentation may be active on a cloak at a time.
4.  Augmentations are temporary.
5.  Some timers are conditional and only tick while their relevant
    condition is active.
6.  Cloak progression controls the maximum effectiveness of an
    augmentation.
7.  Treatment investment is represented by five levels (Simple,
    Weathered, Hardened, Storm-worn, Odin's Gift), which buy duration.
8.  Costs use existing vanilla materials.
9.  MVP effects must map to mechanics/values already supported by
    Valheim.
10. Visual feedback is part of the feature, but should remain restrained
    and Valheim-like.
11. New mechanics requiring substantial AI, biome or combat-system
    invention are roadmap items.
12. All balance values should be configurable through JSON.

## 3. Augmentation model

``` text
Vanilla Cloak + Consumable Treatment -> Augmented Cloak
                                  -> Existing Valheim effect
                                  -> Timer
                                  -> Visual feedback
```

The cloak is not permanently transformed into a new item. Augmentation
state should be stored separately from the base cloak where practical.

### One active augmentation

Example: `Wolf Cape / Frostthread / 14:32 remaining`. Applying a Resin
Treatment while another treatment is active should require confirmation
and replace the current augmentation.

## 4. Cloak tiers

    Tier Progression          Purpose
  ------ -------------------- ----------------------------
       1 Early / Meadows      Basic treatments
       2 Black Forest         Improved treatments
       3 Swamp / Mountains    Strong treatments
       4 Plains / Mistlands   Very strong treatments
       5 Ashlands             Full treatment strength
       6 Deep North           Beyond full strength (1.15x)

The exact vanilla cloak-to-tier mapping must be resolved against the
target 1.0.16 assemblies during implementation.

## 5. Treatment levels

| Level | Needs cloak tier | Cloaks | Duration |
|---|---|---|---|
| Simple | 1 | Deer hide and up | base |
| Weathered | 2 | Troll hide, Wolf | 1.25x |
| Hardened | 4 | Lox, Linen, Feather | 1.5x |
| Storm-worn | 5 | Ashlands capes | 1.75x |
| Odin's Gift | 6 | Deep North capes | 2x |

A treatment on a cloak below its class is refused and the treatment is not consumed. Effect strength does not change with level; it is set by the equipped cloak's tier (section 4). Level keys in config and save data are fixed (`Simple`, `Weathered`, `Hardened`, `StormWorn`, `OdinsGift`); the display names are editable in the config.
## 6. MVP augmentation catalogue

Material cost per level, and the Simple duration (the other levels run 1.25x / 1.5x / 1.75x / 2x as long). Display names shown; config keys are Rainproof, ToxinWard, Frostbound, Emberbound, Lightweave, Fleetfoot, Seabound, Ironbound.

| Augmentation | Material | Existing Valheim effect | Timer | Simple | Weathered | Hardened | Storm-worn | Odin's Gift | Duration |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| **Rainseal** | Resin | Prevents Wet from rain | Rain only | 5 | 20 | 35 | 50 | 75 | 20 min |
| **Honeyward** | Honey | Reduces Poison damage | Continuous | 5 | 15 | 25 | 40 | 60 | 15 min |
| **Frostthread** | Freeze Gland | Reduces Frost damage | Cold exposure only | 2 | 6 | 10 | 15 | 23 | 20 min |
| **Emberstitch** | Surtling Core | Reduces Fire damage | Continuous | 1 | 3 | 5 | 8 | 12 | 15 min |
| **Lightweave** | Feathers | Reduces fall damage, slightly higher jump | Continuous | 5 | 20 | 35 | 50 | 75 | 15 min |
| **Fleetfoot** | Troll Hide | Increases movement speed | Continuous | 1 | 3 | 5 | 8 | 12 | 10 min |
| **Tidescale** | Serpent Scale | Reduces swimming stamina use | Swimming only | 1 | 3 | 5 | 7 | 11 | 20 min |
| **Ironweft** | Black Metal | Improves armour and stagger resistance | Continuous | 5 | 8 | 12 | 15 | 23 | 10 min |

### Conditional timers

**Rainseal:** timer runs only while rain would cause Wet.

**Frostthread:** timer should be consumed only while exposed to the
relevant cold/Frost condition, subject to the exact 1.0.16 mechanic.

**Tidescale:** timer runs only while swimming.

Continuous treatments: Honeyward, Emberstitch, Lightweave, Fleetfoot,
Ironweft.

## 7. Resin / Rainseal reference implementation

-   Simple: **5 Resin / 20 minutes of relevant exposure**
-   Weathered: **20 Resin / 25 minutes**
-   Hardened: **35 Resin / 30 minutes**
-   Storm-worn: **50 Resin / 35 minutes**
-   Odin's Gift: **75 Resin / 40 minutes**

Dry or sheltered: timer pauses. Rain: timer counts down.

Visual: subtle darker/glossier cloak treatment and rain beading/runoff.

## 8. Visual design

Visual feedback should communicate the augmentation without turning the
player into an MMO character.

-   **Emberstitch:** singed edge, orange glow, brief flames and embers
    when protection activates.
-   **Frostthread:** frost on seams, snowflakes in cold, brief frost
    burst on activation.
-   **Rainseal:** darker/glossier treatment, visible water runoff.
-   **Lightweave:** feather trim and occasional loose feather particles.
-   **Ironweft:** dark metallic stitching and small impact sparks.

Effects should be configurable with an overall particle intensity.

## 9. Balance philosophy

Universal effects should be shorter. Situational effects can last longer
because their timers only consume in relevant conditions. Expensive
late-game materials should feel like expedition preparation, not routine
maintenance. One active augmentation prevents stacking.

## 10. Roadmap / excluded from MVP

### Veilbound --- Wraith

Enemy detection/noise reduction; deferred because it requires deeper
AI/perception hooks.

### Mistwalker --- Eitr + Wisps

Mistlands visibility/movement treatment; deferred pending exact 1.0.16
mechanics. Ingredient identity is fixed as **Eitr + Wisps**.

### Drakescale --- Dragon Scale

Strong Fire/Frost protection; deferred to differentiate from
Emberstitch/Frostthread.

### Frostfur --- Wolf Pelt

Advanced cold/mountain treatment; deferred because of overlap with
Frostthread.

### Rootbound --- Ancient Bark

Stagger/knockback resistance; deferred pending combat-system testing.

### Fenris Step --- Fenris Hair

Movement/night treatment; deferred to avoid unnecessary conditional
complexity in MVP.

## 11. Roadmap

**v0.1:** BepInEx plugin, JSON configuration, cloak detection,
augmentation state, timer framework.

**v0.2:** Resin treatment, Rainproof, conditional timer, persistence,
cloak-tier scaling, replacement handling.

**v0.3:** HUD, inventory/equipment presentation, treatment UI, visual
effects.

**v0.4:** Remaining MVP augmentations.

**v0.5:** Dedicated server, multiplayer synchronization, save/reload,
death/corpse and compatibility testing.

**v1.0:** Full MVP, balanced configuration, documentation and release
packaging.

**v1.x/2.0:** Mistwalker, Veilbound, Dragon, Wolf, Rootbound, Fenris and
more advanced visuals.

## 12. JSON configuration

Written to `BepInEx/config/Cloakcraft.json` on first run from the embedded
`config/default-config.json`. Beyond the original schema it adds:

| Key | Purpose |
|---|---|
| `CloakTiers` | Cloak prefab name to tier 1-5, plus `Default` for unlisted cloaks |
| `TierMultiplier` | Fraction of a numeric effect realised per tier (0.6 to 1.0) |
| `ResistanceByTier` | Resistance level per tier for Honeyward, Frostthread, Emberstitch |
| `Augmentations.<Id>.Strength` | Base effect value (speed bonus, fall damage cut, armour bonus...) |
| `VisualEffects.Tints` | Cloak tint colour per augmentation |

`cloakcraft reload` in the console re-reads it in game.
## 13. Technical architecture

``` text
BepInEx
  |
  v
Cloakcraft Plugin
  |
  +-- Configuration
  +-- Augmentation Manager
       +-- Cloak Tier Manager
       +-- Treatment Manager
       +-- Timer Manager
       +-- Effect Manager
       +-- Visual Manager
       +-- Persistence
  |
  v
Valheim API / Harmony patches
```

Suggested source layout:

``` text
Cloakcraft/
├── src/
│   ├── Plugin.cs
│   ├── Augmentation/
│   ├── Cloaks/
│   ├── Items/
│   ├── Effects/
│   ├── UI/
│   └── Configuration/
├── config/
├── assets/
├── docs/
├── tests/
├── Cloakcraft.csproj
├── README.md
└── .gitignore
```

## 14. Current source

See `README.md` for the file map. Design choices that differ from sections 3 and 13:

- An augmentation **is a `StatusEffect` subclass** (`SE_Cloakcraft`), one per augmentation. That gives the HUD icon, countdown and every gameplay hook (damage mods, speed, fall damage, swim stamina, armour, stagger) with no extra UI code. Only Rainproof needs a Harmony patch of its own.
- State is stored in the cloak's `m_customData` (augmentation, level, seconds remaining), so it follows the cloak through saves, chests, tombstones and unequipping. The status effect is re-created from that data every time the cloak is equipped.
- Treatment items are runtime clones of their material's prefab (same icon and model), so no asset bundle is needed for the MVP.
- Cloak tier scales effect **strength**, not duration. Treatment level sets duration and cost.
- `docs/API-NOTES.md` lists every game API used, with line references into the 1.0.16 source.
## 15. Build dependencies

Compile against the exact local Valheim/BepInEx installation. Do not
redistribute proprietary game assemblies. Expected local references:

``` text
lib/
├── assembly_valheim.dll
├── UnityEngine.CoreModule.dll
├── UnityEngine.ParticleSystemModule.dll
├── BepInEx.dll
└── 0Harmony.dll
```

The repository excludes `lib/*.dll`.

## 16. GitHub repository

Recommended repository structure:

``` text
Cloakcraft/
├── src/
├── config/
├── assets/
├── docs/
├── tests/
├── Cloakcraft.csproj
├── README.md
├── LICENSE
└── .gitignore
```

Do not commit Valheim DLLs, BepInEx binaries, build output, save files
or sensitive logs.

## 17. Testing plan

Test treatment cost, duration, cloak tier, scaling, timer pause/resume,
expiry, replacement and save/load.

For Rainseal: apply treatment, enter rain, verify Wet is prevented and
timer ticks; enter shelter and verify timer pauses; return to rain and
verify it resumes; allow expiry and verify normal Wet behaviour returns.

Also test death/corpse recovery, dedicated server, two players with
different treatments, disconnect/reconnect and server restart.

## 18. Promotional identity

# CLOAKCRAFT

**TREAT · ADAPT · ENDURE**

Alternative line: **Enhance your cloak. Prepare for your journey.**

Visual direction: Nordic fantasy, weathered leather, fur, woven
materials, rune-inspired details, atmospheric biome backgrounds and
restrained elemental effects. The included artwork illustrates the
current MVP concepts.

## 19. Immediate implementation order

1.  Verify 1.0.16 API names and cloak definitions.
2.  Implement JSON loader.
3.  Implement cloak-tier resolver.
4.  Implement augmentation state.
5.  Implement treatment item.
6.  Implement Resin application.
7.  Implement Rainproof hook.
8.  Implement conditional timer.
9.  Implement persistence.
10. Implement replacement behaviour.
11. Implement visual treatment.
12. Implement HUD.
13. Add remaining MVP augmentations.
14. Test multiplayer.
15. Balance.
16. Package v1.0.

## 20. Project status

**Built (v0.6.0):** JSON config, cloak tiers, five-level treatments as craftable items (40 recipes), apply via item use, one-augmentation rule with replacement confirmation, all eight MVP augmentations, conditional timers (Rainproof, Frostbound, Seabound), persistence on the cloak item, HUD status icon with countdown and paused marker, cloak tint visual, console commands for testing, patch-target checker.

**Verified:** compiles against the real 1.0.16 assemblies; all 9 Harmony targets and 9 reflected members confirmed present (`tests/`); independent code review against the decompiled game source.

**Not yet done:** in-game testing (section 17), multiplayer validation, particle visuals, custom icons/models, dedicated-server check.

**Next:** drop `build/Cloakcraft.dll` into `BepInEx/plugins`, run the Rainproof test in section 17, then `cloakcraft apply Rainproof Light 1` to test expiry in a minute.
