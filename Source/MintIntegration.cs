using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace MintStatSort
{
    internal sealed class RowContext
    {
        public RecipeDef Recipe;
        public string ExpectedLabel;
    }
    internal static class MintIntegration
    {
        internal static bool Ready;
        internal static FieldInfo ScrollField;
        private static FieldInfo cullingField;
        private static List<Building> currentTables = new List<Building>();
        [ThreadStatic] private static RowContext row;
        internal static void Install(Harmony harmony)
        {
            var type = AccessTools.TypeByName("DubsMintMenus.Patch_BillStack_DoListing");
            if (type == null) throw new InvalidOperationException("No se encuentra Dubs Mint Menus.");
            ScrollField = AccessTools.Field(type, "scrollPositionRecipes");
            cullingField = AccessTools.Field(type, "GizmoListRect");
            if (ScrollField?.FieldType != typeof(Vector2) || cullingField?.FieldType != typeof(Rect))
                throw new InvalidOperationException("Dubs Mint Menus: no se reconoce el área de desplazamiento.");
            try
            {
                harmony.Patch(AccessTools.Method(type, "Dobills", new[] { typeof(Rect), typeof(HashSet<Building>), typeof(List<RecipeDef>) }),
                    prefix: new HarmonyMethod(typeof(MintIntegration), nameof(BeforeMenu)),
                    transpiler: new HarmonyMethod(typeof(MintIntegration), nameof(MenuTranspiler)));
                harmony.Patch(AccessTools.Method(type, "Doink", new[] { typeof(Rect), typeof(List<RecipeDef>), typeof(HashSet<Building>) }),
                    transpiler: new HarmonyMethod(typeof(MintIntegration), nameof(ListTranspiler)));
                harmony.Patch(AccessTools.Method(type, "DoRow", new[] { typeof(RecipeDef), typeof(HashSet<Building>), typeof(Precept_Building) }),
                    prefix: new HarmonyMethod(typeof(MintIntegration), nameof(BeforeRow)),
                    finalizer: new HarmonyMethod(typeof(MintIntegration), nameof(AfterRow)),
                    transpiler: new HarmonyMethod(typeof(MintIntegration), nameof(RowTranspiler)));
                Ready = true;
                Log.Message("[Mint Stat Sort] Integración de recetas conectada a Dubs Mint Menus.");
            }
            catch
            {
                Ready = false;
                harmony.UnpatchAll(harmony.Id);
                throw;
            }
        }
        private static void BeforeMenu(HashSet<Building> __1, List<RecipeDef> __2)
        {
            if (Ready && RecipeSession.Options.Enabled)
            {
                currentTables = __1?.Where(b => b != null).ToList() ?? new List<Building>();
                RecipeSession.SetMap(__1?.FirstOrDefault(b => b?.Map != null)?.Map ?? Find.CurrentMap);
                RecipeSession.SetBench(BenchKey(currentTables));
                RecipeSession.SetRecipes(__2);
            }
        }
        private static IEnumerable<CodeInstruction> MenuTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            int count = 0;
            foreach (var code in codes)
            {
                if ((code.opcode == OpCodes.Call || code.opcode == OpCodes.Callvirt) && code.operand is MethodInfo method &&
                    method.DeclaringType == typeof(Widgets) && method.Name == "BeginScrollView" &&
                    method.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { typeof(Rect), typeof(Vector2).MakeByRefType(), typeof(Rect), typeof(bool) }))
                { code.opcode = OpCodes.Call; code.operand = AccessTools.Method(typeof(MintIntegration), nameof(BeginScroll)); count++; }
            }
            if (count != 1) throw new InvalidOperationException("Dubs Mint Menus: no se reconoce el área de recetas (BeginScrollView).");
            return codes;
        }
        private static void BeginScroll(Rect viewport, ref Vector2 scroll, Rect content, bool showScrollbars)
        {
            if (Ready && RecipeSession.Options.Enabled && viewport.height > Toolbar.Height + 40f)
            {
                var tables = currentTables.ToList();
                Toolbar.Draw(new Rect(viewport.x, viewport.y, viewport.width - 1f, Toolbar.Height),
                    () => tables.Count > 0 && tables.All(t => t.Spawned && !t.Destroyed && t.Map == Find.CurrentMap && Find.Selector.IsSelected(t)),
                    tables.Count == 1 ? tables[0].LabelCap.ToString() : "Dubs Mint Menus", BenchKey(tables));
                viewport.y += Toolbar.Height;
                viewport.height -= Toolbar.Height;
                var culling = (Rect)cullingField.GetValue(null);
                culling.height = viewport.height;
                cullingField.SetValue(null, culling);
            }
            Widgets.BeginScrollView(viewport, ref scroll, content, showScrollbars);
        }
        private static string BenchKey(List<Building> tables)
        {
            string first = tables.FirstOrDefault()?.def.defName;
            return first != null && tables.All(t => t.def.defName == first) ? first : null;
        }
        private static IEnumerable<CodeInstruction> ListTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var result = new List<CodeInstruction>();
            int count = 0;
            var enumerator = AccessTools.Method(typeof(List<RecipeDef>), "GetEnumerator");
            foreach (var code in instructions)
            {
                if (code.Calls(enumerator))
                {
                    var sort = new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(RecipeSession), nameof(RecipeSession.SortForDisplay), new[] { typeof(List<RecipeDef>) }));
                    sort.labels.AddRange(code.labels); code.labels.Clear();
                    sort.blocks.AddRange(code.blocks); code.blocks.Clear();
                    result.Add(sort);
                    count++;
                }
                result.Add(code);
            }
            if (count != 1) throw new InvalidOperationException("Dubs Mint Menus: no se reconoce el recorrido de recetas.");
            return result;
        }
        private static void BeforeRow(RecipeDef __0, Precept_Building __2, out RowContext __state)
        {
            __state = row;
            row = new RowContext
            {
                Recipe = __0,
                ExpectedLabel = __2 == null ? __0.LabelCap.ToString() : "RecipeMake".Translate(__2.LabelCap).CapitalizeFirst()
            };
        }
        private static Exception AfterRow(Exception __exception, RowContext __state)
        {
            row = __state;
            return __exception;
        }
        private static IEnumerable<CodeInstruction> RowTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            int count = 0;
            foreach (var code in codes)
            {
                if ((code.opcode != OpCodes.Call && code.opcode != OpCodes.Callvirt) || !(code.operand is MethodInfo method) ||
                    method.DeclaringType != typeof(Widgets) || method.Name != "Label") continue;
                var parameters = method.GetParameters().Select(p => p.ParameterType).ToArray();
                string replacement = parameters.SequenceEqual(new[] { typeof(Rect), typeof(string) }) ? nameof(LabelString) :
                    parameters.SequenceEqual(new[] { typeof(Rect), typeof(TaggedString) }) ? nameof(LabelTagged) : null;
                if (replacement == null) continue;
                code.opcode = OpCodes.Call;
                code.operand = AccessTools.Method(typeof(MintIntegration), replacement);
                count++;
            }
            if (count == 0) throw new InvalidOperationException("Dubs Mint Menus: no se reconoce la etiqueta de receta.");
            return codes;
        }
        private static void LabelTagged(Rect rect, TaggedString label) { LabelString(rect, label.ToString()); }
        private static void LabelString(Rect rect, string label)
        {
            if (Ready && RecipeSession.Options.Enabled && row != null && label == row.ExpectedLabel)
            {
                if (RecipeSession.HasComparison)
                {
                    var value = RecipeSession.Comparison(row.Recipe);
                    bool wordWrap = Text.WordWrap;
                    try
                    {
                        Text.WordWrap = false;
                        Widgets.Label(rect, (value.Display + " · " + label).Truncate(Math.Max(10f, rect.width - 2f)));
                        TooltipHandler.TipRegion(rect.LeftPartPixels(Math.Min(75f, rect.width)), value.Detail);
                    }
                    finally { Text.WordWrap = wordWrap; }
                    return;
                }
            }
            Widgets.Label(rect, label);
        }
    }
}
