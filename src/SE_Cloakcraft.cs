using System.Collections.Generic;
using UnityEngine;

namespace Cloakcraft
{
    /// One status effect per augmentation. Gives us the HUD icon, the countdown and every
    /// gameplay hook (damage mods, speed, fall damage, swim stamina, armour, stagger) for free.
    /// Rainproof is the one effect that needs a Harmony patch as well (see Patches.SEMan_AddStatusEffect).
    public class SE_Cloakcraft : StatusEffect
    {
        public Augmentation Aug = null!;
        public string Level = "Simple";
        public int Tier = 1;
        public ItemDrop.ItemData? Cloak;   // the cloak this effect belongs to
        public bool Paused;                // conditional timer not currently ticking

        float saveTimer, spawnedAt;
        List<HitData.DamageModPair> mods = new List<HitData.DamageModPair>();

        public static SE_Cloakcraft Create(Augmentation aug)
        {
            var se = CreateInstance<SE_Cloakcraft>();
            se.name = aug.StatusEffectName;
            se.Aug = aug;
            se.m_name = aug.DisplayName;
            se.m_tooltip = aug.Description;
            se.m_category = "cloakcraft";
            se.m_startMessageType = MessageHud.MessageType.TopLeft;
            se.m_stopMessageType = MessageHud.MessageType.TopLeft;
            se.m_stopMessage = aug.DisplayName + " has worn off";
            se.m_icon = Items.IconFor(aug);
            return se;
        }

        /// Called on the clone after SEMan adds it.
        public void Init(ItemDrop.ItemData cloak, string level, int tier, float remainingSeconds, float totalSeconds)
        {
            Cloak = cloak; Level = level; Tier = tier;
            m_ttl = totalSeconds;
            m_time = Mathf.Max(0f, totalSeconds - remainingSeconds);
            mods = new List<HitData.DamageModPair>(); // own list: Clone() is a MemberwiseClone
            if (Aug.Effect == EffectKind.Resist)
                mods.Add(new HitData.DamageModPair { m_type = Aug.ResistType, m_modifier = Config.Current.ResistanceFor(tier) });
        }

        float Strength => Aug.StrengthFor(Tier);

        GameObject[] fx = System.Array.Empty<GameObject>();

        public override void Setup(Character character)
        {
            base.Setup(character);
            fx = Fx.Spawn(Aug, character); spawnedAt = Time.time;
        }

        public void Expire() => m_time = m_ttl + 1f; // SEMan removes it next tick and Stop() clears the cloak

        bool ConditionActive()
        {
            if (m_character == null) return true;
            switch (Aug.Condition)
            {
                case Condition.Rain:
                    return EnvMan.IsWet() && m_character is Player p && !Refs.UnderRoof(p) && !ShieldGenerator.IsInsideShield(p.transform.position) && !m_character.InWater();
                case Condition.Cold:
                    return EnvMan.IsCold() || EnvMan.IsFreezing();
                case Condition.Swimming:
                    return m_character.IsSwimming();
                default:
                    return true;
            }
        }

        bool reported;
        public override void UpdateStatusEffect(float dt)
        {
            if (!reported && Time.time - spawnedAt > 2f) { reported = true; if (Config.Current.General.DebugLogging) Fx.Report(Aug); }
            bool tick = Aug.Cfg.TimerMode == TimerMode.Continuous || ConditionActive();
            if (Paused == tick) Fx.SetTicking(Aug, tick);
            Paused = !tick;
            if (tick) m_time += dt;

            // Persist remaining time to the cloak about once a second.
            saveTimer += dt;
            if (saveTimer >= 1f && Cloak != null)
            {
                saveTimer = 0f;
                CloakState.WriteRemaining(Cloak, GetRemaningTime());
            }
        }

        void KillFx() { foreach (var g in fx) if (g != null) Destroy(g); fx = System.Array.Empty<GameObject>(); }

        public override void OnDestroy() { KillFx(); base.OnDestroy(); }

