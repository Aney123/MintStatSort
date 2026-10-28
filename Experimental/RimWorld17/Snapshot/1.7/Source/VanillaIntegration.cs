using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace MintStatSort
{
    // Preserve the game's option factory, bill actions, warnings and ideology styles.
    // Only replace the window opened by the native workbench's Add bill button.
    // Native source: Chillu1/RimWorldDecompiled, RimWorld/BillStack.cs and ITab_Bills.cs.
    internal static class VanillaIntegration
    {
        internal static bool Ready;
        internal sealed class MenuContext
        {
            internal Building_WorkTable Table;
            internal BillStack Stack;
        }
        internal sealed class ListingState
        {
            internal MenuContext Previous;
        }
        [ThreadStatic] private static MenuContext context;

        internal static void Install(Harmony harmony)
        {
            var method = AccessTools.Method(typeof(BillStack), nameof(BillStack.DoListing), new[]
            {
                typeof(Rect), typeof(Func<List<FloatMenuOption>>), typeof(Vector2).MakeByRefType(), typeof(float).MakeByRefType()
            });
            if (method == null) throw new InvalidOperationException("RimWorld: no se reconoce la lista nativa de órdenes.");
            try
            {
                harmony.Patch(method, prefix: new HarmonyMethod(typeof(VanillaIntegration), nameof(BeforeListing)),
                    transpiler: new HarmonyMethod(typeof(VanillaIntegration), nameof(MenuTranspiler)),
                    finalizer: new HarmonyMethod(typeof(VanillaIntegration), nameof(AfterListing)));
                Ready = true;
                Log.Message("[Mint Stat Sort] Selector conectado al menú normal de RimWorld.");
            }
            catch { Ready = false; harmony.UnpatchAll(harmony.Id); throw; }
        }

        private static void BeforeListing(BillStack __instance, out ListingState __state)
        {
            __state = new ListingState { Previous = context };
            context = Ready && RecipeSession.Options.Enabled && __instance.billGiver is Building_WorkTable table
                ? new MenuContext { Table = table, Stack = __instance } : null;
        }
        private static Exception AfterListing(Exception __exception, ListingState __state)
        {
            // A preceding prefix may skip ours, while Harmony still runs finalizers.
            // In that case do not erase the context of an outer native listing.
            if (__state != null) context = __state.Previous;
            return __exception;
        }

        private static IEnumerable<CodeInstruction> MenuTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            var constructor = AccessTools.Constructor(typeof(FloatMenu), new[] { typeof(List<FloatMenuOption>) });
            int count = 0;
            for (int i = 0; i < codes.Count; i++)
            {
                var code = codes[i];
                if (code.opcode != OpCodes.Newobj || !Equals(code.operand, constructor)) continue;
                var next = codes.Skip(i + 1).FirstOrDefault(candidate => candidate.opcode != OpCodes.Nop);
                if (!(next?.operand is MethodInfo consumer) || consumer.DeclaringType != typeof(WindowStack)
                    || consumer.Name != nameof(WindowStack.Add) || consumer.GetParameters().Length != 1
                    || consumer.GetParameters()[0].ParameterType != typeof(Window))
                    throw new InvalidOperationException("RimWorld: la apertura del menú de recetas ha cambiado.");
                // Editing this instruction retains its labels and exception blocks.
                code.opcode = OpCodes.Call;
                code.operand = AccessTools.Method(typeof(VanillaIntegration), nameof(CreateMenu));
                count++;
            }
            if (count != 1) throw new InvalidOperationException("RimWorld: no se reconoce el selector nativo de recetas.");
            return codes;
        }

        internal static Window CreateMenu(List<FloatMenuOption> options)
        {
            if (Ready && RecipeSession.Options.Enabled && context != null)
            {
                try { return new NativeRecipeWindow(context.Table, context.Stack, options); }
                catch (Exception ex)
                { Log.WarningOnce("[Mint Stat Sort] Could not open the native recipe selector; retaining the game's menu: " + ex, 15904401); }
            }
            return new FloatMenu(options);
        }
        internal static bool IsNativeClosure(Type type)
        {
            // Read only compiler-generated captures belonging to the native bills tab.
            if (!type.IsDefined(typeof(CompilerGeneratedAttribute), false)) return false;
            for (var owner = type; owner != null; owner = owner.DeclaringType)
                if (owner == typeof(ITab_Bills)) return type != owner;
            return false;
        }
    }

    internal sealed class NativeRecipeWindow : ComparisonWindow
    {
        private readonly Building_WorkTable table;
        private readonly BillStack stack;
        internal NativeRecipeWindow(Building_WorkTable table, BillStack stack, List<FloatMenuOption> options)
            : base(CreateRows(table, options), table.Map, null, table.LabelCap.ToString(), true)
        {
            this.table = table;
            this.stack = stack;
            SetBenchContext(table.def.defName);
        }
        private static List<ComparisonRow> CreateRows(Building_WorkTable table, List<FloatMenuOption> options)
        {
            var recipes = new HashSet<RecipeDef>(table.def.AllRecipes.Where(recipe => recipe != null));
            var rows = NativeMenuCore.OriginalOrder(options.Where(option => option != null), option => (int)option.Priority,
                option => option.orderInPriority).Select(option =>
            {
                option.SetSizeMode(FloatMenuSizeMode.Normal);
                return new ComparisonRow
                {
                    Option = option, Label = option.Label,
                    Recipe = NativeMenuCore.ResolveRecipe(new Delegate[] { option.action, option.mouseoverGuiAction, option.extraPartOnGUI },
                        recipes, VanillaIntegration.IsNativeClosure)
                };
            }).ToList();
            if (!rows.Any(row => row.Recipe != null))
                throw new InvalidOperationException("No native recipe captures were found. Unknown menu actions cannot be guessed from their labels.");
            return rows;
        }
        private bool ValidTable()
            => !table.Destroyed && table.Spawned && table.Map == Find.CurrentMap
                && ReferenceEquals(Find.Selector.SingleSelectedThing, table) && ReferenceEquals(table.BillStack, stack)
                && stack.Count < BillStack.MaxCount;
        protected override bool ContextValid() => ValidTable();
        protected override bool RecipeAvailable(RecipeDef recipe)
            => recipe == null || (recipe.AvailableNow && recipe.AvailableOnNow(table));
    }
}
