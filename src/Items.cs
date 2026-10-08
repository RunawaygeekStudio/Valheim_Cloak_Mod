using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Cloakcraft
{
    /// Registers one status effect per augmentation in ObjectDB. Idempotent.
    public static class Items
    {
        static readonly List<SE_Cloakcraft> statusEffects = new List<SE_Cloakcraft>();

        public static void Register(ObjectDB db)
        {
            if (db == null || db.m_items.Count == 0) return; // main-menu ObjectDB is empty
            foreach (var aug in Catalogue.All)
            {
                var se = statusEffects.Find(s => s.Aug.Id == aug.Id);
                if (se == null) { se = SE_Cloakcraft.Create(aug); statusEffects.Add(se); }
                else { se.Aug = aug; se.m_icon = IconFor(aug); }
                if (!db.m_StatusEffects.Contains(se)) db.m_StatusEffects.Add(se);
            }
            if (Config.Current.General.UnlockCapeOfOdin)
            {
                var od = db.GetItemPrefab("CapeOD")?.GetComponent<ItemDrop>();
                if (od != null) od.m_itemData.m_shared.m_dlc = ""; // the only gate: equip, craft and recipe list all check this string
            }
            Localisation.AddWords(Localization.instance); // safe here: not inside the Localization constructor
            Plugin.Log.LogInfo($"Registered {statusEffects.Count} status effects");
        }

        public static Sprite? IconFor(Augmentation aug)
            => Icons.Get(aug.Id) ?? ObjectDB.instance?.GetItemPrefab(aug.Cfg.Material)?.GetComponent<ItemDrop>()?.m_itemData.GetIcon();
    }

    public static class Localisation
    {
        static readonly MethodInfo? addWord = AccessTools.Method(typeof(Localization), "AddWord");

        public static void AddWords(Localization? loc)
        {
            if (loc == null || addWord == null) return;
            Add(loc, "cloakcraft_msg_replace_header", "Replace augmentation?");
            Add(loc, "cloakcraft_msg_replace_text", "Your cloak already has $1 ($2 left). Replace it with $3?");
        }

        static void Add(Localization loc, string key, string text) => addWord!.Invoke(loc, new object[] { key, text });
    }
}
