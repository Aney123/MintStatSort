using System;
using System.IO;
using System.Linq;
using RimWorld;
using Verse;

namespace MintStatSort
{
    internal static class ProfileExchange
    {
        internal static string DirectoryPath => Path.Combine(GenFilePaths.SaveDataFolderPath, "MintStatSort", "Profiles");
        internal static ProfileDocument Document(ComparisonProfile profile)
        {
            var p = profile.Values;
            return new ProfileDocument { Name = profile.Name, Metric = p.Metric, Category = p.Category, Material = p.Material, OrderMode = p.OrderMode,
                Descending = p.Descending, StoredOnly = p.StoredMaterialsOnly, FavoritesOnly = p.FavoritesOnly, OnlyStockReady = p.OnlyStockReady,
                Ammo = p.Ammo, MixSubstitutes = p.MixSubstitutes, Quality = (int)p.Quality, Executions = p.Executions,
                Criteria = p.Criteria.Select(c => new ProfileMetric { Id = c.Id, Higher = c.HigherIsBetter, Weight = c.Weight,
                    HasMinimum = c.HasMinimum, Minimum = c.Minimum, HasMaximum = c.HasMaximum, Maximum = c.Maximum }).ToList(),
                Columns = p.Columns.Select(c => new ProfileColumn { Id = c.Id, Width = c.Width, Visible = c.Visible }).ToList() };
        }
        internal static string Export(ComparisonProfile profile)
        {
            string text = Document(profile).Serialize();
            Directory.CreateDirectory(DirectoryPath);
            string path = Path.Combine(DirectoryPath, "profile-" + Guid.NewGuid().ToString("N") + ".xml");
            File.WriteAllText(path, text); return path;
        }
        internal static ComparisonProfile Import(string path, SortSettings target)
        {
            string full = Path.GetFullPath(path);
            string allowed = Path.GetFullPath(DirectoryPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(allowed, StringComparison.OrdinalIgnoreCase) || !File.Exists(full) || new FileInfo(full).Length > ProfileDocument.MaximumLength) throw new FormatException("Invalid import file");
            var d = ProfileDocument.Parse(File.ReadAllText(full));
            var values = new ComparisonPreferences { Metric = d.Metric, OrderMode = d.OrderMode, Category = d.Category, Material = d.Material,
                Descending = d.Descending, StoredMaterialsOnly = d.StoredOnly, FavoritesOnly = d.FavoritesOnly, OnlyStockReady = d.OnlyStockReady,
                Ammo = d.Ammo, MixSubstitutes = d.MixSubstitutes, Quality = (QualityCategory)d.Quality, Executions = d.Executions,
                Criteria = d.Criteria.Select(c => new SortCriterion { Id = c.Id, HigherIsBetter = c.Higher, Weight = c.Weight,
                    HasMinimum = c.HasMinimum, Minimum = c.Minimum, HasMaximum = c.HasMaximum, Maximum = c.Maximum }).ToList(),
                Columns = d.Columns.Select(c => new ComparisonColumn { Id = c.Id, Width = c.Width, Visible = c.Visible }).ToList() };
            values.Migrate();
            var profile = new ComparisonProfile { Id = Guid.NewGuid().ToString("N"), Name = target.UniqueProfileName(d.Name), Values = values };
            target.Profiles.Add(profile); return profile;
        }
    }
}
