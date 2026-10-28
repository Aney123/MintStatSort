using System;
using System.IO;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace MintStatSort
{
    internal sealed class DiagnosticDialog : Window
    {
        private readonly string report;
        private Vector2 scroll;
        internal static string DirectoryPath => Path.Combine(GenFilePaths.SaveDataFolderPath, "MintStatSort", "Diagnostics");
        internal static string Build(string context)
            => "MintStatSort " + typeof(MintStatSortMod).Assembly.GetName().Version + "\nRimWorld assembly: " + typeof(ThingDef).Assembly.GetName().Version +
                "\n\n" + MintStatSortMod.PatchStatus + "\nCE: " + CombatExtendedIntegration.Active +
                "\n\n" + context + "\n\nProfiles: " + ProfileExchange.DirectoryPath + "\nDiagnostics: " + DirectoryPath +
                "\n\nMods:\n" + string.Join("\n", ModsConfig.ActiveModsInLoadOrder.Select(m => m.Name + " [" + m.PackageIdNonUnique + "]"));
        internal DiagnosticDialog(string context) { report = Build(context); doCloseX = true; absorbInputAroundWindow = true; forcePause = true; }
        public override Vector2 InitialSize => new Vector2(Mathf.Min(830f, UI.screenWidth - 24f), Mathf.Min(650f, UI.screenHeight - 40f));
        internal string Save()
        {
            Directory.CreateDirectory(DirectoryPath);
            string path = Path.Combine(DirectoryPath, "mintstatsort-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N") + ".txt");
            File.WriteAllText(path, report); return path;
        }
        public override void DoWindowContents(Rect rect)
        {
            using (new UiScope())
            {
                Text.Font = GameFont.Medium; Widgets.Label(new Rect(0, 0, rect.width - 35f, 30f), "MSS.Diagnostics".Translate());
                Text.Font = GameFont.Tiny; Text.WordWrap = true;
                Widgets.Label(new Rect(0, 41f, rect.width, 44f), "MSS.DiagnosticsTip".Translate());
                var viewport = new Rect(0, 94f, rect.width, rect.height - 141f);
                var content = new Rect(0, 0, rect.width - 18f, Math.Max(viewport.height, Text.CalcHeight(report, rect.width - 18f)));
                Widgets.BeginScrollView(viewport, ref scroll, content); Widgets.Label(content, report); Widgets.EndScrollView();
                Text.Font = GameFont.Small;
                if (Widgets.ButtonText(new Rect(0, rect.height - 34f, 160f, 32f), "MSS.CopyReport".Translate())) GUIUtility.systemCopyBuffer = report;
                if (Widgets.ButtonText(new Rect(170f, rect.height - 34f, 160f, 32f), "MSS.SaveReport".Translate()))
                {
                    try { Messages.Message("MSS.FileSaved".Translate(Save()), MessageTypeDefOf.TaskCompletion, false); }
                    catch (Exception ex) { Messages.Message("MSS.FileError".Translate(ex.Message), MessageTypeDefOf.RejectInput, false); }
                }
                if (Widgets.ButtonText(new Rect(rect.width - 120f, rect.height - 34f, 120f, 32f), "MSS.Close".Translate())) Close();
            }
        }
    }
}
