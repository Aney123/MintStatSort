using System;
using System.Linq;
using UnityEngine;
using Verse;

namespace MintStatSort
{
    internal sealed class ProfileNameDialog : Window
    {
        private string name;
        private readonly Action<string> save;
        internal string TitleKey = "MSS.ProfileSave";
        internal bool AllowExisting = true;
        private readonly string initialName;
        public override Vector2 InitialSize => new Vector2(460f, 210f);
        internal ProfileNameDialog(string initialName, Action<string> save)
        {
            this.initialName = initialName ?? ""; name = this.initialName; this.save = save;
            doCloseX = true; forcePause = true; absorbInputAroundWindow = true;
        }
        public override void DoWindowContents(Rect rect)
        {
            using (new UiScope())
            {
                Text.Font = GameFont.Medium;
                Widgets.Label(new Rect(0, 0, rect.width - 35f, 30f), TitleKey.Translate());
                Text.Font = GameFont.Small;
                name = Widgets.TextField(new Rect(0, 43f, rect.width, 30f), name);
                if (name.Length > PreferenceNames.MaximumLength) name = name.Substring(0, PreferenceNames.MaximumLength);
                string normalized = PreferenceNames.Normalize(name);
                bool exists = RecipeSession.Options.Profiles.Any(p => PreferenceNames.Same(p.Name, normalized));
                bool conflict = exists && !AllowExisting && !PreferenceNames.Same(initialName, normalized);
                Text.Font = GameFont.Tiny; Text.WordWrap = true;
                Widgets.Label(new Rect(0, 80f, rect.width, 40f), (conflict ? "MSS.ProfileNameConflict" : exists && AllowExisting ? "MSS.ProfileOverwriteTip" : "MSS.ProfileNameTip").Translate());
                Text.Font = GameFont.Small;
                bool enabled = GUI.enabled; GUI.enabled = enabled && normalized.Length > 0 && !conflict;
                if (Widgets.ButtonText(new Rect(rect.width - 240f, rect.height - 34f, 116f, 32f), (exists && AllowExisting ? "MSS.ProfileOverwrite" : TitleKey).Translate()))
                { save(normalized); Close(); }
                GUI.enabled = enabled;
                if (Widgets.ButtonText(new Rect(rect.width - 116f, rect.height - 34f, 116f, 32f), "MSS.Close".Translate())) Close();
            }
        }
    }
}
