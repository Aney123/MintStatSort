using System;
using System.Linq;
using MintStatSort;
using Mono.Cecil;
using Mono.Cecil.Cil;

internal static class WorkshopNotesChecks
{
    internal static void Run(Action<bool, string> check)
    {
        string description = "[h1]Compara recetas[/h1]\nArmaduras y prendas.";
        check(WorkshopDescriptionCore.Select("old", "mintstatsort.recipes", "0.6.9", "MintStatSort 0.6.9\n", description) == description, "Prepared Workshop description is selected only for the matching MintStatSort release");
        check(WorkshopDescriptionCore.Select("old", "another.mod", "0.6.9", "MintStatSort 0.6.9", description) == "old", "Prepared description cannot affect another mod");
        check(WorkshopDescriptionCore.Select("old", "mintstatsort.recipes", "0.6.9", "MintStatSort 0.6.8", description) == "old", "An older publication manifest cannot overwrite the description");
        foreach (string invalid in new[] { "", " ", "bad\0text", new string('ñ', 4000) })
            check(WorkshopDescriptionCore.Select("old", "mintstatsort.recipes", "0.6.9", "MintStatSort 0.6.9", invalid) == "old", "Empty, malformed or oversized UTF-8 descriptions keep the native value");
        check(WorkshopDescriptionCore.Select(null, "mintstatsort.recipes", "0.6.9", "MintStatSort 0.6.9", null) == null, "Missing description does not enable a metadata update");
        const string original = "[Auto-generated text]: Update on 10/9/2026.";
        const string notes = "MintStatSort 0.6.3\nCalidad y notas automáticas.\n- Cambios reales.";
        check(WorkshopNotesCore.Select(original, "mintstatsort.recipes", "0.6.3", notes) == notes, "The uploaded version selects its complete Spanish note");
        check(WorkshopNotesCore.Select(original, "MintStatSort.Recipes", "0.6.3", "\uFEFF" + notes.Replace("\n", "\r\n")) == notes.Replace("\n", "\r\n"), "Package case, UTF-8 BOM and Windows newlines are supported");
        check(WorkshopNotesCore.Select(original, "other.mod", "0.6.3", notes) == original, "Other Workshop mods retain their original note");
        check(WorkshopNotesCore.Select(original, "mintstatsort.recipes", "0.6.2", notes) == original, "A different uploaded version cannot receive stale notes");
        check(WorkshopNotesCore.Select(original, "mintstatsort.recipes", "0.6.30", notes) == original, "Version matching uses equality rather than a prefix");
        foreach (string invalid in new[] { null, "", " ", "MintStatSort 0.6.3", "MintStatSort 0.6.3\n  ", notes + "\0", notes + new string('x', WorkshopNotesCore.MaximumLength) })
            check(WorkshopNotesCore.Select(original, "mintstatsort.recipes", "0.6.3", invalid) == original, "Missing or malformed note preserves publication flow");
        check(WorkshopNotesCore.Select("My explicit note", "mintstatsort.recipes", "0.6.3", notes) == "My explicit note", "An explicit note supplied by another publisher patch is respected");
        check(WorkshopNotesCore.Select(null, "mintstatsort.recipes", "0.6.3", notes) == null, "Null original note is preserved");
    }
    internal static void Metadata(string gamePath, string modPath, Action<bool, string> check)
    {
        using var game = ModuleDefinition.ReadModule(gamePath);
        var workshop = game.Types.Single(t => t.FullName == "Verse.Steam.Workshop");
        check(workshop.Fields.Any(f => f.Name == "uploadingHook" && f.IsStatic && f.FieldType.FullName == "Verse.Steam.WorkshopItemHook"), "Actual game exposes the current upload hook contract");
        var hook = game.Types.Single(t => t.FullName == "Verse.Steam.WorkshopItemHook");
        check(hook.Fields.Any(f => f.Name == "owner" && f.FieldType.Name == "WorkshopUploadable"), "Hook owner identifies the uploaded mod, rather than the active installation");
        foreach (string name in new[] { "Upload", "OnItemCreated" })
        {
            var method = workshop.Methods.Single(m => m.Name == name);
            var submits = method.Body.Instructions.Where(i => i.OpCode == OpCodes.Call && i.Operand is MethodReference m && m.DeclaringType.FullName == "Steamworks.SteamUGC" && m.Name == "SubmitItemUpdate").ToList();
            check(submits.Count == 1 && ((MethodReference)submits[0].Operand).Parameters[1].ParameterType.FullName == "System.String", "Exactly one native note parameter at Workshop." + name);
        }
        using var mod = ModuleDefinition.ReadModule(modPath);
        var integration = mod.Types.Single(t => t.Name == "WorkshopNotesIntegration");
        check(!integration.Methods.Where(m => m.HasBody).SelectMany(m => m.Body.Instructions).Any(i => i.Operand is MethodReference m && m.DeclaringType.FullName == "Steamworks.SteamUGC"), "Notes integration cannot start a Steam upload itself");
        var settings = mod.Types.Single(t => t.Name == "SortSettings");
        check(settings.Fields.Any(f => f.Name == "Quality" && f.FieldType.Name == "QualityCategory"), "Reference quality uses the game's enum");
        check(settings.Methods.Single(m => m.Name == "ExposeData").Body.Instructions.Any(i => Equals(i.Operand, "comparisonQuality")), "Reference quality has its own saved setting");
    }
}
