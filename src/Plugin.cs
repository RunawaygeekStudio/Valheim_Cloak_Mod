using System;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace Cloakcraft
{
    [BepInPlugin(ModGuid, ModName, ModVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string ModGuid = "BenShirley.Cloakcraft";
        public const string ModName = "Cloakcraft";
        public const string ModVersion = "0.7.14";

        internal static ManualLogSource Log = null!;
        Harmony? harmony;

        void Awake()
        {
            Log = Logger;
            Cloakcraft.Config.Load();
            Catalogue.Build();
            harmony = new Harmony(ModGuid);
            harmony.PatchAll();
            Commands.Register();
            Log.LogInfo($"{ModName} {ModVersion} loaded; {Catalogue.All.Count} augmentations enabled; target Valheim 1.0.16");
        }

        void OnDestroy() => harmony?.UnpatchSelf();
    }

    /// Console: F5 then `cloakcraft help`. Cheats require devcommands.
    static class Commands
    {
        public static void Register()
        {
            new Terminal.ConsoleCommand("cloakcraft", "Cloakcraft: status | reload | apply <aug> <level> [minutes] | clear | fx list <text> | items <text>", args =>
            {
                var t = args.Context;
                var sub = args.Length > 1 ? args[1].ToLowerInvariant() : "help";
                var p = Player.m_localPlayer;
                switch (sub)
                {
                    case "status":
                    {
                        if (p == null) { t.AddString("no player"); return; }
                        var cloak = AugmentationManager.Shoulder(p);
                        if (cloak == null) { t.AddString("no cloak equipped"); return; }
                        var tier = Config.Current.TierOf(cloak);
                        t.AddString($"cloak: {cloak.m_dropPrefab?.name} tier {tier} ({Config.Current.ClassFor(tier)} class)");
                        if (CloakState.TryRead(cloak, out var s)) t.AddString($"{s.Aug.Id} {s.Level}, {StatusEffect.GetTimeString(s.RemainingSeconds, true)} left");
                        else t.AddString("no augmentation");
                        var se = AugmentationManager.ActiveEffect(p);
                        t.AddString(se == null ? "no status effect" : $"status effect {se.Aug.Id} paused={se.Paused} ttl={se.GetRemaningTime():0}s");
                        return;
                    }
                    case "reload":
                        Cloakcraft.Config.Load(); Catalogue.Build();
                        if (ObjectDB.instance != null) Items.Register(ObjectDB.instance);
                        t.AddString("Cloakcraft config reloaded");
                        return;
                    case "apply":
                    {
                        if (!Terminal.m_cheat) { t.AddString("devcommands required"); return; }
                        if (p == null) { t.AddString("no player"); return; }
                        if (args.Length < 4) { t.AddString("usage: cloakcraft " + sub + " <augmentation> <Simple|Weathered|Hardened|StormWorn|OdinsGift>"); return; }
                        var aug = Catalogue.ById(args[2]);
                        if (aug == null || !Catalogue.TryLevel(args[3], out var level) || !aug.Cfg.Levels.TryGetValue(level, out var lc)) { t.AddString("augmentations: " + string.Join(", ", Catalogue.All.Select(a => a.Id)) + "; levels: " + string.Join(", ", Catalogue.Levels)); return; }
                        var cloak = AugmentationManager.Shoulder(p);
                        if (!CloakState.IsCloak(cloak)) { t.AddString("equip a cloak first"); return; }
                        var ct = Config.Current.TierOf(cloak);
                        if (ct < lc.MinCloakTier) { t.AddString($"{level} needs cloak tier {lc.MinCloakTier}+, this cloak is tier {ct}"); return; }
                        float mins = args.Length > 4 && float.TryParse(args[4], out var m) ? m : lc.DurationMinutes;
                        AugmentationManager.ApplyDirect(p, cloak!, aug, level, mins * 60f);
                        t.AddString($"applied {aug.Id} {level} for {mins} min");
                        return;
                    }
                    case "items":
                    {
                        var filter = (args.Length > 2 ? args[2] : "").ToLowerInvariant(); int shown = 0;
                        if (ObjectDB.instance != null)
                            foreach (var go in ObjectDB.instance.m_items)
                            {
                                var d = go.GetComponent<ItemDrop>(); if (d == null) continue;
                                var disp = Localization.instance.Localize(d.m_itemData.m_shared.m_name);
                                if (!go.name.ToLowerInvariant().Contains(filter) && !disp.ToLowerInvariant().Contains(filter)) continue;
                                t.AddString(go.name + "  (" + disp + ")"); if (++shown >= 40) { t.AddString("..."); break; }
                            }
                        if (shown == 0) t.AddString("no items match; usage: cloakcraft items <text>");
                        return;
                    }
                    case "fx":
                    {
                        var filter = args.Length > 3 ? args[3] : "";
                        int shown = 0;
                        foreach (var s in Fx.List(filter)) { t.AddString(s); if (++shown >= 40) { t.AddString("... (narrow the filter)"); break; } }
                        if (shown == 0) t.AddString("no particle sources match; usage: cloakcraft fx list <text>");
                        return;
                    }
                    case "ui":
                        foreach (var line in BenchUI.DumpTabs()) Plugin.Log.LogInfo(line);
                        t.AddString("tab hierarchy written to BepInEx log"); return;
                    case "clear":
                        if (p != null) AugmentationManager.ClearCloak(p);
                        t.AddString("cleared");
                        return;
                    default:
                        t.AddString("cloakcraft status | reload | apply <aug> <level> [minutes] | clear | fx list <text> | items <text> | ui");
                        return;
                }
            }, isCheat: false, optionsFetcher: () => new System.Collections.Generic.List<string> { "status", "reload", "apply", "clear", "fx", "items", "ui" });
        }
    }
}
