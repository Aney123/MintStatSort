using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace MintStatSort
{
    internal sealed class PinnedComparisonDialog : Window
    {
        private readonly List<RecipeDef> recipes;
        private readonly List<Metric> metrics;
        private readonly Func<RecipeDef, Metric, Measurement> reading;
        private readonly Func<bool> valid;
        private Vector2 scroll;
        public override Vector2 InitialSize => new Vector2(Mathf.Min(1050f, UI.screenWidth - 24f), Mathf.Min(660f, UI.screenHeight - 40f));
        internal PinnedComparisonDialog(List<RecipeDef> recipes, List<Metric> metrics, Func<RecipeDef, Metric, Measurement> reading, Func<bool> valid)
        { this.recipes = recipes.ToList(); this.metrics = metrics.ToList(); this.reading = reading; this.valid = valid; doCloseX = true; forcePause = true; absorbInputAroundWindow = true; }
        public override void WindowUpdate() { base.WindowUpdate(); if (!valid()) Close(); }
        public override void DoWindowContents(Rect rect)
        {
            using (new UiScope())
            {
                Text.Font = GameFont.Medium; Widgets.Label(new Rect(0, 0, rect.width - 35f, 30f), "MSS.PinnedComparison".Translate());
                Text.Font = GameFont.Tiny; Text.WordWrap = true;
                Widgets.Label(new Rect(0, 42f, rect.width, 45f), "MSS.PinnedTip".Translate());
                float nameWidth = 190f, width = (rect.width - nameWidth - 18f) / Math.Max(1, recipes.Count);
                for (int i = 0; i < recipes.Count; i++)
                {
                    var cell = new Rect(nameWidth + i * width, 97f, width - 6f, 74f);
                    if (recipes[i].ProducedThingDef != null) Widgets.ThingIcon(new Rect(cell.x + 4f, cell.y + 2f, 34f, 34f), recipes[i].ProducedThingDef);
                    Widgets.Label(new Rect(cell.x + 41f, cell.y, cell.width - 43f, 68f), recipes[i].LabelCap.ToString());
                }
                var viewport = new Rect(0, 181f, rect.width, rect.height - 230f);
                var content = new Rect(0, 0, rect.width - 18f, Mathf.Max(1, metrics.Count * 58f));
                Widgets.BeginScrollView(viewport, ref scroll, content);
                for (int row = 0; row < metrics.Count; row++)
                {
                    float y = row * 58f; if (y + 58f < scroll.y || y > scroll.y + viewport.height) continue;
                    Widgets.Label(new Rect(0, y + 5f, nameWidth - 10f, 48f), metrics[row].Name);
                    for (int column = 0; column < recipes.Count; column++)
                    {
                        var value = reading(recipes[column], metrics[row]);
                        var cell = new Rect(nameWidth + column * width, y + 4f, width - 6f, 48f);
                        Text.Anchor = TextAnchor.MiddleCenter; Widgets.Label(cell, value.Display); Text.Anchor = TextAnchor.UpperLeft;
                        TooltipHandler.TipRegion(cell, value.Detail);
                    }
                    Widgets.DrawLineHorizontal(0, y + 57f, content.width);
                }
                if (metrics.Count == 0) Widgets.Label(new Rect(0, 0, content.width, 50f), "MSS.AddStatisticsHint".Translate());
                Widgets.EndScrollView(); Text.Font = GameFont.Small;
                if (Widgets.ButtonText(new Rect(rect.width - 120f, rect.height - 34f, 120f, 32f), "MSS.Close".Translate())) Close();
            }
        }
    }
}
