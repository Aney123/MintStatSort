using System;
using System.Globalization;
using UnityEngine;
using Verse;

namespace MintStatSort
{
    internal sealed class CriterionDialog : Window
    {
        private readonly SortCriterion draft;
        private readonly Action<SortCriterion> accept;
        private readonly string title, unit;
        private readonly double scale;
        private string minimum, maximum;
        public override Vector2 InitialSize => new Vector2(500f, 385f);
        internal CriterionDialog(Metric metric, SortCriterion source, Action<SortCriterion> accept)
        {
            draft = source.Copy(); this.accept = accept; title = metric.Name;
            scale = Metrics.LimitScale(metric); unit = Metrics.LimitUnit(metric);
            minimum = source.HasMinimum ? (source.Minimum * scale).ToString("R", CultureInfo.CurrentCulture) : "";
            maximum = source.HasMaximum ? (source.Maximum * scale).ToString("R", CultureInfo.CurrentCulture) : "";
            doCloseX = true; forcePause = true; absorbInputAroundWindow = true;
        }
        internal bool Commit()
        {
            double? low, high;
            if (!NumericFilterCore.TryLimit(minimum, scale, out low) || !NumericFilterCore.TryLimit(maximum, scale, out high) ||
                low.HasValue && high.HasValue && low.Value > high.Value) return false;
            draft.HasMinimum = low.HasValue; draft.Minimum = low.GetValueOrDefault();
            draft.HasMaximum = high.HasValue; draft.Maximum = high.GetValueOrDefault();
            accept(draft.Copy()); return true;
        }
        public override void DoWindowContents(Rect rect)
        {
            using (new UiScope())
            {
                Text.Font = GameFont.Medium;
                Widgets.Label(new Rect(0, 0, rect.width - 35f, 34f), title.Truncate(rect.width - 35f));
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(0, 46f, rect.width, 26f), "MSS.Importance".Translate(draft.Weight));
                for (int i = 1; i <= 5; i++)
                {
                    var button = new Rect((i - 1) * 56f, 77f, 50f, 28f);
                    if (draft.Weight == i) Widgets.DrawBoxSolid(button, new Color(0.32f, 0.35f, 0.2f));
                    if (Widgets.ButtonText(button, i.ToString())) draft.Weight = i;
                }
                Text.Font = GameFont.Tiny; Text.WordWrap = true;
                Widgets.Label(new Rect(0, 112f, rect.width, 42f), "MSS.ImportanceTip".Translate());
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(0, 160f, rect.width, 26f), "MSS.LimitsUnit".Translate(unit));
                Widgets.Label(new Rect(0, 194f, 95f, 27f), "MSS.Minimum".Translate());
                Widgets.Label(new Rect(rect.width / 2 + 8f, 194f, 95f, 27f), "MSS.Maximum".Translate());
                minimum = Widgets.TextField(new Rect(96f, 192f, rect.width / 2 - 104f, 30f), minimum);
                maximum = Widgets.TextField(new Rect(rect.width / 2 + 104f, 192f, rect.width / 2 - 104f, 30f), maximum);
                Text.Font = GameFont.Tiny;
                double? low = null, high = null;
                bool valid = NumericFilterCore.TryLimit(minimum, scale, out low) && NumericFilterCore.TryLimit(maximum, scale, out high);
                if (valid && low.HasValue && high.HasValue && low.Value > high.Value) valid = false;
                Widgets.Label(new Rect(0, 234f, rect.width, 51f), (valid ? "MSS.LimitsTip" : "MSS.InvalidLimits").Translate());
                Text.Font = GameFont.Small;
                if (Widgets.ButtonText(new Rect(0, rect.height - 34f, 124f, 32f), "MSS.ResetShort".Translate()))
                { minimum = maximum = ""; draft.Weight = 1; }
                bool enabled = GUI.enabled; GUI.enabled = enabled && valid;
                if (Widgets.ButtonText(new Rect(rect.width - 240f, rect.height - 34f, 116f, 32f), "MSS.Apply".Translate()))
                { if (Commit()) Close(); }
                GUI.enabled = enabled;
                if (Widgets.ButtonText(new Rect(rect.width - 116f, rect.height - 34f, 116f, 32f), "MSS.Close".Translate())) Close();
            }
        }
    }
}
