using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MintStatSort
{
    // Plain comparison data: no profile/favourite collections and no settings
    // ownership, so deep serialization cannot form recursive graphs.
    public sealed class ComparisonPreferences : IExposable
    {
        public string Metric = "original", Category = "all", Material = "", OrderMode = "";
        public bool Descending = true, StoredMaterialsOnly = true, FavoritesOnly;
        public bool OnlyStockReady, MixSubstitutes;
        public string Ammo = "";
        public int Executions = 1;
        public List<ComparisonColumn> Columns = new List<ComparisonColumn>();
        public QualityCategory Quality = QualityCategory.Normal;
        public List<SortCriterion> Criteria = new List<SortCriterion>();
        public void ExposeData()
        {
            Scribe_Values.Look(ref Metric, "metric", "original");
            Scribe_Values.Look(ref OrderMode, "orderMode", "");
            Scribe_Values.Look(ref Category, "category", "all");
            Scribe_Values.Look(ref Material, "material", "");
            Scribe_Values.Look(ref Descending, "descending", true);
            Scribe_Values.Look(ref StoredMaterialsOnly, "storedMaterialsOnly", true);
            Scribe_Values.Look(ref FavoritesOnly, "favoritesOnly", false);
            Scribe_Values.Look(ref OnlyStockReady, "onlyStockReady", false);
            Scribe_Values.Look(ref MixSubstitutes, "mixSubstitutes", false);
            Scribe_Values.Look(ref Ammo, "ammo", "");
            Scribe_Values.Look(ref Executions, "executions", 1);
            Scribe_Collections.Look(ref Columns, "columns", LookMode.Deep);
            Scribe_Values.Look(ref Quality, "quality", QualityCategory.Normal);
            Scribe_Collections.Look(ref Criteria, "criteria", LookMode.Deep);
        }
        internal void Migrate()
        {
            Metric = Metric ?? "original"; Category = Category ?? "all"; Material = Material ?? "";
            if (!Enum.IsDefined(typeof(QualityCategory), Quality)) Quality = QualityCategory.Normal;
            Criteria = Criteria ?? new List<SortCriterion>();
            Criteria.RemoveAll(c => c == null || string.IsNullOrEmpty(c.Id) || c.Id == "original" || c.Id == "name");
            foreach (var criterion in Criteria) criterion.Migrate();
            if (OrderMode != "criteria" && OrderMode != "name" && OrderMode != "original")
                OrderMode = Criteria.Count > 0 ? "criteria" : Metric == "name" ? "name" : "original";
            Category = CategorySelection.Pack(CategorySelection.Unpack(Category));
            Ammo = Ammo ?? "";
            Executions = BatchCount.Clamp(Executions); Columns = ComparisonColumn.Normalize(Columns);
        }
        internal static ComparisonPreferences Capture(SortSettings source)
        {
            return new ComparisonPreferences
            {
                Ammo = source.Ammo, MixSubstitutes = source.MixSubstitutes, Metric = source.Metric, OrderMode = source.OrderMode, Category = source.Category, Material = source.Material,
                Descending = source.Descending, StoredMaterialsOnly = source.StoredMaterialsOnly,
                Quality = source.Quality, FavoritesOnly = source.FavoritesOnly, OnlyStockReady = source.OnlyStockReady, Executions = source.Executions, Columns = ComparisonColumn.Normalize(source.Columns),
                Criteria = source.Criteria?.ConvertAll(c => c.Copy()) ?? new List<SortCriterion>()
            };
        }
        internal void ApplyTo(SortSettings target)
        {
            target.Ammo = Ammo; target.MixSubstitutes = MixSubstitutes; target.Metric = Metric; target.OrderMode = OrderMode; target.Category = Category; target.Material = Material;
            target.Descending = Descending; target.StoredMaterialsOnly = StoredMaterialsOnly;
            target.Quality = Quality; target.FavoritesOnly = FavoritesOnly; target.OnlyStockReady = OnlyStockReady; target.Executions = Executions; target.Columns = ComparisonColumn.Normalize(Columns);
            target.Criteria = Criteria?.ConvertAll(c => c.Copy()) ?? new List<SortCriterion>();
        }
        internal bool Matches(SortSettings settings)
        {
            if (Ammo != settings.Ammo || MixSubstitutes != settings.MixSubstitutes || Metric != settings.Metric || OrderMode != settings.OrderMode || Category != settings.Category || Material != settings.Material ||
                Descending != settings.Descending || StoredMaterialsOnly != settings.StoredMaterialsOnly ||
                Quality != settings.Quality || FavoritesOnly != settings.FavoritesOnly || OnlyStockReady != settings.OnlyStockReady || Executions != settings.Executions || Columns.Count != settings.Columns.Count || Criteria.Count != settings.Criteria.Count) return false;
            for (int i = 0; i < Columns.Count; i++)
                if (Columns[i].Id != settings.Columns[i].Id || Columns[i].Width != settings.Columns[i].Width || Columns[i].Visible != settings.Columns[i].Visible) return false;
            for (int i = 0; i < Criteria.Count; i++)
                if (Criteria[i].Id != settings.Criteria[i].Id || Criteria[i].HigherIsBetter != settings.Criteria[i].HigherIsBetter ||
                    Criteria[i].Weight != settings.Criteria[i].Weight || Criteria[i].HasMinimum != settings.Criteria[i].HasMinimum ||
                    Criteria[i].HasMaximum != settings.Criteria[i].HasMaximum ||
                    Criteria[i].HasMinimum && Criteria[i].Minimum != settings.Criteria[i].Minimum ||
                    Criteria[i].HasMaximum && Criteria[i].Maximum != settings.Criteria[i].Maximum) return false;
            return true;
        }
    }
    public sealed class ComparisonProfile : IExposable
    {
        public string Id, Name;
        public ComparisonPreferences Values;
        public void ExposeData()
        {
            Scribe_Values.Look(ref Id, "id"); Scribe_Values.Look(ref Name, "name");
            Scribe_Deep.Look(ref Values, "comparison");
        }
    }
    public sealed class BenchComparison : IExposable
    {
        public string BenchKey, ProfileId;
        public ComparisonPreferences Values;
        public void ExposeData()
        {
            Scribe_Values.Look(ref BenchKey, "bench"); Scribe_Values.Look(ref ProfileId, "profile");
            Scribe_Deep.Look(ref Values, "comparison");
        }
    }
}
