using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cloakcraft
{
    /// "AUGMENT" tab on the crafting window. Three columns (cloaks in your pack, treatments, levels)
    /// drawn on the window's own panel: while the tab is open the vanilla recipe list and detail
    /// content are hidden and our columns take their place. Built from the game's own buttons and font.
    public static class BenchUI
    {
        static InventoryGui? gui;
        static Button? tab;
        static GameObject? panel;
        static Transform? colCloaks, colAugs, colLevels;
        static TMP_Text? summary;
        static Button? applyButton;
        static TMP_FontAsset? font;
        static Image? shade;
        static bool laidOut;
        static readonly List<GameObject> hidden = new List<GameObject>();
        static readonly System.Reflection.MethodInfo? updateCraftingPanel = AccessTools.Method(typeof(InventoryGui), "UpdateCraftingPanel");

        static ItemDrop.ItemData? selCloak;
        static Augmentation? selAug;
        static string? selLevel;
        public static bool Open => panel != null && panel.activeSelf;

        const float RowH = 34f, Gap = 4f, Pad = 12f, DetailH = 110f;
        static readonly Color Dim = new Color(0.55f, 0.5f, 0.42f), Parchment = new Color(0.9f, 0.85f, 0.72f), Orange = new Color(1f, 0.6f, 0.18f);

        // ---------- lifecycle ----------
        public static void Ensure(InventoryGui g)
        {
            if (tab != null) return;
            gui = g;
            font = g.m_recipeName.font;

            tab = Object.Instantiate(g.m_tabUpgrade.gameObject, g.m_tabUpgrade.transform.parent).GetComponent<Button>();
            tab.name = "Cloakcraft_TabAugment";
            tab.transform.SetSiblingIndex(g.m_tabUpgrade.transform.GetSiblingIndex() + 1);
            var rt = (RectTransform)tab.transform; var urt = (RectTransform)g.m_tabUpgrade.transform;
            rt.anchoredPosition = urt.anchoredPosition + new Vector2(urt.rect.width + 6f, 0f);
            var tabText = tab.GetComponentInChildren<TMP_Text>(); var craftText = g.m_tabCraft.GetComponentInChildren<TMP_Text>();
            tabText.text = "AUGMENT";
            if (craftText != null) { tabText.font = craftText.font; tabText.fontSize = craftText.fontSize; tabText.fontStyle = craftText.fontStyle; tabText.characterSpacing = craftText.characterSpacing; }
            tab.onClick = new Button.ButtonClickedEvent();
            tab.onClick.AddListener(OnTabPressed);
            FixSfx(tab);
            tab.gameObject.SetActive(false);

            panel = new GameObject("Cloakcraft_Bench", typeof(RectTransform));
            var prt = (RectTransform)panel.transform; prt.SetParent(g.m_crafting, false);
            prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one; prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;

            var sh = new GameObject("Shade", typeof(RectTransform), typeof(Image)); sh.transform.SetParent(panel.transform, false);
            shade = sh.GetComponent<Image>(); shade.color = new Color(0.11f, 0.085f, 0.075f, 1f); // ponytail: flat colour, hides the vanilla tile and the see-through strip under the recipe list shade.raycastTarget = false;
            var shrt = (RectTransform)sh.transform; shrt.anchorMin = new Vector2(0, 1); shrt.anchorMax = new Vector2(0, 1); shrt.pivot = new Vector2(0, 1);
            colCloaks = Column("Cloaks"); colAugs = Column("Treatments"); colLevels = Column("Levels");

            summary = Text(panel.transform, "", 14f, Parchment);
            var srt = (RectTransform)summary.transform; srt.anchorMin = new Vector2(0, 0); srt.anchorMax = new Vector2(1, 0); srt.pivot = new Vector2(0.5f, 0);
            srt.offsetMin = new Vector2(Pad, 50f); srt.offsetMax = new Vector2(-Pad, 90f);
            summary.alignment = TextAlignmentOptions.BottomLeft; summary.textWrappingMode = TextWrappingModes.Normal;

            applyButton = Object.Instantiate(g.m_craftButton.gameObject, panel.transform).GetComponent<Button>();
            applyButton.name = "Cloakcraft_Apply";
            var art = (RectTransform)applyButton.transform; art.anchorMin = new Vector2(1, 0); art.anchorMax = new Vector2(1, 0); art.pivot = new Vector2(1, 0);
            art.anchoredPosition = new Vector2(-Pad, Pad); art.sizeDelta = new Vector2(220f, 40f);
            applyButton.GetComponentInChildren<TMP_Text>().text = "Apply treatment";
            applyButton.onClick = new Button.ButtonClickedEvent();
            applyButton.onClick.AddListener(OnApply);
            FixSfx(applyButton);

            panel.SetActive(false);
        }

        static Transform Column(string title)
        {
            var go = new GameObject("Col_" + title, typeof(RectTransform), typeof(RectMask2D));
            var rt = (RectTransform)go.transform; rt.SetParent(panel!.transform, false);
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
            var t = Text(rt, title.ToUpperInvariant(), 13f, Dim);
            var trt = (RectTransform)t.transform; trt.anchorMin = new Vector2(0, 1); trt.anchorMax = new Vector2(1, 1); trt.pivot = new Vector2(0, 1);
            trt.anchoredPosition = Vector2.zero; trt.sizeDelta = new Vector2(0, 20f);
            var d = Text(rt, "", 12f, Parchment); d.name = "Detail"; d.textWrappingMode = TextWrappingModes.Normal; d.alignment = TextAlignmentOptions.BottomLeft; d.richText = true;
            var drt = d.rectTransform; drt.anchorMin = new Vector2(0, 0); drt.anchorMax = new Vector2(1, 0); drt.pivot = new Vector2(0, 0); drt.anchoredPosition = new Vector2(0, 10f); drt.sizeDelta = new Vector2(0, DetailH);
            return rt;
        }

        /// Place columns under the tab row and size them from the window. Needs a laid-out canvas, so runs on first open.
        static void Layout()
        {
            if (laidOut || gui == null || panel == null) return;
            float tabBottom = 0f;
            foreach (var t in new[] { gui.m_tabCraft.transform, gui.m_tabUpgrade.transform, tab!.transform })
            {
                var c = new Vector3[4]; ((RectTransform)t).GetWorldCorners(c);
                var local = gui.m_crafting.InverseTransformPoint(c[0]);
                tabBottom = Mathf.Max(tabBottom, gui.m_crafting.rect.yMax - local.y);
            }
            float top = tabBottom + 10f;
            float w = gui.m_crafting.rect.width, h = gui.m_crafting.rect.height - top;
            float cw = (w - Pad * 4f) / 3f; int i = 0;
            foreach (var col in new[] { colCloaks!, colAugs!, colLevels! })
            {
                var rt = (RectTransform)col; rt.anchoredPosition = new Vector2(Pad + i * (cw + Pad), -top); rt.sizeDelta = new Vector2(cw, h - 100f); i++;
            }
            ((RectTransform)applyButton!.transform).sizeDelta = new Vector2(cw, 40f);
            var shrt = (RectTransform)shade!.transform; shrt.anchoredPosition = new Vector2(0f, -(top - 6f)); shrt.sizeDelta = new Vector2(w, h + 6f);
            laidOut = true;
        }

        static TMP_Text Text(Transform parent, string s, float size, Color c)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>(); t.font = font; t.fontSize = size; t.color = c; t.text = s; t.raycastTarget = false;
            return t;
        }

        static void FixSfx(Button b)
        {
            foreach (var c in b.GetComponents<MonoBehaviour>()) if (c.GetType().Name == "ButtonSfx") { c.enabled = false; c.enabled = true; }
        }

        static Button Row(Transform col, int index, string title, Sprite? icon, bool selected, bool enabled, Color labelColor)
        {
            var btn = Object.Instantiate(gui!.m_tabUpgrade.gameObject, col).GetComponent<Button>();
            btn.name = "Row"; btn.gameObject.SetActive(true);
            var rt = (RectTransform)btn.transform; rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(0, -(22f + index * (RowH + Gap))); rt.sizeDelta = new Vector2(0, RowH);
            float left = icon != null ? RowH + 6f : 8f;
            var txt = btn.GetComponentInChildren<TMP_Text>(); txt.text = title; txt.fontSize = 14f; txt.color = labelColor; txt.richText = true;
            txt.textWrappingMode = TextWrappingModes.NoWrap; txt.overflowMode = TextOverflowModes.Truncate;
            var txtRt = (RectTransform)txt.transform; txtRt.anchorMin = Vector2.zero; txtRt.anchorMax = Vector2.one;
            txt.alignment = TextAlignmentOptions.MidlineLeft; txtRt.offsetMin = new Vector2(left, 0); txtRt.offsetMax = new Vector2(-6f, 0);
            foreach (var c in btn.GetComponents<MonoBehaviour>()) if (c.GetType().Name == "ButtonTextColor") { c.enabled = false; Object.Destroy(c); }
            if (icon != null)
            {
                var ig = new GameObject("Icon", typeof(RectTransform), typeof(Image)); ig.transform.SetParent(btn.transform, false);
                var irt = (RectTransform)ig.transform; irt.anchorMin = new Vector2(0, 0.5f); irt.anchorMax = new Vector2(0, 0.5f); irt.pivot = new Vector2(0, 0.5f);
                irt.anchoredPosition = new Vector2(4f, 0); irt.sizeDelta = new Vector2(RowH - 8f, RowH - 8f);
                var im = ig.GetComponent<Image>(); im.sprite = icon; im.preserveAspect = true; im.raycastTarget = false;
            }
            btn.interactable = enabled;
            var img = btn.GetComponent<Image>(); if (img != null) img.color = selected ? new Color(1f, 0.8f, 0.5f, 1f) : (enabled ? Color.white : new Color(0.6f, 0.6f, 0.6f, 0.7f));
            btn.onClick = new Button.ButtonClickedEvent();
            FixSfx(btn);
            return btn;
        }

        static void ClearRows(Transform col) { for (int i = col.childCount - 1; i >= 2; i--) Object.Destroy(col.GetChild(i).gameObject); }
        static void Detail(Transform col, string text) => col.GetChild(1).GetComponent<TMP_Text>().text = text;

        // ---------- show / hide vanilla content ----------
        static void HideVanilla(bool hide)
        {
            if (gui == null) return;
            if (hide)
            {
                hidden.Clear();
                var objs = new List<GameObject> { gui.m_recipeListRoot.gameObject, gui.m_recipeIcon.gameObject, gui.m_recipeName.gameObject, gui.m_recipeDecription.gameObject,
                    gui.m_itemCraftType.gameObject, gui.m_craftButton.gameObject, gui.m_qualityPanel.gameObject, gui.m_variantButton.gameObject, gui.m_minStationLevelIcon.gameObject, gui.m_craftProgressPanel.gameObject };
                foreach (var r in gui.m_recipeRequirementList) objs.Add(r);
                var scroll = gui.m_recipeListRoot.GetComponentInParent<ScrollRect>(); if (scroll != null) { objs.Add(scroll.gameObject); if (scroll.verticalScrollbar != null) objs.Add(scroll.verticalScrollbar.gameObject); }
                var iconParent = gui.m_recipeIcon.transform.parent;
                if (iconParent != null && iconParent != gui.m_crafting && ((RectTransform)iconParent).rect.width < 200f) objs.Add(iconParent.gameObject);
                else if (iconParent != null) foreach (Transform sib in iconParent) if (sib != gui.m_recipeIcon.transform && sib.GetComponent<Image>() != null && ((RectTransform)sib).rect.width < 120f && Mathf.Abs(((RectTransform)sib).anchoredPosition.x - ((RectTransform)gui.m_recipeIcon.transform).anchoredPosition.x) < 40f) objs.Add(sib.gameObject);
                foreach (var o in objs) if (o != null && o.activeSelf) { o.SetActive(false); hidden.Add(o); }
            }
            else
            {
                foreach (var o in hidden) if (o != null) o.SetActive(true);
                hidden.Clear();
            }
        }

        // ---------- state ----------
        /// The station a cloak is treated at: the one its own recipe uses (Galdr table for the feather cape),
        /// or General.CraftingStation when UseCloakStation is off or the cloak has no recipe.
        public static CraftingStation? StationFor(ItemDrop.ItemData cloak)
        {
            var st = Config.Current.General.UseCloakStation ? ObjectDB.instance?.GetRecipe(cloak)?.m_craftingStation : null;
            return st != null ? st : ZNetScene.instance?.GetPrefab(Config.Current.General.CraftingStation)?.GetComponent<CraftingStation>();
        }

        static bool AtStationFor(Player p, ItemDrop.ItemData cloak)
        {
            var here = p.GetCurrentCraftingStation(); var need = StationFor(cloak);
            return here != null && need != null && Utils.GetPrefabName(here.gameObject) == Utils.GetPrefabName(need.gameObject);
        }

        /// Tab shows at any station that treats at least one cloak in the pack (or the configured bench).
        public static bool AtBench(Player p)
        {
            var st = p.GetCurrentCraftingStation();
            if (st == null) return false;
            return Utils.GetPrefabName(st.gameObject) == Config.Current.General.CraftingStation || Cloaks(p.GetInventory()).Any(c => AtStationFor(p, c));
        }

        public static void OnCraftingPanelUpdated()
        {
            if (gui == null || tab == null) return;
            bool at = Player.m_localPlayer != null && AtBench(Player.m_localPlayer);
            tab.gameObject.SetActive(at);
            if (!at && Open) Close();
            if (Open) { HideVanilla(true); Refresh(); }
        }

        static bool dumped;

        static void OnTabPressed()
        {
            if (gui == null || panel == null) return;
            Layout();
            if (!dumped) { dumped = true; foreach (var line in DumpTabs()) Plugin.Log.LogInfo(line); foreach (var line in DumpCrafting()) Plugin.Log.LogInfo(line); }
            gui.m_tabCraft.interactable = true; gui.m_tabUpgrade.interactable = true; tab!.interactable = false;
            var p = Player.m_localPlayer; var cloaks = Cloaks(p.GetInventory());
            if (selCloak == null || !cloaks.Contains(selCloak)) selCloak = cloaks.FirstOrDefault(c => c == AugmentationManager.Shoulder(p)) ?? cloaks.FirstOrDefault();
            selAug ??= Catalogue.All.FirstOrDefault();
            panel.SetActive(true); panel.transform.SetAsLastSibling();
            HideVanilla(true);
            Refresh();
        }

        /// Called before vanilla rebuilds its own tab (prefix), and when the window hides.
        public static void Close()
        {
            if (panel == null || !Open) return;
            foreach (var col in new[] { colCloaks, colAugs, colLevels }) if (col != null) ClearRows(col);
            panel.SetActive(false);
            HideVanilla(false);
            if (tab != null) tab.interactable = true;
            if (gui != null && gui.m_tabCraft.interactable && gui.m_tabUpgrade.interactable) gui.m_tabCraft.interactable = false; // vanilla expects one selected tab
        }

        static string When(Augmentation a) => a.Condition == Condition.Rain ? "the rain" : a.Condition == Condition.Cold ? "the cold" : "water";
        static List<ItemDrop.ItemData> Cloaks(Inventory inv) => inv.GetAllItems().Where(CloakState.IsCloak).ToList();
        static int Tier(ItemDrop.ItemData c) => Config.Current.TierOf(c);

        // ---------- draw ----------
        public static void Refresh()
        {
            if (!Open || Player.m_localPlayer == null) return;
            var p = Player.m_localPlayer; var inv = p.GetInventory();
            ClearRows(colCloaks!); ClearRows(colAugs!); ClearRows(colLevels!);
            Detail(colCloaks!, ""); Detail(colAugs!, ""); Detail(colLevels!, "");

            var cloaks = Cloaks(inv);
            if (selCloak != null && !cloaks.Contains(selCloak)) selCloak = null;
            int i = 0;
            foreach (var c in cloaks)
            {
                var cl = c; bool eq = cl == AugmentationManager.Shoulder(p);
                var title = Localization.instance.Localize(cl.m_shared.m_name) + (eq ? " <size=10><color=#7fb86a>equipped</color></size>" : "");
                var tip = "Tier " + Tier(cl) + ", " + Config.Current.ClassFor(Tier(cl)) + " class" +
                          (CloakState.TryRead(cl, out var st) && st.RemainingSeconds > 0 ? "\n" + st.Aug.DisplayName + " (" + Config.Current.LevelName(st.Level) + "): <color=orange>" + StatusEffect.GetTimeString(st.RemainingSeconds) + "</color> left" : "\nNo treatment");
                bool here = AtStationFor(p, cl);
                if (!here) tip += "\n<color=#e05a4c>Treat at the " + Localization.instance.Localize(StationFor(cl)?.m_name ?? "?") + "</color>";
                if (cl == selCloak) Detail(colCloaks!, tip);
                Row(colCloaks!, i++, title, cl.GetIcon(), cl == selCloak, true, cl == selCloak ? Orange : here ? Parchment : Dim).onClick.AddListener(() => { selCloak = cl; Refresh(); });
            }
            if (cloaks.Count == 0) { var t = Text(colCloaks!, "No cloak in your pack", 13f, Dim); var r = t.rectTransform; r.anchorMin = new Vector2(0, 1); r.anchorMax = new Vector2(1, 1); r.pivot = new Vector2(0, 1); r.anchoredPosition = new Vector2(0, -26f); r.sizeDelta = new Vector2(0, 20f); }

            i = 0;
            foreach (var a in Catalogue.All)
            {
                var aug = a;
                if (aug == selAug) Detail(colAugs!, aug.Description + "\nMaterial: <color=orange>" + MaterialName(aug.Cfg.Material) + "</color>" + (aug.Cfg.TimerMode == TimerMode.RelevantCondition ? "\nOnly fades in " + When(aug) : ""));
                Row(colAugs!, i++, aug.DisplayName, Icons.Get(aug.Id), aug == selAug, true, aug == selAug ? Orange : Parchment)
                    .onClick.AddListener(() => { selAug = aug; selLevel = null; Refresh(); });
            }

            i = 0; bool canApply = false; string why = "";
            if (selAug != null)
            {
                int tier = selCloak != null ? Tier(selCloak) : 0;
                foreach (var key in Catalogue.Levels)
                {
                    if (!selAug.Cfg.Levels.TryGetValue(key, out var lc)) continue;
                    var reqs = Requirements(selAug, lc);
                    bool locked = tier < lc.MinCloakTier;
                    bool afford = reqs.All(r => Has(p, r));
                    var cost = string.Join(", ", reqs.Select(r => (Has(p, r) ? "" : "<color=#e05a4c>") + r.amount + " " + (r.missing ? r.prefab + "?" : Localization.instance.Localize(r.token)) + (Has(p, r) ? "" : "</color>")));
                    var note = locked ? $"Needs a {Config.Current.LevelName(key)} cloak or better" : reqs.Any(r => r.missing) ? "Unknown material in config: " + reqs.First(r => r.missing).prefab : afford ? "" : "You lack the materials";
                    var tip = "Lasts <color=orange>" + lc.DurationMinutes.ToString("0") + " min</color>\nNeeds cloak tier " + lc.MinCloakTier + "+\n" + cost + (note.Length > 0 ? "\n<color=#e05a4c>" + note + "</color>" : "");
                    bool sel = key == selLevel; var k = key;
                    if (sel) Detail(colLevels!, tip);
                    Row(colLevels!, i++, Config.Current.LevelName(key), Icons.Get(selAug.Id + "_" + key), sel, !locked, sel ? Orange : locked ? Dim : Parchment).onClick.AddListener(() => { selLevel = k; Refresh(); });
                    if (sel) { bool here = selCloak != null && AtStationFor(p, selCloak); canApply = selCloak != null && here && !locked && afford; why = selCloak == null ? "Choose a cloak" : !here ? "Wrong station for this cloak" : locked ? note : afford ? "" : note; }
                }
            }
            if (selLevel == null) why = "Choose a level";

            if (selCloak != null && selAug != null && selLevel != null && selAug.Cfg.Levels.TryGetValue(selLevel, out var lcs))
            {
                var s = $"{selAug.DisplayName} ({Config.Current.LevelName(selLevel)}) on {Localization.instance.Localize(selCloak.m_shared.m_name)}: {lcs.DurationMinutes:0} min";
                if (selAug.Cfg.TimerMode == TimerMode.RelevantCondition) s += ", only counts down in " + When(selAug);
                if (CloakState.TryRead(selCloak, out var ex) && ex.RemainingSeconds > 0) s += $". Replaces {ex.Aug.DisplayName} ({StatusEffect.GetTimeString(ex.RemainingSeconds)} left)";
                if (why.Length > 0) s += "\n<color=#e05a4c>" + why + "</color>";
                summary!.text = s;
            }
            else summary!.text = why;
            applyButton!.interactable = canApply;
        }

        // ---------- requirements ----------
        public struct Req { public string token; public int amount; public string prefab; public bool missing; }

        /// Materials for a level, merged by item. A prefab the game doesn't know becomes an unmeetable requirement (never free).
        public static List<Req> Requirements(Augmentation aug, LevelConfig lc)
        {
            var list = new List<Req>();
            void Add(string prefab, int amount)
            {
                var drop = ObjectDB.instance?.GetItemPrefab(prefab)?.GetComponent<ItemDrop>();
                if (drop == null) { list.Add(new Req { token = prefab, amount = amount, prefab = prefab, missing = true }); return; }
                var token = drop.m_itemData.m_shared.m_name;
                int i = list.FindIndex(r => r.token == token);
                if (i >= 0) { var r = list[i]; r.amount += amount; list[i] = r; } else list.Add(new Req { token = token, amount = amount, prefab = prefab });
            }
            Add(aug.Cfg.Material, System.Math.Max(1, lc.Cost));
            foreach (var e in lc.Extra) Add(e.Prefab, System.Math.Max(1, e.Amount));
            return list;
        }

        static string MaterialName(string prefab) { var d = ObjectDB.instance?.GetItemPrefab(prefab)?.GetComponent<ItemDrop>(); return d == null ? prefab : Localization.instance.Localize(d.m_itemData.m_shared.m_name); }

        static bool Has(Player p, Req r) => !r.missing && Materials.Count(p, r.token) >= r.amount;

        // ---------- apply ----------
        static void OnApply()
        {
            var p = Player.m_localPlayer;
            if (p == null || selCloak == null || selAug == null || selLevel == null || !AtStationFor(p, selCloak) || !selAug.Cfg.Levels.TryGetValue(selLevel, out var lc)) return;
            var cloak = selCloak; var aug = selAug; var level = selLevel;
            if (CloakState.TryRead(cloak, out var ex) && ex.RemainingSeconds > 0f)
            {
                var text = Localization.instance.Localize("$cloakcraft_msg_replace_text", ex.Aug.DisplayName, StatusEffect.GetTimeString(ex.RemainingSeconds, true), aug.DisplayName);
                UnifiedPopup.Push(new YesNoPopup("$cloakcraft_msg_replace_header", text, () => { UnifiedPopup.Pop(); Do(p, cloak, aug, level, lc); }, () => UnifiedPopup.Pop()));
            }
            else Do(p, cloak, aug, level, lc);
        }

        static void Do(Player p, ItemDrop.ItemData cloak, Augmentation aug, string level, LevelConfig lc)
        {
            var reqs = Requirements(aug, lc);
            if (!p.GetInventory().ContainsItem(cloak) || Config.Current.TierOf(cloak) < lc.MinCloakTier || !reqs.All(r => Has(p, r))) { Refresh(); return; }
            foreach (var r in reqs) Materials.Remove(p, r.token, r.amount);
            AugmentationManager.ApplyDirect(p, cloak, aug, level, lc.DurationMinutes * 60f);
            Refresh();
        }

        // ---------- debug: dump the tab hierarchy so the Augment tab can match the vanilla ones exactly ----------
        public static IEnumerable<string> DumpTabs()
        {
            if (gui == null) yield break;
            var parent = gui.m_tabCraft.transform.parent;
            yield return "tab parent: " + Path(parent) + "  components: " + string.Join(",", parent.GetComponents<Component>().Select(c => c.GetType().Name));
            foreach (Transform child in parent) foreach (var line in Dump(child, 1)) yield return line;
        }

        /// Everything directly under m_crafting, two levels deep, so leftover vanilla elements can be named.
        public static IEnumerable<string> DumpCrafting()
        {
            if (gui == null) yield break;
            yield return "crafting root: " + Path(gui.m_crafting);
            foreach (Transform child in gui.m_crafting) foreach (var line in Dump(child, 1, 2)) yield return line;
        }

        static IEnumerable<string> Dump(Transform t, int depth, int maxDepth = 4)
        {
            var rt = t as RectTransform;
            yield return new string(' ', depth * 2) + t.name + (t.gameObject.activeSelf ? "" : " (inactive)") + " [" + string.Join(",", t.GetComponents<Component>().Select(c => c.GetType().Name)) + "]" +
                         (rt != null ? $" pos={rt.anchoredPosition} size={rt.rect.size} anchors={rt.anchorMin}-{rt.anchorMax}" : "") +
                         (t.GetComponent<Image>() is Image im && im.sprite != null ? " sprite=" + im.sprite.name : "");
            if (depth < maxDepth) foreach (Transform c in t) foreach (var line in Dump(c, depth + 1, maxDepth)) yield return line;
        }

        static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;
    }
}
