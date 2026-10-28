using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation.Language;
using MintStatSort;
using Mono.Cecil;
using Mono.Cecil.Cil;

internal sealed class Sample
{
    public string Name;
    public double? Value;
    public string Id;
}
internal sealed class MultiSample
{
    public string Id;
    public string Name;
    public double?[] Values;
}
internal sealed class StockSample
{
    public int Count;
    public bool Spawned;
    public bool Stored;
}
internal static class Checks
{
    private static int count;
    private static void Check(bool test, string message)
    {
        if (!test) throw new Exception(message);
        count++;
    }
    private static string Order(IEnumerable<Sample> rows) { return string.Join(",", rows.Select(r => r.Id)); }
    public static void Main(string[] args)
    {
        if (args.Length != 0 && args[0] == "--parse-script")
        {
            if (args.Length < 2) throw new Exception("Pass one or more PowerShell script paths after --parse-script.");
            CheckScripts(args.Skip(1));
            Console.WriteLine("Passed " + count + " PowerShell syntax checks.");
            return;
        }
        if (args.Length > 0 && args[0] == "--ce")
        {
            foreach (string path in args.Skip(1))
            using (var module = ModuleDefinition.ReadModule(path))
            {
                foreach (var contract in new[] {
                    new[] { "ProjectilePropertiesCE", "armorPenetrationSharp", "armorPenetrationBlunt", "pelletCount", "warmupMultiplier", "warmupOffset" },
                    new[] { "CompProperties_AmmoUser", "ammoSet", "magazineSize", "reloadTime" },
                    new[] { "AmmoSetDef", "ammoTypes" }, new[] { "AmmoLink", "ammo", "projectile" },
                    new[] { "VerbPropertiesCE", "useEquipmentStatValues" } })
                {
                    var type = module.Types.Single(t => t.FullName == "CombatExtended." + contract[0]);
                    foreach (string field in contract.Skip(1))
                        Check(type.Fields.Any(f => f.Name == field && f.IsPublic), "CE public field " + contract[0] + "." + field);
                }
                Console.WriteLine("CE schema checked: " + path);
            }
            using (var module = ModuleDefinition.ReadModule(Path.Combine(AppContext.BaseDirectory, "../../../../Mod/Assemblies/MintStatSort.dll")))
                Check(!module.AssemblyReferences.Any(a => a.Name.Contains("CombatExtended")), "CE remains optional: no assembly reference");
            Console.WriteLine("Passed " + count + " CE schema and optional dependency checks.");
            return;
        }
        if (args.Length != 0 && args[0] == "--optional") args = args.Skip(1).ToArray();
        PerformanceChecks.Run(Check);
        WorkshopNotesChecks.Run(Check);
        if (args.Length != 0 && args[0] == "--performance")
        {
            Console.WriteLine("Passed " + count + " UI-thread work budget and cancellation checks.");
            return;
        }
        int addedBills = 0;
        Action addBill = () => addedBills++;
        Check(!NativeBillAction.TryExecute(true, true, false, null), "No selected action creates no bill");
        Check(!NativeBillAction.TryExecute(false, true, false, addBill), "Destroyed or unselected table cannot receive a bill");
        Check(!NativeBillAction.TryExecute(true, false, false, addBill), "Unavailable recipe cannot be added");
        Check(!NativeBillAction.TryExecute(true, true, true, addBill), "Disabled native option cannot be added");
        Check(addedBills == 0, "Rejected actions leave bill stack unchanged");
        Check(NativeBillAction.TryExecute(true, true, false, addBill) && addedBills == 1, "Add bill invokes the original action exactly once");
        var samples = new List<Sample>
        {
            new Sample { Id = "missing", Name = "A", Value = null },
            new Sample { Id = "low", Name = "Plate", Value = 0.20 },
            new Sample { Id = "high", Name = "Helmet", Value = 0.95 },
            new Sample { Id = "medium", Name = "Vest", Value = 0.50 },
            new Sample { Id = "zero", Name = "Cloth", Value = 0 }
        };
        Check(Order(SortingCore.Numeric(samples, r => r.Value, r => r.Name, true)) == "high,medium,low,zero,missing", "Armor highest first, zero is valid, missing last");
        Check(Order(SortingCore.Numeric(samples, r => r.Value, r => r.Name, false)) == "zero,low,medium,high,missing", "Lowest first still puts missing last");
        Check(Order(samples) == "missing,low,high,medium,zero", "Sorting must not mutate recipes");
        var special = new[]
        {
            new Sample { Id = "nan", Name = "X", Value = double.NaN },
            new Sample { Id = "infinite", Name = "Y", Value = double.PositiveInfinity },
            new Sample { Id = "negative", Name = "A", Value = -10 },
            new Sample { Id = "valid", Name = "B", Value = 10 }
        };
        Check(Order(SortingCore.Numeric(special, r => r.Value, r => r.Name, true)) == "valid,negative,nan,infinite", "Non-finite values cannot outrank valid ones");
        Check(Order(SortingCore.Numeric(special, r => r.Value, r => r.Name, false)) == "negative,valid,nan,infinite", "Negative stats are not missing");
        var ties = new[]
        {
            new Sample { Id = "B", Name = "Vest", Value = 1 },
            new Sample { Id = "A1", Name = "Helmet", Value = 1 },
            new Sample { Id = "A2", Name = "Helmet", Value = 1 }
        };
        Check(Order(SortingCore.Numeric(ties, r => r.Value, r => r.Name, true)) == "A1,A2,B", "Ties use names and keep identical recipes stable");
        Check(Order(SortingCore.Alphabetical(ties, r => r.Name, true)) == "B,A1,A2", "Reverse alphabetical sorting");
        Check(SortingCore.Numeric(Array.Empty<Sample>(), r => r.Value, r => r.Name, true).Count == 0, "Empty result after filters");
        int evaluations = 0;
        SortingCore.Numeric(samples, r => { evaluations++; return r.Value; }, r => r.Name, true);
        Check(evaluations == samples.Count, "Stats calculated once per sorting operation");
        Check(SortingCore.Numeric(samples.Concat(samples.Take(1)), r => r.Value, r => r.Name, true).Count == 6, "Keep all recipes including duplicate styles");
        CheckMultipleGoals();
        CheckReferences();
        ComparisonChecks.Run(Check);
        FeatureChecks.Run(Check);
        IngredientChecks.Run(Check);
        Release0610Checks.Run(Check);

        if (args.Length != 0 && args[0] == "--vanilla")
        {
            if (args.Length != 3 || !File.Exists(args[1]) || !File.Exists(args[2]))
                throw new Exception("Use --vanilla RIMWORLD_ASSEMBLY_CSHARP.dll MINTSTATSORT.dll.");
            NativeMenuChecks.Run(Check);
            NativeMenuChecks.Metadata(args[1], args[2], Check);
            WorkshopNotesChecks.Metadata(args[1], args[2], Check);
            using (var mod = ModuleDefinition.ReadModule(args[2])) PerformanceChecks.Metadata(mod, Check);
            CheckInstallScripts();
            Console.WriteLine("Passed " + count + " sorting, native menu, optional dependency and PowerShell checks.");
            return;
        }

        if (args.Length < 2 || !File.Exists(args[0]) || !File.Exists(args[1]))
            throw new Exception("Pass the Dubs Mint Menus 1.6 DLL and Nice Bill Tab DLL paths to check their real patch points.");
        using (var module = ModuleDefinition.ReadModule(args[0]))
        {
            var type = module.Types.Single(t => t.FullName == "DubsMintMenus.Patch_BillStack_DoListing");
            Check(type.Fields.Any(f => f.Name == "scrollPositionRecipes" && f.FieldType.FullName == "UnityEngine.Vector2") &&
                  type.Fields.Any(f => f.Name == "GizmoListRect" && f.FieldType.FullName == "UnityEngine.Rect"), "Dubs scroll and culling fields");
            var menu = type.Methods.Single(m => m.Name == "Dobills");
            Check(menu.IsStatic && menu.Parameters.Count == 3 && menu.Parameters[2].ParameterType.FullName.Contains("Verse.RecipeDef"), "Dubs recipe menu signature");
            var scrollCalls = Calls(menu).Where(m => m.DeclaringType.FullName == "Verse.Widgets" && m.Name == "BeginScrollView").ToList();
            Check(scrollCalls.Count == 1 && scrollCalls[0].Parameters.Count == 4 &&
                  scrollCalls[0].Parameters[1].ParameterType.FullName == "UnityEngine.Vector2&" &&
                  scrollCalls[0].Parameters[3].ParameterType.FullName == "System.Boolean", "Single supported scroll hook in actual Dubs DLL");
            var listing = type.Methods.Single(m => m.Name == "Doink");
            var enumerators = Calls(listing).Where(m => m.Name == "GetEnumerator" && m.DeclaringType.FullName.Contains("System.Collections.Generic.List`1<Verse.RecipeDef>")).ToList();
            Check(enumerators.Count == 1, "Single recipe enumeration hook after Dubs search/A-Z sorting");
            var row = type.Methods.Single(m => m.Name == "DoRow");
            Check(row.Parameters.Count == 3 && row.Parameters[2].ParameterType.FullName == "RimWorld.Precept_Building", "Row recipe/style signature");
            var labels = Calls(row).Where(m => m.DeclaringType.FullName == "Verse.Widgets" && m.Name == "Label" &&
                m.Parameters.Count == 2 && (m.Parameters[1].ParameterType.FullName == "System.String" || m.Parameters[1].ParameterType.FullName == "Verse.TaggedString")).ToList();
            Check(labels.Count > 0, "Row labels support numeric badges without replacing the bill action");
            Console.WriteLine("Dubs assembly checked: " + module.Assembly.Name);
        }
        using (var module = ModuleDefinition.ReadModule(args[1]))
        {
            var drawer = module.Types.Single(t => t.FullName == "NiceBillTab.TabBillsDrawer");
            var holder = module.Types.Single(t => t.FullName == "NiceBillTab.RecipeHolder");
            var settings = module.Types.Single(t => t.FullName == "NiceBillTab.Settings");
            Check(holder.Fields.Any(f => f.Name == "recipe" && !f.IsStatic && f.FieldType.FullName == "Verse.RecipeDef") &&
                  holder.Fields.Any(f => f.Name == "label" && !f.IsStatic && f.FieldType.FullName == "System.String"), "Nice holder recipe and label accessors");
            Check(holder.Fields.Any(f => f.Name == "style" && f.FieldType.FullName == "RimWorld.Precept_ThingStyle"), "Nice holds style variants separately");
            Check(drawer.Fields.Any(f => f.Name == "availableRecipes" && f.IsStatic && f.FieldType.FullName == "System.Collections.Generic.List`1<NiceBillTab.RecipeHolder>"), "Nice full recipe source");
            Check(drawer.Fields.Any(f => f.Name == "scrollPosition" && f.IsStatic && f.FieldType.FullName == "UnityEngine.Vector2") &&
                  drawer.Fields.Any(f => f.Name == "desiredYtoScrollRecipes" && f.IsStatic && f.FieldType.FullName == "System.Nullable`1<System.Single>"), "Nice scroll reset can cancel its animation");
            var menu = drawer.Methods.Single(m => m.Name == "DrawLeftPart");
            Check(menu.IsStatic && menu.ReturnType.FullName == "System.Void" &&
                  menu.Parameters.Select(p => p.ParameterType.FullName).SequenceEqual(new[] { "UnityEngine.Rect", "RimWorld.Building_WorkTable" }), "Nice workbench menu signature");
            var scrollCalls = Calls(menu).Where(m => m.DeclaringType.FullName == "Verse.Widgets" && m.Name == "BeginScrollView").ToList();
            Check(scrollCalls.Count == 1 && scrollCalls[0].Parameters.Select(p => p.ParameterType.FullName).SequenceEqual(new[]
                  { "UnityEngine.Rect", "UnityEngine.Vector2&", "UnityEngine.Rect", "System.Boolean" }), "Nice single supported scroll hook");
            int initialized = menu.Body.Instructions.ToList().FindIndex(i => i.OpCode == OpCodes.Stsfld && (i.Operand as FieldReference)?.Name == "availableRecipes");
            int beginScroll = menu.Body.Instructions.ToList().FindIndex(i => (i.OpCode == OpCodes.Call || i.OpCode == OpCodes.Callvirt) && (i.Operand as MethodReference)?.Name == "BeginScrollView");
            Check(initialized >= 0 && beginScroll > initialized, "Nice initializes recipes before the toolbar reads them");
            var listing = drawer.Methods.Single(m => m.Name == "DrawRecipeList");
            Check(listing.IsStatic && listing.ReturnType.FullName == "System.Single" &&
                  listing.Parameters.Select(p => p.ParameterType.FullName).SequenceEqual(new[]
                  { "System.Collections.Generic.List`1<NiceBillTab.RecipeHolder>", "System.Single", "System.Single", "System.Single" }), "Nice list hook preserves typed holders");
            Check(settings.Fields.Any(f => f.Name == "RecipeListMode" && f.IsStatic && f.FieldType.FullName == "System.Int32") &&
                  listing.Body.Instructions.Count(i => i.OpCode == OpCodes.Ldsfld && (i.Operand as FieldReference)?.FullName == "System.Int32 NiceBillTab.Settings::RecipeListMode") == 3,
                  "Nice global sorting can bypass all three tree-mode checks");
            foreach (string methodName in new[] { "DrawRecipePreview", "DrawRecipePreviewCompact" })
            {
                var row = drawer.Methods.Single(m => m.Name == methodName);
                Check(row.IsStatic && row.ReturnType.FullName == "System.Single" && row.Parameters.Select(p => p.ParameterType.FullName).SequenceEqual(new[]
                      { "UnityEngine.Rect", "NiceBillTab.RecipeHolder", "System.Boolean" }), "Nice card signature: " + methodName);
                Check(row.Body.Instructions.Count(i => i.OpCode == OpCodes.Ldfld && (i.Operand as FieldReference)?.FullName == "System.String NiceBillTab.RecipeHolder::label") == 1 &&
                      Calls(row).Any(m => m.Name == "CalcHeight" && m.DeclaringType.FullName == "Verse.Text") &&
                      Calls(row).Any(m => m.Name == "Label" && m.DeclaringType.FullName == "Verse.Widgets"), "Nice card measures decorated text before drawing: " + methodName);
                Check(Calls(row).Count(m => m.Name == "HandleRecipeClick" && m.DeclaringType.FullName == drawer.FullName) == 1, "Nice card keeps its original click/compare handler: " + methodName);
            }
            var click = drawer.Methods.Single(m => m.Name == "HandleRecipeClick");
            Check(click.IsStatic && click.ReturnType.FullName == "System.Void" &&
                  click.Parameters.Select(p => p.ParameterType.FullName).SequenceEqual(new[] { "UnityEngine.Rect", "NiceBillTab.RecipeHolder" }), "Nice tooltip hook uses the existing card hit area");
            Console.WriteLine("Nice assembly checked: " + module.Assembly.Name);
        }
        using (var mod = ModuleDefinition.ReadModule(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Mod/Assemblies/MintStatSort.dll"))))
            PerformanceChecks.Metadata(mod, Check);
        CheckInstallScripts();
        CheckScripts(args.Skip(2));
        Console.WriteLine("Passed " + count + " sorting, Dubs/Nice integration and PowerShell checks.");
    }
    private static void CheckInstallScripts()
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        CheckScripts(Directory.GetFiles(Path.Combine(root, "Windows"), "*.ps1"));
    }
    private static void CheckScripts(IEnumerable<string> scripts)
    {
        foreach (string script in scripts)
        {
            if (!File.Exists(script)) throw new FileNotFoundException("PowerShell script does not exist.", script);
            Parser.ParseFile(script, out _, out ParseError[] errors);
            Check(errors.Length == 0, "PowerShell syntax: " + script);
        }
    }
    private static IEnumerable<MethodReference> Calls(MethodDefinition method)
    {
        return method.Body.Instructions.Where(i => i.OpCode == OpCodes.Call || i.OpCode == OpCodes.Callvirt)
            .Select(i => i.Operand).OfType<MethodReference>();
    }
    private static List<Ranked<MultiSample>> Balanced(IEnumerable<MultiSample> rows, bool descending = true, params bool[] goals)
    {
        return SortingCore.Balanced(rows, r => r.Values, r => r.Name, goals.Length == 0 ? new[] { true, true } : goals, descending);
    }
    private static string MultiOrder(IEnumerable<Ranked<MultiSample>> rows) { return string.Join(",", rows.Select(r => r.Item.Id)); }
    private static void CheckMultipleGoals()
    {
        var rows = new[]
        {
            new MultiSample { Id = "sharp", Name = "Alpha", Values = new double?[] { 100, 0 } },
            new MultiSample { Id = "mixed", Name = "Balanced", Values = new double?[] { 20, 20 } },
            new MultiSample { Id = "blunt", Name = "Zeta", Values = new double?[] { 0, 100 } },
            new MultiSample { Id = "missing", Name = "A", Values = new double?[] { 1e100, null } }
        };
        var ranked = Balanced(rows);
        Check(MultiOrder(ranked) == "mixed,sharp,blunt,missing", "Combining sharp and blunt rewards balance over a single extreme");
        Check(MultiOrder(Balanced(rows, false)) == "sharp,blunt,mixed,missing", "Reverse balance still keeps missing values last");
        Check(ranked[0].Score.HasValue && Math.Abs(ranked[0].Score.Value - 20) < 1e-10, "Combined score is comparable points, not a raw sum of units");
        Check(ranked[1].Score == 0 && ranked[3].Score == null, "A known zero is different from unavailable attack data");
        Check(string.Join(",", rows.Select(r => r.Id)) == "sharp,mixed,blunt,missing", "Multi-criteria sorting cannot mutate source order");
        Check(ranked.All(r => rows.Any(original => ReferenceEquals(original, r.Item))), "Multi-criteria sorting preserves holder identity");
        var rescaled = rows.Select(r => new MultiSample { Id = r.Id, Name = r.Name, Values = new[] { r.Values[0] * 1000, r.Values[1] / 1000 } }).ToList();
        var scaledRank = Balanced(rescaled);
        Check(MultiOrder(scaledRank) == MultiOrder(ranked) && Math.Abs(scaledRank[0].Score.Value - ranked[0].Score.Value) < 1e-10, "Different stat units cannot give one goal more importance");
        var swapped = rows.Select(r => new MultiSample { Id = r.Id, Name = r.Name, Values = r.Values.Reverse().ToArray() });
        Check(MultiOrder(Balanced(swapped)) == MultiOrder(ranked), "Changing checkbox order does not make the first criterion dominate");
        var constant = rows.Select(r => new MultiSample { Id = r.Id, Name = r.Name, Values = r.Values.Concat(new double?[] { 42 }).ToArray() });
        var constantRank = Balanced(constant, true, true, true, true);
        Check(MultiOrder(constantRank) == MultiOrder(ranked) && Math.Abs(constantRank[0].Score.Value - ranked[0].Score.Value) < 1e-10, "A constant stat cannot dilute the meaningful goals");
        Check(Math.Abs(Balanced(rows.Take(3))[0].Score.Value - ranked[0].Score.Value) < 1e-10, "An incomplete outlier cannot distort normalization of comparable items");
        var damageWeight = new[]
        {
            new MultiSample { Id = "heavy", Name = "Heavy", Values = new double?[] { 100, 10 } },
            new MultiSample { Id = "light", Name = "Light", Values = new double?[] { 80, 2 } },
            new MultiSample { Id = "weak", Name = "Weak", Values = new double?[] { 20, 10 } }
        };
        Check(Balanced(damageWeight, true, true, false).First().Item.Id == "light", "More damage and less weight can be combined");
        var extremes = new[]
        {
            new MultiSample { Id = "low", Name = "Low", Values = new double?[] { -double.MaxValue, -double.MaxValue } },
            new MultiSample { Id = "middle", Name = "Middle", Values = new double?[] { 0, 0 } },
            new MultiSample { Id = "high", Name = "High", Values = new double?[] { double.MaxValue, double.MaxValue } },
            new MultiSample { Id = "nan", Name = "A", Values = new double?[] { double.NaN, 2 } },
            new MultiSample { Id = "infinity", Name = "B", Values = new double?[] { 1, double.PositiveInfinity } }
        };
        var extremeRank = Balanced(extremes);
        Check(MultiOrder(extremeRank) == "high,middle,low,nan,infinity", "Negative and extreme finite values remain sortable without overflow");
        Check(extremeRank.Take(3).All(r => r.Score.HasValue && SortingCore.Finite(r.Score.Value) && r.Score >= 0 && r.Score <= 100), "Combined scores stay finite and between zero and one hundred");
        Check(MultiOrder(Balanced(extremes, false)) == "low,middle,high,nan,infinity", "Invalid values stay last when reversing extreme scores");
        var equal = new[]
        {
            new MultiSample { Id = "B", Name = "Vest", Values = new double?[] { 2, 2 } },
            new MultiSample { Id = "A1", Name = "Helmet", Values = new double?[] { 2, 2 } },
            new MultiSample { Id = "A2", Name = "Helmet", Values = new double?[] { 2, 2 } }
        };
        Check(MultiOrder(Balanced(equal)) == "A1,A2,B" && Balanced(equal).All(r => r.Score == 100), "Equal criteria preserve style variants and stable ties");
        Check(Balanced(Array.Empty<MultiSample>()).Count == 0, "Empty filtered recipe list supports several goals");
        int calls = 0;
        SortingCore.Balanced(rows, r => { calls++; return r.Values; }, r => r.Name, new[] { true, true }, true);
        Check(calls == rows.Length, "Read each item's selected stats once per combined ranking");
        var unsupported = new[] { new MultiSample { Id = "missing", Name = "A", Values = new double?[] { null, null } } };
        Check(Balanced(unsupported).Single().Score == null, "No fully comparable candidates yields missing scores rather than an exception");
    }
    private static void CheckReferences()
    {
        var attacks = new[]
        {
            new AttackReading { Category = "Sharp", Damage = 12 },
            new AttackReading { Category = "Blunt", Damage = 18 },
            new AttackReading { Category = "Sharp", Damage = 20 },
            new AttackReading { Category = "Heat", Damage = 100 }
        };
        Check(ReferenceCore.HighestDamage(attacks, "Sharp") == 20 && ReferenceCore.HighestDamage(attacks, "Blunt") == 18,
            "Typed damage must not confuse blunt, sharp and heat attacks");
        Check(ReferenceCore.HighestDamage(attacks, null) == 100, "Overall hit damage includes all exposed attack types");
        Check(ReferenceCore.HighestDamage(attacks.Take(1), "Blunt") == 0, "Known weapon without blunt attacks reports zero rather than unavailable");
        Check(ReferenceCore.HighestDamage(Array.Empty<AttackReading>(), "Sharp") == null, "Custom weapons with no standard attacks report unavailable");
        Check(ReferenceCore.HighestDamage(new[] { new AttackReading { Category = "Sharp", Damage = double.NaN } }, "Sharp") == null,
            "Non-finite damage is not a valid zero");
        Check(ReferenceCore.HighestDamage(new[] { new AttackReading { Category = "Sharp", Damage = -10 } }, "Sharp") == null,
            "Negative damage is not a valid hit reference");
        Check(ReferenceCore.HighestDamage(new[] { new AttackReading { Category = "Sharp", Damage = 0 } }, "Sharp") == 0,
            "A zero-damage standard attack remains a known zero");
        var stocks = new[]
        {
            new StockSample { Count = 100, Spawned = true, Stored = true },
            new StockSample { Count = 30, Spawned = true, Stored = true },
            new StockSample { Count = 50, Spawned = true, Stored = false },
            new StockSample { Count = 10, Spawned = false, Stored = false },
            new StockSample { Count = 999, Spawned = false, Stored = true },
            new StockSample { Count = 0, Spawned = true, Stored = true }
        };
        Check(ReferenceCore.StoredQuantity(stocks, s => s.Count, s => s.Spawned, s => s.Stored) == 130,
            "Storage totals include stockpiles and shelves, excluding ground drops, carried items and despawned contents");
        Check(ReferenceCore.StoredQuantity(Array.Empty<StockSample>(), s => s.Count, s => s.Spawned, s => s.Stored) == 0,
            "An empty colony storage has no materials");
        var large = Enumerable.Repeat(new StockSample { Count = int.MaxValue, Spawned = true, Stored = true }, 2);
        Check(ReferenceCore.StoredQuantity(large, s => s.Count, s => s.Spawned, s => s.Stored) == 2L * int.MaxValue,
            "Modded large stockpiles do not overflow a 32-bit material count");
    }
}
