using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using HarmonyLib;
using MintStatSort;
using RimWorld;
using UnityEngine;
using Verse;

namespace MintStatSortSmoke
{
    public sealed class DerivedQuality : CompQuality { }
    // No reference to a particular MintStatSort build: the same probe can load
    // the preserved 0.6.1 binary or the candidate in separate game processes.
    public sealed class SmokeGameComponent : GameComponent
    {
        private static readonly string Root = Environment.GetEnvironmentVariable("MSS_PROBE_ROOT") ?? System.IO.Path.Combine(GenFilePaths.SaveDataFolderPath, "MintStatSortProbe");
        private const BindingFlags Fields = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private int updates, failures, quitAfter = -1;
        private IEnumerator test;
        private string phase;
        private Assembly product;
        private Type windowType, rowType, metricType, metricHelper;
        private object window;
        private bool incremental;
        private int pumpCalls;
        private double maximumPumpMs;
        private readonly bool graphicsProbe = Environment.CommandLine.IndexOf("-nographics", StringComparison.OrdinalIgnoreCase) < 0;
        public SmokeGameComponent(Game game) { Application.runInBackground = true; }
        private string Results { get { return Path.Combine(Root, "results-" + (graphicsProbe ? "graphics-" : "") + phase + ".txt"); } }
        public override void GameComponentUpdate()
        {
            if (Find.CurrentMap != null && Current.ProgramState == ProgramState.Playing) Find.TickManager.Pause();
            if (quitAfter >= 0) { if (--quitAfter <= 0) Application.Quit(); return; }
            if (test == null)
            {
                if (Find.CurrentMap == null || Current.ProgramState != ProgramState.Playing || ++updates < 90 || LongEventHandler.AnyEventNowOrWaiting) return;
                phase = File.ReadAllText(Path.Combine(Root, "phase.txt")).Trim();
                File.WriteAllText(Results, "MintStatSort runtime diagnostics " + phase + " " + DateTime.UtcNow.ToString("o") + Environment.NewLine);
                test = graphicsProbe ? CaptureInterface() : Run();
            }
            try
            {
                if (!test.MoveNext()) Finish();
            }
            catch (Exception exception)
            {
                failures++;
                Write("FAIL unexpected exception: " + exception);
                Finish();
            }
        }
        private void Finish()
        {
            Write("DONE " + (failures == 0 ? "PASS" : "FAIL") + " failures=" + failures + " pumpCalls=" + pumpCalls + " maximumPumpMs=" + Number(maximumPumpMs));
            quitAfter = 5;
        }
        private void Write(string text) { File.AppendAllText(Results, text + Environment.NewLine); Log.Message("[MintStatSortSmoke] " + text); }
        private void Assert(bool condition, string name) { if (!condition) failures++; Write((condition ? "PASS " : "FAIL ") + name); }
        private static string Number(double value) { return value.ToString("F3", CultureInfo.InvariantCulture); }
        private static FieldInfo Field(Type type, string name)
        {
            while (type != null) { var field = type.GetField(name, Fields | BindingFlags.DeclaredOnly); if (field != null) return field; type = type.BaseType; }
            return null;
        }
        private static object Read(object instance, string name) { return Field(instance.GetType(), name).GetValue(instance); }
        private static void Set(object instance, string name, object value) { Field(instance.GetType(), name).SetValue(instance, value); }
        private static MethodInfo Method(Type type, string name)
        {
            while (type != null) { var method = type.GetMethods(Fields | BindingFlags.DeclaredOnly).FirstOrDefault(m => m.Name == name); if (method != null) return method; type = type.BaseType; }
            return null;
        }
        private static object Call(object instance, string name, params object[] arguments)
        {
            var method=Method(instance.GetType(),name);var parameters=method.GetParameters();
            if(arguments.Length<parameters.Length) arguments=arguments.Concat(parameters.Skip(arguments.Length).Select(p=>p.DefaultValue)).ToArray();
            return method.Invoke(instance,arguments);
        }
        private static bool Pending(object job)
        {
            if (job == null) return false;
            var property = job.GetType().GetProperty("Pending", Fields);
            if (property != null) return (bool)property.GetValue(job, null);
            var field = Field(job.GetType(), "Pending");
            return field != null && (bool)field.GetValue(job);
        }
        private bool IsPending()
        {
            var discovery = Field(windowType, "discovery"); var preparation = Field(windowType, "preparation"); var inventory = Field(windowType, "inventory");
            return discovery != null && Pending(discovery.GetValue(window)) || preparation != null && Pending(preparation.GetValue(window)) || inventory != null && Pending(inventory.GetValue(window));
        }
        private int CachedCount()
        {
            var readings = (IDictionary)Read(window, "readings");
            int count = 0; foreach (DictionaryEntry entry in readings) count += ((IDictionary)entry.Value).Count;
            return count;
        }
        private double Pump()
        {
            Stopwatch watch = Stopwatch.StartNew();
            Call(window, "WindowUpdate");
            watch.Stop(); pumpCalls++; maximumPumpMs = Math.Max(maximumPumpMs, watch.Elapsed.TotalMilliseconds);
            return watch.Elapsed.TotalMilliseconds;
        }
        private IEnumerable Drain(string name)
        {
            Stopwatch wall = Stopwatch.StartNew(); int steps = 0; double maximum = 0;
            while (IsPending())
            {
                if (++steps > 10000) throw new InvalidOperationException("Job never finished: " + name);
                maximum = Math.Max(maximum, Pump());
                yield return null;
            }
            // A completed discovery step can leave display dirty without a job.
            Call(window, "RefreshDisplay");
            while (IsPending())
            {
                if (++steps > 10000) throw new InvalidOperationException("Display job never finished: " + name);
                maximum = Math.Max(maximum, Pump());
                yield return null;
            }
            Write("MEASURE " + name + " steps=" + steps + " maxStepMs=" + Number(maximum) + " wallMs=" + Number(wall.Elapsed.TotalMilliseconds) + " cached=" + CachedCount());
        }
        private object Resolve(string id) { return Method(metricHelper, "Resolve").Invoke(null, new object[] { id }); }
        private object DirectMeasure(RecipeDef recipe, object metric, ThingDef material)
        { return Method(metricHelper, "Measure").Invoke(null, new object[] { recipe, metric, material }); }
        private static double? Value(object reading)
        {
            object value = Read(reading, "Value");
            return value == null ? (double?)null : Convert.ToDouble(value, CultureInfo.InvariantCulture);
        }
        private static bool Equal(double? first, double? second)
        { return first.HasValue == second.HasValue && (!first.HasValue || Math.Abs(first.Value - second.Value) <= 0.00001 * Math.Max(1, Math.Abs(first.Value))); }
        private IList CreateRows(List<RecipeDef> recipes)
        {
            IList rows = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(rowType));
            foreach (RecipeDef recipe in recipes)
            {
                object row = Activator.CreateInstance(rowType, true);
                Set(row, "Recipe", recipe); Set(row, "Label", recipe.LabelCap.ToString()); rows.Add(row);
            }
            // Real recipe definitions with repeated UI rows exercise long lists,
            // while retaining the same definition identity and stat semantics.
            int originals = rows.Count;
            while (rows.Count < 600 && originals > 0)
            {
                object previous = rows[rows.Count % originals];
                object row = Activator.CreateInstance(rowType, true);
                Set(row, "Recipe", Read(previous, "Recipe")); Set(row, "Label", Read(previous, "Label")); rows.Add(row);
            }
            return rows;
        }
        private object NewWindow(IList rows)
        {
            var constructor = windowType.GetConstructors(Fields).Single(c => c.GetParameters().Length == 5);
            Write("BEGIN constructor " + DateTime.UtcNow.ToString("o"));
            Stopwatch watch = Stopwatch.StartNew();
            object created = constructor.Invoke(new object[] { rows, Find.CurrentMap, new Func<bool>(() => true), "Runtime diagnostics", false });
            watch.Stop(); Write("MEASURE constructor rows=" + rows.Count + " ms=" + Number(watch.Elapsed.TotalMilliseconds));
            return created;
        }
        private void SetCriterion(string id)
        {
            object draft = Read(window, "draft");
            IList criteria = (IList)Read(draft, "Criteria"); criteria.Clear();
            object criterion = Activator.CreateInstance(product.GetType("MintStatSort.SortCriterion"));
            Set(criterion, "Id", id); Set(criterion, "HigherIsBetter", true); criteria.Add(criterion);
            Set(draft, "Metric", "original"); Set(draft, "OrderMode", "criteria"); Set(window, "comparisonMode", "criteria"); Set(draft, "Descending", true); Set(window, "dirty", true);
            Call(window, "RefreshDisplay");
        }
        private void CheckCategories(List<RecipeDef> recipes)
        {
            IList categories = (IList)Read(window, "categories");
            MethodInfo matches = Method(product.GetType("MintStatSort.RecipeSession"), "Matches");
            var ids = categories.Cast<object>().Select(c => (string)Read(c, "Id")).ToList();
            Assert(ids.Distinct().Count() == ids.Count && ids.Contains("all"), "category index contains unique IDs and the complete all category");
            var names = categories.Cast<object>().Select(c => (string)Read(c, "Name")).ToList();
            Assert(names.All(n => !String.IsNullOrEmpty(n)) && names.Distinct(StringComparer.CurrentCultureIgnoreCase).Count() == names.Count, "category label grouping has no duplicates or empty names");
            int merged = 0;
            foreach (object category in categories)
            {
                string id = (string)Read(category, "Id");
                var matching = recipes.Where(r => (bool)matches.Invoke(null, new object[] { r, id })).Distinct().ToList();
                Assert((int)Read(category, "Count") == matching.Count && object.ReferenceEquals(Read(category, "Icon"), matching.Select(r => r.ProducedThingDef).FirstOrDefault(p => p != null)), "category count and first icon match real recipe membership: " + id);
                if (id.StartsWith("defs:", StringComparison.Ordinal)) merged++;
            }
            Write("OBS categories=" + categories.Count + " mergedLabels=" + merged);
        }
        private void CheckNativeRows(List<RecipeDef> recipes)
        {
            ThingDef tableDef = DefDatabase<ThingDef>.AllDefsListForReading.Where(d => d.thingClass != null && typeof(Building_WorkTable).IsAssignableFrom(d.thingClass) && d.AllRecipes.Any(r => r.AvailableNow))
                .OrderByDescending(d => d.AllRecipes.Count).FirstOrDefault();
            if (tableDef == null) { Write("OBS native option factory fixture has no available workbench; helper check omitted."); return; }
            Map map = Find.CurrentMap;
            IntVec3 cell = GenRadial.RadialCellsAround(map.Center, 70, true).First(c => GenAdj.CellsOccupiedBy(c, Rot4.North, tableDef.Size).All(g => g.InBounds(map) && g.Standable(map) && g.GetThingList(map).Count == 0));
            var table = (Building_WorkTable)ThingMaker.MakeThing(tableDef, tableDef.MadeFromStuff ? GenStuff.DefaultStuffFor(tableDef) : null);
            GenSpawn.Spawn(table, cell, map, Rot4.North);
            try
            {
                Find.Selector.ClearSelection(); Find.Selector.Select(table, false, false);
                var tab = new ITab_Bills();
                MethodInfo factory = typeof(ITab_Bills).GetMethods(Fields).Single(m => m.Name.Contains("OptionsMaker"));
                var options = (List<FloatMenuOption>)factory.Invoke(tab, null);
                Type native = product.GetType("MintStatSort.NativeRecipeWindow", true);
                Stopwatch watch = Stopwatch.StartNew();
                IList nativeRows = (IList)Method(native, "CreateRows").Invoke(null, new object[] { table, options });
                watch.Stop();
                Write("MEASURE native-CreateRows table=" + tableDef.defName + " tableRecipes=" + tableDef.AllRecipes.Count + " options=" + options.Count + " ms=" + Number(watch.Elapsed.TotalMilliseconds));
                Assert(nativeRows.Count == options.Count && nativeRows.Cast<object>().All(r => options.Contains((FloatMenuOption)Read(r, "Option"))), "native mapping retains every genuine original menu option identity");
                Assert(nativeRows.Cast<object>().Any(r => Read(r, "Recipe") != null), "genuine ITab_Bills closures resolve at least one available recipe without invoking callbacks");
                watch.Restart();
                object nativeWindow = native.GetConstructors(Fields).Single().Invoke(new object[] { table, table.BillStack, options });
                watch.Stop();
                Write("MEASURE native-constructor options=" + options.Count + " ms=" + Number(watch.Elapsed.TotalMilliseconds));
                Assert((bool)Call(nativeWindow, "ContextValid"), "native comparator recognizes the actual selected spawned workbench");
                Assert(((IDictionary)Read(nativeWindow, "readings")).Count == 0, "native comparator construction also performs no stat measurements");
                Call(nativeWindow, "PreClose");
            }
            finally
            {
                Find.Selector.ClearSelection(); table.Destroy(DestroyMode.Vanish);
                Method(product.GetType("MintStatSort.RecipeSession"), "SetRecipes").Invoke(null, new object[] { recipes });
            }
        }
        private void CheckWorkshopNotes()
        {
            Type integration = product.GetType("MintStatSort.WorkshopNotesIntegration", true);
            string original = "[Auto-generated text]: Update on fixture.";
            string fixture = Path.Combine(Root, "notes-fixture");
            Directory.CreateDirectory(Path.Combine(fixture, "About"));
            Directory.CreateDirectory(Path.Combine(fixture, "Publicacion-Workshop"));
            string notes = "MintStatSort 0.6.10\nCalidad y notas automáticas.\n- Cambios de esta versión.";
            File.WriteAllText(Path.Combine(fixture, "About", "About.xml"), "<ModMetaData><name>Note fixture</name><author>Test</author><packageId>mintstatsort.recipes</packageId><modVersion>0.6.10</modVersion><supportedVersions><li>1.6</li></supportedVersions></ModMetaData>");
            string notesPath = Path.Combine(fixture, "Publicacion-Workshop", "notas-actualizacion-steam.txt");
            File.WriteAllText(notesPath, notes);
            var metadata = new ModMetaData(fixture, false);
            // A normal hook constructor can contact Steam. An uninitialized
            // fixture hook with no Workshop identity tests the selector only.
            var hook = (Verse.Steam.WorkshopItemHook)FormatterServices.GetUninitializedObject(typeof(Verse.Steam.WorkshopItemHook));
            Set(hook, "owner", metadata);
            MethodInfo select = Method(integration, "SelectNotes");
            Assert((string)select.Invoke(null, new object[] { original, hook }) == notes, "Workshop selector reads notes from the uploaded mod's own directory");
            string manifestPath=Path.Combine(fixture,"Publicacion-Workshop","publicacion-version.txt");
            string descriptionPath=Path.Combine(fixture,"Publicacion-Workshop","descripcion-workshop-es.txt");
            string preparedDescription="[h1]Compara recetas[/h1]\nDescripción preparada de la prueba.";
            File.WriteAllText(manifestPath,"MintStatSort 0.6.10\n");File.WriteAllText(descriptionPath,preparedDescription);
            Assert((string)Method(integration,"SelectDescription").Invoke(null,new object[]{"native",hook})==preparedDescription,"Workshop metadata selector uses the uploaded mod's prepared description");
            Assert((bool)Method(integration,"ShouldUpdateDescription").Invoke(null,new object[]{false,hook}),"Matching MintStatSort update enables the original description submission");
            File.WriteAllText(manifestPath,"MintStatSort 0.6.8\n");
            Assert(!(bool)Method(integration,"ShouldUpdateDescription").Invoke(null,new object[]{false,hook}) && (string)Method(integration,"SelectDescription").Invoke(null,new object[]{"native",hook})=="native","Stale publication manifest preserves native metadata behavior");
            File.WriteAllText(manifestPath,"MintStatSort 0.6.10\n");File.Delete(descriptionPath);
            Assert(!(bool)Method(integration,"ShouldUpdateDescription").Invoke(null,new object[]{false,hook}),"Missing description cannot force a Workshop metadata update");
            Assert((bool)Method(integration,"ShouldUpdateDescription").Invoke(null,new object[]{true,null}) && !(bool)Method(integration,"ShouldUpdateDescription").Invoke(null,new object[]{false,null}),"Unknown owners preserve original create-versus-update semantics");
            var metadataMethod=AccessTools.Method(typeof(Verse.Steam.Workshop),"SetWorkshopItemDataFrom");
            Assert(Harmony.GetPatchInfo(metadataMethod).Transpilers.Any(p=>p.owner=="mintstatsort.workshopnotes"),"Workshop metadata patch is connected in the real game");
            var originalMetadata=PatchProcessor.GetOriginalInstructions(metadataMethod).ToList();
            var changedMetadata=((IEnumerable<CodeInstruction>)Method(integration,"InjectMetadata").Invoke(null,new object[]{originalMetadata})).ToList();
            Func<List<CodeInstruction>,List<string>> steamCalls=code=>code.Where(c=>c.operand is MethodInfo && ((MethodInfo)c.operand).DeclaringType.FullName=="Steamworks.SteamUGC").Select(c=>((MethodInfo)c.operand).Name).ToList();
            Assert(changedMetadata.Count==originalMetadata.Count+4 && steamCalls(changedMetadata).SequenceEqual(steamCalls(originalMetadata)),"Metadata transpiler preserves all native Steam setters without starting an upload");
            Assert(changedMetadata.Count(c=>c.operand is MethodInfo && ((MethodInfo)c.operand).Name=="SelectDescription")==1 && changedMetadata.Count(c=>c.operand is MethodInfo && ((MethodInfo)c.operand).Name=="ShouldUpdateDescription")==1,"Metadata transpiler inserts only owner-checked description selection and update gating");

            File.WriteAllText(notesPath, notes.Replace("0.6.10", "0.6.2"));
            Assert((string)select.Invoke(null, new object[] { original, hook }) == original, "Workshop selector preserves native upload if notes belong to another version");
            File.Delete(notesPath);
            Assert((string)select.Invoke(null, new object[] { original, hook }) == original, "Missing Workshop note cannot prevent native publication");
            Assert((string)select.Invoke(null, new object[] { original, null }) == original, "Unknown Workshop owner retains the original note");
            foreach (string name in new[] { "Upload", "OnItemCreated" })
            {
                MethodInfo method = AccessTools.Method(typeof(Verse.Steam.Workshop), name);
                Assert(Harmony.GetPatchInfo(method).Transpilers.Any(p => p.owner == "mintstatsort.workshopnotes"), "Workshop note patch installed successfully on " + name);
                var originalCode = PatchProcessor.GetOriginalInstructions(method).ToList();
                var changed = ((IEnumerable<CodeInstruction>)Method(integration, "InjectNotes").Invoke(null, new object[] { originalCode })).ToList();
                var calls = changed.Where(i => i.opcode == OpCodes.Call && i.operand is MethodInfo && ((MethodInfo)i.operand).Name == "SubmitItemUpdate").ToList();
                int index = changed.IndexOf(calls.Single());
                Assert(calls.Count == 1 && changed.Count == originalCode.Count + 2 && (MethodInfo)changed[index - 1].operand == select && changed[index - 2].opcode == OpCodes.Ldsfld,
                    "Transpiler preserves the single native Steam submission and inserts only note selection: " + name);
            }
        }
        private IEnumerable CheckQuality(List<RecipeDef> recipes, IList rows, object options)
        {
            Call(window, "PreClose"); Call(options, "Reset");
            Assert((QualityCategory)Read(options, "Quality") == QualityCategory.Normal, "Existing/default settings compare Normal quality");
            Set(options, "Quality", QualityCategory.Legendary);
            Assert((QualityCategory)Read(Call(options, "Copy"), "Quality") == QualityCategory.Legendary, "Draft copies preserve selected reference quality");
            Set(options, "Quality", (QualityCategory)99); Call(options, "Migrate");
            Assert((QualityCategory)Read(options, "Quality") == QualityCategory.Normal, "Invalid saved quality migrates to Normal");
            Call(options, "Reset");
            window = NewWindow(rows);
            Call(window, "Category", "kind:armor"); SetCriterion("stat:ArmorRating_Sharp");
            foreach (object step in Drain("quality-normal")) yield return step;
            var stat = DefDatabase<StatDef>.GetNamed("ArmorRating_Sharp");
            var recipe = recipes.First(r => r.ProducedThingDef.IsApparel && r.ProducedThingDef.HasComp(typeof(CompQuality)) && !r.ProducedThingDef.MadeFromStuff && stat.Worker.GetValue(StatRequest.For(r.ProducedThingDef, null, QualityCategory.Normal)) > 0);
            object metric = Resolve("stat:ArmorRating_Sharp");
            double? normal = Value(Call(window, "Measure", recipe, metric));
            bool changed = false;
            foreach (QualityCategory quality in Enum.GetValues(typeof(QualityCategory)))
            {
                Call(window, "SelectQuality", quality);
                foreach (object step in Drain("quality-" + quality)) yield return step;
                double? actual = Value(Call(window, "Measure", recipe, metric));
                double? expected = stat.Worker.GetValue(StatRequest.For(recipe.ProducedThingDef, null, quality));
                Assert(Equal(actual, expected), "Reference armor value matches game stat worker at " + quality);
                changed |= !Equal(normal, actual);
            }
            Assert(changed, "Quality selector materially changes a quality-dependent armor statistic");
            Call(window, "SelectQuality", QualityCategory.Awful);
            Call(window, "SelectQuality", QualityCategory.Legendary);
            foreach (object step in Drain("quality-cancellation")) yield return step;
            Assert(Equal(Value(Call(window, "Measure", recipe, metric)), stat.Worker.GetValue(StatRequest.For(recipe.ProducedThingDef, null, QualityCategory.Legendary))), "Rapid quality changes cannot publish readings from the abandoned quality");
            Assert((QualityCategory)Read(options, "Quality") == QualityCategory.Normal, "Preview quality changes do not alter applied settings");
            RecipeDef withoutQuality = recipes.First(r => !r.ProducedThingDef.HasComp(typeof(CompQuality)));
            object mass = Resolve("stat:Mass");
            var measure = Method(metricHelper, "MeasureAtQuality");
            Assert(Equal(Value(measure.Invoke(null, new object[] { withoutQuality, mass, null, QualityCategory.Awful, "" })), Value(measure.Invoke(null, new object[] { withoutQuality, mass, null, QualityCategory.Legendary, "" }))), "Products without quality ignore the selector");
            var derivedDef = new ThingDef { defName = "QualityTest", label = "Quality test", comps = new List<CompProperties> { new CompProperties { compClass = typeof(DerivedQuality) } } };
            var derivedRecipe = new RecipeDef { defName = "QualityTestRecipe", workAmount = 10, products = new List<ThingDefCountClass> { new ThingDefCountClass(derivedDef, 1) } };
            object derived = measure.Invoke(null, new object[] { derivedRecipe, Resolve("recipe.work"), null, QualityCategory.Legendary, "" });
            Assert(((string)Read(derived, "Detail")).IndexOf(QualityCategory.Legendary.GetLabel(), StringComparison.CurrentCultureIgnoreCase) >= 0, "Quality components derived by another mod retain selected reference quality");
            RecipeDef melee = recipes.First(r => r.ProducedThingDef.IsMeleeWeapon && r.ProducedThingDef.HasComp(typeof(CompQuality)) && r.ProducedThingDef.tools != null);
            ThingDef stuff = melee.ProducedThingDef.MadeFromStuff ? GenStuff.DefaultStuffFor(melee.ProducedThingDef) : null;
            object damage = Resolve("weapon.melee.damage");
            double? baseDamage = Value(DirectMeasure(melee, damage, stuff));
            double? qualityDamage = Value(measure.Invoke(null, new object[] { melee, damage, stuff, QualityCategory.Legendary, "" }));
            StatDef multiplier = DefDatabase<StatDef>.GetNamed("MeleeWeapon_DamageMultiplier");
            double ratio = multiplier.Worker.GetValue(StatRequest.For(melee.ProducedThingDef, stuff, QualityCategory.Legendary)) / multiplier.Worker.GetValue(StatRequest.For(melee.ProducedThingDef, stuff, QualityCategory.Normal));
            Assert(baseDamage.HasValue && qualityDamage.HasValue && ratio > 1 && Equal(qualityDamage, baseDamage * ratio), "Melee tool damage follows the game's quality multiplier without creating items");
            Call(window, "Apply");
            Assert((QualityCategory)Read(options, "Quality") == QualityCategory.Legendary, "Applying comparison saves the quality choice");
            Type session = product.GetType("MintStatSort.RecipeSession", true);
            Assert(Equal(Value(Method(session, "Get").Invoke(null, new object[] { recipe, metric })), Value(Call(window, "Measure", recipe, metric))), "Applied dialog readings are reusable at the same quality");
            Set(options, "Quality", QualityCategory.Normal);
            object pending = Method(session, "Get").Invoke(null, new object[] { recipe, metric });
            Assert(!Value(pending).HasValue && (string)Read(pending, "Display") == "…", "Changing applied quality invalidates completed and queued readings");
            Method(session, "ImportMeasurements").Invoke(null, new object[] { Read(window, "readings"), null, QualityCategory.Legendary, "" });
            Assert(!Value(Method(session, "Get").Invoke(null, new object[] { recipe, metric })).HasValue, "Import refuses measurements from a different quality");
            while (Pending(Field(session, "measurements").GetValue(null)))
            { Method(session, "PumpMeasurements").Invoke(null, null); yield return null; }
            Assert(Equal(Value(Method(session, "Get").Invoke(null, new object[] { recipe, metric })), normal), "Session recalculates Normal values after quality invalidation");
            Call(window, "PreClose"); Call(options, "Reset");
            CheckWorkshopNotes();
        }
        private IEnumerable CheckProfilesAndFavorites(List<RecipeDef> recipes, IList rows, object options)
        {
            Call(options, "Reset");
            ((IList)Read(options, "Profiles")).Clear(); ((IList)Read(options, "BenchComparisons")).Clear();
            ((IList)Read(options, "FavoriteRecipes")).Clear(); Call(options, "Migrate");
            window = NewWindow(rows); Call(window, "SetBenchContext", "TableMachining"); SetCriterion("stat:Mass");
            foreach(object step in Drain("profile-initial-mass")) yield return step;
            int cached = CachedCount();
            Call(window, "SaveProfile", "Equipo ligero");
            IList profiles = (IList)Read(options, "Profiles");
            string firstId = (string)Read(profiles[0], "Id");
            Assert(profiles.Count == 1 && ((IList)Read(options, "Criteria")).Count == 0, "Saving a profile persists it without committing the comparison preview");
            SetCriterion("stat:MarketValue"); Call(window, "LoadProfile", firstId);
            Assert((string)Read(((IList)Read(Read(window, "draft"), "Criteria"))[0], "Id") == "stat:Mass", "Loading a profile restores its own independent criterion snapshot");
            foreach(object step in Drain("profile-reuse")) yield return step;
            Assert(CachedCount() == cached, "Loading a profile at unchanged quality/material reuses prepared statistics");
            Call(window, "SetFavoritesOnly", true);
            foreach(object step in Drain("favorites-empty")) yield return step;
            Assert(((IList)Read(window, "display")).Count == 0, "An empty favourites filter has a recoverable empty result");
            RecipeDef favorite = recipes.First(r => r.ProducedThingDef.IsApparel);
            Call(window, "ToggleFavorite", favorite);
            foreach(object step in Drain("favorites-mark")) yield return step;
            IList visible = (IList)Read(window, "display");
            Assert(visible.Count > 0 && visible.Cast<object>().All(r => object.ReferenceEquals(Read(r, "Recipe"), favorite)), "Favourites retain every row/style for the same recipe definition");
            Assert(CachedCount() == cached, "Favourite edits/filtering do not evaluate additional statistics");
            object selectedBefore = Read(window, "selected");
            Call(window, "ToggleFavorite", favorite); Call(window, "ToggleFavorite", favorite);
            Assert(object.ReferenceEquals(Read(window, "selected"), selectedBefore), "Marking a favourite does not select or execute a native bill action");
            Set(window, "search", "no-result-9acf10"); Set(window, "dirty", true); Call(window, "RefreshDisplay");
            Assert(((IList)Read(window, "display")).Count == 0, "Favourites combine with the recipe search");
            Set(window, "search", ""); Set(window, "dirty", true); Call(window, "RefreshDisplay");
            Call(window, "SaveProfile", " EQUIPO LIGERO ");
            Assert(profiles.Count == 1 && (string)Read(profiles[0], "Id") == firstId, "Updating a profile by normalized name preserves its ID and avoids duplicates");
            Call(window, "Apply");
            object benchA = Call(options, "Bench", "TableMachining");
            Assert((bool)Read(Read(benchA, "Values"), "FavoritesOnly"), "Applying remembers favourites filtering for the current bench type");
            Call(window, "PreClose"); window = NewWindow(rows); Call(window, "SetBenchContext", "ElectricTailoringBench");
            Call(window, "SetFavoritesOnly", false); Call(window, "Category", "kind:armor"); Call(window, "SelectQuality", QualityCategory.Good);
            SetCriterion("stat:ArmorRating_Sharp"); Call(window, "SaveProfile", "Protección");
            string secondId = (string)Read(profiles[1], "Id"); Call(window, "Apply");
            Assert(((IList)Read(options, "BenchComparisons")).Count == 2, "Different bench types retain separate applied comparisons");
            Call(window, "PreClose"); window = NewWindow(rows); Call(window, "SetBenchContext", "TableMachining");
            object draft = Read(window, "draft");
            Assert((QualityCategory)Read(draft, "Quality") == QualityCategory.Normal && (bool)Read(draft, "FavoritesOnly") && (string)Read(((IList)Read(draft, "Criteria"))[0], "Id") == "stat:Mass", "Reopening a bench restores its comparison rather than the last bench's filters");
            Assert((QualityCategory)Read(options, "Quality") == QualityCategory.Good, "Remembered comparison remains a preview until Apply");
            Call(window, "SelectQuality", QualityCategory.Legendary); Call(window, "SaveProfile", "Equipo ligero");
            Assert((QualityCategory)Read(Read(benchA, "Values"), "Quality") == QualityCategory.Normal, "Updating a named profile cannot mutate comparisons already applied to other benches");
            Call(window, "PreClose");
            var otherRecipes = recipes.Where(r => !r.ProducedThingDef.IsApparel).Take(10).ToList();
            window = NewWindow(CreateRows(otherRecipes)); Call(window, "LoadProfile", secondId);
            Assert((string)Read(Read(window, "draft"), "Category") == "all" && ((IList)Read(window, "display")).Count > 0, "Loading an unavailable category falls back to All instead of hiding the entire workbench");
            Call(window, "PreClose");
            Call(options, "DeleteProfile", firstId);
            Assert(Call(options, "IsFavorite", favorite).Equals(true) && Read(benchA, "ProfileId") == null && (QualityCategory)Read(Read(benchA, "Values"), "Quality") == QualityCategory.Normal,
                "Deleting a profile preserves favourites and the bench's applied snapshot");
            var typed = (SortSettings)options;
            typed.FavoriteRecipes.Add("UnavailableModRecipe"); Call(options, "Migrate");
            string path = Path.Combine(Root, "preference-roundtrip-" + phase + ".xml");
            Scribe.saver.InitSaving(path, "MintStatSortPreferenceTest"); Scribe_Deep.Look(ref typed, "settings"); Scribe.saver.FinalizeSaving();
            SortSettings restored = null;
            Scribe.loader.InitLoading(path); Scribe_Deep.Look(ref restored, "settings"); Scribe.loader.FinalizeLoading();
            Assert(restored != null && restored.Profiles.Count == 1 && restored.BenchComparisons.Count == 2 && restored.Quality == QualityCategory.Good,
                "Real Scribe roundtrip preserves named profiles, per-bench comparisons and quality");
            Assert(Call(restored, "IsFavorite", favorite).Equals(true) && restored.FavoriteRecipes.Contains("UnavailableModRecipe"), "Favourites survive reload, including temporarily unloaded mod recipes");
            Call(options, "Reset");
            Assert(typed.Profiles.Count == 1 && typed.FavoriteRecipes.Count == 2, "Restoring comparison defaults preserves saved profiles and favourite data");
            var session = product.GetType("MintStatSort.RecipeSession");
            Method(session, "SetBench").Invoke(null, new object[] { "TableMachining" });
            Assert((bool)Read(options, "FavoritesOnly") && (QualityCategory)Read(options, "Quality") == QualityCategory.Normal, "Optional menu session restores the correct workbench comparison");
            Method(session, "SetRecipes").Invoke(null, new object[] { recipes });
            var sorted = (IList)Method(session, "SortForDisplay").Invoke(null, new object[] { recipes });
            Assert(sorted.Count == 1 && object.ReferenceEquals(sorted[0], favorite), "Optional recipe cards obey the favourites filter and retain original recipe identity");
            Method(session, "SetBench").Invoke(null, new object[] { "ElectricTailoringBench" });
            Assert(!(bool)Read(options, "FavoritesOnly") && (QualityCategory)Read(options, "Quality") == QualityCategory.Good, "Switching optional menus restores another bench without carrying the favourites restriction");
            Call(options, "Reset"); Method(session, "Invalidate").Invoke(null, null);
        }
        private IEnumerator Run()
        {
            product = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "MintStatSort");
            Write("BUILD " + product.GetName().Version + " DLL=" + product.Location);
            windowType = product.GetType("MintStatSort.ComparisonWindow", true);
            rowType = product.GetType("MintStatSort.ComparisonRow", true);
            metricType = product.GetType("MintStatSort.Metric", true);
            metricHelper = product.GetType("MintStatSort.Metrics", true);
            incremental = Field(windowType, "discovery") != null;
            Assert(incremental == (phase != "baseline"), "phase uses the intended baseline or incremental build");
            Assert((bool)Field(product.GetType("MintStatSort.VanillaIntegration"), "Ready").GetValue(null), "native BillStack integration connected in the real game");
            if (AccessTools.TypeByName("NiceBillTab.TabBillsDrawer") != null)
                Assert((bool)Field(product.GetType("MintStatSort.NiceIntegration"), "Ready").GetValue(null), "Nice Bill Tab integration installs against the current optional DLL");
            if (AccessTools.TypeByName("DubsMintMenus.Patch_BillStack_DoListing") != null)
                Assert((bool)Field(product.GetType("MintStatSort.MintIntegration"), "Ready").GetValue(null), "Dubs Mint Menus integration installs against the current optional DLL");
            object instance = Field(product.GetType("MintStatSort.MintStatSortMod"), "Instance").GetValue(null);
            object options = Read(instance, "Options"); Call(options, "Reset");
            var recipes = DefDatabase<RecipeDef>.AllDefsListForReading.Where(r => r.ProducedThingDef != null).ToList();
            Write("CATALOG distinctRecipes=" + recipes.Count + " apparel=" + recipes.Count(r => r.ProducedThingDef.IsApparel) + " ranged=" + recipes.Count(r => r.ProducedThingDef.IsRangedWeapon));
            Assert(recipes.Count >= 100, "fixture exercises at least 100 real production recipes");
            IList rows = CreateRows(recipes);
            window = NewWindow(rows);
            if (incremental)
            {
                Assert(CachedCount() == 0, "constructing the window does not evaluate any recipe statistics");
                Assert(IsPending(), "discovery is scheduled instead of blocking construction");
            }
            foreach (object step in Drain("opening")) yield return step;
            Assert(((IList)Read(window, "display")).Count == rows.Count, "all recipe rows remain visible in the completed initial list");
            Assert(((IList)Read(window, "choices")).Count > 0, "recommended characteristics are available after discovery");
            if (incremental)
            {
                CheckCategories(recipes);
                // FloatMenuOption initializes Verse.Text and GUI.skin. Its real
                // option factory requires an OnGUI context, absent in this probe.
                Write("OBS native option factory benchmark omitted: FloatMenuOption requires OnGUI; native closure mapping remains covered by metadata and pure helper tests.");
                IDictionary stored = (IDictionary)Read(window, "counts");
                IList allowed = (IList)Read(window, "materials");
                Assert(stored.Count == allowed.Count, "incremental inventory publishes a complete count for every material");
                foreach (ThingDef material in allowed)
                {
                    long expected = Find.CurrentMap.listerThings.ThingsOfDef(material).Where(t => t.stackCount > 0 && t.Spawned && t.IsInAnyStorage()).Sum(t => (long)t.stackCount);
                    Assert(stored.Contains(material) && (long)stored[material] == expected, "incremental stored count matches actual map storage: " + material.defName);
                }
            }

