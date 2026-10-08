# Valheim 1.0.16 API used by Cloakcraft

Verified against the decompiled `assembly_valheim.dll` shipped with the project. `tests/PatchTargets` re-checks the Harmony targets and reflected members automatically.

## Harmony patches

| Target | Kind | Why |
|---|---|---|
| `ObjectDB.Awake` (private) | postfix | Register items/recipes/status effects in the main scene |
| `ObjectDB.CopyOtherDB` | postfix | Same, for the main-menu to game transition |
| `ZNetScene.Awake` (private) | postfix | Add prefabs to `m_prefabs` and private `m_namedPrefabs`; crafting station lookup |
| `Localization.SetupLanguage` (assembly_guiutils) | postfix | Re-add our strings on language change via private `AddWord` |
| `Humanoid.UseItem(Inventory, ItemData, bool)` | prefix, returns false | Intercept treatment items. Both InventoryGui and hotbar route here |
| `Player.Update` (private) | postfix | Per-frame sync of cloak state and status effect (local player only) |
| `Player.UpdateEnvStatusEffects` (private) | prefix+postfix flag | Marks when the rain Wet check is running |
| `SEMan.AddStatusEffect(int,bool,int,float,short)` | prefix | Block `Wet` only during that check when Rainproof is active. Swimming Wet (from `Character.UpdateWater`) untouched |
| `VisEquipment.SetShoulderEquipped` (private) | postfix | Re-apply the cloak tint after the model is rebuilt |

## Game mechanics mapped

| Augmentation | Hook | Evidence |
|---|---|---|
| Rainproof | SEMan prefix above | `Player.cs:2230` `if (flag5 && !m_underRoof && !flag10) AddStatusEffect(s_statusEffectWet...)`. Timer condition mirrors it: `EnvMan.IsWet() && !m_underRoof && !ShieldGenerator.IsInsideShield(pos) && !InWater()` |
| Toxin Ward, Frostbound, Emberbound | `StatusEffect.ModifyDamageMods` | `Character.GetDamageModifiers` -> `SEMan.ApplyDamageMods`. Uses `HitData.DamageModPair` with `DamageModifier` from config |
| Lightweave | `StatusEffect.ModifyFallDamage` | `Character.cs:2801` |
| Fleetfoot | `StatusEffect.ModifySpeed` | `SEMan.ApplyStatusEffectSpeedMods` |
| Seabound | `StatusEffect.ModifySwimStaminaUsage` | `Player.cs:2637` in `OnSwimming` |
| Ironbound | `StatusEffect.ModifyArmorMods` + `ModifyStagger` | `Player.GetBodyArmor` -> `SEMan.ApplyArmorMods`; `Character.AddStaggerDamage` -> `SEMan.ModifyStagger` |
| Cold condition | `EnvMan.IsCold() \|\| EnvMan.IsFreezing()` | static, `EnvMan.cs:990-1000` |
| Swimming condition | `Character.IsSwimming()` | `Character.cs:3523` |

## Data and items

| Need | API |
|---|---|
| Per-cloak state | `ItemDrop.ItemData.m_customData` (Dictionary<string,string>), saved/loaded/cloned by `ItemData.Clone/SaveToPackage/LoadFromPackage` |
| Equipped cloak | `Humanoid.m_shoulderItem` (protected, via `AccessTools.FieldRefAccess`) |
| Cloak tier key | `ItemData.m_dropPrefab.name`, set by `Inventory.AddItem` and `ItemDrop.Awake` |
| Status effect HUD | Anything in `SEMan.m_statusEffects` with non-null `m_icon` and `!m_hidden` (`SEMan.GetHUDStatusEffects`). Countdown from `GetIconText()` |
| Status effect lookup | `ObjectDB.GetStatusEffect(int)` iterates `m_StatusEffects` by `NameHash()` = `name.GetStableHashCode()` |
| New item without asset bundle | `Instantiate(materialPrefab, inactiveParent)`, rename, copy `SharedData` by reflection, add to `ObjectDB.m_items` + call private `UpdateRegisters()` |
| Recipe | `ScriptableObject.CreateInstance<Recipe>()`, `m_item`, `m_resources = Piece.Requirement[]`, `m_craftingStation` (null = hand craft) |
| Confirm dialog | `UnifiedPopup.Push(new YesNoPopup(header, text, yes, no))`, `UnifiedPopup.Pop()`. `Localization.Localize(text, words)` substitutes `$1 $2 $3` |
| Console | `new Terminal.ConsoleCommand(name, help, ConsoleEvent, ...)`. `args[0]` is the command. `Terminal.m_cheat` = devcommands on |
| Particle sources | `ZNetScene.GetPrefab(name)` subtree with a `ParticleSystem`; `EnvMan.instance.m_environments[].m_psystems` for weather. Attached to `Utils.GetBoneTransform(Character.m_animator, Spine)` |
| Cloak model | `Humanoid.m_visEquipment` (protected) -> `VisEquipment.m_shoulderItemInstances` (private List<GameObject>), rebuilt by `SetShoulderEquipped` |

## Vanilla cloak prefab names (for `CloakTiers`)

CapeDeerHide, CapeTrollHide, CapeWolf, CapeLox, CapeLinen, CapeFeather, CapeAsh, CapeAsksvin, CapeDeepNorth (Moose Hide Cape), CapeDeepNorthMage (Cape of the Caller), CapeOD. Not verifiable from the assembly (they live in asset bundles); the two Deep North IDs come from item databases, the rest from memory. Confirm in game with `cloakcraft status`.
