using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MintStatSort
{
    // Optional integration: never references or distributes a CE assembly.
    internal static class CombatExtendedIntegration
    {
        // The comparison UI runs after mod assemblies are loaded. Cache missing
        // members too, so an optional or older CE installation is equally cheap.
        private static readonly Dictionary<string, Type> types = new Dictionary<string, Type>(StringComparer.Ordinal);
        private static readonly Dictionary<Type, Dictionary<string, FieldInfo>> fields = new Dictionary<Type, Dictionary<string, FieldInfo>>();
        private static Type TypeByName(string name)
        {
            if (!types.TryGetValue(name, out var type))
            {
                // CE names are fully qualified: a direct lookup avoids Harmony's
                // fallback enumeration of every type when CE is not installed.
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    type = assembly.GetType(name, false);
                    if (type != null) break;
                }
                types[name] = type;
            }
            return type;
        }
        internal static List<ThingDef> Ammunition(ThingDef product)
        {
            var ammoType = TypeByName("CombatExtended.CompProperties_AmmoUser");
            var props = product?.comps?.FirstOrDefault(c => ammoType != null && ammoType.IsInstanceOfType(c));
            var links = Field(Field(props, "ammoSet"), "ammoTypes") as IEnumerable;
            return links == null ? new List<ThingDef>() : links.Cast<object>().Select(l => Field(l, "ammo") as ThingDef)
                .Where(a => a != null).Distinct().OrderBy(a => a.LabelCap.ToString()).ToList();
        }
        internal static bool Active => TypeByName("CombatExtended.ProjectilePropertiesCE") != null;
        internal static readonly string[] Ids = { "ce.penetration.sharp", "ce.penetration.blunt", "ce.magazine", "ce.reload", "ce.dps.sustained" };
        internal static Metric Resolve(string id)
        {
            int index = Array.IndexOf(Ids, id);
            if (index < 0 || !Active) return null;
            string[] keys = { "MSS.CE.Sharp", "MSS.CE.Blunt", "MSS.CE.Magazine", "MSS.CE.Reload", "MSS.CE.Sustained" };
            return new Metric { Id = id, Name = keys[index].Translate(), GroupKey = "attack",
                Group = "MSS.Group.attack".Translate(), Preferred = true, HigherIsBetter = index != 3 };
        }
        internal static object Field(object instance, string name)
        {
            if (instance == null) return null;
            var type = instance.GetType();
            if (!fields.TryGetValue(type, out var members))
                fields[type] = members = new Dictionary<string, FieldInfo>(StringComparer.Ordinal);
            if (!members.TryGetValue(name, out var field))
                members[name] = field = AccessTools.Field(type, name);
            return field?.GetValue(instance);
        }
        private static float Number(object instance, string name, float fallback = 0f)
        {
            object value = Field(instance, name);
            return value == null ? fallback : Convert.ToSingle(value, CultureInfo.InvariantCulture);
        }
        private static float Stat(StatRequest request, string name, float fallback)
        {
            var stat = DefDatabase<StatDef>.GetNamedSilentFail(name);
            return stat == null ? fallback : stat.Worker.GetValue(request);
        }
        internal static bool TryMeasure(ThingDef product, StatRequest request, Metric metric, out Measurement result, string selectedAmmo = "")
        {
            result = null;
            if (metric?.Id == null || !(metric.Id.StartsWith("ce.", StringComparison.Ordinal)
                || metric.Id.StartsWith("weapon.", StringComparison.Ordinal) && !metric.Id.StartsWith("weapon.melee.", StringComparison.Ordinal))) return false;
            if (!Active) return false;
            if (!product.IsRangedWeapon) { result = Measurement.Missing(); return true; }
            var ceType = TypeByName("CombatExtended.ProjectilePropertiesCE");
            var ammoType = TypeByName("CombatExtended.CompProperties_AmmoUser");
            var props = product.comps?.FirstOrDefault(c => ammoType != null && ammoType.IsInstanceOfType(c));
            var links = (Field(Field(props, "ammoSet"), "ammoTypes") as IEnumerable)?.Cast<object>().ToList();
            var verb = product.Verbs?.FirstOrDefault(v => !v.IsMeleeAttack &&
                (ceType.IsInstanceOfType(v.defaultProjectile?.projectile) || v.verbClass?.FullName?.StartsWith("CombatExtended.") == true));
            if (verb == null) { if (!metric.Id.StartsWith("ce.")) return false; result = Measurement.Missing(); return true; }
            var projectileDef = verb.defaultProjectile;
            // Match the verb's reference projectile; otherwise use CE's first ammo link.
            var selectedLink = links?.FirstOrDefault(l => (Field(l, "ammo") as Def)?.defName == selectedAmmo);
            var link = selectedLink ?? links?.FirstOrDefault(l => Field(l, "projectile") == projectileDef) ?? links?.FirstOrDefault();
            if (link != null) projectileDef = Field(link, "projectile") as ThingDef;
            var projectile = projectileDef?.projectile;
            string ammo = (Field(link, "ammo") as Def)?.LabelCap.ToString() ?? projectileDef?.LabelCap.ToString() ?? "MSS.NoData".Translate().ToString();
            string detail = "MSS.CE.Reference".Translate(ammo).ToString();
            if (!string.IsNullOrEmpty(selectedAmmo) && selectedLink == null) detail += "\n" + "MSS.CE.AmmoFallback".Translate();
            float value;
            string unit = "";
            bool useStats = Equals(Field(verb, "useEquipmentStatValues"), true);
            float burst = useStats ? Stat(request, "BurstShotCount", verb.burstShotCount) : verb.burstShotCount;
            if (burst <= 0) burst = verb.burstShotCount;
            switch (metric.Id)
            {
                case "ce.magazine":
                    if (props == null || Number(props, "magazineSize") <= 0) { result = Measurement.Missing(); return true; }
                    value = Stat(request, "MagazineCapacity", Number(props, "magazineSize")); break;
                case "ce.reload":
                    if (props == null || Number(props, "magazineSize") <= 0) { result = Measurement.Missing(); return true; }
                    value = (Equals(Field(verb, "useEquipmentStatValues"), true) ? Stat(request, "ReloadTime", Number(props, "reloadTime")) : Number(props, "reloadTime"))
                        * Stat(request, "CE_RangedWeapon_ReloadFactor", 1f);
                    unit = " s"; detail += "\n" + "MSS.CE.ReloadReference".Translate(); break;
                case "weapon.range":
                    value = verb.rangeStat == null ? verb.range : verb.rangeStat.Worker.GetValue(request); break;
                case "weapon.burst": value = burst; break;
                default:
                    if (projectile == null || !ceType.IsInstanceOfType(projectile)) { result = Measurement.Missing(); return true; }
                    float damage = projectile.GetDamageAmount(Stat(request, "RangedWeapon_DamageMultiplier", 1f), null);
                    if (metric.Id == "weapon.damage") { value = damage; detail += "\n" + "MSS.CE.DamageReference".Translate(Number(projectile, "pelletCount", 1f)); }
                    else if (metric.Id == "weapon.dps" || metric.Id == "ce.dps.sustained")
                    {
                        float warmup = Math.Max(0, verb.warmupTime * (Stat(request, "RangedWeapon_WarmupMultiplier", 1f)
                            + Number(projectile, "warmupMultiplier", 1f) - 1f) + Number(projectile, "warmupOffset"));
                        float seconds = warmup + Stat(request, "RangedWeapon_Cooldown", verb.defaultCooldownTime)
                            + Math.Max(0, burst - 1) * (useStats ? Stat(request, "TicksBetweenBurstShots", verb.ticksBetweenBurstShots) : verb.ticksBetweenBurstShots) / 60f;
                        if (seconds <= 0 || burst <= 0) { result = Measurement.Missing(); return true; }
                        value = damage * Number(projectile, "pelletCount", 1f) * burst / seconds;
                        if (metric.Id == "ce.dps.sustained")
                        {
                            float capacity = Stat(request, "MagazineCapacity", Number(props, "magazineSize"));
                            float consumption = Stat(request, "AmmoConsumedPerShotCount", Number(verb, "ammoConsumedPerShotCount", 1f));
                            if (props == null || capacity < 1 || consumption <= 0 || burst > int.MaxValue || capacity > int.MaxValue)
                            { result = Measurement.Missing(); return true; }
                            float reload = (useStats ? Stat(request, "ReloadTime", Number(props, "reloadTime")) : Number(props, "reloadTime")) * Stat(request, "CE_RangedWeapon_ReloadFactor", 1f);
                            if (Equals(Field(props, "reloadOneAtATime"), true)) reload *= capacity;
                            var sustained = SustainedDamage.Calculate(damage, Number(projectile, "pelletCount", 1f),
                                (int)Math.Floor(capacity / consumption), (int)burst, warmup,
                                Stat(request, "RangedWeapon_Cooldown", verb.defaultCooldownTime),
                                (useStats ? Stat(request, "TicksBetweenBurstShots", verb.ticksBetweenBurstShots) : verb.ticksBetweenBurstShots) / 60f, reload);
                            if (!sustained.HasValue) { result = Measurement.Missing(); return true; }
                            value = (float)sustained.Value;
                            detail += "\n" + "MSS.CE.SustainedReference".Translate();
                        }
                        else detail += "\n" + "MSS.CE.DpsReference".Translate();
                    }
                    else
                    {
                        bool sharp = metric.Id == "ce.penetration.sharp" || metric.Id == "weapon.penetration" && projectile.damageDef?.armorCategory == DamageArmorCategoryDefOf.Sharp;
                        if (metric.Id == "weapon.penetration" && projectile.damageDef?.armorCategory != DamageArmorCategoryDefOf.Sharp && projectile.damageDef?.armorCategory?.defName != "Blunt")
                        { result = Measurement.Missing(); return true; }
                        value = Number(projectile, sharp ? "armorPenetrationSharp" : "armorPenetrationBlunt") * Stat(request, "RangedWeapon_ArmorPenetrationMultiplier", 1f);
                        unit = sharp ? " mm RHA" : " MPa";
                    }
                    break;
            }
            if (!SortingCore.Finite(value)) { result = Measurement.Missing(); return true; }
            string display = value.ToString("0.##", CultureInfo.CurrentCulture) + unit;
            result = new Measurement { Value = value, Display = display, Detail = metric.Name + ": " + display + "\n" + detail };
            return true;
        }
    }
}
