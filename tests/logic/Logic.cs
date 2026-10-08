// Self-check for the pure logic: config defaults, tier/class mapping, level names, legacy saves.
// dotnet run --project tests/logic   (needs build/Cloakcraft.dll and lib/)
using Cloakcraft;
using Newtonsoft.Json;
using System.Reflection;

var json = new StreamReader(typeof(Plugin).Assembly.GetManifestResourceStream("Cloakcraft.default-config.json")!).ReadToEnd();
var cfg = JsonConvert.DeserializeObject<Config>(json)!;
Config.Current = cfg;
int n = 0;
void Check(bool ok, string what) { n++; if (!ok) throw new Exception("FAIL: " + what); }

Check(cfg.ConfigVersion == Config.CurrentVersion, "embedded config version matches code");
Check(cfg.Augmentations.Count == 8, "8 augmentations");
foreach (var (id, a) in cfg.Augmentations)
{
    Check(a.Levels.Keys.SequenceEqual(Catalogue.Levels), $"{id} has the 5 levels in order");
    var ls = a.Levels.Values.ToList();
    for (int i = 1; i < ls.Count; i++) { Check(ls[i].Cost > ls[i-1].Cost, $"{id} cost rises"); Check(ls[i].DurationMinutes > ls[i-1].DurationMinutes, $"{id} duration rises"); Check(ls[i].MinCloakTier > ls[i-1].MinCloakTier, $"{id} tier gate rises"); }
    Check(ls.Select(l => l.MinCloakTier).SequenceEqual(new[]{1,2,4,5,6}), $"{id} gates are 1,2,4,5,6");
    Check(!string.IsNullOrEmpty(a.Name) && !string.IsNullOrEmpty(a.Material), $"{id} has Name and Material");
}
// tiers
Check(cfg.TierFor("CapeDeerHide") == 1 && cfg.TierFor("CapeWolf") == 3 && cfg.TierFor("CapeAsh") == 5 && cfg.TierFor("CapeDeepNorth") == 6, "cape tiers");
Check(cfg.TierFor("NotACape") == 1, "unknown cloak uses Default");
Check(cfg.MultiplierFor(6) > cfg.MultiplierFor(5) && cfg.MultiplierFor(5) == 1f, "tier 6 beyond full");
Check(cfg.TierFor("CapeOD") == 7 && cfg.MultiplierFor(7) > cfg.MultiplierFor(6) && cfg.ResistanceFor(7) == HitData.DamageModifier.Immune, "Cape of Odin tier 7");
// class names
Check(cfg.ClassFor(1) == "Simple" && cfg.ClassFor(3) == "Weathered" && cfg.ClassFor(4) == "Hardened" && cfg.ClassFor(5) == "Storm-worn" && cfg.ClassFor(6) == "Odin's Gift", "class per tier");
Check(cfg.LevelName("StormWorn") == "Storm-worn" && cfg.LevelName("Nope") == "Nope", "level display names");
// legacy level keys from old saves
Check(Catalogue.TryLevel("Heavy", out var k1) && k1 == "Hardened", "legacy Heavy -> Hardened");
Check(Catalogue.TryLevel("top", out var k2) && k2 == "StormWorn", "legacy Top (any case) -> StormWorn");
Check(Catalogue.TryLevel("odinsgift", out var k3) && k3 == "OdinsGift", "case-insensitive current key");
Check(!Catalogue.TryLevel("Mythic", out _), "unknown level rejected");
// resistance
Check(cfg.ResistanceFor(1) == HitData.DamageModifier.SlightlyResistant && cfg.ResistanceFor(5) == HitData.DamageModifier.VeryResistant, "resistance by tier");
Check(cfg.ResistanceFor(99) == HitData.DamageModifier.Resistant, "resistance fallback");
System.Console.WriteLine($"{n} checks passed");
