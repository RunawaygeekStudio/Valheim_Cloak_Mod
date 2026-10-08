using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Cloakcraft
{
    public class FxConfig
    {
        public string Source = "";          // "ext:" = the workbench-extension sparkles; "se:SlowFall" = a vanilla status effect's start effects; "prefab:StaffFireball" = an item's particle subtree
        public float Scale = 1f;            // transform scale of the spawned effect
        public float Emission = 1f;         // multiplier on particles per second
        public float Glow = 1f;             // multiplier on particle size (the soft radial glow of each particle), no light emitted
        public float[]? Tint;               // RGBA 0-1, overrides particle start colour (local player only)
        public string Bone = "Spine1";       // rig bone the effect hangs from (Spine, Spine1, Spine2, Hips...), so it moves with the cape
        public float[] Offset = { 0f, -0.15f, -0.3f }; // metres from the bone: x right, y up, z forward (negative = behind, at the cape)
        public float Speed = -1f;          // particle start speed in m/s; the workbench effect fires its sparks fast along a line, so slow them down (-1 keeps the donor speed)
        public float[]? Drift;              // steady drift in m/s [x, y, z] in the bone's axes (y up = rising); Speed adds random jitter on top
        public float[]? Spread;             // emitter box size in metres [x, y, z] around the bone; null keeps the donor shape
        public string Only = "";            // keep only particle systems whose name contains this (drops a donor's mist / fog layers); names are logged
        public bool OnlyWhileTicking = false; // conditional timers: emit only while the timer runs (in rain, in cold...)
        public float LightRange = 2f;       // soft glow reach in metres, wisp-like; 0 = no light at all
        public float LightIntensity = 0.3f; // keep low so caves still need a torch
    }

    /// Particles borrowed from the game's own prefabs, cloned under a rig bone of the wearer and tuned from config.
    /// ponytail: one static `systems` array = one treated cloak per client (only the local player's effects run here).
    public static class Fx
    {
        static ParticleSystem[] systems = System.Array.Empty<ParticleSystem>();

        /// Start effects for an augmentation, or null when none configured / found.
        public static GameObject[]? PrefabsFor(Augmentation aug)
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
                return se.m_startEffects.m_effectPrefabs.Where(e => e.m_enabled && e.m_prefab != null).Select(e => e.m_prefab).ToArray();
            }
            if (parts[0] == "ext") // the sparkle line a station extension draws to its workbench: "ext:piece_workbench_ext1", or "ext:" for the first extension found
            {
                var piece = parts[1].Length > 0 ? ZNetScene.instance?.GetPrefab(parts[1]) : ZNetScene.instance?.m_prefabs.FirstOrDefault(p => p != null && p.GetComponent<StationExtension>()?.m_connectionPrefab != null);
                var conn = piece?.GetComponent<StationExtension>()?.m_connectionPrefab;
                if (conn == null) { Plugin.Log.LogWarning($"Fx {aug.Id}: no station extension '{parts[1]}' with a connection effect"); return null; }
                return new[] { conn };
            }
            if (parts[0] == "prefab")
            {
                // "prefab:StaffFireball" takes the particle subtree of an item or creature (never the whole networked object); "prefab:Name/Child" picks a child by path
                var pc = parts[1].Split(new[] { '/' }, 2);
                var prefab = ZNetScene.instance?.GetPrefab(pc[0]);
                var go = prefab == null ? null : pc.Length == 2 ? prefab.transform.Find(pc[1])?.gameObject : prefab.GetComponent<ZNetView>() == null ? prefab : prefab.GetComponentInChildren<ParticleSystem>(true)?.gameObject;
                if (go == null) { Plugin.Log.LogWarning($"Fx {aug.Id}: prefab '{parts[1]}' not found or has no particles (try: cloakcraft fx list <text>)"); return null; }
                return new[] { go };
            }
            return null;
        }

        /// Spawn the prefabs ourselves under the configured rig bone (the game's EffectList left ours loose in the world), then tune.
        public static GameObject[] Spawn(Augmentation aug, Character c)
        {
            var prefabs = PrefabsFor(aug);
            if (prefabs == null || !Config.Current.VisualEffects.Particles.TryGetValue(aug.Id, out var fx)) return System.Array.Empty<GameObject>();
            var bone = Utils.FindChild(c.transform, fx.Bone) ?? c.transform;
            var k = bone.lossyScale; // the rig's bones carry a 95x scale: undo it so metres are metres (and the prefab's own child offsets stay small)
            var inv = new Vector3(1f / Mathf.Max(k.x, 1e-4f), 1f / Mathf.Max(k.y, 1e-4f), 1f / Mathf.Max(k.z, 1e-4f));
            var list = prefabs.Select(p =>
            {
                bool was = p.activeSelf; p.SetActive(false); // clone inactive: donor scripts (ZNetView, light flicker, timed destroy) must never Awake
                var g = Object.Instantiate(p, bone); p.SetActive(was);
                g.name = "Cloakcraft_Fx_" + aug.Id; g.transform.localScale = inv;
                foreach (var c in g.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(c);
                foreach (var l in g.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(l);
                foreach (var r in g.GetComponentsInChildren<Renderer>(true)) if (!(r is ParticleSystemRenderer)) Object.DestroyImmediate(r); // item prefabs carry their mesh next to the particles
                return g;
            }).ToArray();
            Tune(aug, list);
            return list;
        }

        /// Local tweaks on the spawned instances (other players see the vanilla look).
        static void Tune(Augmentation aug, GameObject[] instances)
        {
            systems = System.Array.Empty<ParticleSystem>();
            if (!Config.Current.VisualEffects.Particles.TryGetValue(aug.Id, out var fx)) return;
            var all = instances.SelectMany(g => g.GetComponentsInChildren<ParticleSystem>(true)).ToArray();
            bool Keep(ParticleSystem ps) => fx.Only.Length == 0 || ps.name.IndexOf(fx.Only, System.StringComparison.OrdinalIgnoreCase) >= 0;
            systems = all.Where(Keep).ToArray();
            foreach (var g in instances)
            {
                g.transform.localScale *= fx.Scale; g.SetActive(true);
                var off = new Vector3(fx.Offset[0], fx.Offset.Length > 1 ? fx.Offset[1] : 0f, fx.Offset.Length > 2 ? fx.Offset[2] : 0f);
                var pk = g.transform.parent != null ? g.transform.parent.lossyScale : Vector3.one;
                g.transform.localPosition = new Vector3(off.x / Mathf.Max(pk.x, 1e-4f), off.y / Mathf.Max(pk.y, 1e-4f), off.z / Mathf.Max(pk.z, 1e-4f)); g.transform.localRotation = Quaternion.identity;
            }
            foreach (var ps in all) if (!Keep(ps)) { var m0 = ps.main; m0.playOnAwake = false; var em0 = ps.emission; em0.enabled = false; ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); }
            if (fx.LightRange > 0f && instances.Length > 0)
            {
                var l = instances[0].AddComponent<Light>(); l.type = LightType.Point; l.range = fx.LightRange; l.intensity = fx.LightIntensity; l.shadows = LightShadows.None;
                l.color = fx.Tint != null && fx.Tint.Length >= 3 ? new Color(fx.Tint[0], fx.Tint[1], fx.Tint[2]) : systems.Length > 0 ? systems[0].main.startColor.color : Color.white;
            }
            foreach (var ps in systems)
            {
                var mn = ps.main; mn.scalingMode = ParticleSystemScalingMode.Local; mn.loop = true; // Local: rig bones carry a scale, Hierarchy mode blew sizes, speeds and spread up with it
                if (fx.Speed >= 0f) mn.startSpeed = fx.Speed;
                if (fx.Spread != null && fx.Spread.Length >= 3) { var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(fx.Spread[0], fx.Spread[1], fx.Spread[2]); sh.position = Vector3.zero; sh.randomDirectionAmount = 1f; }
                if (fx.Drift != null && fx.Drift.Length >= 3) { var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local; vel.x = fx.Drift[0]; vel.y = fx.Drift[1]; vel.z = fx.Drift[2]; }
                mn.startSizeMultiplier *= fx.Glow;
                var em = ps.emission; em.enabled = true; em.rateOverTimeMultiplier *= fx.Emission; em.rateOverDistanceMultiplier *= fx.Emission;
                if (fx.Tint != null && fx.Tint.Length >= 3)
                {
                    var tint = new Color(fx.Tint[0], fx.Tint[1], fx.Tint[2], fx.Tint.Length > 3 ? fx.Tint[3] : 1f);
                    var main = ps.main; main.startColor = tint;
                    var col = ps.colorOverLifetime; if (col.enabled) { var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(tint, 0f), new GradientColorKey(tint, 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(tint.a, 0.2f), new GradientAlphaKey(0f, 1f) }); col.color = g; } // donor gradients would wash the tint back to white
                    var r = ps.GetComponent<ParticleSystemRenderer>();
                    if (r != null && r.material != null) foreach (var prop in new[] { "_Color", "_TintColor", "_EmissionColor", "_BaseColor" }) if (r.material.HasProperty(prop)) r.material.SetColor(prop, tint); // whichever the shader reads
                }
            }
            foreach (var ps in systems) ps.Play();
            Plugin.Log.LogInfo($"Fx {aug.Id}: '{fx.Source}' spawned {instances.Length} effect(s), {systems.Length} particle system(s), scale {fx.Scale}, emission x{fx.Emission}, glow x{fx.Glow}, light {fx.LightRange}m @ {fx.LightIntensity}, bone {fx.Bone}; systems: {string.Join(", ", all.Select(ps => ps.name).Distinct())}");
        }

        /// Debug (General.DebugLogging): what each live system is doing, ~2s after spawn.
        public static void Report(Augmentation aug)
        {
            var pl = Player.m_localPlayer;
            foreach (var ps in systems)
            {
                if (ps == null) continue;
                var m = ps.main; var em = ps.emission; var r = ps.GetComponent<ParticleSystemRenderer>();
                var parent = ps.transform.parent; var top = ps.transform; while (top.parent != null && top.parent.GetComponent<Character>() == null && top.parent != top.root) top = top.parent;
                Plugin.Log.LogInfo($"Fx {aug.Id} where '{ps.name}': pos={ps.transform.position} player={(pl != null ? pl.transform.position : Vector3.zero)} eye={(pl != null ? pl.GetEyePoint() : Vector3.zero)} parent={(parent != null ? parent.name : "none")} localPos={top.localPosition} speed={m.startSpeed.constant:0.##} color={m.startColor.color} rendererEnabled={(r != null && r.enabled)} tex={(r != null && r.sharedMaterial != null && r.sharedMaterial.mainTexture != null ? r.sharedMaterial.mainTexture.name : "none")} bounds={(r != null ? r.bounds.size : Vector3.zero)} layer={ps.gameObject.layer}");
                Plugin.Log.LogInfo($"Fx {aug.Id} system '{ps.name}': playing={ps.isPlaying} alive={ps.particleCount} loop={m.loop} dur={m.duration:0.##} life={m.startLifetime.constant:0.##} rate={em.rateOverTime.constant:0.##}/s dist={em.rateOverDistance.constant:0.##}/m size={m.startSize.constant:0.###} space={m.simulationSpace} shape={ps.shape.shapeType} scale={ps.transform.lossyScale} renderer={(r == null ? "none" : r.renderMode + "/" + (r.sharedMaterial == null ? "nomat" : r.sharedMaterial.shader.name))} active={ps.gameObject.activeInHierarchy}");
            }
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
            if (ZNetScene.instance != null)
                foreach (var p in ZNetScene.instance.m_prefabs)
                    if (p != null && p.name.ToLowerInvariant().Contains(filter) && p.GetComponentInChildren<ParticleSystem>(true) != null)
                        yield return "prefab:" + p.name + "  (" + string.Join(", ", p.GetComponentsInChildren<ParticleSystem>(true).Select(ps => ps.name).Distinct().Take(4)) + ")";
        }
    }
}
