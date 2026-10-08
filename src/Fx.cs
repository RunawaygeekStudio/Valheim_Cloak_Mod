using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Cloakcraft
{
    public class FxConfig
    {
        public string Source = "";          // "se:SlowFall" = a vanilla status effect's start effects (feather cape, Poison, Frost, Wet, Burning); "env:Snow" = a weather system; "prefab:Wisp" = any particle prefab
        public float Scale = 1f;            // transform scale of the spawned effect
        public float Emission = 1f;         // multiplier on particles per second
        public float Glow = 1f;             // multiplier on particle size (the soft radial glow of each particle), no light emitted
        public float[]? Tint;               // RGBA 0-1, overrides particle start colour (local player only)
        public bool OnlyWhileTicking = false; // conditional timers: emit only while the timer runs (in rain, in cold...)
        public float LightRange = 2f;       // soft glow reach in metres, wisp-like; 0 = no light at all
        public float LightIntensity = 0.3f; // keep low so caves still need a torch
    }

    /// Particles the same way the feather cape does it: the status effect's own start effects. The game spawns,
    /// networks and removes them with the effect; we only pick the prefabs and tune the local copies.
    public static class Fx
    {
        static ParticleSystem[] systems = System.Array.Empty<ParticleSystem>();

        /// Start effects for an augmentation, or null when none configured / found.
        public static EffectList? EffectsFor(Augmentation aug)
        {
            var v = Config.Current.VisualEffects;
            if (!v.Enabled || !v.Particles.TryGetValue(aug.Id, out var fx) || string.IsNullOrEmpty(fx.Source)) return null;
            var parts = fx.Source.Split(new[] { ':' }, 2);
            if (parts.Length != 2) return null;
            if (parts[0] == "se")
            {
                var all = ObjectDB.instance?.m_StatusEffects;
                var se = all?.FirstOrDefault(s => s.name == parts[1]) ?? all?.FirstOrDefault(s => s.name.ToLowerInvariant().Contains(parts[1].ToLowerInvariant()));
                if (se == null || se.m_startEffects.m_effectPrefabs.Length == 0) { Plugin.Log.LogWarning($"Fx {aug.Id}: status effect '{parts[1]}' not found or has no start effects"); return null; }
                return new EffectList { m_effectPrefabs = se.m_startEffects.m_effectPrefabs };
            }
            if (parts[0] == "env") // a weather system (Snow, Rain, Mist...) attached to the player; emission is switched on in Tune
            {
                var env = EnvMan.instance?.m_environments.FirstOrDefault(e => e.m_name == parts[1]);
                var go = env?.m_psystems?.FirstOrDefault(ps => ps != null && ps.GetComponentInChildren<ParticleSystem>(true) != null);
                if (go == null) { Plugin.Log.LogWarning($"Fx {aug.Id}: environment '{parts[1]}' not found or has no particles"); return null; }
                return new EffectList { m_effectPrefabs = new[] { new EffectList.EffectData { m_prefab = go, m_enabled = true, m_attach = true, m_inheritParentRotation = true } } };
            }
            if (parts[0] == "prefab")
            {
                // "prefab:StaffFireball" takes the particle subtree of an item or creature (never the whole networked object); "prefab:Name/Child" picks a child by path
                var pc = parts[1].Split(new[] { '/' }, 2);
                var prefab = ZNetScene.instance?.GetPrefab(pc[0]);
                var go = prefab == null ? null : pc.Length == 2 ? prefab.transform.Find(pc[1])?.gameObject : prefab.GetComponent<ZNetView>() == null ? prefab : prefab.GetComponentInChildren<ParticleSystem>(true)?.gameObject;
                if (go == null) { Plugin.Log.LogWarning($"Fx {aug.Id}: prefab '{parts[1]}' not found or has no particles (try: cloakcraft fx list <text>)"); return null; }
                return new EffectList { m_effectPrefabs = new[] { new EffectList.EffectData { m_prefab = go, m_enabled = true, m_attach = true, m_inheritParentRotation = true } } };
            }
            return null;
        }

        /// Local tweaks on the spawned instances (other players see the vanilla look).
        public static void Tune(Augmentation aug, GameObject[]? instances)
        {
            systems = System.Array.Empty<ParticleSystem>();
            if (instances == null || !Config.Current.VisualEffects.Particles.TryGetValue(aug.Id, out var fx)) return;
            systems = instances.Where(g => g != null).SelectMany(g => g.GetComponentsInChildren<ParticleSystem>(true)).ToArray();
            foreach (var g in instances)
            {
                if (g == null) continue;
                g.transform.localScale *= fx.Scale; g.SetActive(true);
                foreach (var l in g.GetComponentsInChildren<Light>(true)) Object.Destroy(l); // donor lights go; ours below is the only glow
            }
            if (fx.LightRange > 0f && instances.FirstOrDefault(g => g != null) is GameObject host)
            {
                var l = host.AddComponent<Light>(); l.type = LightType.Point; l.range = fx.LightRange; l.intensity = fx.LightIntensity; l.shadows = LightShadows.None;
                l.color = fx.Tint != null && fx.Tint.Length >= 3 ? new Color(fx.Tint[0], fx.Tint[1], fx.Tint[2]) : systems.Length > 0 ? systems[0].main.startColor.color : Color.white;
            }
            foreach (var ps in systems)
            {
                var mn = ps.main; mn.scalingMode = ParticleSystemScalingMode.Hierarchy; mn.loop = true;
                mn.startSizeMultiplier *= fx.Glow;
                var em = ps.emission; em.enabled = true; em.rateOverTimeMultiplier *= fx.Emission; em.rateOverDistanceMultiplier *= fx.Emission;
                if (fx.Tint != null && fx.Tint.Length >= 3) { var main = ps.main; main.startColor = new Color(fx.Tint[0], fx.Tint[1], fx.Tint[2], fx.Tint.Length > 3 ? fx.Tint[3] : 1f); }
            }
            foreach (var ps in systems) ps.Play();
            Plugin.Log.LogInfo($"Fx {aug.Id}: '{fx.Source}' spawned {instances.Count(g => g != null)} effect(s), {systems.Length} particle system(s), scale {fx.Scale}, emission x{fx.Emission}, glow x{fx.Glow}, light {fx.LightRange}m @ {fx.LightIntensity}");
        }

        /// Conditional timers: pause emission while the timer is paused.
        public static void SetTicking(Augmentation aug, bool ticking)
        {
            if (!Config.Current.VisualEffects.Particles.TryGetValue(aug.Id, out var fx) || !fx.OnlyWhileTicking) return;
            foreach (var ps in systems) if (ps != null) { var em = ps.emission; em.enabled = ticking; }
        }

        /// Console helper: names a player can put in Source.
        public static IEnumerable<string> List(string filter)
        {
            filter = filter.ToLowerInvariant();
            if (ObjectDB.instance != null)
                foreach (var s in ObjectDB.instance.m_StatusEffects)
                    if (s.m_startEffects.m_effectPrefabs.Length > 0 && s.name.ToLowerInvariant().Contains(filter))
                        yield return "se:" + s.name + "  (" + string.Join(", ", s.m_startEffects.m_effectPrefabs.Select(e => e.m_prefab?.name).Take(4)) + ")";
            if (EnvMan.instance != null)
                foreach (var e in EnvMan.instance.m_environments)
                    if (e.m_psystems != null && e.m_psystems.Length > 0 && e.m_name.ToLowerInvariant().Contains(filter)) yield return "env:" + e.m_name;
            if (ZNetScene.instance != null)
                foreach (var p in ZNetScene.instance.m_prefabs)
                    if (p != null && p.name.ToLowerInvariant().Contains(filter) && p.GetComponentInChildren<ParticleSystem>(true) != null)
                        yield return "prefab:" + p.name + "  (" + string.Join(", ", p.GetComponentsInChildren<ParticleSystem>(true).Select(ps => ps.name).Distinct().Take(4)) + ")";
        }
    }
}
