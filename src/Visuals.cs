using System.Collections.Generic;
using UnityEngine;

namespace Cloakcraft
{
    /// MVP visual: tint the equipped cloak's materials towards the augmentation colour.
    /// ponytail: colour tint only. Particles, trims and activation bursts are roadmap.
    public static class Visuals
    {
        static readonly Dictionary<Material, Color> originals = new Dictionary<Material, Color>(); // to restore
        public static Augmentation? Current;   // what the local player's cloak should show

        public static void Apply(Player player, Augmentation aug)
        {
            Clear();
            Current = aug;
            var v = Config.Current.VisualEffects;
            if (!v.Enabled || !v.Tints.TryGetValue(aug.Id, out var rgba) || rgba.Length < 3) return;
            var tint = new Color(rgba[0], rgba[1], rgba[2], 1f);
            float k = Mathf.Clamp01(v.TintStrength);
            var instances = Refs.ShoulderInstances(Refs.Vis(player));
            if (instances == null) return;
            foreach (var go in instances)
            {
                if (go == null) continue;
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                    foreach (var m in r.materials)
                    {
                        if (m == null || !m.HasProperty("_Color")) continue;
                        originals[m] = m.color;
                        m.color = Color.Lerp(m.color, tint, k);
                    }
            }
        }

        public static void Clear()
        {
            foreach (var kv in originals) if (kv.Key != null) kv.Key.color = kv.Value;
            originals.Clear();
            Current = null;
        }

        /// VisEquipment rebuilt the cloak model (see Patches.VisEquipment_SetShoulderEquipped): re-tint it.
        public static void OnCloakModelChanged(VisEquipment vis)
        {
            var p = Player.m_localPlayer;
            if (p == null || Current == null || Refs.Vis(p) != vis) return;
            Apply(p, Current);
        }
    }
}
