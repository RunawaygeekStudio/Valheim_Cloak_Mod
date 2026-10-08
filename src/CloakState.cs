using System.Globalization;

namespace Cloakcraft
{
    /// Augmentation state lives in the cloak's m_customData, so it is saved with the inventory,
    /// follows the cloak into chests and tombstones, and syncs to other clients with the item.
    public static class CloakState
    {
        const string KeyAug = "cloakcraft.aug";
        const string KeyLevel = "cloakcraft.level";
        const string KeyRemaining = "cloakcraft.remaining"; // seconds

        public struct State
        {
            public Augmentation Aug;
            public string Level;
            public float RemainingSeconds;
        }

        public static bool IsCloak(ItemDrop.ItemData? item)
            => item != null && item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shoulder;

        public static bool TryRead(ItemDrop.ItemData? item, out State state)
        {
            state = default;
            if (item == null || !item.m_customData.TryGetValue(KeyAug, out var id)) return false;
            var aug = Catalogue.ById(id);
            if (aug == null) return false; // disabled or unknown in this config; treat as none
            state.Aug = aug;
            state.Level = item.m_customData.TryGetValue(KeyLevel, out var lvl) && Catalogue.TryLevel(lvl, out var key) ? key : Catalogue.Levels[0];
            state.RemainingSeconds = item.m_customData.TryGetValue(KeyRemaining, out var r) &&
                                     float.TryParse(r, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : 0f;
            return true;
        }

        public static void Write(ItemDrop.ItemData item, Augmentation aug, string level, float remainingSeconds)
        {
            item.m_customData[KeyAug] = aug.Id;
            item.m_customData[KeyLevel] = level;
            WriteRemaining(item, remainingSeconds);
        }

        public static void WriteRemaining(ItemDrop.ItemData item, float remainingSeconds)
            => item.m_customData[KeyRemaining] = remainingSeconds.ToString("0.0", CultureInfo.InvariantCulture);

        public static void Clear(ItemDrop.ItemData item)
        {
            item.m_customData.Remove(KeyAug);
            item.m_customData.Remove(KeyLevel);
            item.m_customData.Remove(KeyRemaining);
        }
    }
}
