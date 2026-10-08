using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace MintStatSort
{
    internal static class PickerTiles
    {
        public static bool Draw(Rect rect, ThingDef icon, string title, string subtitle, bool selected)
        {
            bool clicked = Widgets.ButtonText(rect, "");
            if (selected) Widgets.DrawHighlightSelected(rect);
            using (new UiScope())
            {
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.MiddleLeft;
                Text.WordWrap = true;
                if (icon != null) Widgets.ThingIcon(new Rect(rect.x + 7f, rect.y + 8f, 42f, 42f), icon,
                    icon.MadeFromStuff ? GenStuff.DefaultStuffFor(icon) : null);
                float inset = icon == null ? 10f : 58f;
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(rect.x + inset, rect.y + 4f, rect.width - inset - 7f, 39f), title);
                Text.Font = GameFont.Tiny;
                GUI.color = Color.gray;
                Widgets.Label(new Rect(rect.x + inset, rect.y + 43f, rect.width - inset - 7f, 20f), subtitle);
            }
            TooltipHandler.TipRegion(rect, title + "\n" + subtitle);
            return clicked;
        }
        public static bool Matches(string name, string search)
        { return string.IsNullOrWhiteSpace(search) || name.IndexOf(search, StringComparison.CurrentCultureIgnoreCase) >= 0; }
    }

    internal sealed class MetricPicker : Window
    {
        private List<Metric> choices;
        private readonly List<SortCriterion> draft;
        private string mode;
        private bool advanced;
        private string search = "";
        private Vector2 scroll;
        public override Vector2 InitialSize => new Vector2(630f, 650f);
        public MetricPicker()
        {
            choices = RecipeSession.AvailableMetrics();
            draft = RecipeSession.Options.Criteria.Where(c => Metrics.Resolve(c.Id) != null).Select(c => c.Copy()).ToList();
            mode = draft.Count != 0 ? "criteria" : RecipeSession.Options.Metric == "name" ? "name" : "criteria";
            doCloseX = true;
            absorbInputAroundWindow = true;
        }
        public override void DoWindowContents(Rect inRect)
        {
            using (new UiScope())
            {
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.WordWrap = false;
                Widgets.Label(new Rect(0, 0, inRect.width - 25f, 28f), "MSS.GoalsTitle".Translate());
                string nextSearch = Widgets.TextField(new Rect(0, 33f, inRect.width - 128f, 27f), search);
                if (nextSearch != search) { search = nextSearch; scroll = Vector2.zero; }
                if (Widgets.ButtonText(new Rect(inRect.width - 120f, 33f, 120f, 27f),
                    (advanced ? "MSS.Recommended" : "MSS.AllStats").Translate()))
                {
                    advanced = !advanced;
                    choices = RecipeSession.AvailableMetrics(advanced).Concat(draft.Select(c => Metrics.Resolve(c.Id)).Where(m => m != null))
                        .GroupBy(m => m.Id).Select(g => g.First()).OrderBy(m => m.Group).ThenBy(m => m.Name).ToList();
                    scroll = Vector2.zero;
                }
                float tabWidth = (inRect.width - 8f) / 3f;
                foreach (var pair in new[] { new[] { "criteria", "MSS.Characteristics" }, new[] { "name", "MSS.Name" }, new[] { "original", "MSS.Original" } })
                {
                    int index = pair[0] == "criteria" ? 0 : pair[0] == "name" ? 1 : 2;
                    var tab = new Rect(index * (tabWidth + 4f), 68f, tabWidth, 25f);
                    if (Widgets.ButtonText(tab, pair[1].Translate())) mode = pair[0];
                    if (mode == pair[0]) Widgets.DrawHighlightSelected(tab);
                }
                Text.Font = GameFont.Tiny;
                Widgets.Label(new Rect(0, 101f, inRect.width, 28f), "MSS.GoalsHelp".Translate());
                var viewport = new Rect(0, 132f, inRect.width, inRect.height - 222f);
                var filtered = choices.Where(c => PickerTiles.Matches(c.Name + " " + c.Group + " " + c.Id, search)).ToList();
                var groups = filtered.GroupBy(c => c.Group).ToList();
                float height = groups.Sum(g => 28f + g.Count() * 35f);
                var content = new Rect(0, 0, viewport.width - 18f, Math.Max(viewport.height, height));
                if (mode == "criteria")
                {
                    Widgets.BeginScrollView(viewport, ref scroll, content);
                    float y = 0;
                    foreach (var group in groups)
                    {
                        Text.Font = GameFont.Small;
                        GUI.color = new Color(1f, 0.82f, 0.48f);
                        Widgets.Label(new Rect(4f, y, content.width - 8f, 26f), group.Key);
                        GUI.color = Color.white;
                        y += 28f;
                        foreach (var choice in group)
                        {
                            var row = new Rect(4f, y, content.width - 8f, 32f);
                            y += 35f;
                            if (row.yMax < scroll.y || row.y > scroll.y + viewport.height) continue;
                            var criterion = draft.FirstOrDefault(c => c.Id == choice.Id);
                            bool selected = criterion != null;
                            bool wasSelected = selected;
                            var check = new Rect(row.x, row.y, row.width - 83f, row.height);
                            Widgets.CheckboxLabeled(check, choice.Name.Truncate(check.width - 32f), ref selected);
                            if (selected != wasSelected)
                            {
                                if (selected) { criterion = new SortCriterion { Id = choice.Id, HigherIsBetter = choice.HigherIsBetter }; draft.Add(criterion); }
                                else { draft.Remove(criterion); criterion = null; }
                            }
                            if (criterion != null && Widgets.ButtonText(new Rect(row.xMax - 76f, row.y + 2f, 76f, 27f),
                                (criterion.HigherIsBetter ? "MSS.More" : "MSS.Less").Translate()))
                                criterion.HigherIsBetter = !criterion.HigherIsBetter;
                            TooltipHandler.TipRegion(check, choice.Name + "\n" + (choice.Stat?.description ??
                                (choice.Id.StartsWith("weapon.melee.", StringComparison.Ordinal) ? "MSS.MeleeReference" : "MSS.CompareTip").Translate().ToString()));
                        }
                    }
                    if (filtered.Count == 0) Widgets.Label(new Rect(4f, 4f, content.width - 8f, 45f), "MSS.NoCharacteristics".Translate());
                    Widgets.EndScrollView();
                }
                else
                {
                    Text.Font = GameFont.Small;
                    Widgets.Label(viewport, (mode == "name" ? "MSS.NameHelp" : "MSS.OriginalHelp").Translate());
                }
                Text.Font = GameFont.Tiny;
                Text.WordWrap = true;
                Widgets.Label(new Rect(0, inRect.height - 82f, inRect.width, 38f),
                    "MSS.SelectedCount".Translate(mode == "criteria" ? draft.Count : 0).ToString() + " · " + "MSS.BalanceShort".Translate());
                Text.Font = GameFont.Small;
                if (Widgets.ButtonText(new Rect(0, inRect.height - 33f, 126f, 30f), "MSS.ClearSelection".Translate()))
                { draft.Clear(); mode = "criteria"; }
                if (Widgets.ButtonText(new Rect(inRect.width - 126f, inRect.height - 33f, 126f, 30f), "MSS.Apply".Translate()))
                {
                    RecipeSession.Options.Criteria = mode == "criteria" ? draft.Select(c => c.Copy()).ToList() : new List<SortCriterion>();
                    RecipeSession.Options.Metric = mode == "name" ? "name" : "original";
                    RecipeSession.Options.Descending = true;
                    RecipeSession.Changed();
                    Close();
                }
            }
        }
    }

    internal sealed class CategoryPicker : Window
    {
        private readonly List<CategoryChoice> choices = RecipeSession.Categories();
        private string search = "";
        private bool expanded;
        private Vector2 scroll;
        public override Vector2 InitialSize => new Vector2(570f, 590f);
        public CategoryPicker() { doCloseX = true; doCloseButton = true; absorbInputAroundWindow = true; }
        public override void DoWindowContents(Rect inRect)
        {
            using (new UiScope())
            {
                GUI.color = Color.white; Text.Font = GameFont.Small; Text.Anchor = TextAnchor.UpperLeft;
                Widgets.Label(new Rect(0, 0, inRect.width - 25f, 28f), "MSS.CategoryTitle".Translate());
                string next = Widgets.TextField(new Rect(0, 34f, inRect.width, 28f), search);
                if (next != search) { search = next; scroll = Vector2.zero; }
                bool previous = expanded;
                Widgets.CheckboxLabeled(new Rect(0, 69f, inRect.width, 27f), "MSS.MoreCategories".Translate(), ref expanded);
                if (expanded != previous) scroll = Vector2.zero;
                var filtered = choices.Where(c => (c.Primary || expanded || !string.IsNullOrWhiteSpace(search)) && PickerTiles.Matches(c.Name, search)).ToList();
                var viewport = new Rect(0, 108f, inRect.width, inRect.height - 166f);
                var content = new Rect(0, 0, viewport.width - 18f, Math.Max(viewport.height, ((filtered.Count + 1) / 2) * 76f));
                float width = (content.width - 8f) / 2f;
                Widgets.BeginScrollView(viewport, ref scroll, content);
                for (int i = 0; i < filtered.Count; i++)
                {
                    var choice = filtered[i];
                    var tile = new Rect((i % 2) * (width + 8f), (i / 2) * 76f, width, 68f);
                    if (tile.yMax < scroll.y || tile.y > scroll.y + viewport.height) continue;
                    if (PickerTiles.Draw(tile, choice.Icon, choice.Name, "MSS.RecipeCount".Translate(choice.Count), RecipeSession.Options.Category == choice.Id))
                    { RecipeSession.SelectCategory(choice.Id); Close(); }
                }
                if (filtered.Count == 0) Widgets.Label(new Rect(0, 0, content.width, 30f), "MSS.NoMatches".Translate());
                Widgets.EndScrollView();
            }
        }
    }

    internal static class MaterialInventory
    {
        internal static Dictionary<ThingDef, long> Count(Map map, IEnumerable<ThingDef> materials)
        {
            var counts = new Dictionary<ThingDef, long>();
            foreach (var material in materials)
                counts[material] = map == null ? 0 : ReferenceCore.StoredQuantity(map.listerThings.ThingsOfDef(material),
                    t => t.stackCount, t => t.Spawned, t => t.IsInAnyStorage());
            return counts;
        }
    }
    internal sealed class MaterialPicker : Window
    {
        private readonly List<ThingDef> materials = RecipeSession.Materials().ToList();
        private readonly Map map = RecipeSession.ActiveMap ?? Find.CurrentMap;
        private Dictionary<ThingDef, long> counts;
        private bool storedOnly = RecipeSession.Options.StoredMaterialsOnly;
        private float nextRefresh;
        private string search = "";
        private Vector2 scroll;
        public override Vector2 InitialSize => new Vector2(600f, 630f);
        public MaterialPicker() { doCloseX = true; doCloseButton = true; absorbInputAroundWindow = true; }
        public override void DoWindowContents(Rect inRect)
        {
            using (new UiScope())
            {
                if (counts == null || Time.realtimeSinceStartup >= nextRefresh)
                { counts = MaterialInventory.Count(map, materials); nextRefresh = Time.realtimeSinceStartup + 1.5f; }
                GUI.color = Color.white; Text.Font = GameFont.Small; Text.Anchor = TextAnchor.UpperLeft;
                Widgets.Label(new Rect(0, 0, inRect.width - 25f, 28f), "MSS.MaterialTitle".Translate());
                string next = Widgets.TextField(new Rect(0, 34f, inRect.width, 28f), search);
                if (next != search) { search = next; scroll = Vector2.zero; }
                bool previous = storedOnly;
                Widgets.CheckboxLabeled(new Rect(0, 69f, inRect.width, 27f), "MSS.StoredOnly".Translate(), ref storedOnly);
                if (storedOnly != previous)
                { RecipeSession.Options.StoredMaterialsOnly = storedOnly; MintStatSortMod.Instance.WriteSettings(); scroll = Vector2.zero; }
                var auto = new Rect(0, 108f, inRect.width, 68f);
                if (PickerTiles.Draw(auto, null, "MSS.AutoMaterial".Translate(), "MSS.AutoMaterialHelp".Translate(), string.IsNullOrEmpty(RecipeSession.Options.Material)))
                { RecipeSession.Options.Material = ""; RecipeSession.Changed(); Close(); }
                var filtered = materials.Where(m => (!storedOnly || counts[m] > 0) && PickerTiles.Matches(m.LabelCap.ToString(), search)).ToList();
                var viewport = new Rect(0, 186f, inRect.width, inRect.height - 244f);
                var content = new Rect(0, 0, viewport.width - 18f, Math.Max(viewport.height, ((filtered.Count + 1) / 2) * 76f));
                float width = (content.width - 8f) / 2f;
                Widgets.BeginScrollView(viewport, ref scroll, content);
                for (int i = 0; i < filtered.Count; i++)
                {
                    var material = filtered[i];
                    var tile = new Rect((i % 2) * (width + 8f), (i / 2) * 76f, width, 68f);
                    if (tile.yMax < scroll.y || tile.y > scroll.y + viewport.height) continue;
                    if (PickerTiles.Draw(tile, material, material.LabelCap, "MSS.StoredCount".Translate(counts[material]), RecipeSession.Material == material))
                    { RecipeSession.Options.Material = material.defName; RecipeSession.Changed(); Close(); }
                }
                if (filtered.Count == 0)
                {
                    Text.WordWrap = true;
                    Widgets.Label(new Rect(0, 4f, content.width, 86f), (map == null ? "MSS.NoMap" : storedOnly ? "MSS.NoStoredMaterials" : "MSS.NoMatches").Translate());
                }
                Widgets.EndScrollView();
            }
        }
    }
}