        public override void Stop()
        {
            KillFx();
            base.Stop();
            if (Cloak != null && IsDone()) CloakState.Clear(Cloak); // expired, not merely unequipped
        }

        public override string GetIconText()
        {
            var t = base.GetIconText();
            return Paused ? "|| " + t : t;
        }

        /// Compendium / hover text, in the game's own stat-line format and tokens.
        public override string GetTooltipString()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(Aug.Description).Append("\n").Append(Config.Current.LevelName(Level)).Append(" treatment, cloak tier ").Append(Tier).Append("\n\n");
            string pct(float f) => (f * 100f).ToString("+0;-0");
            switch (Aug.Effect)
            {
                case EffectKind.Resist: sb.Append(SE_Stats.GetDamageModifiersTooltipString(mods).TrimStart('\n')).Append("\n"); break;
                case EffectKind.BlockRainWet: sb.Append("Rain: <color=orange>no Wet</color>\n"); break;
                case EffectKind.FallDamage:
                    sb.Append("$item_falldamage: <color=orange>").Append(pct(-Mathf.Clamp01(Strength))).Append("%</color>\n");
                    if (Aug.Cfg.JumpStrength > 0f) sb.Append("$se_jumpheight: <color=orange>").Append(pct(Aug.Cfg.JumpStrength * Config.Current.MultiplierFor(Tier))).Append("%</color>\n");
                    break;
                case EffectKind.MoveSpeed: sb.Append("$item_movement_modifier: <color=orange>").Append(pct(Strength)).Append("%</color>\n"); break;
                case EffectKind.SwimStamina: sb.Append("$se_swimstamina: <color=orange>").Append(pct(-Mathf.Clamp01(Strength))).Append("%</color>\n"); break;
                case EffectKind.Armour:
                    sb.Append("$item_armor: <color=orange>").Append(pct(Strength)).Append("%</color>\n");
                    sb.Append("$se_stagger: <color=orange>").Append(pct(-Mathf.Clamp01(Strength))).Append("%</color>\n");
                    break;
            }
            sb.Append("$se_ttl: <color=orange>").Append(Mathf.CeilToInt(GetRemaningTime())).Append("</color>");
            if (Aug.Cfg.TimerMode == TimerMode.RelevantCondition) sb.Append(Paused ? " (paused)" : " (running)");
            return sb.ToString();
        }

        // ----- gameplay hooks -----
        public override void ModifyDamageMods(ref HitData.DamageModifiers modifiers)
        {
            if (mods.Count > 0) modifiers.Apply(mods);
        }

        public override void ModifyFallDamage(float baseDamage, ref float damage)
        {
            if (Aug.Effect == EffectKind.FallDamage) damage -= baseDamage * Mathf.Clamp01(Strength);
        }

        public override void ModifyJump(Vector3 baseJump, ref Vector3 jump)
        {
            if (Aug.Effect == EffectKind.FallDamage && Aug.Cfg.JumpStrength > 0f) jump += baseJump * (Aug.Cfg.JumpStrength * Config.Current.MultiplierFor(Tier));
        }

        public override void ModifySpeed(float baseSpeed, ref float speed, Character character, Vector3 dir)
        {
            if (Aug.Effect == EffectKind.MoveSpeed) speed += baseSpeed * Strength;
        }

        public override void ModifySwimStaminaUsage(float baseStaminaUse, ref float staminaUse)
        {
            if (Aug.Effect == EffectKind.SwimStamina) staminaUse -= baseStaminaUse * Mathf.Clamp01(Strength);
        }

        public override void ModifyArmorMods(ref float armor)
        {
            if (Aug.Effect == EffectKind.Armour) armor *= 1f + Strength;
        }

        public override void ModifyStagger(float baseValue, ref float use)
        {
            if (Aug.Effect == EffectKind.Armour) use -= baseValue * Mathf.Clamp01(Strength);
        }
    }
}
