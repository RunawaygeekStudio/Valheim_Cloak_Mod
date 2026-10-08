using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Cloakcraft
{
    /// Private game members reached by reflection. tests/PatchTargets checks these names exist.
    static class Refs
    {
        public static readonly AccessTools.FieldRef<Humanoid, ItemDrop.ItemData?> Shoulder = AccessTools.FieldRefAccess<Humanoid, ItemDrop.ItemData?>("m_shoulderItem");
        public static readonly AccessTools.FieldRef<Humanoid, VisEquipment> Vis = AccessTools.FieldRefAccess<Humanoid, VisEquipment>("m_visEquipment");
        public static readonly AccessTools.FieldRef<VisEquipment, List<GameObject>> ShoulderInstances = AccessTools.FieldRefAccess<VisEquipment, List<GameObject>>("m_shoulderItemInstances");
        public static readonly AccessTools.FieldRef<Player, bool> UnderRoof = AccessTools.FieldRefAccess<Player, bool>("m_underRoof");
        public static readonly AccessTools.FieldRef<Character, Animator> Animator = AccessTools.FieldRefAccess<Character, Animator>("m_animator");
        public static readonly AccessTools.FieldRef<SEMan, Character> SECharacter = AccessTools.FieldRefAccess<SEMan, Character>("m_character");
        public static readonly AccessTools.FieldRef<ZNetScene, Dictionary<int, GameObject>> NamedPrefabs = AccessTools.FieldRefAccess<ZNetScene, Dictionary<int, GameObject>>("m_namedPrefabs");
    }
}
