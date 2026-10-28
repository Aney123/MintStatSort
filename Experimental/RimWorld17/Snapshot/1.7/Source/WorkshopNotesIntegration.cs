using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Verse;
using Verse.Steam;

namespace MintStatSort
{
    internal static class WorkshopNotesIntegration
    {
        private static readonly FieldInfo UploadingHook = AccessTools.Field(typeof(Workshop), "uploadingHook");
        private static readonly FieldInfo Owner = AccessTools.Field(typeof(WorkshopItemHook), "owner");
        private static readonly MethodInfo Selector = AccessTools.Method(typeof(WorkshopNotesIntegration), nameof(SelectNotes));
        private static bool IsSubmit(MethodInfo method)
        {
            return method?.DeclaringType?.FullName == "Steamworks.SteamUGC" && method.Name == "SubmitItemUpdate" &&
                method.IsStatic && method.GetParameters().Length == 2 && method.GetParameters()[1].ParameterType == typeof(string);
        }
        internal static void Install(Harmony harmony)
        {
            if (UploadingHook == null || !UploadingHook.IsStatic || UploadingHook.FieldType != typeof(WorkshopItemHook) || Owner == null)
                throw new MissingFieldException("RimWorld Workshop upload contract changed");
            try
            {
                foreach (string name in new[] { "Upload", "OnItemCreated" })
                {
                    var method = AccessTools.Method(typeof(Workshop), name);
                    if (method == null) throw new MissingMethodException("Workshop." + name);
                    harmony.Patch(method, transpiler: new HarmonyMethod(typeof(WorkshopNotesIntegration), nameof(InjectNotes)));
                }
                var metadataMethod = AccessTools.Method(typeof(Workshop), "SetWorkshopItemDataFrom");
                if (metadataMethod == null) throw new MissingMethodException("Workshop.SetWorkshopItemDataFrom");
                harmony.Patch(metadataMethod, transpiler: new HarmonyMethod(typeof(WorkshopNotesIntegration), nameof(InjectMetadata)));
            }
            catch { harmony.UnpatchAll(harmony.Id); throw; }
        }
        internal static IEnumerable<CodeInstruction> InjectNotes(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            var submits = code.Where(c => c.opcode == OpCodes.Call && c.operand is MethodInfo method && IsSubmit(method)).ToList();
            if (submits.Count != 1) throw new InvalidOperationException("Expected one Workshop SubmitItemUpdate call");
            foreach (var instruction in code)
            {
                if (ReferenceEquals(instruction, submits[0]))
                {
                    // Stack: original update handle, original note. Keep the native
                    // submission and change only its note for this uploaded mod.
                    var loadHook = new CodeInstruction(OpCodes.Ldsfld, UploadingHook);
                    loadHook.labels.AddRange(instruction.labels); instruction.labels.Clear();
                    loadHook.blocks.AddRange(instruction.blocks); instruction.blocks.Clear();
                    yield return loadHook;
                    yield return new CodeInstruction(OpCodes.Call, Selector);
                }
                yield return instruction;
            }
        }
        internal static string SelectNotes(string original, WorkshopItemHook hook)
        {
            try
            {
                var metadata = hook == null ? null : Owner.GetValue(hook) as ModMetaData;
                if (metadata == null || !string.Equals(metadata.PackageIdNonUnique, "mintstatsort.recipes", StringComparison.OrdinalIgnoreCase)) return original;
                string path = Path.Combine(hook.Directory.FullName, "Publicacion-Workshop", "notas-actualizacion-steam.txt");
                var file = new FileInfo(path);
                if (!file.Exists || file.Length > WorkshopNotesCore.MaximumLength) return original;
                return WorkshopNotesCore.Select(original, metadata.PackageIdNonUnique, metadata.ModVersion, File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                Log.Warning("[MintStatSort] Could not read this upload's change notes; keeping RimWorld's note: " + ex.GetType().Name);
                return original;
            }
        }
        internal static bool ShouldUpdateDescription(bool creating, WorkshopItemHook hook)
            => creating || SelectDescription(null, hook) != null;
        internal static string SelectDescription(string original, WorkshopItemHook hook)
        {
            try
            {
                var metadata = hook == null ? null : Owner.GetValue(hook) as ModMetaData;
                if (metadata == null || !string.Equals(metadata.PackageIdNonUnique, "mintstatsort.recipes", StringComparison.OrdinalIgnoreCase)) return original;
                string root = Path.Combine(hook.Directory.FullName, "Publicacion-Workshop");
                var manifest = new FileInfo(Path.Combine(root, "publicacion-version.txt"));
                var description = new FileInfo(Path.Combine(root, "descripcion-workshop-es.txt"));
                if (!manifest.Exists || manifest.Length > 128 || !description.Exists || description.Length > WorkshopDescriptionCore.MaximumBytes + 3) return original;
                return WorkshopDescriptionCore.Select(original, metadata.PackageIdNonUnique, metadata.ModVersion,
                    File.ReadAllText(manifest.FullName), File.ReadAllText(description.FullName));
            }
            catch (Exception ex)
            {
                Log.Warning("[MintStatSort] Could not read the prepared Workshop description; retaining RimWorld's behavior: " + ex.GetType().Name);
                return original;
            }
        }
        internal static IEnumerable<CodeInstruction> InjectMetadata(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            var getters = code.Where(c => (c.opcode == OpCodes.Callvirt || c.opcode == OpCodes.Call) &&
                c.operand is MethodInfo method && method.DeclaringType == typeof(WorkshopItemHook) && method.Name == "get_Description").ToList();
            if (getters.Count != 1) throw new InvalidOperationException("Expected one native Workshop description getter");
            int getterIndex = code.IndexOf(getters[0]);
            var gates = code.Take(getterIndex).Where((c, index) => c.opcode == OpCodes.Ldarg_2 &&
                index + 1 < code.Count && code[index + 1].opcode.FlowControl == FlowControl.Cond_Branch).ToList();
            if (gates.Count != 1) throw new InvalidOperationException("Expected one native new-item description condition");
            foreach (var instruction in code)
            {
                yield return instruction;
                if (ReferenceEquals(instruction, gates[0]))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(WorkshopNotesIntegration), nameof(ShouldUpdateDescription)));
                }
                if (ReferenceEquals(instruction, getters[0]))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(WorkshopNotesIntegration), nameof(SelectDescription)));
                }
            }
        }
    }
}
