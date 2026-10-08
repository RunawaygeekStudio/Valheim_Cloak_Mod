using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Cloakcraft
{
    /// Material supply for the bench: the player's inventory plus any chest they can open nearby.
    public static class Materials
    {
        static readonly AccessTools.FieldRef<Container, ZNetView> nview = AccessTools.FieldRefAccess<Container, ZNetView>("m_nview");
        static readonly System.Reflection.MethodInfo? checkAccess = AccessTools.Method(typeof(Container), "CheckAccess");
        static List<Container> cache = new List<Container>();
        static float cacheTime = -10f;

        public static List<Container> NearbyChests(Player p)
        {
            var g = Config.Current.General;
            if (!g.UseNearbyChests) return new List<Container>();
            if (Time.time - cacheTime < 1f) return cache;
            cacheTime = Time.time;
            long id = p.GetPlayerID(); var pos = p.transform.position; float r2 = g.ChestRadius * g.ChestRadius;
            cache = Object.FindObjectsByType<Container>(FindObjectsSortMode.None)
                .Where(c => c != null && c.m_wagon == null && (c.transform.position - pos).sqrMagnitude <= r2)
                .Where(c => checkAccess == null || (bool)checkAccess.Invoke(c, new object[] { id }))
                .Where(c => !c.IsInUse() || c.IsOwner())
                .Where(c => !c.m_checkGuardStone || PrivateArea.CheckAccess(c.transform.position, 0f, false))
                .OrderBy(c => (c.transform.position - pos).sqrMagnitude)
                .ToList();
            return cache;
        }

        public static int Count(Player p, string token)
            => p.GetInventory().CountItems(token) + NearbyChests(p).Sum(c => c.GetInventory().CountItems(token));

        public static int CountInChests(Player p, string token) => NearbyChests(p).Sum(c => c.GetInventory().CountItems(token));

        /// Takes from the player's pack first, then nearest chests. Claims ownership of a chest so the change saves.
        public static void Remove(Player p, string token, int amount)
        {
            var inv = p.GetInventory();
            int take = Mathf.Min(amount, inv.CountItems(token));
            if (take > 0) { inv.RemoveItem(token, take); amount -= take; }
            foreach (var c in NearbyChests(p))
            {
                if (amount <= 0) break;
                var ci = c.GetInventory();
                take = Mathf.Min(amount, ci.CountItems(token));
                if (take <= 0) continue;
                if (!c.IsOwner()) nview(c).ClaimOwnership();
                ci.RemoveItem(token, take);
                amount -= take;
            }
        }
    }
}
