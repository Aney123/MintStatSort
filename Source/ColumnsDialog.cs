using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace MintStatSort
{
    internal sealed class ColumnsDialog : Window
    {
        private readonly List<ComparisonColumn> columns, inactive;
        private readonly Action<List<ComparisonColumn>> commit;
        private readonly Func<bool> valid;
        private Vector2 scroll;
        public override Vector2 InitialSize => new Vector2(Mathf.Min(680f, UI.screenWidth - 24f), Mathf.Min(560f, UI.screenHeight - 40f));
        internal ColumnsDialog(List<Metric> metrics, List<ComparisonColumn> settings, Action<List<ComparisonColumn>> commit, Func<bool> valid)
        {
            this.commit = commit; this.valid = valid;
            columns = settings.Where(c => metrics.Any(m => m.Id == c.Id)).Select(c => c.Copy()).ToList();
            columns.AddRange(metrics.Where(m => !columns.Any(c => c.Id == m.Id)).Select(m => new ComparisonColumn { Id = m.Id }));
            inactive = settings.Where(c => !metrics.Any(m => m.Id == c.Id)).Select(c => c.Copy()).ToList();
            doCloseX = true; forcePause = true; absorbInputAroundWindow = true;
        }
        public override void WindowUpdate() { base.WindowUpdate(); if (!valid()) Close(); }
        internal void Move(int index, int direction)
        {
            int next = index + direction;
            if (index < 0 || next < 0 || index >= columns.Count || next >= columns.Count) return;
            var value = columns[index]; columns.RemoveAt(index); columns.Insert(next, value);
        }
        internal void Commit() { commit(columns.Concat(inactive).Select(c => c.Copy()).ToList()); Close(); }
        public override void DoWindowContents(Rect rect)
        {
            using (new UiScope())
            {
                Text.Font = GameFont.Medium; Widgets.Label(new Rect(0, 0, rect.width - 35f, 30f), "MSS.Columns".Translate());
                Text.Font = GameFont.Tiny; Text.WordWrap = true;
                Widgets.Label(new Rect(0, 42f, rect.width, 55f), "MSS.ColumnsTip".Translate());
                var viewport = new Rect(0, 105f, rect.width, rect.height - 155f);
                var content = new Rect(0, 0, rect.width - 18f, Math.Max(1, columns.Count * 69f));
                Widgets.BeginScrollView(viewport, ref scroll, content);
                int move = -1, direction = 0;
                for (int i = 0; i < columns.Count; i++)
                {
                    var column = columns[i]; float y = i * 69f;
                    if (y + 69f < scroll.y || y > scroll.y + viewport.height) continue;
                    var metric = Metrics.Resolve(column.Id);
                    Widgets.CheckboxLabeled(new Rect(0, y, content.width - 85f, 30f), metric?.Name ?? column.Id, ref column.Visible);
                    if (Widgets.ButtonText(new Rect(content.width - 80f, y, 36f, 29f), "↑")) { move = i; direction = -1; }
                    if (Widgets.ButtonText(new Rect(content.width - 39f, y, 36f, 29f), "↓")) { move = i; direction = 1; }
                    Widgets.Label(new Rect(0, y + 35f, 110f, 24f), "MSS.ColumnWidth".Translate(column.Width));
                    column.Width = (int)Math.Round(Widgets.HorizontalSlider(new Rect(120f, y + 35f, content.width - 130f, 25f), column.Width, 80f, 260f));
                    Widgets.DrawLineHorizontal(0, y + 67f, content.width);
                }
                if (columns.Count == 0) Widgets.Label(new Rect(0, 0, content.width, 50f), "MSS.AddStatisticsHint".Translate());
                Widgets.EndScrollView(); if (move >= 0) Move(move, direction);
                Text.Font = GameFont.Small;
                if (Widgets.ButtonText(new Rect(rect.width - 248f, rect.height - 34f, 120f, 32f), "MSS.Apply".Translate())) Commit();
                if (Widgets.ButtonText(new Rect(rect.width - 120f, rect.height - 34f, 120f, 32f), "MSS.Close".Translate())) Close();
            }
        }
    }
}
