using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace MintStatSort
{
    public sealed class ProfileMetric
    {
        public string Id;
        public bool Higher = true, HasMinimum, HasMaximum;
        public int Weight = 1;
        public double Minimum, Maximum;
    }
    public sealed class ProfileColumn
    { public string Id; public int Width = 134; public bool Visible = true; }
    public sealed class ProfileDocument
    {
        public string Name, Metric = "original", OrderMode = "criteria", Category = "all", Material = "";
        public bool Descending = true, StoredOnly = true, FavoritesOnly, OnlyStockReady, MixSubstitutes;
        public string Ammo = "";
        public int Quality = 2, Executions = 1;
        public List<ProfileMetric> Criteria = new List<ProfileMetric>();
        public List<ProfileColumn> Columns = new List<ProfileColumn>();
        public const int MaximumLength = 1024 * 1024;
        private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);
        public string Serialize()
        {
            var root = new XElement("MintStatSortProfile", new XAttribute("format", 1),
                new XElement("name", Name), new XElement("metric", Metric), new XElement("order", OrderMode),
                new XElement("category", Category), new XElement("material", Material),
                new XElement("descending", Descending), new XElement("storedOnly", StoredOnly),
                new XElement("favoritesOnly", FavoritesOnly), new XElement("onlyStockReady", OnlyStockReady),
                new XElement("mixSubstitutes", MixSubstitutes), new XElement("ammo", Ammo),
                new XElement("quality", Quality), new XElement("executions", Executions),
                new XElement("criteria", Criteria.Select(c => new XElement("criterion", new XAttribute("id", c.Id),
                    new XAttribute("higher", c.Higher), new XAttribute("weight", c.Weight),
                    c.HasMinimum ? new XAttribute("minimum", Number(c.Minimum)) : null,
                    c.HasMaximum ? new XAttribute("maximum", Number(c.Maximum)) : null))),
                new XElement("columns", Columns.Select(c => new XElement("column", new XAttribute("id", c.Id),
                    new XAttribute("width", c.Width), new XAttribute("visible", c.Visible)))));
            string text = root.ToString();
            // Validate exports too: never emit non-finite or ambiguous profiles.
            Parse(text); return text;
        }
        public static ProfileDocument Parse(string text)
        {
            if (text == null || text.Length > MaximumLength) throw new FormatException("Profile too large");
            using (var reader = XmlReader.Create(new StringReader(text), new XmlReaderSettings {
                DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaximumLength }))
            {
                var root = XDocument.Load(reader).Root;
                if (root == null || root.Name != "MintStatSortProfile" || (string)root.Attribute("format") != "1") throw new FormatException("Unknown profile format");
                string Read(string name, string fallback) => (string)root.Element(name) ?? fallback;
                bool Flag(string name, bool fallback) => bool.Parse(Read(name, fallback.ToString()));
                int Integer(string name, int fallback) => int.Parse(Read(name, fallback.ToString(CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture);
                var result = new ProfileDocument { Name = Read("name", ""), Metric = Read("metric", "original"), OrderMode = Read("order", "criteria"),
                    Category = Read("category", "all"), Material = Read("material", ""), Descending = Flag("descending", true),
                    StoredOnly = Flag("storedOnly", true), FavoritesOnly = Flag("favoritesOnly", false), OnlyStockReady = Flag("onlyStockReady", false),
                    Ammo = Read("ammo", ""), MixSubstitutes = Flag("mixSubstitutes", false), Quality = Integer("quality", 2), Executions = Integer("executions", 1) };
                if (result.Name.Length == 0 || result.Name.Length > 80 || result.Category.Length > 8192 || result.Material.Length > 256 || result.Ammo.Length > 256 || result.Metric.Length > 256 ||
                    result.Quality < 0 || result.Quality > 6 || result.Executions < 1 || result.Executions > BatchCount.Maximum ||
                    !new[] { "criteria", "name", "original" }.Contains(result.OrderMode)) throw new FormatException("Invalid profile settings");
                foreach (var node in root.Element("criteria")?.Elements("criterion") ?? Enumerable.Empty<XElement>())
                {
                    string id = (string)node.Attribute("id");
                    if (string.IsNullOrEmpty(id) || id.Length > 256 || result.Criteria.Count >= 64 || result.Criteria.Any(c => c.Id == id)) throw new FormatException("Invalid criterion IDs");
                    var criterion = new ProfileMetric { Id = id, Higher = bool.Parse((string)node.Attribute("higher") ?? "true"),
                        Weight = int.Parse((string)node.Attribute("weight") ?? "1", CultureInfo.InvariantCulture) };
                    foreach (string bound in new[] { "minimum", "maximum" })
                    {
                        var value = node.Attribute(bound);
                        if (value == null) continue;
                        double number = double.Parse(value.Value, NumberStyles.Float, CultureInfo.InvariantCulture);
                        if (double.IsNaN(number) || double.IsInfinity(number)) throw new FormatException("Invalid numeric bound");
                        if (bound == "minimum") { criterion.HasMinimum = true; criterion.Minimum = number; }
                        else { criterion.HasMaximum = true; criterion.Maximum = number; }
                    }
                    if (criterion.Weight < 1 || criterion.Weight > 5 || criterion.HasMinimum && criterion.HasMaximum && criterion.Minimum > criterion.Maximum) throw new FormatException("Invalid criterion range");
                    result.Criteria.Add(criterion);
                }
                foreach (var node in root.Element("columns")?.Elements("column") ?? Enumerable.Empty<XElement>())
                {
                    string id = (string)node.Attribute("id");
                    int width = int.Parse((string)node.Attribute("width") ?? "134", CultureInfo.InvariantCulture);
                    if (string.IsNullOrEmpty(id) || id.Length > 256 || result.Columns.Count >= 256 || result.Columns.Any(c => c.Id == id) || width < 80 || width > 260) throw new FormatException("Invalid columns");
                    result.Columns.Add(new ProfileColumn { Id = id, Width = width, Visible = bool.Parse((string)node.Attribute("visible") ?? "true") });
                }
                return result;
            }
        }
    }
}
