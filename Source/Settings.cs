using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace MintStatSort
{
    public sealed class SortCriterion : IExposable
    {
        public string Id;
        public bool HigherIsBetter = true;
        public void ExposeData()
        {
            Scribe_Values.Look(ref Id, "id");
            Scribe_Values.Look(ref HigherIsBetter, "higherIsBetter", true);
        }
        public SortCriterion Copy() { return new SortCriterion { Id = Id, HigherIsBetter = HigherIsBetter }; }
    }
    public sealed class SortSettings : ModSettings
    {
        public bool Enabled = true;
        public string Metric = "original";
        public bool Descending = true;
        public string Category = "all";
        public string Material = "";
        public List<SortCriterion> Criteria;
        public bool StoredMaterialsOnly = true;
        public override void ExposeData()
        {
            Scribe_Values.Look(ref Enabled, "enabled", true);
            Scribe_Values.Look(ref Metric, "metric", "original");
            Scribe_Values.Look(ref Descending, "descending", true);
            Scribe_Values.Look(ref Category, "category", "all");
            Scribe_Values.Look(ref Material, "material", "");
            Scribe_Collections.Look(ref Criteria, "criteria", LookMode.Deep);
            Scribe_Values.Look(ref StoredMaterialsOnly, "storedMaterialsOnly", true);
            base.ExposeData();
        }
        internal void Migrate()
        {
            if (Criteria == null)
            {
                Criteria = new List<SortCriterion>();
                if (!string.IsNullOrEmpty(Metric) && Metric != "original" && Metric != "name")
                    Criteria.Add(new SortCriterion { Id = Metric, HigherIsBetter = true });
            }
            Criteria.RemoveAll(c => c == null || string.IsNullOrEmpty(c.Id) || c.Id == "original" || c.Id == "name");
        }
        internal void Reset()
        {
            Metric = "original";
            Criteria = new List<SortCriterion>();
            Category = "all";
            Material = "";
            Descending = true;
            StoredMaterialsOnly = true;
        }
    }
    public sealed class MintStatSortMod : Mod
    {
        public static MintStatSortMod Instance;
        public SortSettings Options;
        internal static string PatchStatus = "";
        public MintStatSortMod(ModContentPack content) : base(content)
        {
            Instance = this;
            Options = GetSettings<SortSettings>();
            Options.Migrate();
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                var statuses = new List<string>();
                if (AccessTools.TypeByName("NiceBillTab.TabBillsDrawer") != null)
                    Connect("Nice Bill Tab", () => NiceIntegration.Install(new Harmony("mintstatsort.nicebilltab")), statuses);
                if (AccessTools.TypeByName("DubsMintMenus.Patch_BillStack_DoListing") != null)
                    Connect("Dubs Mint Menus", () => MintIntegration.Install(new Harmony("mintstatsort.recipes")), statuses);
                PatchStatus = statuses.Count == 0 ? "MSS.NoMenu".Translate().ToString() : string.Join("\n", statuses);
            });
        }
        private static void Connect(string name, Action install, List<string> statuses)
        {
            try
            {
                install();
                statuses.Add("MSS.ConnectedMenu".Translate(name));
            }
            catch (Exception ex)
            {
                statuses.Add(name + ": " + ex.Message);
                Log.Error("[Mint Stat Sort] Integración desactivada para " + name + ": " + ex);
            }
        }
        public override string SettingsCategory() { return "Mint Stat Sort"; }
        public override void DoSettingsWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.CheckboxLabeled("MSS.Enabled".Translate(), ref Options.Enabled);
            listing.Label("MSS.SettingsInfo".Translate());
            listing.Gap();
            if (!string.IsNullOrEmpty(PatchStatus)) listing.Label(PatchStatus);
            if (listing.ButtonText("MSS.Reset".Translate()))
            {
                Options.Reset();
                RecipeSession.Invalidate();
                WriteSettings();
            }
            listing.End();
        }
    }
}
