using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace MintStatSort
{
    internal sealed class IngredientDialog : Window
    {
        private readonly IngredientInspector inspector;
        private readonly Func<bool> valid;
        private Vector2 scroll;
        private string executions = "1";
        internal void SetMixing(bool value) { inspector.SetMixing(value); }
        internal void SetExecutions(int value) { inspector.SetExecutions(value); executions = inspector.Executions.ToString(); }
        public override Vector2 InitialSize => new Vector2(Mathf.Min(740f, UI.screenWidth - 24f), Mathf.Min(550f, UI.screenHeight - 40f));
        internal IngredientDialog(RecipeDef recipe, Map map, ThingDef material,
            Dictionary<IngredientCount, List<ThingDef>> choices, Func<bool> valid)
        {
            inspector = new IngredientInspector(recipe, map, material, choices); this.valid = valid;
            doCloseX = true; forcePause = true; absorbInputAroundWindow = true;
        }
        public override void WindowUpdate()
        {
            base.WindowUpdate();
            if (valid != null && !valid()) { Close(); return; }
            inspector.Update();
        }
        public override void PreClose() { inspector.Dispose(); base.PreClose(); }
        public override void DoWindowContents(Rect rect)
        {
            using (new UiScope())
            {
                Text.Font = GameFont.Medium;
                Widgets.Label(new Rect(0, 0, rect.width - 35f, 32f), "MSS.IngredientsTitle".Translate());
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(0, 39f, rect.width, 26f), inspector.Recipe.LabelCap.ToString().Truncate(rect.width));
                Text.Font = GameFont.Tiny; Text.WordWrap = true;
                Widgets.Label(new Rect(0, 74f, rect.width, 34f), "MSS.IngredientsReference".Translate());
                Widgets.Label(new Rect(0, 112f, 180f, 25f), "MSS.Executions".Translate());
                executions = Widgets.TextField(new Rect(184f, 111f, 65f, 25f), executions);
                if (BatchCount.TryParse(executions, out var quantity)) inspector.SetExecutions(quantity);
                else TooltipHandler.TipRegion(new Rect(184f, 111f, 65f, 25f), "MSS.ExecutionsRange".Translate());
                TooltipHandler.TipRegion(new Rect(0, 111f, rect.width, 27f), "MSS.ExecutionsTip".Translate());
                bool mix = inspector.MixSubstitutes;
                Widgets.CheckboxLabeled(new Rect(265f, 111f, rect.width - 265f, 25f), "MSS.MixSubstitutes".Translate(), ref mix);
                inspector.SetMixing(mix);
                TooltipHandler.TipRegion(new Rect(265f, 111f, rect.width - 265f, 25f), "MSS.MixTip".Translate());
                float nameWidth = Mathf.Max(140f, rect.width - 264f);
                Widgets.Label(new Rect(0, 144f, nameWidth, 22f), "MSS.IngredientChoice".Translate());
                for (int i = 0; i < 3; i++)
                    Widgets.Label(new Rect(nameWidth + i * 88f, 144f, 86f, 22f), (new[] { "MSS.Required", "MSS.StoredAvailable", "MSS.Shortfall" }[i]).Translate());
                var viewport = new Rect(0, 170f, rect.width, Mathf.Max(30f, rect.height - 286f));
                var content = new Rect(0, 0, rect.width - 18f, Mathf.Max(1f, inspector.Rows.Count * 73f));
                Widgets.BeginScrollView(viewport, ref scroll, content);
                for (int i = 0; i < inspector.Rows.Count; i++)
                {
                    int index = i;
                    var row = inspector.Rows[i];
                    var tile = new Rect(0, i * 73f, content.width, 70f);
                    if (tile.yMax < scroll.y || tile.y > scroll.y + viewport.height) continue;
                    Text.Font = GameFont.Tiny;
                    Widgets.Label(new Rect(0, tile.y, nameWidth - 6f, 22f), row.Label.Truncate(nameWidth - 6f));
                    var button = new Rect(0, tile.y + 26f, nameWidth - 8f, 29f);
                    bool before = GUI.enabled; GUI.enabled = before && inspector.Ready && row.Choices.Count > 1;
                    if (Widgets.ButtonText(button, (row.Reference?.LabelCap.ToString() ?? "MSS.NoIngredientReference".Translate().ToString()).Truncate(button.width - 8f)))
                        Find.WindowStack.Add(new FloatMenu(row.Choices.Select(d => new FloatMenuOption(d.LabelCap, () => inspector.Choose(index, d))).ToList()));
                    GUI.enabled = before;
                    TooltipHandler.TipRegion(button, "MSS.IngredientChoiceTip".Translate());
                    string[] values = { row.Required?.ToString() ?? "—", row.Available?.ToString() ?? "—", row.Missing?.ToString() ?? "—" };
                    Text.Anchor = TextAnchor.MiddleCenter;
                    for (int column = 0; column < 3; column++)
                    {
                        var cell = new Rect(nameWidth + column * 88f, tile.y + 23f, 76f, 30f);
                        Widgets.Label(cell, inspector.Pending && values[column] == "—" ? "…" : values[column]);
                        TooltipHandler.TipRegion(cell, "MSS.IngredientAvailabilityTip".Translate());
                    }
                    Text.Anchor = TextAnchor.UpperLeft;
                    Widgets.DrawLineHorizontal(0, tile.yMax, content.width);
                }
                if (inspector.Rows.Count == 0)
                    Widgets.Label(new Rect(0, 0, content.width, 45f), (inspector.Pending ? "MSS.PreparingIngredients" : "MSS.NoIngredients").Translate());
                Widgets.EndScrollView();
                Text.WordWrap = true; Text.Font = GameFont.Tiny;
                Widgets.Label(new Rect(0, rect.height - 107f, rect.width, 58f),
                    (inspector.Map == null ? "MSS.IngredientsNoMap" : inspector.Recipe.ignoreIngredientCountTakeEntireStacks ? "MSS.IngredientsWholeStacks" : "MSS.IngredientsStorageNote").Translate());
                Text.Font = GameFont.Small;
                Text.Font = GameFont.Tiny;
                if (inspector.Pending) Widgets.Label(new Rect(0, rect.height - 29f, Mathf.Max(0, rect.width - 252f), 24f), "MSS.PreparingIngredients".Translate());
                Text.Font = GameFont.Small;
                if (Widgets.ButtonText(new Rect(rect.width - 244f, rect.height - 34f, 116f, 32f), "MSS.RefreshIngredients".Translate())) inspector.Refresh();
                if (Widgets.ButtonText(new Rect(rect.width - 116f, rect.height - 34f, 116f, 32f), "MSS.Close".Translate())) Close();
            }
        }
    }
}
