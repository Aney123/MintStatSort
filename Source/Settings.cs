using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace MintStatSort
{
    public sealed class SortCriterion : IExposable
    {
        public string Id;
        public bool HigherIsBetter = true;
        public int Weight = 1;
        public bool HasMinimum, HasMaximum;
        public double Minimum, Maximum;
        public void ExposeData()
        {
            Scribe_Values.Look(ref Id, "id");
            Scribe_Values.Look(ref HigherIsBetter, "higherIsBetter", true);
            Scribe_Values.Look(ref Weight, "importance", 1);
            Scribe_Values.Look(ref HasMinimum, "hasMinimum", false);
            Scribe_Values.Look(ref HasMaximum, "hasMaximum", false);
            Scribe_Values.Look(ref Minimum, "minimum", 0d);
            Scribe_Values.Look(ref Maximum, "maximum", 0d);
        }
        internal void Migrate()
        {
            Weight = NumericFilterCore.Importance(Weight);
            if (!SortingCore.Finite(Minimum)) HasMinimum = false;
            if (!SortingCore.Finite(Maximum)) HasMaximum = false;
            if (HasMinimum && HasMaximum && Minimum > Maximum) { HasMinimum = false; HasMaximum = false; }
        }
        internal bool Matches(double? value) => NumericFilterCore.Matches(value,
            HasMinimum ? (double?)Minimum : null, HasMaximum ? (double?)Maximum : null);
        public SortCriterion Copy() { return new SortCriterion { Id = Id, HigherIsBetter = HigherIsBetter,
            Weight = Weight, HasMinimum = HasMinimum, HasMaximum = HasMaximum, Minimum = Minimum, Maximum = Maximum }; }
    }
    public sealed class SortSettings : ModSettings
    {
        public bool Enabled = true;
        public string Metric = "original";
        public string OrderMode = "";
        public bool Descending = true;
        public string Category = "all";
        public string Material = "";
        public List<SortCriterion> Criteria;
        public bool StoredMaterialsOnly = true;
        public QualityCategory Quality = QualityCategory.Normal;
        public bool FavoritesOnly;
        public bool OnlyStockReady, MixSubstitutes;
        public string Ammo = "";
        public int Executions = 1;
        public List<ComparisonColumn> Columns = new List<ComparisonColumn>();
        public List<string> FavoriteRecipes = new List<string>();
        public List<ComparisonProfile> Profiles = new List<ComparisonProfile>();
        public List<BenchComparison> BenchComparisons = new List<BenchComparison>();
        private HashSet<string> favoriteIndex;
        public override void ExposeData()
        {
            Scribe_Values.Look(ref Enabled, "enabled", true);
            Scribe_Values.Look(ref Metric, "metric", "original");
            Scribe_Values.Look(ref OrderMode, "orderMode", "");
            Scribe_Values.Look(ref Descending, "descending", true);
            Scribe_Values.Look(ref Category, "category", "all");
            Scribe_Values.Look(ref Material, "material", "");
            Scribe_Collections.Look(ref Criteria, "criteria", LookMode.Deep);
            Scribe_Values.Look(ref StoredMaterialsOnly, "storedMaterialsOnly", true);
            Scribe_Values.Look(ref Quality, "comparisonQuality", QualityCategory.Normal);
            Scribe_Values.Look(ref FavoritesOnly, "favoritesOnly", false);
            Scribe_Values.Look(ref OnlyStockReady, "onlyStockReady", false);
            Scribe_Values.Look(ref MixSubstitutes, "mixSubstitutes", false);
            Scribe_Values.Look(ref Ammo, "ammo", "");
            Scribe_Values.Look(ref Executions, "executions", 1);
            Scribe_Collections.Look(ref Columns, "columns", LookMode.Deep);
            Scribe_Collections.Look(ref FavoriteRecipes, "favoriteRecipes", LookMode.Value);
            Scribe_Collections.Look(ref Profiles, "comparisonProfiles", LookMode.Deep);
            Scribe_Collections.Look(ref BenchComparisons, "benchComparisons", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) Migrate();
            base.ExposeData();
        }
        internal void Migrate()
        {
            if (!Enum.IsDefined(typeof(QualityCategory), Quality)) Quality = QualityCategory.Normal;
            if (Criteria == null)
            {
                Criteria = new List<SortCriterion>();
                if (!string.IsNullOrEmpty(Metric) && Metric != "original" && Metric != "name")
                    Criteria.Add(new SortCriterion { Id = Metric, HigherIsBetter = true });
            }
            Criteria.RemoveAll(c => c == null || string.IsNullOrEmpty(c.Id) || c.Id == "original" || c.Id == "name");
            foreach (var criterion in Criteria) criterion.Migrate();
            if (OrderMode != "criteria" && OrderMode != "name" && OrderMode != "original")
                OrderMode = Criteria.Count > 0 ? "criteria" : Metric == "name" ? "name" : "original";
            Category = CategorySelection.Pack(CategorySelection.Unpack(Category));
            Ammo = Ammo ?? "";
            Executions = BatchCount.Clamp(Executions); Columns = ComparisonColumn.Normalize(Columns);
            FavoriteRecipes = (FavoriteRecipes ?? new List<string>()).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList();
            favoriteIndex = null;
            Profiles = Profiles ?? new List<ComparisonProfile>();
            Profiles.RemoveAll(p => p == null || p.Values == null || string.IsNullOrEmpty(PreferenceNames.Normalize(p.Name)));
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var profile in Profiles)
            {
                profile.Name = PreferenceNames.Normalize(profile.Name);
                if (string.IsNullOrEmpty(profile.Id) || !ids.Add(profile.Id)) { profile.Id = Guid.NewGuid().ToString("N"); ids.Add(profile.Id); }
                profile.Values.Migrate();
            }
            BenchComparisons = (BenchComparisons ?? new List<BenchComparison>()).Where(b => b != null && b.Values != null && !string.IsNullOrEmpty(b.BenchKey))
                .GroupBy(b => b.BenchKey, StringComparer.Ordinal).Select(g => g.Last()).ToList();
            foreach (var bench in BenchComparisons)
            { bench.Values.Migrate(); if (!ids.Contains(bench.ProfileId ?? "")) bench.ProfileId = null; }
        }
        internal void Reset()
        {
            Metric = "original"; OrderMode = "original";
            Criteria = new List<SortCriterion>();
            Category = "all";
            Material = "";
            Descending = true;
            StoredMaterialsOnly = true;
            Quality = QualityCategory.Normal;
            Ammo = ""; MixSubstitutes = false; FavoritesOnly = false; OnlyStockReady = false; Executions = 1; Columns = new List<ComparisonColumn>();
        }
        internal SortSettings Copy()
        {
            var copy = new SortSettings { Enabled = Enabled };
            copy.CopyComparisonFrom(this);
            return copy;
        }
        internal void CopyComparisonFrom(SortSettings source)
        {
            Metric = source.Metric; OrderMode = source.OrderMode;
            Descending = source.Descending;
            Category = source.Category;
            Material = source.Material;
            StoredMaterialsOnly = source.StoredMaterialsOnly;
            Quality = source.Quality;
            Ammo = source.Ammo; MixSubstitutes = source.MixSubstitutes; FavoritesOnly = source.FavoritesOnly; OnlyStockReady = source.OnlyStockReady; Executions = source.Executions;
            Columns = ComparisonColumn.Normalize(source.Columns);
            Criteria = source.Criteria?.ConvertAll(c => c.Copy()) ?? new List<SortCriterion>();
        }
        internal bool IsFavorite(RecipeDef recipe) => recipe != null && IsFavoriteId(recipe.defName);
        internal bool IsFavoriteId(string id)
        {
            if (favoriteIndex == null) favoriteIndex = new HashSet<string>(FavoriteRecipes ?? new List<string>(), StringComparer.Ordinal);
            return id != null && favoriteIndex.Contains(id);
        }
        internal bool ToggleFavorite(RecipeDef recipe)
        {
            if (recipe == null || string.IsNullOrEmpty(recipe.defName)) return false;
            IsFavoriteId(recipe.defName);
            if (favoriteIndex.Remove(recipe.defName)) { FavoriteRecipes.RemoveAll(id => id == recipe.defName); return false; }
            favoriteIndex.Add(recipe.defName); FavoriteRecipes.Add(recipe.defName); return true;
        }
        internal ComparisonProfile SaveProfile(string name, SortSettings comparison)
        {
            name = PreferenceNames.Normalize(name);
            if (name.Length == 0) return null;
            var profile = Profiles.FirstOrDefault(p => PreferenceNames.Same(p.Name, name));
            if (profile == null) { profile = new ComparisonProfile { Id = Guid.NewGuid().ToString("N") }; Profiles.Add(profile); }
            profile.Name = name; profile.Values = ComparisonPreferences.Capture(comparison); return profile;
        }
        internal string UniqueProfileName(string name)
        {
            string original = PreferenceNames.Normalize(name);
            if (original.Length == 0) original = "MSS.ProfileImported".Translate();
            string result = original; int number = 2;
            while (Profiles.Any(p => PreferenceNames.Same(p.Name, result)))
            {
                string suffix = " (" + number++ + ")";
                result = original.Substring(0, Math.Min(original.Length, PreferenceNames.MaximumLength - suffix.Length)) + suffix;
            }
            return result;
        }
        internal bool RenameProfile(string id, string name)
        {
            var profile = Profiles.FirstOrDefault(p => p.Id == id); name = PreferenceNames.Normalize(name);
            if (profile == null || name.Length == 0 || Profiles.Any(p => p.Id != id && PreferenceNames.Same(p.Name, name))) return false;
            profile.Name = name; return true;
        }
        internal ComparisonProfile DuplicateProfile(string id, string name)
        {
            var original = Profiles.FirstOrDefault(p => p.Id == id); name = PreferenceNames.Normalize(name);
            if (original == null || name.Length == 0 || Profiles.Any(p => PreferenceNames.Same(p.Name, name))) return null;
            var copy = new SortSettings(); original.Values.ApplyTo(copy);
            var profile = new ComparisonProfile { Id = Guid.NewGuid().ToString("N"), Name = name, Values = ComparisonPreferences.Capture(copy) };
            Profiles.Add(profile); return profile;
        }
        internal void DeleteProfile(string id)
        {
            Profiles.RemoveAll(p => p.Id == id);
            foreach (var bench in BenchComparisons.Where(b => b.ProfileId == id)) bench.ProfileId = null;
        }
        internal BenchComparison Bench(string key) => string.IsNullOrEmpty(key) ? null : BenchComparisons.FirstOrDefault(b => b.BenchKey == key);
        internal void RememberBench(string key, SortSettings comparison, string profileId)
        {
            if (string.IsNullOrEmpty(key)) return;
            var bench = Bench(key);
            if (bench == null) { bench = new BenchComparison { BenchKey = key }; BenchComparisons.Add(bench); }
            bench.Values = ComparisonPreferences.Capture(comparison);
            bench.ProfileId = Profiles.Any(p => p.Id == profileId) ? profileId : null;
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
                Connect("MSS.NativeMenu".Translate(), () => VanillaIntegration.Install(new Harmony("mintstatsort.vanilla")), statuses);
                if (AccessTools.TypeByName("NiceBillTab.TabBillsDrawer") != null)
                    Connect("Nice Bill Tab", () => NiceIntegration.Install(new Harmony("mintstatsort.nicebilltab")), statuses);
                if (AccessTools.TypeByName("DubsMintMenus.Patch_BillStack_DoListing") != null)
                    Connect("Dubs Mint Menus", () => MintIntegration.Install(new Harmony("mintstatsort.recipes")), statuses);
                if (CombatExtendedIntegration.Active) statuses.Add("MSS.CE.Status".Translate());
                Connect("MSS.WorkshopNotes".Translate(), () => WorkshopNotesIntegration.Install(new Harmony("mintstatsort.workshopnotes")), statuses);
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
            if (listing.ButtonText("MSS.Diagnostics".Translate())) Find.WindowStack.Add(new DiagnosticDialog("Settings"));
            if (listing.ButtonText("MSS.Reset".Translate()))
            {
                Options.Reset();
                Options.BenchComparisons.Clear();
                RecipeSession.Invalidate();
                WriteSettings();
            }
            listing.End();
        }
    }
}
