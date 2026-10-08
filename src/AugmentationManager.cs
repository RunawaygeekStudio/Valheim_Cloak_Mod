using HarmonyLib;
using UnityEngine;

namespace Cloakcraft
{
    public static class AugmentationManager
    {
        public static ItemDrop.ItemData? Shoulder(Humanoid h) => Refs.Shoulder(h);

        public static SE_Cloakcraft? ActiveEffect(Character c)
        {
            foreach (var se in c.GetSEMan().GetStatusEffects())
                if (se is SE_Cloakcraft s) return s;
            return null;
        }

        /// Put an augmentation on any cloak the player holds. Bench UI and console both use this.
        public static void ApplyDirect(Player player, ItemDrop.ItemData cloak, Augmentation aug, string level, float seconds)
        {
            bool equipped = cloak == Shoulder(player);
            if (equipped) RemoveEffect(player);
            CloakState.Write(cloak, aug, level, seconds);
            player.Message(MessageHud.MessageType.TopLeft, aug.DisplayName + " applied to " + Localization.instance.Localize(cloak.m_shared.m_name));
            if (equipped && aug.Effect == EffectKind.BlockRainWet && !player.InWater())
                player.GetSEMan().RemoveStatusEffect(SEMan.s_statusEffectWet, quiet: true);
            if (equipped) Sync(player);
        }

        public static void ClearCloak(Player player)
        {
            RemoveEffect(player);
            var cloak = Shoulder(player);
            if (cloak != null) CloakState.Clear(cloak);
        }

        static void RemoveEffect(Character c)
        {
            var se = ActiveEffect(c);
            if (se != null) { se.Cloak = null; c.GetSEMan().RemoveStatusEffect(se, quiet: true); }
        }

        /// Keep the status effect in step with whatever cloak is equipped. Runs every frame for the local player.
        public static void Sync(Player player)
        {
            var seman = player.GetSEMan();
            var cloak = Shoulder(player);
            var se = ActiveEffect(player);
            bool hasState = CloakState.TryRead(cloak, out var state);

            if (se != null && (!hasState || se.Cloak != cloak || se.Aug.Id != state.Aug.Id))
            {
                se.Cloak = null;                                // detach so Stop() doesn't clear another cloak's data
                seman.RemoveStatusEffect(se, quiet: true);
                se = null;
                Visuals.Clear();
            }

            if (!hasState || cloak == null) { Visuals.Clear(); return; }
            if (state.RemainingSeconds <= 0f)
            {
                if (se != null) se.Expire(); else CloakState.Clear(cloak);
                Visuals.Clear();
                return;
            }

            if (se == null)
            {
                var proto = ObjectDB.instance.GetStatusEffect(state.Aug.StatusEffectName.GetStableHashCode()) as SE_Cloakcraft;
                if (proto == null) return;
                if (!(seman.AddStatusEffect(proto, resetTime: true) is SE_Cloakcraft added)) return;
                int tier = Config.Current.TierOf(cloak);
                float total = state.Aug.Cfg.Levels.TryGetValue(state.Level, out var lc) ? lc.DurationMinutes * 60f : state.RemainingSeconds;
                added.Init(cloak, state.Level, tier, state.RemainingSeconds, Mathf.Max(total, state.RemainingSeconds));
                Visuals.Apply(player, state.Aug);
            }
        }
    }
}
