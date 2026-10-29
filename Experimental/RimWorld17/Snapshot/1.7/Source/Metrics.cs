using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RimWorld;
using Verse;

namespace MintStatSort
{
    internal sealed class Measurement
    {
        public double? Value;
        public string Display;
        public string Detail;
        public static Measurement Missing(string reason = null)
        {
            return new Measurement { Display = "MSS.NoData".Translate(), Detail = reason ?? "MSS.NoDataTip".Translate().ToString() };
        }
    }
    internal sealed class Metric
    {
        public string Id;
        public string Name;
        public string Group;
        public StatDef Stat;
        public bool Preferred;
        public string GroupKey;
        public bool HigherIsBetter = true;
    }
    internal static class Metrics
    {
        private static readonly HashSet<string> Preferred = new HashSet<string>(StringComparer.Ordinal)
        {
            "ArmorRating_Blunt", "ArmorRating_Sharp", "ArmorRating_Heat", "MeleeWeapon_AverageDPS",
            "Mass", "MarketValue", "MaxHitPoints", "Insulation_Cold", "Insulation_Heat",
            "AccuracyShort", "AccuracyMedium", "AccuracyLong", "Nutrition", "MedicalPotency", "Beauty"
        };
        private static readonly HashSet<string> CePreferred = new HashSet<string>(StringComparer.Ordinal)
            { "Bulk", "WornBulk", "SightsEfficiency", "ShotSpread", "SwayFactor" };
        private static readonly HashSet<string> LowerIsBetter = new HashSet<string>(StringComparer.Ordinal)
            { "Mass", "WorkToMake", "Bulk", "WornBulk", "ShotSpread", "SwayFactor", "ReloadTime" };
        private static readonly HashSet<string> CeHidden = new HashSet<string>(StringComparer.Ordinal)
            { "Caliber", "BipodStats", "UBGLInfo", "MeleeArmorPenetration_Weapon", "MagazineCapacity" };
        private static readonly string[] RecipeMetricIds =
        {
            "recipe.work", "weapon.melee.damage", "weapon.melee.blunt", "weapon.melee.sharp",
            "weapon.damage", "weapon.dps", "weapon.range", "weapon.burst", "weapon.penetration"
        };
        internal static Metric Resolve(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (id.StartsWith("ce.", StringComparison.Ordinal)) return CombatExtendedIntegration.Resolve(id);
            if (id.StartsWith("stat:", StringComparison.Ordinal))
            {
                var stat = DefDatabase<StatDef>.GetNamedSilentFail(id.Substring(5));
                if (stat == null) return null;
                string group = StatGroup(stat.defName);
                string shortKey = stat.defName == "ArmorRating_Blunt" ? "MSS.ArmorBlunt" :
                    stat.defName == "ArmorRating_Sharp" ? "MSS.ArmorSharp" :
                    stat.defName == "ArmorRating_Heat" ? "MSS.ArmorHeat" :
                    stat.defName == "Mass" ? "MSS.Weight" : null;
                return new Metric
                {
                    Id = id, Stat = stat, Name = shortKey == null ? stat.LabelCap.ToString() : shortKey.Translate().ToString(),
                    GroupKey = group, Group = ("MSS.Group." + group).Translate(),
                    Preferred = Preferred.Contains(stat.defName) || CePreferred.Contains(stat.defName) && CombatExtendedIntegration.Active,
                    HigherIsBetter = !LowerIsBetter.Contains(stat.defName)
                };
            }
            string key;
            switch (id)
            {
                case "original": key = "MSS.Original"; break;
                case "name": key = "MSS.Name"; break;
                case "recipe.work": key = "MSS.Work"; break;
                case "weapon.damage": key = "MSS.ShotDamage"; break;
                case "weapon.range": key = "MSS.Range"; break;
                case "weapon.burst": key = "MSS.Burst"; break;
                case "weapon.penetration": key = "MSS.Penetration"; break;
                case "weapon.dps": key = "MSS.RangedDps"; break;
                case "weapon.melee.damage": key = "MSS.MeleeDamage"; break;
                case "weapon.melee.blunt": key = "MSS.BluntDamage"; break;
                case "weapon.melee.sharp": key = "MSS.SharpDamage"; break;
                default: return null;
            }
            string groupKey = id.StartsWith("weapon.", StringComparison.Ordinal) ? "attack" : "practical";
            return new Metric { Id = id, Name = key.Translate(), GroupKey = groupKey,
                Group = ("MSS.Group." + groupKey).Translate(), Preferred = true, HigherIsBetter = id != "recipe.work" };
        }
        internal static double LimitScale(Metric metric)
        {
            if (CombatExtendedIntegration.Active && (metric.Id == "weapon.penetration" ||
                metric.Id == "stat:ArmorRating_Sharp" || metric.Id == "stat:ArmorRating_Blunt")) return 1;
            return metric.Id == "weapon.penetration" || metric.Stat != null &&
                metric.Stat.toStringStyle.ToString().StartsWith("Percent", StringComparison.Ordinal) ? 100 : 1;
        }
        internal static string LimitUnit(Metric metric)
        {
            if (LimitScale(metric) == 100) return "%";
            if (metric.Id == "stat:Mass") return "kg";
            if (metric.Id == "ce.penetration.sharp" || CombatExtendedIntegration.Active &&
                (metric.Id == "weapon.penetration" || metric.Id == "stat:ArmorRating_Sharp")) return "mm RHA";
            if (metric.Id == "ce.penetration.blunt" || CombatExtendedIntegration.Active && metric.Id == "stat:ArmorRating_Blunt") return "MPa";
            if (metric.Id == "ce.reload") return "s";
            // Custom workers may display non-linear or translated units. Use the
            // explicit base value instead of guessing from their formatted text.
            return "MSS.BaseValue".Translate();
        }
        private static string StatGroup(string name)
        {
            if (name.StartsWith("ArmorRating_", StringComparison.Ordinal)) return "protection";
            if (name.StartsWith("Insulation_", StringComparison.Ordinal)) return "climate";
            if (name.StartsWith("MeleeWeapon_", StringComparison.Ordinal) || name.StartsWith("Accuracy", StringComparison.Ordinal) ||
                name.StartsWith("RangedWeapon_", StringComparison.Ordinal)) return "attack";
            if (name == "Mass" || name == "MarketValue" || name == "MaxHitPoints" || name == "WorkToMake") return "practical";
            return "other";
        }
        public static IEnumerable<Metric> Candidates(bool advanced = true)
        {
            bool ceActive = CombatExtendedIntegration.Active;
            if (ceActive)
                foreach (string id in CombatExtendedIntegration.Ids) yield return Resolve(id);
            foreach (string id in RecipeMetricIds)
                yield return Resolve(id);
            foreach (var stat in DefDatabase<StatDef>.AllDefsListForReading)
            {
                if (stat.alwaysHide || ceActive && CeHidden.Contains(stat.defName)) continue;
                if (!advanced && !(Preferred.Contains(stat.defName) || ceActive && CePreferred.Contains(stat.defName))) continue;
                // Resolve once, preserving fresh translations for the current language.
                yield return Resolve("stat:" + stat.defName);
            }
        }
        private static StatDef NamedStat(string name) { return DefDatabase<StatDef>.GetNamedSilentFail(name); }
        private static float StatValue(StatRequest request, string name, float fallback = 1f)
        {
            var stat = NamedStat(name);
            return stat == null ? fallback : stat.Worker.GetValue(request);
        }
        public static Measurement Measure(RecipeDef recipe, Metric metric, ThingDef selectedMaterial)
        {
            return MeasureAtQuality(recipe, metric, selectedMaterial, QualityCategory.Normal);
        }
        public static Measurement MeasureAtQuality(RecipeDef recipe, Metric metric, ThingDef selectedMaterial, QualityCategory quality, string ammo = "")
        {
            if (metric == null || metric.Id == "original" || metric.Id == "name") return Measurement.Missing();
            var product = recipe.ProducedThingDef;
            bool hasQuality = product?.HasComp<CompQuality>() == true;
            if (!hasQuality || !Enum.IsDefined(typeof(QualityCategory), quality)) quality = QualityCategory.Normal;
            string qualityLabel = hasQuality ? quality.GetLabel().CapitalizeFirst() : "MSS.NotApplicable".Translate().ToString();
            if (product == null && metric.Id != "recipe.work") return Measurement.Missing("MSS.MultiProduct".Translate());
            if (metric.Stat == null && product != null)
            {
                // Bayonet/melee verbs on ranged weapons remain eligible. StatDef
                // workers keep their own applicability checks, including mod parts.
                if (metric.Id.StartsWith("weapon.melee.", StringComparison.Ordinal))
                { if (!product.IsWeapon) return Measurement.Missing(); }
                else if (metric.Id.StartsWith("weapon.", StringComparison.Ordinal) || metric.Id.StartsWith("ce.", StringComparison.Ordinal))
                { if (!product.IsRangedWeapon) return Measurement.Missing(); }
            }
            ThingDef stuff = null;
            if (product != null && product.MadeFromStuff)
            {
                if (selectedMaterial != null)
                {
                    if (!GenStuff.AllowedStuffsFor(product).Contains(selectedMaterial) || !RecipeAllowsStuff(recipe, selectedMaterial))
                        return Measurement.Missing("MSS.IncompatibleMaterial".Translate());
                    stuff = selectedMaterial;
                }
                else
                {
                    stuff = GenStuff.DefaultStuffFor(product);
                    if (stuff == null || !RecipeAllowsStuff(recipe, stuff))
                        stuff = GenStuff.AllowedStuffsFor(product).Where(s => RecipeAllowsStuff(recipe, s))
                            .OrderBy(s => s.defName, StringComparer.Ordinal).FirstOrDefault();
                    if (stuff == null) return Measurement.Missing("MSS.NoMaterial".Translate());
                }
            }
            try
            {
                float value;
                string display;
                if (metric.Id == "recipe.work")
                {
                    value = recipe.workAmount >= 0 ? recipe.workAmount : product == null ? recipe.WorkAmountTotal(null) :
                        NamedStat("WorkToMake").Worker.GetValue(StatRequest.For(product, stuff, quality));
                    display = value.ToStringWorkAmount();
                }
                else
                {
                    if (product == null) return Measurement.Missing("MSS.MultiProduct".Translate());
                    var request = StatRequest.For(product, stuff, quality);
                    if (CombatExtendedIntegration.TryMeasure(product, request, metric, out var ceReading, ammo))
                    {
                        ceReading.Detail += "\n" + "MSS.ReferenceDetail".Translate(stuff == null ? "MSS.NotApplicable".Translate().ToString() : stuff.LabelCap.ToString(), qualityLabel);
                        return ceReading;
                    }
                    if (metric.Stat != null)
                    {
                        if (!metric.Stat.Worker.ShouldShowFor(request)) return Measurement.Missing();
                        value = metric.Stat.Worker.GetValue(request);
                        display = metric.Stat.ValueToString(value);
                        if (CombatExtendedIntegration.Active && (metric.Stat.defName == "ArmorRating_Sharp" || metric.Stat.defName == "ArmorRating_Blunt"))
                            display = value.ToString("0.##", CultureInfo.CurrentCulture) + (metric.Stat.defName == "ArmorRating_Sharp" ? " mm RHA" : " MPa");
                    }
                    else if (metric.Id.StartsWith("weapon.melee.", StringComparison.Ordinal))
                    {
                        if (!product.IsWeapon) return Measurement.Missing();
                        var attacks = VerbUtility.GetAllVerbProperties(product.Verbs, product.tools)
                            .Where(a => a.verbProps.IsMeleeAttack && a.verbProps.meleeDamageDef != null).ToList();
                        if (attacks.Count == 0) return Measurement.Missing();
                        string damageCategory = metric.Id == "weapon.melee.blunt" ? "Blunt" :
                            metric.Id == "weapon.melee.sharp" ? "Sharp" : null;
                        // Keep the normal-quality definition/verb calculation (and
                        // any mod patches), scaling tool hits by the quality stat.
                        float qualityFactor = 1f;
                        if (quality != QualityCategory.Normal)
                        {
                            float normal = StatValue(StatRequest.For(product, stuff, QualityCategory.Normal), "MeleeWeapon_DamageMultiplier");
                            float chosen = StatValue(request, "MeleeWeapon_DamageMultiplier");
                            if (normal > 0 && SortingCore.Finite(normal) && SortingCore.Finite(chosen)) qualityFactor = chosen / normal;
                        }
                        var highest = ReferenceCore.HighestDamage(attacks.Select(a => new AttackReading
                        {
                            Category = a.verbProps.meleeDamageDef.armorCategory?.defName,
                            Damage = a.verbProps.AdjustedMeleeDamageAmount(a.tool, null, product, stuff, null) * (a.tool == null ? 1f : qualityFactor)
                        }), damageCategory);
                        if (!highest.HasValue) return Measurement.Missing();
                        value = (float)highest.Value;
                        display = value.ToString("0.##", CultureInfo.CurrentCulture);
                    }
                    else
                    {
                        if (!product.IsRangedWeapon) return Measurement.Missing();
                        // First damaging ranged verb is a reproducible reference;
                        // special beam/ammunition systems without this data are unavailable.
                        var verb = product.Verbs?.FirstOrDefault(v => !v.IsMeleeAttack &&
                            (metric.Id == "weapon.range" || metric.Id == "weapon.burst" || v.defaultProjectile?.projectile != null));
                        if (verb == null) return Measurement.Missing();
                        var projectile = verb.defaultProjectile?.projectile;
                        switch (metric.Id)
                        {
                            case "weapon.damage":
                                value = projectile.GetDamageAmount(StatValue(request, "RangedWeapon_DamageMultiplier"), null);
                                break;
                            case "weapon.range":
                                value = verb.rangeStat == null ? verb.range : verb.rangeStat.Worker.GetValue(request);
                                break;
                            case "weapon.burst": value = verb.burstShotCount; break;
                            case "weapon.penetration":
                                value = projectile.GetArmorPenetration() * StatValue(request, "RangedWeapon_ArmorPenetrationMultiplier");
                                break;
                            case "weapon.dps":
                                float duration = verb.warmupTime + StatValue(request, "RangedWeapon_Cooldown", verb.defaultCooldownTime) +
                                    Math.Max(0, verb.burstShotCount - 1) * verb.ticksBetweenBurstShots / 60f;
                                if (duration <= 0) return Measurement.Missing();
                                value = projectile.GetDamageAmount(StatValue(request, "RangedWeapon_DamageMultiplier"), null) * verb.burstShotCount / duration;
                                break;
                            default: return Measurement.Missing();
                        }
                        display = metric.Id == "weapon.penetration" ? value.ToStringPercent() : value.ToString("0.##", CultureInfo.CurrentCulture);
                    }
                }
                if (!SortingCore.Finite(value)) return Measurement.Missing();
                string material = stuff == null ? "MSS.NotApplicable".Translate().ToString() : stuff.LabelCap.ToString();
                return new Measurement
                {
                    Value = value, Display = display,
                    Detail = metric.Name + ": " + display + "\n" + "MSS.NumericReference".Translate((value * LimitScale(metric)).ToString("G9", CultureInfo.CurrentCulture), LimitUnit(metric)) + "\n" + "MSS.ReferenceDetail".Translate(material, qualityLabel).ToString() +
                        (metric.Id == "weapon.dps" || metric.Id == "weapon.damage" || metric.Id == "weapon.penetration" ? "\n" + "MSS.WeaponReference".Translate().ToString() : "")
                        + (metric.Id.StartsWith("weapon.melee.", StringComparison.Ordinal) ? "\n" + "MSS.MeleeReference".Translate().ToString() : "")
                };
            }
            catch (Exception ex)
            {
                return Measurement.Missing("MSS.StatUnavailable".Translate() + " (" + ex.GetType().Name + ")");
            }
        }
        public static bool RecipeAllowsStuff(RecipeDef recipe, ThingDef stuff)
        {
            // A default ingredient filter is a user-editable bill default, not a
            // hard restriction. The recipe's fixed filter is the restriction.
            return recipe.fixedIngredientFilter == null || recipe.fixedIngredientFilter.AllowedDefCount == 0 || recipe.fixedIngredientFilter.Allows(stuff);
        }
    }
}
