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
        public const float Height = 34f;
        public static void Draw(Rect rect, Func<bool> valid, string contextName, string benchKey = null)
        {
            using (new UiScope())
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                Text.WordWrap = false;
                GUI.color = Color.white;
                float buttonWidth = Mathf.Min(180f, rect.width);
                if (Widgets.ButtonText(new Rect(rect.x, rect.y, buttonWidth, 28f), "MSS.OpenComparison".Translate()))
                {
                    var window = new ComparisonWindow(RecipeSession.Recipes.Select(r =>
                        new ComparisonRow { Recipe = r, Label = r.LabelCap.ToString() }).ToList(),
                        RecipeSession.ActiveMap, valid, contextName);
                    window.SetBenchContext(benchKey);
                    Find.WindowStack.Add(window);
                }
                var summary = new Rect(rect.x + buttonWidth + 8f, rect.y, Mathf.Max(0, rect.width - buttonWidth - 8f), 28f);
                Text.Font = GameFont.Tiny;
                Widgets.Label(summary, RecipeSession.SortSummary().Truncate(summary.width));
                TooltipHandler.TipRegion(rect, "MSS.OpenComparisonTip".Translate());
            }
        }
    }
}
