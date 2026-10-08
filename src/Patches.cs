using HarmonyLib;

namespace Cloakcraft
{
    // ---- registration ----
    [HarmonyPatch(typeof(ObjectDB), "Awake")]
    static class ObjectDB_Awake
    {
        static void Postfix(ObjectDB __instance) => Items.Register(__instance);
    }

    [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB))]
    static class ObjectDB_CopyOtherDB
    {
        static void Postfix(ObjectDB __instance) => Items.Register(__instance);
    }

    [HarmonyPatch(typeof(Localization), nameof(Localization.SetupLanguage))]
    static class Localization_SetupLanguage
    {
        // The Localization constructor calls SetupLanguage before the static instance is assigned,
        // so never touch Localization.instance here: use the instance Harmony gives us.
        static void Postfix(Localization __instance) => Localisation.AddWords(__instance);
    }

    // ---- Treating Bench tab on the crafting window ----
    [HarmonyPatch(typeof(InventoryGui), "Awake")]
    static class InventoryGui_Awake
    {
        static void Postfix(InventoryGui __instance) { try { BenchUI.Ensure(__instance); } catch (System.Exception e) { Plugin.Log.LogError("Bench UI setup failed: " + e); } }
    }

    [HarmonyPatch(typeof(InventoryGui), "UpdateCraftingPanel")]
    static class InventoryGui_UpdateCraftingPanel
    {
        static void Postfix() => BenchUI.OnCraftingPanelUpdated();
    }

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.OnTabCraftPressed))]
    static class InventoryGui_OnTabCraftPressed { static void Prefix() => BenchUI.Close(); } // before vanilla rebuilds its list

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.OnTabUpgradePressed))]
    static class InventoryGui_OnTabUpgradePressed { static void Prefix() => BenchUI.Close(); }

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Hide))]
    static class InventoryGui_Hide { static void Postfix() => BenchUI.Close(); }

    // While the Augment tab is open the vanilla per-frame recipe refresh would re-show the hidden detail elements.
    [HarmonyPatch(typeof(InventoryGui), "UpdateRecipe")]
    static class InventoryGui_UpdateRecipe
    {
        static bool Prefix() => !BenchUI.Open;
    }

    // ---- per-frame sync for the local player ----
    [HarmonyPatch(typeof(Player), "Update")]
    static class Player_Update
    {
        static void Postfix(Player __instance)
        {
            if (!Config.Current.General.Enabled || __instance != Player.m_localPlayer) return;
            try { AugmentationManager.Sync(__instance); }
            catch (System.Exception e) { if (Config.Current.General.DebugLogging) Plugin.Log.LogError(e); }
        }
    }

    // ---- Rainproof: block the Wet status while the rain check in Player.UpdateEnvStatusEffects runs ----
    // Wet from swimming comes from Character.UpdateWater and is left alone, so Rainproof is rain-only.
    [HarmonyPatch(typeof(Player), "UpdateEnvStatusEffects")]
    static class Player_UpdateEnvStatusEffects
    {
        public static bool Running;
        static void Prefix() => Running = true;
        static void Postfix() => Running = false;
    }

    [HarmonyPatch(typeof(SEMan), nameof(SEMan.AddStatusEffect), typeof(int), typeof(bool), typeof(int), typeof(float), typeof(short))]
    static class SEMan_AddStatusEffect
    {
        static bool Prefix(SEMan __instance, int nameHash, ref StatusEffect __result)
        {
            if (!Player_UpdateEnvStatusEffects.Running || nameHash != SEMan.s_statusEffectWet) return true;
            var c = Refs.SECharacter(__instance);
            if (c == null || AugmentationManager.ActiveEffect(c)?.Aug.Effect != EffectKind.BlockRainWet) return true;
            __result = null!;
            return false;
        }
    }

    // ---- cloak model rebuilt (equip, unequip, quality change): re-apply the tint ----
    [HarmonyPatch(typeof(VisEquipment), "SetShoulderEquipped")]
    static class VisEquipment_SetShoulderEquipped
    {
        static void Postfix(VisEquipment __instance, bool __result) { if (__result) Visuals.OnCloakModelChanged(__instance); }
    }
}
