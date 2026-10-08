using System;
using System.Collections.Generic;
using System.Linq;

namespace Cloakcraft
{
    /// What an augmentation does in game terms. Values come from config; wiring lives here.
    public enum EffectKind { BlockRainWet, Resist, FallDamage, MoveSpeed, SwimStamina, Armour }

    public enum Condition { None, Rain, Cold, Swimming }

    public class Augmentation
    {
        public string Id = "";            // config key, e.g. "Rainproof"
        public string DisplayName = "";   // "Rainproof"
        public string Description = "";
        public EffectKind Effect;
        public HitData.DamageType ResistType;   // when Effect == Resist
        public Condition Condition;
        public AugmentationConfig Cfg = new AugmentationConfig();

        public string StatusEffectName => "Cloakcraft_" + Id;
        public string ItemPrefab(string level) => $"Cloakcraft_{Id}_{level}";
        public string ItemToken(string level) => $"$cloakcraft_{Id.ToLowerInvariant()}_{level.ToLowerInvariant()}";

        public float StrengthFor(int tier) => Cfg.Strength * Config.Current.MultiplierFor(tier);
    }

    public static class Catalogue
    {
        public static readonly string[] Levels = { "Simple", "Weathered", "Hardened", "StormWorn", "OdinsGift" };
        // Earlier builds used these keys; saved cloaks are mapped on read.
        static readonly Dictionary<string, string> legacy = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        { { "Light", "Simple" }, { "Basic", "Simple" }, { "Mid", "Weathered" }, { "Heavy", "Hardened" }, { "High", "Hardened" }, { "Top", "StormWorn" } };

        // Fixed wiring. Only augmentations listed here can be enabled from config.
        static readonly (string id, string name, EffectKind effect, HitData.DamageType resist, Condition cond, string desc)[] Wiring =
        {
            ("Rainproof",  "Rainseal",    EffectKind.BlockRainWet, 0,                         Condition.Rain,     "Rain no longer makes you Wet. Timer only runs in the rain."),
            ("ToxinWard",  "Honeyward",   EffectKind.Resist,       HitData.DamageType.Poison, Condition.None,     "Resist Poison damage."),
            ("Frostbound", "Frostthread", EffectKind.Resist,       HitData.DamageType.Frost,  Condition.Cold,     "Resist Frost damage. Timer only runs in the cold."),
            ("Emberbound", "Emberstitch", EffectKind.Resist,       HitData.DamageType.Fire,   Condition.None,     "Resist Fire damage."),
            ("Lightweave", "Lightweave",  EffectKind.FallDamage,   0,                         Condition.None,     "Take less fall damage and jump a little higher."),
            ("Fleetfoot",  "Fleetfoot",   EffectKind.MoveSpeed,    0,                         Condition.None,     "Move faster."),
            ("Seabound",   "Tidescale",   EffectKind.SwimStamina,  0,                         Condition.Swimming, "Swimming uses less stamina. Timer only runs while swimming."),
            ("Ironbound",  "Ironweft",    EffectKind.Armour,       0,                         Condition.None,     "More armour, less stagger."),
        };

        public static readonly List<Augmentation> All = new List<Augmentation>();
        static readonly Dictionary<string, Augmentation> byId = new Dictionary<string, Augmentation>(StringComparer.OrdinalIgnoreCase);

        public static void Build()
        {
            All.Clear(); byId.Clear();
            foreach (var w in Wiring)
            {
                if (!Config.Current.Augmentations.TryGetValue(w.id, out var cfg) || !cfg.Enabled) continue;
                if (string.IsNullOrEmpty(cfg.Material)) { Plugin.Log.LogWarning($"{w.id}: no Material in config, skipped"); continue; }
                var a = new Augmentation { Id = w.id, DisplayName = string.IsNullOrEmpty(cfg.Name) ? w.name : cfg.Name, Effect = w.effect, ResistType = w.resist, Condition = w.cond, Description = string.IsNullOrEmpty(cfg.Description) ? w.desc : cfg.Description, Cfg = cfg };
                All.Add(a); byId[a.Id] = a;
            }
            foreach (var key in Config.Current.Augmentations.Keys.Where(k => !Wiring.Any(w => w.id == k)))
                Plugin.Log.LogWarning($"Config augmentation '{key}' has no implementation and is ignored");
        }

        public static Augmentation? ById(string id) => byId.TryGetValue(id, out var a) ? a : null;

        /// Accepts current or legacy level names, any case. Returns the canonical key.
        public static bool TryLevel(string s, out string key)
        {
            key = legacy.TryGetValue(s, out var m) ? m : Levels.FirstOrDefault(l => string.Equals(l, s, StringComparison.OrdinalIgnoreCase)) ?? "";
            return key.Length > 0;
        }
    }
}
