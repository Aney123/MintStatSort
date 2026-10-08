using System;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace MintStatSort
{
    internal sealed class UiScope : IDisposable
    {
        private readonly GameFont font = Text.Font;
        private readonly TextAnchor anchor = Text.Anchor;
        private readonly Color color = GUI.color;
        private readonly bool wrap = Text.WordWrap;
        public void Dispose() { Text.Font = font; Text.Anchor = anchor; GUI.color = color; Text.WordWrap = wrap; }
    }
    internal static class Toolbar
    {
        public const float Height = 58f;
        public static void Draw(Rect rect)
        {
            using (new UiScope())
            {
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleLeft;
                Text.WordWrap = false;
                GUI.color = Color.white;
                float categoryWidth = Mathf.Clamp(rect.width * 0.30f, 70f, 130f);
                var categoryRect = new Rect(rect.x, rect.y, categoryWidth, 24f);
                var sortRect = new Rect(categoryRect.xMax + 4f, rect.y, rect.width - categoryWidth - 37f, 24f);
                var directionRect = new Rect(sortRect.xMax + 4f, rect.y, 29f, 24f);
                string category = RecipeSession.CategoryName();
                if (Widgets.ButtonText(categoryRect, category.Truncate(categoryRect.width - 10f)))
                    Find.WindowStack.Add(new CategoryPicker());
                TooltipHandler.TipRegion(categoryRect, "MSS.CategoryButton".Translate(category));
                string sort = RecipeSession.HasComparison || RecipeSession.Options.Metric == "name" ? RecipeSession.SortSummary() :
                    "MSS.ChooseGoals".Translate().ToString();
                if (Widgets.ButtonText(sortRect, sort.Truncate(sortRect.width - 10f)))
                    Find.WindowStack.Add(new MetricPicker());
                string selected = string.Join("\n", RecipeSession.SelectedMetrics.Select(m =>
                    (RecipeSession.Options.Criteria.First(c => c.Id == m.Id).HigherIsBetter ? "MSS.More" : "MSS.Less").Translate().ToString() + " · " + m.Name));
                TooltipHandler.TipRegion(sortRect, selected + "\n" + "MSS.BalanceTip".Translate().ToString() +
                    (NiceIntegration.Ready ? "\n" + "MSS.NiceFlatTip".Translate().ToString() : ""));
                if (Widgets.ButtonText(directionRect, RecipeSession.Options.Descending ? "↓" : "↑"))
                { RecipeSession.Options.Descending = !RecipeSession.Options.Descending; RecipeSession.Changed(); }
                TooltipHandler.TipRegion(directionRect, (RecipeSession.Options.Descending ? "MSS.BestFirst" : "MSS.WorstFirst").Translate());
                var materialRect = new Rect(rect.x, rect.y + 28f, rect.width - 67f, 24f);
                var resetRect = new Rect(materialRect.xMax + 4f, materialRect.y, 63f, 24f);
                var material = RecipeSession.Material;
                string reference = material?.LabelCap.ToString() ?? "MSS.AutoMaterial".Translate().ToString();
                if (Widgets.ButtonText(materialRect, "")) Find.WindowStack.Add(new MaterialPicker());
                float iconSpace = material == null ? 0 : 25f;
                if (material != null) Widgets.ThingIcon(new Rect(materialRect.x + 3f, materialRect.y + 1f, 22f, 22f), material);
                Widgets.Label(new Rect(materialRect.x + iconSpace + 5f, materialRect.y, materialRect.width - iconSpace - 10f, 24f),
                    reference.Truncate(materialRect.width - iconSpace - 12f));
                TooltipHandler.TipRegion(materialRect, "MSS.MaterialButton".Translate(reference).ToString() + "\n" + "MSS.MaterialTip".Translate());
                if (Widgets.ButtonText(resetRect, "MSS.ResetShort".Translate()))
                { RecipeSession.Options.Reset(); RecipeSession.Changed(); }
            }
        }
    }
}