            Call(window, "Category", "kind:armor");
            foreach (object step in Drain("category-armor")) yield return step;
            IList armor = (IList)Read(window, "display");
            Assert(armor.Count > 0 && armor.Cast<object>().All(r => ((RecipeDef)Read(r, "Recipe")).ProducedThingDef.IsApparel), "armor category contains real apparel recipes");
            SetCriterion("stat:ArmorRating_Sharp");
            foreach (object step in Drain("armor-sharp-sort")) yield return step;
            armor = (IList)Read(window, "display");
            object sharp = Resolve("stat:ArmorRating_Sharp");
            var distinctArmor = armor.Cast<object>().Select(r => (RecipeDef)Read(r, "Recipe")).Distinct().ToList();
            int compared = 0;
            foreach (RecipeDef recipe in distinctArmor)
            {
                object cached = Call(window, "Measure", recipe, sharp);
                object direct = DirectMeasure(recipe, sharp, null);
                Assert(Equal(Value(cached), Value(direct)) && (string)Read(cached, "Display") == (string)Read(direct, "Display"), "prepared armor value matches synchronous game stat: " + recipe.defName);
                if (++compared % 12 == 0) yield return null;
            }
            var expectedArmor = armor.Cast<object>().Select((row, index) => new { Row = row, Index = index, Reading = Value(Call(window, "Measure", (RecipeDef)Read(row, "Recipe"), sharp)), Name = (string)Read(row, "Label") })
                .OrderBy(r => r.Reading.HasValue ? 0 : 1).ThenBy(r => r.Reading.HasValue ? -r.Reading.Value : 0).ThenBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(r => r.Index).Select(r => r.Row).ToList();
            Assert(armor.Cast<object>().SequenceEqual(expectedArmor), "armor sorting matches final numeric values, with unavailable data last");
            int warmCount = CachedCount();
            for (int repeat = 0; repeat < 8; repeat++)
            {
                Set(window, "search", repeat % 2 == 0 ? "armor" : ""); Set(window, "dirty", true); Call(window, "RefreshDisplay");
                foreach (object step in Drain("warm-search-" + repeat)) yield return step;
                if (repeat % 2 == 0) Assert(((IList)Read(window, "display")).Cast<object>().All(r => ((string)Read(r, "Label")).IndexOf("armor", StringComparison.CurrentCultureIgnoreCase) >= 0), "search displays only matching labels");
            }
            Assert(CachedCount() == warmCount, "repeated filtering and sorting reuse completed measurements");

