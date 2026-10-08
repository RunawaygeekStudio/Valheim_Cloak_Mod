using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;

namespace Cloakcraft
{
    public enum TimerMode { Continuous, RelevantCondition }

    public class ExtraMaterial { public string Prefab = ""; public int Amount = 1; }

    public class LevelConfig
    {
        public string Name = "";
        public int Cost = 1;
        public List<ExtraMaterial> Extra = new List<ExtraMaterial>();
        public float DurationMinutes = 10f;
        public int MinCloakTier = 1;
    }

    public class AugmentationConfig
    {
        public bool Enabled = true;
        public string Name = "";
        public string Description = "";
        public string Material = "";
        public TimerMode TimerMode = TimerMode.Continuous;
        public float Strength = 1f;
        public float JumpStrength = 0f;      // Lightweave: extra jump height at full potential (0.1 = 10 percent)
        public Dictionary<string, LevelConfig> Levels = new Dictionary<string, LevelConfig>();
    }

    public class GeneralConfig
    {
        public bool Enabled = true;
        public bool DebugLogging = false;
        public string CraftingStation = "piece_workbench"; // fallback when UseCloakStation is off or a cloak has no recipe
        public bool UseCloakStation = true;
        public bool UnlockCapeOfOdin = false;               // clears the DLC flag on CapeOD so anyone can craft and wear it                  // treat each cloak at the station its own recipe uses
        public bool UseNearbyChests = false;   // count and take materials from chests near the bench
        public float ChestRadius = 10f;
    }

    public class VisualConfig
    {
        public bool Enabled = true;
        public float TintStrength = 0.35f;
        public Dictionary<string, float[]> Tints = new Dictionary<string, float[]>();
        public Dictionary<string, FxConfig> Particles = new Dictionary<string, FxConfig>();
    }

    public class Config
    {
        public const int CurrentVersion = 32;
        public int ConfigVersion = 0;
        public GeneralConfig General = new GeneralConfig();
        public Dictionary<string, int> CloakTiers = new Dictionary<string, int>();
        public Dictionary<string, float> TierMultiplier = new Dictionary<string, float>();
        public Dictionary<string, string> ResistanceByTier = new Dictionary<string, string>();
        public Dictionary<string, AugmentationConfig> Augmentations = new Dictionary<string, AugmentationConfig>();
        public VisualConfig VisualEffects = new VisualConfig();

        public static Config Current = new Config();
        public static string FilePath => Path.Combine(BepInEx.Paths.ConfigPath, "Cloakcraft.json");

        public static void Load()
        {
            try
            {
                var cfg = File.Exists(FilePath) ? JsonConvert.DeserializeObject<Config>(File.ReadAllText(FilePath)) : null;
                if (cfg == null || cfg.ConfigVersion < CurrentVersion)
                {
                    if (cfg != null) { File.Copy(FilePath, FilePath + ".v" + cfg.ConfigVersion + ".bak", true); Plugin.Log.LogWarning("Config format changed; old file backed up, defaults written"); }
                    File.WriteAllText(FilePath, DefaultJson());
                    cfg = Defaults();
                }
                Current = cfg;
                Plugin.Log.LogInfo($"Loaded config: {Current.Augmentations.Count} augmentations");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Failed to load {FilePath}, using built-in defaults: {e.Message}");
                Current = Defaults();
            }
        }

        static Config Defaults() => JsonConvert.DeserializeObject<Config>(DefaultJson()) ?? new Config();

        static string DefaultJson()
        {
            using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Cloakcraft.default-config.json")
                          ?? throw new Exception("embedded default config missing");
            using var r = new StreamReader(s);
            return r.ReadToEnd();
        }

        public int TierOf(ItemDrop.ItemData? cloak) => TierFor(cloak?.m_dropPrefab != null ? cloak.m_dropPrefab.name : "");

        public int TierFor(string cloakPrefabName)
            => Math.Max(1, Math.Min(7, CloakTiers.TryGetValue(cloakPrefabName, out var t) ? t : CloakTiers.TryGetValue("Default", out var d) ? d : 1));

        public float MultiplierFor(int tier)
            => TierMultiplier.TryGetValue(tier.ToString(), out var m) ? m : 1f;

        public HitData.DamageModifier ResistanceFor(int tier)
        {
            if (ResistanceByTier.TryGetValue(tier.ToString(), out var s) &&
                Enum.TryParse<HitData.DamageModifier>(s, true, out var mod))
                return mod;
            return HitData.DamageModifier.Resistant;
        }

        // Levels share Name and MinCloakTier across augmentations by design; read them from the first one that has the key.
        LevelConfig? Level(string key) => Augmentations.Values.Select(a => a.Levels.TryGetValue(key, out var l) ? l : null).FirstOrDefault(l => l != null);

        /// Cloak class name for a tier: the highest treatment level the tier meets.
        public string ClassFor(int tier) => LevelName(Catalogue.Levels.LastOrDefault(lv => (Level(lv)?.MinCloakTier ?? 99) <= tier) ?? Catalogue.Levels[0]);

        /// Display name for a level key ("StormWorn" -> "Storm-worn").
        public string LevelName(string key) { var n = Level(key)?.Name; return string.IsNullOrEmpty(n) ? key : n!; }
    }
}