            Set(window, "search", ""); Call(window, "Category", "kind:weapons");
            Call(window, "Category", "kind:armor"); Call(window, "Category", "kind:clothing");
            foreach (object step in Drain("category-cancellation")) yield return step;
            var matches = Method(product.GetType("MintStatSort.RecipeSession"), "Matches");
            Assert(((IList)Read(window, "display")).Cast<object>().All(r => (bool)matches.Invoke(null, new object[] { Read(r, "Recipe"), "kind:clothing" })), "changing categories while jobs are pending cannot publish stale rows");
            SetCriterion("stat:Mass");
            foreach (object step in Drain("clothing-mass")) yield return step;
            Assert(((IList)Read(window, "display")).Count > 0, "clothing remains usable after interrupted preparation");
            Call(window, "Category", "all"); Set(window, "advanced", true); Call(window, "RefreshChoices");
            foreach (object step in Drain("advanced-stat-discovery")) yield return step;
            Assert(((IList)Read(window, "choices")).Count > 10, "advanced characteristics complete without an empty interface");
            if (incremental) Assert(CachedCount() <= recipes.Count * 10, "discovery does not retain a product of unavailable stat by recipe entries");

            object ceActive = product.GetType("MintStatSort.CombatExtendedIntegration").GetProperty("Active", Fields).GetValue(null, null);
            Write("OBS CE active=" + ceActive);
            if ((bool)ceActive)
            {
                Call(window, "Category", "kind:ranged"); SetCriterion("ce.penetration.sharp");
                foreach (object step in Drain("CE-penetration")) yield return step;
                object ce = Resolve("ce.penetration.sharp"); int ceValues = 0;
                foreach (RecipeDef recipe in ((IList)Read(window, "display")).Cast<object>().Select(r => (RecipeDef)Read(r, "Recipe")).Distinct().Take(32))
                {
                    object cached = Call(window, "Measure", recipe, ce); object direct = DirectMeasure(recipe, ce, null);
                    Assert(Equal(Value(cached), Value(direct)), "CE prepared penetration matches synchronous reference: " + recipe.defName);
                    if (Value(cached).HasValue) ceValues++;
                    yield return null;
                }
                Assert(ceValues > 0, "CE integration supplies real penetration values");
            }
            if (phase == "compat" || phase == "ce")
            {
                foreach (string assemblyName in new[] { "RitualSoundscapes", "MaterialTracker", "CreativeMode", "ColonyWardrobe" })
                    Assert(AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == assemblyName), "exact supplied companion mod loaded: " + assemblyName);
            }
            if (incremental)
            {
                Type session = product.GetType("MintStatSort.RecipeSession", true);
                RecipeDef stuffedArmor = recipes.FirstOrDefault(r => r.ProducedThingDef.IsApparel && r.ProducedThingDef.MadeFromStuff);
                if (stuffedArmor != null)
                {
                    var alternatives = GenStuff.AllowedStuffsFor(stuffedArmor.ProducedThingDef)
                        .Where(s => (bool)Method(metricHelper, "RecipeAllowsStuff").Invoke(null, new object[] { stuffedArmor, s })).Take(2).ToList();
                    if (alternatives.Count == 2)
                    {
                        Call(window, "Category", "kind:armor"); SetCriterion("stat:ArmorRating_Sharp");
                        object draft = Read(window, "draft");
                        foreach (ThingDef alternative in alternatives)
                        {
                            Set(draft, "Material", alternative.defName); ((IDictionary)Read(window, "readings")).Clear();
                            Call(window, "RefreshChoices"); Call(window, "RefreshDisplay");
                        }
                        foreach (object step in Drain("material-cancellation")) yield return step;
                        object sharpMaterial = Resolve("stat:ArmorRating_Sharp");
                        foreach (RecipeDef recipe in ((IList)Read(window, "display")).Cast<object>().Select(r => (RecipeDef)Read(r, "Recipe")).Distinct())
                        {
                            object cached = Call(window, "Measure", recipe, sharpMaterial);
                            object direct = DirectMeasure(recipe, sharpMaterial, alternatives[1]);
                            Assert(Equal(Value(cached), Value(direct)), "rapid material change retains only final material stats: " + recipe.defName);
                            yield return null;
                        }
                    }
                }
                Call(window, "Apply");
                IDictionary imported = (IDictionary)Field(session, "cache").GetValue(null);
                Assert(imported.Count > 0, "saving filters imports prepared readings into the optional menu session");
                int importedCount = imported.Count;
                string preservedMaterial = (string)Read(options, "Material");
                Method(session, "SetRecipes").Invoke(null, new object[] { recipes });
                Assert(((IDictionary)Field(session, "cache").GetValue(null)).Count == importedCount && (string)Read(options, "Material") == preservedMaterial, "setting the same recipe list retains imported selected-material readings");
                Call(options, "Reset");
                IList criteria = (IList)Read(options, "Criteria");
                object criterion = Activator.CreateInstance(product.GetType("MintStatSort.SortCriterion"));
                Set(criterion, "Id", "stat:Mass"); Set(criterion, "HigherIsBetter", true); criteria.Add(criterion);
                Method(session, "Invalidate").Invoke(null, null);
                Method(session, "SetRecipes").Invoke(null, new object[] { recipes });
                object mass = Resolve("stat:Mass");
                object pending = Method(session, "Get").Invoke(null, new object[] { recipes[0], mass });
                Assert(!Value(pending).HasValue && (string)Read(pending, "Display") == "…", "cold optional menu reading is explicitly pending instead of synchronous or false NoData");
                Method(session, "Get").Invoke(null, new object[] { recipes[0], mass });
                Assert(((ICollection)Field(session, "measurementQueue").GetValue(null)).Count == 1, "duplicate pending requests produce one optional menu job");
                foreach (RecipeDef recipe in recipes) Method(session, "Get").Invoke(null, new object[] { recipe, mass });
                int legacySteps = 0; double legacyMaximum = 0;
                while (Pending(Field(session, "measurements").GetValue(null)))
                {
                    if (++legacySteps > 10000) throw new InvalidOperationException("Optional menu measurements never completed.");
                    Stopwatch watch = Stopwatch.StartNew(); Method(session, "PumpMeasurements").Invoke(null, null); watch.Stop();
                    legacyMaximum = Math.Max(legacyMaximum, watch.Elapsed.TotalMilliseconds);
                    int before = ((ICollection)Field(session, "measurementQueue").GetValue(null)).Count;
                    Method(session, "PumpMeasurements").Invoke(null, null);
                    Assert(((ICollection)Field(session, "measurementQueue").GetValue(null)).Count == before, "optional menu pumps heavy work at most once per frame");
                    yield return null;
                }
                Write("MEASURE optional-menu-cold steps=" + legacySteps + " maxStepMs=" + Number(legacyMaximum));
                foreach (RecipeDef recipe in recipes.Take(32))
                {
                    object cached = Method(session, "Get").Invoke(null, new object[] { recipe, mass });
                    object direct = DirectMeasure(recipe, mass, null);
                    Assert(Equal(Value(cached), Value(direct)), "optional menu asynchronous stat matches game reference: " + recipe.defName);
                }
                Method(session, "Invalidate").Invoke(null, null);
                Method(session, "Get").Invoke(null, new object[] { recipes[0], mass });
                Method(session, "Invalidate").Invoke(null, null);
                Method(session, "PumpMeasurements").Invoke(null, null);
                Assert(((ICollection)Field(session, "measurementQueue").GetValue(null)).Count == 0 && ((IDictionary)Field(session, "cache").GetValue(null)).Count == 0, "invalidating an optional menu session cancels pending results");
            }
            if (incremental) foreach (object step in CheckQuality(recipes, rows, options)) yield return step;
            if (incremental) foreach (object step in CheckProfilesAndFavorites(recipes, rows, options)) yield return step;
            if (incremental) foreach (object step in CheckLimitsAndImportance(recipes, rows, options)) yield return step;
            if (incremental) foreach (object step in CheckFiltersAndIngredients(recipes, rows, options)) yield return step;
            if (incremental) foreach (object step in CheckInterface067(recipes, rows, options)) yield return step;
            if (incremental) foreach (object step in CheckFeatures068(recipes, rows, options)) yield return step;
            if (incremental) foreach(object step in CheckFeatures0610(recipes,rows,options)) yield return step;
            // The component never saves this fixture or invokes recipe callbacks.
            Write("OBS real-game helper tests completed; graphical OnGUI input and actual production require an in-game visual check.");
        }
        private IEnumerable CheckLimitsAndImportance(List<RecipeDef> recipes, IList rows, object options)
        {
            Call(window, "PreClose"); Call(options, "Reset");
            window = NewWindow(rows); SetCriterion("stat:Mass");
            foreach (object step in Drain("limits-initial")) yield return step;
            int cached = CachedCount();
            var typed = (SortSettings)Read(window, "draft");
            var criterion = typed.Criteria[0].Copy();
            var mass = Resolve("stat:Mass");
            var values = recipes.Select(r => Value(Call(window, "Measure", r, mass))).Where(v => v.HasValue).Select(v => v.Value).OrderBy(v => v).ToList();
            double boundary = values[values.Count / 2];
            criterion.HasMaximum = true; criterion.Maximum = boundary; criterion.Weight = 4;
            Call(window, "SetCriterion", criterion); Call(window, "RefreshDisplay");
            foreach (object step in Drain("limits-max-mass")) yield return step;
            IList filtered = (IList)Read(window, "display");
            Assert(filtered.Count > 0 && filtered.Count < rows.Count, "A mass maximum filters a real recipe catalogue without emptying every row");
            Assert(filtered.Cast<object>().All(r => (bool)Call(criterion, "Matches", Value(Call(window, "Reading", Read(r, "Recipe"), mass)))), "Every displayed row satisfies its selected mass limit");
            Assert(CachedCount() == cached, "Changing thresholds and importance reuses the existing measurement cache");
            Assert(((SortSettings)options).Criteria.Count == 0, "Criterion edits remain a preview until the parent comparator applies");
            Call(window, "SetBenchContext", "NumericDiagnosticBench");
            Call(window, "SaveProfile", "Numeric limits test");
            var profile = ((SortSettings)options).Profiles.First(p => p.Name == "Numeric limits test");
            criterion.Maximum = -100; Call(window, "SetCriterion", criterion); Call(window, "RefreshDisplay");
            foreach (object step in Drain("limits-empty")) yield return step;
            Assert(((IList)Read(window, "display")).Count == 0 && Read(window, "selected") == null, "Impossible limits produce a recoverable empty list without a selected action");
            Call(window, "LoadProfile", profile.Id);
            foreach (object step in Drain("limits-profile")) yield return step;
            typed = (SortSettings)Read(window, "draft");
            Assert(typed.Criteria[0].Weight == 4 && typed.Criteria[0].HasMaximum && typed.Criteria[0].Maximum == boundary, "Named profiles restore independent numeric limits and importance");
            object dialog = product.GetType("MintStatSort.CriterionDialog").GetConstructors(Fields).Single().Invoke(new object[] { mass, typed.Criteria[0], new Action<SortCriterion>(c => Call(window, "SetCriterion", c)) });
            Set(dialog, "minimum", "20"); Set(dialog, "maximum", "10");
            Assert(!(bool)Call(dialog, "Commit") && typed.Criteria[0].Maximum == boundary, "An invalid numeric interval cannot modify the comparator draft");
            Set(dialog, "minimum", ""); Set(dialog, "maximum", "1,5");
            Assert((bool)Call(dialog, "Commit") && typed.Criteria[0].Maximum == 1.5 && typed.Criteria[0].Weight == 4, "The real dialog accepts a decimal comma and preserves importance");
            Call(window, "LoadProfile", profile.Id); Call(window, "Apply");
            var persisted = (SortSettings)options;
            string path = Path.Combine(Root, "numeric-roundtrip-" + phase + ".xml");
            Scribe.saver.InitSaving(path, "MintStatSortNumericTest"); Scribe_Deep.Look(ref persisted, "settings"); Scribe.saver.FinalizeSaving();
            SortSettings restored = null;
            Scribe.loader.InitLoading(path); Scribe_Deep.Look(ref restored, "settings"); Scribe.loader.FinalizeLoading();
            var reloaded = restored.Profiles.First(p => p.Name == "Numeric limits test").Values.Criteria[0];
            Assert(reloaded.Weight == 4 && reloaded.HasMaximum && Math.Abs(reloaded.Maximum - boundary) < 1e-14 * Math.Max(1, Math.Abs(boundary)) && !reloaded.HasMinimum, "Real Scribe roundtrip preserves bound presence, values and priority");
            object remembered = Call(restored, "Bench", "NumericDiagnosticBench");
            Assert(((ComparisonPreferences)Read(remembered, "Values")).Criteria[0].Weight == 4, "Per-bench snapshots serialize the new criterion settings");
            var old = new SortCriterion { Id = "stat:Mass" }; Call(old, "Migrate");
            Assert(old.Weight == 1 && !old.HasMinimum && !old.HasMaximum, "Old criteria retain equal importance and no limits");
            old.Weight = 99; old.HasMinimum = true; old.Minimum = double.NaN; Call(old, "Migrate");
            Assert(old.Weight == 5 && !old.HasMinimum, "Malformed saved numeric preferences are sanitized");
            var armor = Resolve("stat:ArmorRating_Sharp");
            double armorScale = (double)Method(metricHelper, "LimitScale").Invoke(null, new[] { armor });
            Assert(armorScale == (phase == "ce" ? 1 : 100), "Vanilla armor uses percentage inputs; CE armor uses its raw penetration units");
            Assert((string)Method(metricHelper, "LimitUnit").Invoke(null, new[] { mass }) == "kg", "Mass thresholds identify kilograms explicitly");
            var session = product.GetType("MintStatSort.RecipeSession");
            Method(session, "SetRecipes").Invoke(null, new object[] { recipes });
            Method(session, "Invalidate").Invoke(null, null);
            var cold = (IList)Method(session, "SortForDisplay").Invoke(null, new object[] { recipes });
            Assert(cold.Count == recipes.Count, "Optional menus retain pending recipes while bounded stats are being prepared");
            IList sorted = cold;
            for (int i = 0; i < 200; i++)
            {
                yield return null;
                sorted = (IList)Method(session, "SortForDisplay").Invoke(null, new object[] { recipes });
                if (((ICollection)Field(session, "measurementQueue").GetValue(null)).Count == 0 && !Pending(Field(session, "measurements").GetValue(null))) break;
            }
            Assert(sorted.Count > 0 && sorted.Count < recipes.Count && sorted.Cast<RecipeDef>().All(r => (bool)Call(persisted.Criteria[0], "Matches", Value(Method(session, "Get").Invoke(null, new object[] { r, mass })))), "Optional menus apply inclusive numeric bounds after asynchronous work completes");
            criterion.HasMaximum = false; criterion.Weight = 1; Call(window, "SetCriterion", criterion); Call(window, "RefreshDisplay");
            foreach (object step in Drain("limits-clear")) yield return step;
            Assert(((IList)Read(window, "display")).Count == rows.Count, "Removing the bound restores all original rows and style variants");
            Call(window, "PreClose"); Call(options, "Reset");
        }
        private IEnumerable DrainIngredients(object inspector, string name)
        {
            int steps = 0; double maximum = 0;
            while (Pending(inspector))
            {
                if (++steps > 5000) throw new InvalidOperationException("Ingredient work never finished: " + name);
                var watch = Stopwatch.StartNew(); Call(inspector, "Update"); watch.Stop();
                maximum = Math.Max(maximum, watch.Elapsed.TotalMilliseconds);
                yield return null;
            }
            Write("MEASURE ingredients-" + name + " steps=" + steps + " maxStepMs=" + Number(maximum));
        }
        private object NewInspector(RecipeDef recipe, Map map, ThingDef reference, Dictionary<IngredientCount, List<ThingDef>> cache)
        {
            return product.GetType("MintStatSort.IngredientInspector").GetConstructors(Fields).Single()
                .Invoke(new object[] { recipe, map, reference, cache });
        }
        private IEnumerable CheckFiltersAndIngredients(List<RecipeDef> recipes, IList rows, object options)
        {
            Call(options, "Reset"); window = NewWindow(rows); SetCriterion("stat:Mass");
            foreach (object step in Drain("filter-summary-initial")) yield return step;
            int count = CachedCount();
            var draft = (SortSettings)Read(window, "draft");
            var criterion = draft.Criteria[0]; criterion.Weight = 4;
            criterion.HasMinimum = criterion.HasMaximum = true; criterion.Minimum = 0; criterion.Maximum = 8;
            draft.Category = "kind:weapons"; draft.FavoritesOnly = true; Set(window, "search", "sample");
            IList filters = (IList)Call(window, "ActiveFilters");
            Assert(filters.Count == 5 && filters.Cast<object>().Any(f => (string)Read(f, "Key") == "min:stat:Mass"), "Summary identifies search, category, favourites and each bound separately");
            Call(window, "RemoveActiveFilter", "min:stat:Mass");
            Assert(!criterion.HasMinimum && criterion.HasMaximum && criterion.Weight == 4 && draft.FavoritesOnly, "Removing a minimum preserves the maximum, importance and other filters");
            Call(window, "RemoveActiveFilter", "search");
            Assert((string)Read(window, "search") == "" && draft.Category == "kind:weapons", "Removing search preserves the category");
            Call(window, "ClearActiveFilters");
            foreach (object step in Drain("filter-summary-clear")) yield return step;
            Assert(((IList)Call(window, "ActiveFilters")).Count == 0 && draft.Criteria.Count == 1 && criterion.Weight == 4 && draft.Quality == QualityCategory.Normal, "Clear filters retains selected comparison criteria and reference quality");
            Assert(((SortSettings)options).Criteria.Count == 0 && CachedCount() == count && ((IList)Read(window, "display")).Count == rows.Count, "Filter summary changes remain a cached preview and recover all original recipe rows");
            RecipeDef actualRecipe = recipes.First(r => r.ingredients != null && r.ingredients.Count > 1 && !r.ignoreIngredientCountTakeEntireStacks);
            var cache = new Dictionary<IngredientCount, List<ThingDef>>();
            object inspector = NewInspector(actualRecipe, Find.CurrentMap, ThingDefOf.Steel, cache);
            Assert(Pending(inspector) && ((IList)Read(inspector, "Rows")).Count == 0, "Opening ingredients schedules work without scanning stock immediately");
            foreach (object step in DrainIngredients(inspector, "real-recipe")) yield return step;
            IList details = (IList)Read(inspector, "Rows");
            Assert(details.Count == actualRecipe.ingredients.Count, "Ingredient inspector preserves all declared requirement slots");
            foreach (object detail in details)
            {
                ThingDef reference = (ThingDef)Read(detail, "Reference");
                if (reference == null) continue;
                int expected = ((IngredientCount)Read(detail, "Ingredient")).CountRequiredOfFor(reference, actualRecipe, null);
                Assert((int?)Read(detail, "Required") == expected, "Ingredient quantity matches the game's value getter: " + actualRecipe.defName + "/" + reference.defName);
            }
            int alternateIndex = Enumerable.Range(0, details.Count).FirstOrDefault(i => ((IList)Read(details[i], "Choices")).Count > 1);
            if (((IList)Read(details[alternateIndex], "Choices")).Count > 1)
            {
                var alternatives = (IList)Read(details[alternateIndex], "Choices");
                ThingDef reference = (ThingDef)alternatives[alternatives.Count - 1];
                Call(inspector, "Choose", alternateIndex, reference);
                foreach (object step in DrainIngredients(inspector, "alternative")) yield return step;
                details = (IList)Read(inspector, "Rows");
                Assert(object.ReferenceEquals(Read(details[alternateIndex], "Reference"), reference) && draft.Material == "", "Choosing an alternative changes only the ingredient preview");
            }
            Call(inspector, "Dispose");
            RecipeDef fixedRecipe = recipes.First(r => !r.ignoreIngredientCountTakeEntireStacks && r.ingredients != null && r.ingredients.Count > 1 && r.ingredients.All(i => i.IsFixedIngredient) && r.fixedIngredientFilter != null && r.fixedIngredientFilter.AllowedDefCount == 0);
            object fixedView = NewInspector(fixedRecipe, Find.CurrentMap, null, new Dictionary<IngredientCount, List<ThingDef>>());
            foreach (object step in DrainIngredients(fixedView, "fixed-requirements")) yield return step;
            var fixedRows = (IList)Read(fixedView, "Rows");
            Assert(fixedRows.Count == fixedRecipe.ingredients.Count && fixedRows.Cast<object>().All(r => Read(r, "Reference") != null && Read(r, "Required") != null && (int?)Read(r, "Required") == ((IngredientCount)Read(r, "Ingredient")).CountRequiredOfFor((ThingDef)Read(r, "Reference"), fixedRecipe, null)), "Real fixed requirements bypass an empty recipe-wide filter and show numeric quantities");
            Call(fixedView, "Dispose");
            object reused = NewInspector(actualRecipe, null, null, cache);
            foreach (object step in DrainIngredients(reused, "no-map")) yield return step;
            Assert(((IList)Read(reused, "Rows")).Cast<object>().All(r => Read(r, "Available") == null && Read(r, "Missing") == null), "Without a map stock and shortfall remain unknown rather than zero");
            Assert(cache.Count == actualRecipe.ingredients.Count, "Reopening a recipe reuses its ingredient definition choices");
            Call(reused, "Dispose");
            RecipeDef nutrition = recipes.FirstOrDefault(r => (Type)Read(r, "ingredientValueGetterClass") == typeof(IngredientValueGetter_Nutrition) && r.ingredients.Count > 0);
            if (nutrition != null)
            {
                object food = NewInspector(nutrition, Find.CurrentMap, null, new Dictionary<IngredientCount, List<ThingDef>>());
                foreach (object step in DrainIngredients(food, "nutrition")) yield return step;
                var foodRows = (IList)Read(food, "Rows");
                Assert(foodRows.Cast<object>().All(r => Read(r, "Reference") == null || (int?)Read(r, "Required") == ((IngredientCount)Read(r, "Ingredient")).CountRequiredOfFor((ThingDef)Read(r, "Reference"), nutrition, null)), "Nutrition-based recipes use per-reference conversion and whole-unit rounding");
                Call(food, "Dispose");
            }
            Map map = Find.CurrentMap;
            IntVec3 cell = map.AllCells.First(c => c.Standable(map) && c.GetZone(map) == null && c.GetFirstItem(map) == null);
            var zone = new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile, map.zoneManager);
            map.zoneManager.RegisterZone(zone); zone.AddCell(cell); zone.settings.filter.SetAllow(ThingDefOf.Steel, true);
            Thing added = ThingMaker.MakeThing(ThingDefOf.Steel); added.stackCount = 15; GenSpawn.Spawn(added, cell, map);
            long total = map.listerThings.ThingsOfDef(ThingDefOf.Steel).Where(t => t.Spawned && t.IsInAnyStorage()).Sum(t => (long)t.stackCount);
            var ingredient = new IngredientCount(); ingredient.filter.SetAllow(ThingDefOf.Steel, true); ingredient.SetBaseCount((total + 10) * ThingDefOf.Steel.VolumePerUnit);
            var another = new IngredientCount(); another.filter.SetAllow(ThingDefOf.Steel, true); another.SetBaseCount(10 * ThingDefOf.Steel.VolumePerUnit);
            var synthetic = new RecipeDef { defName = "SmokeSharedStock", label = "Shared stock test", ingredients = new List<IngredientCount> { ingredient, another }, fixedIngredientFilter = new ThingFilter() };
            Set(synthetic, "ingredientValueGetterClass", typeof(IngredientValueGetter_Volume));
            object shared = NewInspector(synthetic, map, ThingDefOf.Steel, new Dictionary<IngredientCount, List<ThingDef>>());
            foreach (object step in DrainIngredients(shared, "shared-stock")) yield return step;
            var sharedRows = (IList)Read(shared, "Rows");
            Assert(total >= 15 && (long?)Read(sharedRows[0], "Available") == total && (long?)Read(sharedRows[1], "Available") == 0 && (long?)Read(sharedRows[1], "Missing") == another.CountRequiredOfFor(ThingDefOf.Steel, synthetic, null), "A real stored stack cannot be counted twice for overlapping requirements");
            Assert(added.stackCount == 15 && map.listerThings.ThingsOfDef(ThingDefOf.Steel).Where(t => t.Spawned && t.IsInAnyStorage()).Sum(t => (long)t.stackCount) == total, "Ingredient reservations never consume or alter real inventory");
            added.stackCount = 20; Call(shared, "Refresh");
            foreach (object step in DrainIngredients(shared, "refresh")) yield return step;
            Assert((long?)Read(((IList)Read(shared, "Rows"))[0], "Available") == total + 5, "Refreshing the selected recipe sees live stock changes");
            ingredient.SetBaseCount(25 * ThingDefOf.Steel.VolumePerUnit); Call(shared, "Refresh");
            foreach (object step in DrainIngredients(shared, "recipe-change")) yield return step;
            Assert((int?)Read(((IList)Read(shared, "Rows"))[0], "Required") == ingredient.CountRequiredOfFor(ThingDefOf.Steel, synthetic, null), "Ingredient refresh sees recipe quantity changes instead of keeping stale requirements");
            Call(shared, "Dispose");
            Set(shared, "nextRefresh", 0f); Call(shared, "Update"); Call(shared, "Refresh");
            Assert(!Pending(shared), "A closed prepared ingredient panel cannot restart its refresh work");
            synthetic.ignoreIngredientCountTakeEntireStacks = true;
            object whole = NewInspector(synthetic, map, null, new Dictionary<IngredientCount, List<ThingDef>>());
            foreach (object step in DrainIngredients(whole, "whole-stacks")) yield return step;
            Assert(((IList)Read(whole, "Rows")).Cast<object>().All(r => Read(r, "Required") == null && Read(r, "Missing") == null), "Whole-stack recipes show unestimable requirements explicitly");
            Call(whole, "Dispose"); added.Destroy(DestroyMode.Vanish); map.zoneManager.DeregisterZone(zone);
            var none = new RecipeDef { defName = "SmokeEmpty", label = "No ingredients", ingredients = new List<IngredientCount>() };
            object empty = NewInspector(none, map, null, new Dictionary<IngredientCount, List<ThingDef>>());
            foreach (object step in DrainIngredients(empty, "empty")) yield return step;
            Assert(((IList)Read(empty, "Rows")).Count == 0 && (bool)Read(empty, "Ready"), "Recipes without ingredients have a finished empty detail panel"); Call(empty, "Dispose");
            object cancelled = NewInspector(actualRecipe, map, null, new Dictionary<IngredientCount, List<ThingDef>>());
            Call(cancelled, "Dispose"); Call(cancelled, "Update");
            Assert(!Pending(cancelled) && ((IList)Read(cancelled, "Rows")).Count == 0, "Closing an unprepared ingredient view cancels discovery and stock work");
            Assert(CachedCount() == count && ((SortSettings)options).Criteria.Count == 0, "Ingredient inspection preserves comparison cache and uncommitted preferences");
            Call(window, "PreClose"); Call(options, "Reset");
        }
        private IEnumerable CheckInterface067(List<RecipeDef> recipes, IList rows, object options)
        {
            Call(options, "Reset"); window = NewWindow(rows); SetCriterion("stat:Mass");
            Call(window, "ToggleCategory", "kind:armor"); Call(window, "ToggleCategory", "kind:clothing");
            foreach (object step in Drain("multi-category")) yield return step;
            var draft = (SortSettings)Read(window, "draft");
            var display = ((IList)Read(window, "display")).Cast<object>().ToList();
            var matches = Method(product.GetType("MintStatSort.RecipeSession"), "Matches");
            var expected = rows.Cast<object>().Where(r => (bool)matches.Invoke(null, new object[] { Read(r, "Recipe"), "kind:armor" }) || (bool)matches.Invoke(null, new object[] { Read(r, "Recipe"), "kind:clothing" })).ToList();
            Assert(CategorySelection.Unpack(draft.Category).Count == 2 && display.Count == expected.Count && display.All(expected.Contains), "Multiple categories include armour and clothing without duplicating style rows");
            Assert(((IList)Call(window, "ActiveFilters")).Count == 2, "Each selected category has its own removable filter chip");
            Call(window, "RemoveActiveFilter", "category:kind:armor");
            Assert(draft.Category == "kind:clothing", "Removing armour preserves the clothing category");
            Call(window, "ToggleCategory", "kind:armor");
            draft.Criteria[0].Weight = 3; draft.Criteria[0].HasMaximum = true; draft.Criteria[0].Maximum = 8;
            Call(window, "SelectOrder", "name");
            foreach (object step in Drain("order-name-preserves")) yield return step;
            var named = ((IList)Read(window, "display")).Cast<object>().ToList();
            Assert(draft.Criteria.Count == 1 && draft.Criteria[0].HasMaximum && draft.Criteria[0].Weight == 3, "Name ordering preserves statistics importance and numeric bounds");
            Assert(named.Select(r => (string)Read(r, "Label")).SequenceEqual(named.Select(r => (string)Read(r, "Label")).OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase)), "Name mode orders the filtered rows alphabetically");
            Assert(named.All(r => Value(Call(window, "Reading", Read(r, "Recipe"), Resolve("stat:Mass"))) <= 8.000001), "Name mode continues to enforce numeric bounds");
            Call(window, "SelectOrder", "original");
            foreach (object step in Drain("order-original-preserves")) yield return step;
            var original = ((IList)Read(window, "display")).Cast<object>().ToList();
            Assert(original.SequenceEqual(rows.Cast<object>().Where(named.Contains)) && draft.Criteria.Count == 1 && draft.Criteria[0].HasMaximum, "Original ordering restores source order while retaining bounds and row identity");
            Call(window, "SelectOrder", "criteria");
            foreach (object step in Drain("order-criteria-restored")) yield return step;
            Assert(draft.Criteria.Count == 1 && draft.Criteria[0].Weight == 3 && draft.Criteria[0].HasMaximum, "Returning to statistical ordering restores the same comparison");
            Call(window, "SelectOrder", "name"); Call(window, "SaveProfile", "Multiple categories diagnostic"); Call(window, "Apply");
            var persisted = (SortSettings)options;
            string savePath = Path.Combine(Root, "multiple-categories-" + phase + ".xml");
            Scribe.saver.InitSaving(savePath, "diagnostic"); Scribe_Deep.Look(ref persisted, "comparison"); Scribe.saver.FinalizeSaving();
            SortSettings loaded = null; Scribe.loader.InitLoading(savePath); Scribe_Deep.Look(ref loaded, "comparison"); Scribe.loader.FinalizeLoading();
            Assert(loaded != null && loaded.OrderMode == "name" && CategorySelection.Unpack(loaded.Category).Count == 2 && loaded.Criteria.Count == 1 && loaded.Criteria[0].HasMaximum, "Real Scribe roundtrip retains multiple categories and independent name mode");
            var old = new SortSettings { Category = "kind:armor", Criteria = new List<SortCriterion> { new SortCriterion { Id = "stat:Mass" } }, Metric = "original", OrderMode = "" };
            Call(old, "Migrate");
            Assert(old.Category == "kind:armor" && old.OrderMode == "criteria", "Legacy settings preserve their single category and statistical ordering");
            var oldProfile = new ComparisonPreferences { Category = "kind:clothing", Metric = "name", OrderMode = "" };
            Call(oldProfile, "Migrate");
            Assert(oldProfile.OrderMode == "name" && oldProfile.Category == "kind:clothing", "Legacy alphabetical profiles migrate without losing their category");
            var session = product.GetType("MintStatSort.RecipeSession");
            Method(session, "Invalidate").Invoke(null, null);
            Method(session, "ImportMeasurements").Invoke(null, new object[] { Read(window, "readings"), null, QualityCategory.Normal, "" });
            var optional = (IList)Method(session, "SortForDisplay").Invoke(null, new object[] { recipes });
            Assert(optional.Cast<RecipeDef>().Select(r => r.LabelCap.ToString()).SequenceEqual(optional.Cast<RecipeDef>().Select(r => r.LabelCap.ToString()).OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase)), "Optional menu session respects name mode with retained criteria");
            Assert(optional.Cast<RecipeDef>().All(r => (bool)matches.Invoke(null, new object[] { r, draft.Category }) && Value(Call(window, "Reading", r, Resolve("stat:Mass"))) <= 8.000001), "Optional menus apply the category union and numeric bounds together");
            if (named.Count > 0)
            {
                Set(window, "selected", named.First()); Pump();
                object preview = Read(window, "ingredientPreview");
                Assert(preview != null && Read(preview, "Recipe") == Read(named.First(), "Recipe"), "Selecting a recipe schedules only that recipe's inline ingredient summary");
                foreach (object step in DrainIngredients(preview, "inline-selected")) yield return step;
                var other = expected.FirstOrDefault(r => Read(r, "Recipe") != Read(named.First(), "Recipe"));
                if (other != null)
                {
                    Set(window, "selected", other); Pump();
                    Assert(!Pending(preview) && Read(window, "ingredientPreview") != preview, "Changing selected recipe cancels and replaces the ingredient preview");
                }
                Call(window, "PreClose"); Pump();
                Assert(Read(window, "ingredientPreview") == null, "Closing comparator cancels ingredient preview and prevents restart");
            }
            else Call(window, "PreClose");
            Call(options, "Reset");
        }
        private static object Property(object instance, string name)
        { return instance.GetType().GetProperty(name, Fields).GetValue(instance, null); }
        private IEnumerable CheckFeatures068(List<RecipeDef> recipes, IList rows, object options)
        {
            Call(options, "Reset"); window = NewWindow(rows); SetCriterion("stat:Mass");
            foreach (object step in Drain("features-068-initial")) yield return step;
            var draft = (SortSettings)Read(window, "draft");
            var chosen = recipes.Distinct().Take(4).ToList();
            foreach (var r in chosen.Take(3)) Call(window, "TogglePin", r);
            Assert(((IList)Read(window, "pinned")).Count == 3, "Three distinct recipes can be pinned without changing filters or creating bills");
            Call(window, "TogglePin", chosen[3]);
            Assert(((IList)Read(window, "pinned")).Count == 3, "A fourth pin is rejected without replacing existing pins");
            Call(window, "TogglePin", chosen[0]); Call(window, "TogglePin", chosen[0]);
            Assert(((IList)Read(window, "pinned")).Cast<RecipeDef>().Distinct().Count() == 3, "Unpin and repin do not create duplicates");
            Set(window, "search", "___no_results___"); Set(window, "dirty", true); Pump();
            for (int i = 0; i < 200 && Pending(Read(window, "pinnedWork")); i++) { Pump(); yield return null; }
            Assert(((IList)Read(window, "display")).Count == 0 && ((IList)Read(window, "pinned")).Count == 3 && !Pending(Read(window, "pinnedWork")), "Filtered-out pinned recipes retain prepared cached readings");
            foreach (RecipeDef r in ((IList)Read(window, "pinned")))
                Assert(Equal(Value(Call(window, "Reading", r, Resolve("stat:Mass"))), Value(DirectMeasure(r, Resolve("stat:Mass"), null))), "Pinned cached statistic equals the game measurement");
            Call(window, "CommitColumns", new List<ComparisonColumn> { new ComparisonColumn { Id = "stat:Mass", Width = 240, Visible = false } });
            Assert(((IList)Call(window, "DisplayMetrics")).Count == 0 && draft.Criteria.Count == 1 && draft.Columns[0].Width == 240, "Hiding a column keeps its criterion and chosen width");
            Call(window, "SetExecutions", 3); draft.OnlyStockReady = true;
            var privateSettings = new SortSettings(); Call(privateSettings, "CopyComparisonFrom", draft);
            var profile = (ComparisonProfile)Call(privateSettings, "SaveProfile", "Feature profile", draft);
            Call(privateSettings, "RememberBench", "PrivateBench", draft, profile.Id);
            Assert((bool)Call(privateSettings, "RenameProfile", profile.Id, "Renamed feature profile") && privateSettings.BenchComparisons[0].ProfileId == profile.Id, "Renaming a profile preserves bench references and profile identity");
            var duplicate = (ComparisonProfile)Call(privateSettings, "DuplicateProfile", profile.Id, "Independent copy");
            duplicate.Values.Columns[0].Width = 80; duplicate.Values.Criteria[0].Weight = 5;
            Assert(duplicate.Id != profile.Id && profile.Values.Columns[0].Width == 240 && profile.Values.Criteria[0].Weight == 1, "Duplicated profiles have independent criteria and column layouts");
            Assert(!(bool)Call(privateSettings, "RenameProfile", profile.Id, "Independent copy"), "Renaming cannot overwrite another named profile");
            var exchange = product.GetType("MintStatSort.ProfileExchange");
            string exported = (string)Method(exchange, "Export").Invoke(null, new object[] { profile });
            var imported = (ComparisonProfile)Method(exchange, "Import").Invoke(null, new object[] { exported, privateSettings });
            Assert(imported.Id != profile.Id && imported.Name != profile.Name && imported.Values.Executions == 3 && imported.Values.OnlyStockReady && !imported.Values.Columns[0].Visible, "Import creates a unique profile and preserves quantities, stock filter and hidden columns");
            int priorProfiles = privateSettings.Profiles.Count;
            string invalidPath = Path.Combine(Path.GetDirectoryName(exported), "invalid-probe.xml"); File.WriteAllText(invalidPath, "<!DOCTYPE p [<!ENTITY x SYSTEM 'file:///none'>]><MintStatSortProfile format='1'><name>&x;</name></MintStatSortProfile>");
            bool rejected = false; try { Method(exchange, "Import").Invoke(null, new object[] { invalidPath, privateSettings }); } catch (TargetInvocationException) { rejected = true; }
            Assert(rejected && privateSettings.Profiles.Count == priorProfiles, "Invalid XML cannot partially mutate saved profiles");
            string scribefile = Path.Combine(Root, "features-068-" + phase + ".xml");
            Scribe.saver.InitSaving(scribefile, "featureTest"); Scribe_Deep.Look(ref privateSettings, "settings"); Scribe.saver.FinalizeSaving();
            SortSettings restored = null; Scribe.loader.InitLoading(scribefile); Scribe_Deep.Look(ref restored, "settings"); Scribe.loader.FinalizeLoading();
            Assert(restored.Executions == 3 && restored.OnlyStockReady && restored.Columns[0].Width == 240 && !restored.Columns[0].Visible && restored.Profiles.Count == 3 && restored.BenchComparisons[0].Values.Executions == 3, "Real Scribe roundtrip retains batch, stock filter, custom columns and managed profiles");
            File.Delete(exported); File.Delete(invalidPath);
            RecipeDef actual = recipes.First(r => r.ingredients.Count > 0 && !r.ignoreIngredientCountTakeEntireStacks);
            object batch = NewInspector(actual, Find.CurrentMap, null, new Dictionary<IngredientCount,List<ThingDef>>()); Call(batch, "SetExecutions", 3);
            foreach(object step in DrainIngredients(batch, "batch-three")) yield return step;
            foreach(object row in (IList)Read(batch, "Rows"))
            {
                var reference = (ThingDef)Read(row, "Reference"); if(reference == null) continue;
                Assert((int?)Read(row, "Required") == 3 * ((IngredientCount)Read(row, "Ingredient")).CountRequiredOfFor(reference, actual, null), "Batch requirement equals three executions of the game's rounded base quantity");
            }
            Call(batch, "SetExecutions", 2);
            Assert(Pending(batch) && ((IList)Read(batch, "Rows")).Cast<object>().All(r => Read(r,"Required") == null), "Changing execution count invalidates stale quantities and schedules recalculation");
            foreach(object step in DrainIngredients(batch, "batch-two")) yield return step;
            Assert((int)Property(batch,"Executions") == 2, "Ingredient batches update without changing recipe definitions or bills"); Call(batch,"Dispose");
            Map map = Find.CurrentMap;
            IntVec3 cell = map.AllCells.First(c => c.Standable(map) && c.GetZone(map) == null && c.GetFirstItem(map) == null);
            var zone = new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile,map.zoneManager); map.zoneManager.RegisterZone(zone); zone.AddCell(cell); zone.settings.filter.SetAllow(ThingDefOf.Steel,true);
            Thing stack = ThingMaker.MakeThing(ThingDefOf.Steel); stack.stackCount = 20; GenSpawn.Spawn(stack,cell,map);
            long total = map.listerThings.ThingsOfDef(ThingDefOf.Steel).Where(t => t.Spawned && t.IsInAnyStorage()).Sum(t => (long)t.stackCount);
            var requirement = new IngredientCount(); requirement.filter.SetAllow(ThingDefOf.Steel,true); requirement.SetBaseCount(total * ThingDefOf.Steel.VolumePerUnit);
            var sufficient = new RecipeDef {defName="StockReadyProbe",label="Stock ready",ingredients=new List<IngredientCount>{requirement},fixedIngredientFilter=new ThingFilter()}; Set(sufficient,"ingredientValueGetterClass",typeof(IngredientValueGetter_Volume));
            var empty = new RecipeDef {defName="EmptyStockProbe",label="No ingredients",ingredients=new List<IngredientCount>()};
            var whole = new RecipeDef {defName="WholeStockProbe",label="Unknown whole stacks",ingredients=new List<IngredientCount>{requirement},fixedIngredientFilter=new ThingFilter(),ignoreIngredientCountTakeEntireStacks=true}; Set(whole,"ingredientValueGetterClass",typeof(IngredientValueGetter_Volume));
            var stockRecipes = new List<RecipeDef>{sufficient,empty,whole};
            object availability = product.GetType("MintStatSort.RecipeAvailability").GetConstructors(Fields).Single().Invoke(new object[]{map,new Dictionary<IngredientCount,List<ThingDef>>()});
            Call(availability,"Configure",stockRecipes,ThingDefOf.Steel,1,true);
            Assert(Pending(availability) && !(bool)Property(availability,"Ready") && Call(availability,"Result",sufficient)==null,"Stock filtering starts deferred with unknown results");
            foreach(object step in DrainIngredients(availability,"stock-initial")) yield return step;
            Assert((bool)Property(availability,"Ready") && object.Equals(Call(availability,"Result",sufficient),true) && object.Equals(Call(availability,"Result",empty),true) && Call(availability,"Result",whole)==null,"Stock filter distinguishes sufficient, empty and unestimable whole-stack recipes");
            Call(availability,"Configure",stockRecipes,ThingDefOf.Steel,2,true);
            Assert(!(bool)Property(availability,"Ready"),"Batch changes invalidate the complete stock-filter snapshot");
            foreach(object step in DrainIngredients(availability,"stock-batch")) yield return step;
            Assert(object.Equals(Call(availability,"Result",sufficient),false) && stack.stackCount==20,"Stock filter applies batch requirements without consuming inventory");
            stack.stackCount=1; Call(availability,"Configure",stockRecipes,ThingDefOf.Steel,1,true);
            foreach(object step in DrainIngredients(availability,"stock-changed")) yield return step;
            Assert(object.Equals(Call(availability,"Result",sufficient),false),"A refreshed stock pass sees inventory shortages");
            var session = product.GetType("MintStatSort.RecipeSession"); Call(options,"Reset"); ((SortSettings)options).OnlyStockReady=true; ((SortSettings)options).Material=ThingDefOf.Steel.defName;
            Method(session,"SetMap").Invoke(null,new object[]{map}); Method(session,"SetRecipes").Invoke(null,new object[]{stockRecipes}); Method(session,"Invalidate").Invoke(null,null);
            IList sorted = (IList)Method(session,"SortForDisplay").Invoke(null,new object[]{stockRecipes});
            Assert(sorted.Count==3,"Optional stock filter preserves all rows while its first pass is pending");
            for(int i=0;i<500;i++) { yield return null; sorted=(IList)Method(session,"SortForDisplay").Invoke(null,new object[]{stockRecipes}); if((bool)Property(Field(session,"stockAvailability").GetValue(null),"Ready")) break; }
            Assert(sorted.Count==1 && object.ReferenceEquals(sorted[0],empty),"Optional menus apply the same shortage filter after gradual calculation");
            Call(options,"Reset"); Method(session,"Invalidate").Invoke(null,null);
            Call(availability,"Dispose"); Call(availability,"Update"); Assert(!Pending(availability),"Closing stock filtering prevents background work from restarting");
            stack.Destroy(DestroyMode.Vanish); map.zoneManager.DeregisterZone(zone);
            object noMap = product.GetType("MintStatSort.RecipeAvailability").GetConstructors(Fields).Single().Invoke(new object[]{null,new Dictionary<IngredientCount,List<ThingDef>>()});
            Call(noMap,"Configure",new[]{sufficient},ThingDefOf.Steel,1,true); foreach(object step in DrainIngredients(noMap,"stock-no-map")) yield return step;
            Assert(Call(noMap,"Result",sufficient)==null,"Stock filtering without a map does not invent availability"); Call(noMap,"Dispose");
            var diagnosticType=product.GetType("MintStatSort.DiagnosticDialog");
            var diagnostic=diagnosticType.GetConstructors(Fields).Single().Invoke(new object[]{"Private fixture context"});
            string report=(string)Read(diagnostic,"report"); string reportPath=(string)Call(diagnostic,"Save");
            Assert(report.Contains("MintStatSort 0.6.10") && report.Contains("mintstatsort.recipes") && report.Contains("Private fixture context") && File.ReadAllText(reportPath)==report,"Diagnostic report includes real build, active mods and context in an explicitly saved local file");
            Assert(reportPath.StartsWith(GenFilePaths.SaveDataFolderPath,StringComparison.OrdinalIgnoreCase),"Diagnostics and portable profiles use the current game save-data folder"); File.Delete(reportPath);
            Call(window,"PreClose"); Pump(); Assert(!Pending(Read(window,"pinnedWork")) && !Pending(Read(window,"availability")),"Closing comparator cancels pinned and stock-filter jobs");
            Write("FEATURES 0.6.10 complete");
        }

        private IEnumerable CheckFeatures0610(List<RecipeDef> recipes, IList rows, object options)
        {
            Call(options,"Reset");window=NewWindow(rows);
            Type starters=product.GetType("MintStatSort.StarterProfiles",true);
            foreach(string id in new[]{"Protection","Cold","Light","Range"})
            {
                var preset=(ComparisonPreferences)Method(starters,"Create").Invoke(null,new object[]{id,Read(window,"draft")});
                Assert(preset.Criteria.Count>0 && preset.OrderMode=="criteria" && preset.Descending,"Starter profile provides usable independent criteria: "+id);
                Call(window,"UsePreferences",preset,null);
            }
            SetCriterion("stat:Mass");Call(window,"SetCriterion",new SortCriterion{Id="recipe.work",HigherIsBetter=false});Call(window,"RefreshDisplay");
            foreach(object step in Drain("0610-score-details"))yield return step;
            Assert(((IDictionary)Read(window,"scoreDetails")).Count>0,"Prepared comparison has cached score explanations with no drawing-time stat reads");
            var draft=(SortSettings)Read(window,"draft");draft.Ammo="Ammo_Test";draft.MixSubstitutes=true;
            var saved=(SortSettings)Call(draft,"Copy");
            var pref=(ComparisonPreferences)Method(typeof(ComparisonPreferences),"Capture").Invoke(null,new object[]{draft});
            Assert(saved.Ammo==draft.Ammo && saved.MixSubstitutes && (bool)Call(pref,"Matches",draft),"Draft copy and comparison matching preserve ammunition and substitute mode");
            var round=new SortSettings();Call(pref,"ApplyTo",round);
            string f=Path.Combine(Root,"references-0610-"+phase+".xml");
            Scribe.saver.InitSaving(f,"references");Scribe_Deep.Look(ref round,"settings");Scribe.saver.FinalizeSaving();
            round=null;Scribe.loader.InitLoading(f);Scribe_Deep.Look(ref round,"settings");Scribe.loader.FinalizeLoading();
            Assert(round.Ammo=="Ammo_Test" && round.MixSubstitutes,"New comparison references survive a real Scribe save/load");
            var req=recipes.First(r=>r.skillRequirements!=null && r.skillRequirements.Count>0);
            var text=(string)Method(product.GetType("MintStatSort.RecipeRequirements",true),"Describe").Invoke(null,new object[]{req,false,req.LabelCap.ToString()});
            Assert(text.Contains(req.skillRequirements[0].skill.LabelCap.ToString()) && !text.Contains("MSS.RequiredSkill"),"Recipe explanations show translated skill requirements and retain native labels");
            Map map=Find.CurrentMap;
            IntVec3 cell=map.AllCells.First(c=>c.Standable(map) && c.GetZone(map)==null && c.GetFirstItem(map)==null);
            IntVec3 cell2=map.AllCells.First(c=>c.Standable(map) && c.GetZone(map)==null && c.GetFirstItem(map)==null && c!=cell);
            var zone=new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile,map.zoneManager);map.zoneManager.RegisterZone(zone);zone.AddCell(cell);zone.AddCell(cell2);
            zone.settings.filter.SetAllow(ThingDefOf.Steel,true);zone.settings.filter.SetAllow(ThingDefOf.WoodLog,true);
            Thing steel=ThingMaker.MakeThing(ThingDefOf.Steel);steel.stackCount=5;GenSpawn.Spawn(steel,cell,map);
            Thing wood=ThingMaker.MakeThing(ThingDefOf.WoodLog);wood.stackCount=5;GenSpawn.Spawn(wood,cell2,map);
            var ingredient=new IngredientCount();ingredient.SetBaseCount(10*Math.Min(ThingDefOf.Steel.VolumePerUnit,ThingDefOf.WoodLog.VolumePerUnit));ingredient.filter.SetAllow(ThingDefOf.Steel,true);ingredient.filter.SetAllow(ThingDefOf.WoodLog,true);
            var recipe=new RecipeDef{defName="MSS_SubstituteProbe",ingredients=new List<IngredientCount>{ingredient},allowMixingIngredients=true};
            recipe.fixedIngredientFilter.SetAllow(ThingDefOf.Steel,true);recipe.fixedIngredientFilter.SetAllow(ThingDefOf.WoodLog,true);
            Set(recipe,"ingredientValueGetterClass",typeof(IngredientValueGetter_Volume));
            object inspector=NewInspector(recipe,map,ThingDefOf.Steel,new Dictionary<IngredientCount,List<ThingDef>>());Call(inspector,"SetMixing",true);
            foreach(object step in DrainIngredients(inspector,"0610-mixed"))yield return step;
            object detail=((IList)Read(inspector,"Rows"))[0];
            Assert((long?)Read(detail,"Missing")==0 && steel.stackCount==5 && wood.stackCount==5,"Mixing-permitted recipe combines stored substitutes without consuming them");
            recipe.allowMixingIngredients=false;Call(inspector,"Refresh");
            foreach(object step in DrainIngredients(inspector,"0610-single-kind"))yield return step;
            // Existing fixture stock can also supply this synthetic requirement;
            // the pure allocation checks cover the isolated shortage edge cases.
            Assert(Read(((IList)Read(inspector,"Rows"))[0],"Missing")!=null,"No-mixing recipes produce a known single-substitute estimate");
            recipe.ignoreIngredientCountTakeEntireStacks=true;Call(inspector,"Refresh");
            foreach(object step in DrainIngredients(inspector,"0610-whole-stack"))yield return step;
            Assert(Read(((IList)Read(inspector,"Rows"))[0],"Missing")==null,"Whole-stack requirements remain unknown in substitute mode");Call(inspector,"Dispose");
            steel.Destroy();wood.Destroy();zone.Delete();
            var ce=product.GetType("MintStatSort.CombatExtendedIntegration",true);
            bool active=(bool)ce.GetProperty("Active",Fields).GetValue(null,null);
            if(active)
            {
                var weapon=recipes.First(r=>r.ProducedThingDef!=null && ((IList)Method(ce,"Ammunition").Invoke(null,new object[]{r.ProducedThingDef})).Count>1);
                var ammunition=(List<ThingDef>)Method(ce,"Ammunition").Invoke(null,new object[]{weapon.ProducedThingDef});
                Call(window,"SelectAmmo",ammunition[0].defName);var first=Call(window,"Measure",weapon,Resolve("weapon.damage"));
                Call(window,"SelectAmmo",ammunition[1].defName);var second=Call(window,"Measure",weapon,Resolve("weapon.damage"));
                Assert(((string)Read(first,"Detail")).Contains(ammunition[0].LabelCap.ToString()) && ((string)Read(second,"Detail")).Contains(ammunition[1].LabelCap.ToString()),"CE selected ammunition replaces the projectile reference and invalidates cached measurements");
                Assert(((SortSettings)options).Ammo=="","CE ammunition preview does not change saved settings before Apply");
                Assert(Value(Call(window,"Measure",weapon,Resolve("ce.dps.sustained"))).HasValue,"CE magazine weapons expose sustained theoretical DPS");
                Call(window,"SelectAmmo","Unknown_Ammo");
                var fallback=Call(window,"Measure",weapon,Resolve("weapon.damage"));
                Assert(Value(fallback).HasValue && ((string)Read(fallback,"Detail")).Contains("MSS.CE.AmmoFallback".Translate().ToString()),"Incompatible ammunition explicitly falls back to the weapon reference");
            }
            Call(window,"PreClose");Call(options,"Reset");
            Write("FEATURES 0.6.10 complete");
        }

        private IEnumerator CaptureInterface()
        {
            var tutorPreference = typeof(Prefs).GetProperty("AdaptiveTutorEnabled", BindingFlags.Public | BindingFlags.Static);
            if (tutorPreference != null && tutorPreference.CanWrite) tutorPreference.SetValue(null, false, null);
            Screen.SetResolution(1600, 1000, false);
            for (int i = 0; i < 30; i++) yield return null;
            product = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "MintStatSort");
            windowType = product.GetType("MintStatSort.ComparisonWindow", true);
            rowType = product.GetType("MintStatSort.ComparisonRow", true);
            metricType = product.GetType("MintStatSort.Metric", true);
            metricHelper = product.GetType("MintStatSort.Metrics", true);
            object instance = Field(product.GetType("MintStatSort.MintStatSortMod"), "Instance").GetValue(null);
            object options = Read(instance, "Options"); Call(options, "Reset");
            var recipes = DefDatabase<RecipeDef>.AllDefsListForReading.Where(r => r.ProducedThingDef != null).ToList();
            window = NewWindow(CreateRows(recipes)); Call(window, "SetBenchContext", "TableMachining");
            Set(window, "native", true); Call(window, "Category", "all"); Call(window, "SetFavoritesOnly", false); SetCriterion("stat:Mass");
            if (!Call(options, "IsFavorite", recipes[0]).Equals(true)) Call(window, "ToggleFavorite", recipes[0]);
            Call(window, "SaveProfile", "Equipo ligero");
            Find.WindowStack.Add((Window)window);
            for(int i = 0; i < 120; i++) yield return null;
            Assert(((IList)Read(window, "display")).Count > 0, "Graphical comparator renders a populated list with profile and favourite controls");
            string path = Path.Combine(Root, "interface-0.6.10.png");
            ScreenCapture.CaptureScreenshot(path);
            for(int i = 0; i < 25; i++) yield return null;
            Assert(File.Exists(path) && new FileInfo(path).Length > 0, "Game renderer produces a real screenshot for visual inspection");
            Call(window, "SetFavoritesOnly", true);
            for(int i = 0; i < 45; i++) yield return null;
            Assert(((IList)Read(window, "display")).Count > 0, "Graphical favourites filter retains the starred recipe");
            string favoritesPath = Path.Combine(Root, "favorites-0.6.10.png");
            ScreenCapture.CaptureScreenshot(favoritesPath);
            for(int i = 0; i < 25; i++) yield return null;
            Assert(File.Exists(favoritesPath) && new FileInfo(favoritesPath).Length > 0, "Game renderer captures filled favourite stars and the active filter");
            var nameDialog = (Window)product.GetType("MintStatSort.ProfileNameDialog", true).GetConstructors(Fields).Single()
                .Invoke(new object[] { "Equipo ligero", new Action<string>(n => { }) });
            Find.WindowStack.Add(nameDialog);
            for(int i = 0; i < 25; i++) yield return null;
            string dialogPath = Path.Combine(Root, "profile-dialog-0.6.10.png");
            ScreenCapture.CaptureScreenshot(dialogPath);
            for(int i = 0; i < 25; i++) yield return null;
            Assert(File.Exists(dialogPath) && new FileInfo(dialogPath).Length > 0, "Game renderer captures the profile name/update dialog");
            nameDialog.Close();
            Call(window, "SetFavoritesOnly", false); Call(window, "OpenCriterion", "stat:Mass");
            for(int i = 0; i < 25; i++) yield return null;
            var numericDialog = Find.WindowStack.Windows.First(w => w.GetType().Name == "CriterionDialog");
            Set(numericDialog, "maximum", "8"); ((SortCriterion)Read(numericDialog, "draft")).Weight = 3;
            string numericPath = Path.Combine(Root, "limits-dialog-0.6.10.png");
            ScreenCapture.CaptureScreenshot(numericPath);
            for(int i = 0; i < 25; i++) yield return null;
            Assert(File.Exists(numericPath) && new FileInfo(numericPath).Length > 0, "Game renderer captures the numeric limits and importance dialog");
            Assert((bool)Call(numericDialog, "Commit"), "Rendered criterion dialog applies its valid preview");
            numericDialog.Close();
            for(int i = 0; i < 45; i++) yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(Root, "filtered-0.6.10.png"));
            for(int i = 0; i < 25; i++) yield return null;
            ((SortSettings)Read(window, "draft")).Criteria[0].HasMinimum = true;
            ((SortSettings)Read(window, "draft")).Criteria[0].Minimum = 0;
            Call(window, "Category", "kind:armor"); Call(window, "ToggleCategory", "kind:clothing"); Set(window, "dirty", true);
            for(int i = 0; i < 70; i++) yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(Root, "active-filters-0.6.10.png"));
            for(int i = 0; i < 25; i++) yield return null;
            Assert(((IList)Call(window, "ActiveFilters")).Count == 4, "Rendered active-filter bar contains both categories and independent bounds");
            var ingredientRow = ((IList)Read(window, "display")).Cast<object>().First(r => ((RecipeDef)Read(r, "Recipe")).ingredients.Count > 1);
            Set(window, "selected", ingredientRow);
            Call(window, "SetCriterion", new SortCriterion { Id = "stat:ArmorRating_Sharp", Weight = 2 });
            Call(window, "SetCriterion", new SortCriterion { Id = "stat:ArmorRating_Blunt" });
            Call(window, "SetCriterion", new SortCriterion { Id = "stat:Insulation_Cold" });
            for(int i = 0; i < 70; i++) yield return null;
            Assert(Read(window, "ingredientPreview") != null, "Rendered comparison includes the selected recipe ingredient summary");
            ScreenCapture.CaptureScreenshot(Path.Combine(Root, "selected-summary-0.6.10.png"));
            for(int i = 0; i < 25; i++) yield return null;
            Call(window, "OpenIngredients");
            var ingredientDialog = Find.WindowStack.Windows.First(w => w.GetType().Name == "IngredientDialog");
            for(int i = 0; i < 90; i++) yield return null;
            object inspector = Read(ingredientDialog, "inspector");
            Assert((bool)Read(inspector, "Ready") && ((IList)Read(inspector, "Rows")).Count > 1, "Rendered ingredient panel prepares real recipe quantities");
            Assert(((IList)Read(inspector, "Rows")).Cast<object>().All(r => Read(r, "Reference") != null && Read(r, "Required") != null), "Rendered armour ingredient panel contains usable references and numeric requirements");
            string ingredientPath = Path.Combine(Root, "ingredients-0.6.10.png");
            ScreenCapture.CaptureScreenshot(ingredientPath);
            for(int i = 0; i < 25; i++) yield return null;
            Assert(File.Exists(ingredientPath) && new FileInfo(ingredientPath).Length > 0, "Game renderer captures the ingredient panel with quantities and references");
            ingredientDialog.Close();
            Assert(!Pending(inspector), "Closing the rendered ingredient panel cancels inventory work");
            Call(window,"SetExecutions",3);
            for(int i=0;i<50;i++) yield return null;
            Call(window,"OpenIngredients");
            var batchDialog=Find.WindowStack.Windows.First(w => w.GetType().Name=="IngredientDialog");
            for(int i=0;i<70;i++) yield return null;
            Assert((int)Property(Read(batchDialog,"inspector"),"Executions")==3,"Rendered ingredient panel inherits the comparator batch quantity");
            ScreenCapture.CaptureScreenshot(Path.Combine(Root,"batch-ingredients-0.6.10.png"));
            for(int i=0;i<25;i++) yield return null; batchDialog.Close();
            Call(window,"OpenColumns");
            var columnsDialog=Find.WindowStack.Windows.First(w => w.GetType().Name=="ColumnsDialog");
            var columns=((IList)Read(columnsDialog,"columns")).Cast<ComparisonColumn>().ToList();
            columns[0].Width=240; columns[0].Visible=false; Call(columnsDialog,"Move",0,1);
            ScreenCapture.CaptureScreenshot(Path.Combine(Root,"columns-dialog-0.6.10.png"));
            for(int i=0;i<25;i++) yield return null;
            Call(columnsDialog,"Commit");
            for(int i=0;i<50;i++) yield return null;
            Assert(((SortSettings)Read(window,"draft")).Criteria.Count==4 && ((IList)Read(window,"tableMetrics")).Count==3,"Rendered custom columns retain hidden criteria while changing table layout");
            var pinRecipes=((IList)Read(window,"display")).Cast<object>().Select(r => (RecipeDef)Read(r,"Recipe")).Distinct().Take(3).ToList();
            foreach(var r in pinRecipes) Call(window,"TogglePin",r);
            for(int i=0;i<35;i++) yield return null;
            Call(window,"OpenPinned");
            var pinnedDialog=Find.WindowStack.Windows.First(w => w.GetType().Name=="PinnedComparisonDialog");
            for(int i=0;i<30;i++) yield return null;
            Assert(((IList)Read(pinnedDialog,"recipes")).Count==3 && ((IList)Read(pinnedDialog,"metrics")).Count==4,"Game renderer opens side-by-side comparison with three recipes and all active statistics");
            ScreenCapture.CaptureScreenshot(Path.Combine(Root,"pinned-comparison-0.6.10.png"));
            for(int i=0;i<25;i++) yield return null; pinnedDialog.Close();
            Call(window,"OpenDiagnostics");
            var diagnosticDialog=Find.WindowStack.Windows.First(w => w.GetType().Name=="DiagnosticDialog");
            for(int i=0;i<30;i++) yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(Root,"diagnostics-0.6.10.png"));
            for(int i=0;i<25;i++) yield return null;
            Assert(((string)Read(diagnosticDialog,"report")).Contains("Executions: 3"),"Rendered diagnostic includes current comparator context"); diagnosticDialog.Close();
            ((SortSettings)Read(window,"draft")).OnlyStockReady=true; Set(window,"dirty",true);
            for(int i=0;i<25;i++) yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(Root,"stock-progress-0.6.10.png"));
            for(int i=0;i<5000 && !(bool)Property(Read(window,"availability"),"Ready");i++) yield return null;
            for(int i=0;i<35;i++) yield return null;
            Assert((bool)Property(Read(window,"availability"),"Ready"),"Graphical stock filter completes its gradual pass");
            ScreenCapture.CaptureScreenshot(Path.Combine(Root,"stock-filter-0.6.10.png"));
            for(int i=0;i<25;i++) yield return null;
            Assert(((IList)Call(window,"ActiveFilters")).Cast<object>().Any(f => (string)Read(f,"Key")=="stock"),"Rendered stock filter has an independently removable active-filter chip");
            Call(window,"RemoveActiveFilter","stock");

            ((Window)window).Close();
            Screen.SetResolution(1100, 740, false);
            for(int i = 0; i < 30; i++) yield return null;
            window = NewWindow(CreateRows(recipes)); SetCriterion("stat:ArmorRating_Sharp");
            Find.WindowStack.Add((Window)window);
            for(int i = 0; i < 70; i++) yield return null;
            Assert(((Window)window).windowRect.width <= UI.screenWidth && ((Window)window).windowRect.height <= UI.screenHeight, "Comparator fits a smaller screen with scrollable controls");
            ScreenCapture.CaptureScreenshot(Path.Combine(Root, "small-interface-0.6.10.png"));
            for(int i = 0; i < 25; i++) yield return null;
            ((Window)window).Close();
            Screen.SetResolution(900,650,false);
            for(int i=0;i<30;i++)yield return null;
            window=NewWindow(CreateRows(recipes));SetCriterion("stat:Mass");Find.WindowStack.Add((Window)window);
            for(int i=0;i<80;i++)yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(Root,"compact-recipes-0.6.10.png"));
            for(int i=0;i<20;i++)yield return null;
            Set(window,"compactCriteria",true);
            for(int i=0;i<30;i++)yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(Root,"compact-criteria-0.6.10.png"));
            for(int i=0;i<20;i++)yield return null;
            Write("HOLD compact keyboard check");
            for(int i=0;i<18000 && File.Exists(Path.Combine(Root,"graphics-hold.txt"));i++)yield return null;
            Assert(Read(window,"selected")!=null && ((IList)Read(window,"pinned")).Count==1,"Real keyboard shortcuts select and pin a recipe in the compact panel");
            Assert(((SortSettings)Read(window,"draft")).Criteria.Count==((SortSettings)product.GetType("MintStatSort.RecipeSession",true).GetProperty("Options",Fields).GetValue(null,null)).Criteria.Count,"Real Ctrl+Enter retains comparison criteria when saving");
            ((Window)window).Close();
            Assert("MSS.Shortcuts".Translate().ToString()=="Atajos de teclado" && "MSS.AddBill".Translate().ToString()=="Añadir proyecto","SpanishLatin loads complete mod strings and its regional native bill label");
            Write("SCREENSHOT " + path);
        }
    }
}
