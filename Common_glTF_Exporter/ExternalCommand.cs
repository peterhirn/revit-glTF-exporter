namespace Revit_glTF_Exporter
{
    using Autodesk.Revit.ApplicationServices;
    using Autodesk.Revit.Attributes;
    using Autodesk.Revit.DB;
    using Autodesk.Revit.DB.Events;
    using Autodesk.Revit.UI;
    using Common_glTF_Exporter.Core;
    using Common_glTF_Exporter.Utils;
    using Microsoft.Win32;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.IO;
    using System.Text;
    using System.Text.RegularExpressions;

    static class Export
    {
        static readonly HashSet<string> Need3DGeometry = [
            "Generic Analyzer",
            "CCM Vertical Base Plate (ATU-PVT-GATE-OP4)",
            "cobas pro i 601 Waste Container",
            "cobas pulse",
            "Stago sthemO 301-LAS",
            "Sysmex XN-9100 - Side panel L",
            "Sysmex XN-9100 - Side panel R",
            "Sysmex XR-9000 - XR-10 on CV-50",
            "Sysmex XR-9000 - XR-20 XR-20 on CV-55",
            "Sysmex XR-9000 - XR-20 on CV-50",
            "Powervar - UPS 0.8 kVA"
        ];

        public static void SetCategoryVisibility(Document doc, View view)
        {
            var category = doc.Settings.Categories.get_Item(BuiltInCategory.OST_SpecialityEquipment);
            var subCategories = category?.SubCategories.Cast<Category>();

            var geometry = subCategories?.FirstOrDefault(c => c.Name == "3D Geometry");
            var serviceArea = subCategories?.FirstOrDefault(c => c.Name == "Working & Service Area");

            using var tx = new Transaction(doc, "Set visibility");
            tx.Start();

            // Enable "Preview Visiblity"
            view.TemporaryViewModes.PreviewFamilyVisibility = PreviewFamilyVisibilityMode.On;

            if (geometry is not null || serviceArea is not null)
            {
                if (geometry is not null && Need3DGeometry.Contains(doc.Title))
                {
                    // Show "3D Geometry"
                    view.SetCategoryHidden(geometry.Id, false);
                }

                // Hide "Working & Service Area"
                if (serviceArea is not null) view.SetCategoryHidden(serviceArea.Id, true);
            }

            tx.Commit();
        }

        public static void View(Document doc, View view, string path)
        {
            SettingsConfig.SetValue("materials", "true");
            SettingsConfig.SetValue("format", "glb");
            //SettingsConfig.SetValue("format", "gltf");
            SettingsConfig.SetValue("normals", "true");
            SettingsConfig.SetValue("levels", "false");
            SettingsConfig.SetValue("lights", "false");
            SettingsConfig.SetValue("grids", "false");
            SettingsConfig.SetValue("batchId", "false");
            //SettingsConfig.SetValue("properties", "true");
            SettingsConfig.SetValue("properties", "false");
            //SettingsConfig.SetValue("relocateTo0", "true");
            SettingsConfig.SetValue("relocateTo0", "false");
            SettingsConfig.SetValue("flipAxis", "true");
            //SettingsConfig.SetValue("flipAxis", "false");
            SettingsConfig.SetValue("units", "autodesk.unit.unit:meters-1.0.0");
            //SettingsConfig.SetValue("compression", "none");
            SettingsConfig.SetValue("compression", "Meshopt");
            SettingsConfig.SetValue("path", path);
            SettingsConfig.SetValue("fileName", Path.GetFileName(path));
            SettingsConfig.SetValue("user", "");
            SettingsConfig.SetValue("release", "");
            SettingsConfig.SetValue("isRFA", "false");

            var ctx = new GLTFExportContext(doc, view, true);

            /// NOTE: Disposing this object (ie. `using`) increases the export runtime considerably.
            /// It's more efficient to run the export and then kill Revit.exe
            var exporter = new CustomExporter(doc, ctx)
            {
                ShouldStopOnError = false
            };
            exporter.Export(view);
        }

        public static void Family(Log log, Document doc, string target, SortedDictionary<string, string> mapping, HashSet<string> typeNames)
        {
            if (!doc.IsFamilyDocument) throw new InvalidOperationException("Document is not a family");
            if (doc.FamilyManager is null) throw new InvalidOperationException("FamilyManager is null");

            using var stencilIdParameter =
                doc.FamilyManager.Parameters.Cast<FamilyParameter>().FirstOrDefault(p => p.Definition.Name == "06 Stencil ID")
                ?? throw new InvalidOperationException("Missing StencilId parameter");

            using var collector = new FilteredElementCollector(doc).OfClass(typeof(View3D));
            using var view = collector.Cast<View3D>().FirstOrDefault() ?? throw new InvalidOperationException("Missing 3d view");

            SetCategoryVisibility(doc, view);

            var familyTypes = doc.FamilyManager.Types.Cast<FamilyType>().Where(t => !string.IsNullOrWhiteSpace(t.Name)).ToList();
            if (familyTypes.Count == 0)
            {
                try
                {
                    log.Info($"Family has no types '{doc.Title}'");

                    var stencilId = doc.FamilyManager.Types.Cast<FamilyType>().FirstOrDefault()?.AsString(stencilIdParameter);
                    if (string.IsNullOrEmpty(stencilId))
                    {
                        log.Debug($"Missing stencil id '{doc.Title}'");
                        return;
                    }

                    if (!mapping.TryAdd(stencilId, doc.Title))
                    {
                        log.Warn($"Duplicated stencil id {stencilId} '{doc.Title}' '{mapping[stencilId]}'");
                        return;
                    }

                    if (!typeNames.Add(doc.Title))
                    {
                        log.Warn($"Duplicated type name '{doc.Title}'");
                        return;
                    }

                    var path = Path.Combine(target, doc.Title);
                    Export.View(doc, view, path);

                    var exportedPath = path + ".glb";
                    if (!File.Exists(exportedPath))
                    {
                        log.Warn($"Export created no file '{doc.Title}' {exportedPath}");
                        mapping.Remove(stencilId);
                        typeNames.Remove(doc.Title);
                    }
                }
                catch (Exception ex)
                {
                    log.Error($"Failed to export base type '{doc.Title}' {ex.Message}");
                }
            }
            else
            {
                var typesFolder = Path.Combine(target, doc.Title);
                Directory.CreateDirectory(typesFolder);

                foreach (var type in familyTypes)
                {
                    try
                    {
                        var name = $"{doc.Title}/{type.Name}";
                        var stencilId = type.AsString(stencilIdParameter);
                        if (string.IsNullOrEmpty(stencilId))
                        {
                            log.Debug($"Missing stencil id '{name}'");
                            continue;
                        }

                        if (!mapping.TryAdd(stencilId, name))
                        {
                            log.Warn($"Duplicated stencil id {stencilId} '{name}' '{mapping[stencilId]}'");
                            continue;
                        }

                        if (!typeNames.Add(name))
                        {
                            log.Warn($"Duplicated type name '{name}'");
                            continue;
                        }

                        using var tx = new Transaction(doc, "Set family type");
                        tx.Start();
                        doc.FamilyManager.CurrentType = type;
                        tx.Commit();

                        var path = Path.Combine(typesFolder, type.Name);
                        Export.View(doc, view, path);

                        var exportedPath = path + ".glb";
                        if (!File.Exists(exportedPath))
                        {
                            log.Warn($"Export created no file '{name}' {exportedPath}");
                            mapping.Remove(stencilId);
                            typeNames.Remove(type.Name);
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Error($"Failed to export type '{type.Name}' '{doc.Title}' {ex.Message}");
                    }

                    if (Directory.Exists(typesFolder) && !Directory.EnumerateFileSystemEntries(typesFolder).Any())
                    {
                        Directory.Delete(typesFolder);
                    }
                }
            }
        }

        public static EventHandler<FailuresProcessingEventArgs> CreateFailureProcessing(Log log)
        {
            return (object? sender, FailuresProcessingEventArgs e) =>
            {
                var fa = e?.GetFailuresAccessor();
                if (fa == null) return;

                var failures = fa.GetFailureMessages();
                var deletedElements = false;

                foreach (var failure in failures)
                {
                    var severity = failure.GetSeverity();
                    var message = failure.GetDescriptionText();

                    var elementIds = failure.GetFailingElementIds().ToList();

                    log.Warn($"{severity}: {message} [{string.Join(", ", elementIds.Select(e => e.Value))}]");

                    if (severity == FailureSeverity.Error)
                    {
                        if (fa.IsElementsDeletionPermitted(elementIds))
                        {
                            log.Warn($"Deleting failed elements [{string.Join(", ", elementIds.Select(id => id.Value))}]");
                            fa.DeleteElements(elementIds);
                            deletedElements = true;
                        }
                    }
                }

                fa.DeleteAllWarnings();

                var resolvable = failures.Where(fail => fail.HasResolutions()).ToList();
                fa.ResolveFailures(resolvable);

                if (deletedElements) e!.SetProcessingResult(FailureProcessingResult.ProceedWithCommit);
            };
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ExternalCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIApplication uiapp = commandData.Application;
                UIDocument uidoc = uiapp.ActiveUIDocument;
                Application app = uiapp.Application;
                Document doc = uidoc.Document;

                View view = doc.ActiveView;

                SettingsConfig.SetValue("user", app.Username);
                SettingsConfig.SetValue("release", app.VersionName);

                if (view.GetType().Name != "View3D")
                {
                    MessageWindow.Show("Wrong View", "You must be in a 3D view to export");
                    return Result.Succeeded;
                }

                MainWindow mainWindow = new MainWindow(view);
                mainWindow.ShowDialog();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Analytics.Send("Error", ex.Message).GetAwaiter();
                MessageWindow.Show("Error", ex.Message);
                return Result.Failed;
            }
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class Debug : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var uiapp = commandData.Application;
                var uidoc = uiapp.ActiveUIDocument;
                var app = uiapp.Application;
                var doc = uidoc.Document;

                var view = doc.ActiveView;

                if (view.GetType().Name != "View3D")
                {
                    TaskDialog.Show("Error", "You must be in a 3D view to export");
                    return Result.Succeeded;
                }

                Export.SetCategoryVisibility(doc, view);

                var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "out");
                Export.View(doc, view, path);

                TaskDialog.Show("glTF Export", "Finished");

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", ex.ToString());
                return Result.Failed;
            }
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ExportFamilies : IExternalCommand
    {
        static readonly ElementClassFilter SymbolFilter =
            new(typeof(FamilyInstance));

        static readonly ElementCategoryFilter SpecialityEquipmentCategoryFilter =
            new(BuiltInCategory.OST_SpecialityEquipment);

        static LogicalAndFilter SpecialityEquipmentDocumentFilter() =>
            new(SymbolFilter, SpecialityEquipmentCategoryFilter);

        static IEnumerable<FamilyInstance> SpecialityEquipment(Document document)
        {
            using var filter = SpecialityEquipmentDocumentFilter();
            using var collector = new FilteredElementCollector(document)
                .WherePasses(filter);

            foreach (var instance in collector.Cast<FamilyInstance>())
                yield return instance;
        }

        static IEnumerable<FamilyInstance> ObjectInstances(Document document) =>
            SpecialityEquipment(document).Where(i => i.SuperComponent is null);

        static IEnumerable<FamilySymbol> ObjectSymbols(Document document) =>
            ObjectInstances(document).Select(i => i.Symbol).Cast<FamilySymbol>().DistinctBy(s => s.Id);

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var uiapp = commandData.Application;
                var uidoc = uiapp.ActiveUIDocument;
                var app = uiapp.Application;
                var doc = uidoc.Document;

                var symbols = ObjectSymbols(doc).ToList();

                var sb = new StringBuilder();
                sb.AppendLine($"Symbols: {symbols.Count.ToString()}");

                foreach (var item in symbols)
                {
                    sb.AppendLine(item.Family.Name);
                }

                TaskDialog.Show("Families", sb.ToString());
                var mapping = new Dictionary<string, string>();

                var target = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "ccm");
                Directory.CreateDirectory(target);

                foreach (var symbol in symbols)
                {
                    var family = symbol.Family;
                    var familyDoc = doc.EditFamily(family);
                    try
                    {
                        if (familyDoc.FamilyManager is null) throw new InvalidOperationException("FamilyManager is null");

                        using var stencilIdParameter =
                            familyDoc.FamilyManager.Parameters.Cast<FamilyParameter>().FirstOrDefault(p => p.Definition.Name == "06 Stencil ID")
                            ?? throw new InvalidOperationException("Missing StencilId parameter");

                        var stencilIds = familyDoc.FamilyManager.Types.Cast<FamilyType>().Select(type => type.AsString(stencilIdParameter)).Distinct();
                        foreach (var stencilId in stencilIds)
                        {
                            mapping.Add(stencilId, family.Name);
                        }

                        using var collector = new FilteredElementCollector(familyDoc)
                            .OfClass(typeof(View3D));

                        using var view = collector.Cast<View3D>().FirstOrDefault() ?? throw new InvalidOperationException("Missing 3d view");

                        var serviceArea = familyDoc.Settings.Categories.get_Item(BuiltInCategory.OST_SpecialityEquipment)?.SubCategories.Cast<Category>().FirstOrDefault(c => c.Name == "Working & Service Area");
                        if (serviceArea is not null)
                        {
                            view.SetCategoryHidden(serviceArea.Id, true);
                        }

                        var path = Path.Combine(target, family.Name);
                        Export.View(familyDoc, view, path);
                    }
                    catch (Exception ex)
                    {
                        TaskDialog.Show("Error", family.Name + Environment.NewLine + ex.ToString());
                    }
                    finally
                    {
                        familyDoc.Close(false);
                    }
                }

                var serialized = JsonConvert.SerializeObject(mapping,
                    new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });

                var mappingFile = Path.Combine(target, "mapping.json");
                File.WriteAllText(mappingFile, serialized);

                TaskDialog.Show("Families", "Done");

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", ex.ToString());
                return Result.Failed;
            }
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class FamilyStencilIds : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var uiapp = commandData.Application;
                var uidoc = uiapp.ActiveUIDocument;
                var app = uiapp.Application;
                var doc = uidoc.Document;

                if (!doc.IsFamilyDocument) throw new InvalidOperationException("Document is not a family");
                if (doc.FamilyManager is null) throw new InvalidOperationException("FamilyManager is null");

                using var stencilId =
                    doc.FamilyManager.Parameters.Cast<FamilyParameter>().FirstOrDefault(p => p.Definition.Name == "06 Stencil ID")
                    ?? throw new InvalidOperationException("Missing StencilId parameter");

                var stencilIds = doc.FamilyManager.Types.Cast<FamilyType>().Select(type => type.AsString(stencilId)).Distinct().ToList();

                var dict = stencilIds.ToImmutableDictionary(s => s, _ => doc.Title);

                var serialized = JsonConvert.SerializeObject(dict,
                    new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });

                TaskDialog.Show("StencilIds", serialized);

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", ex.ToString());
                return Result.Failed;
            }
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ExportFamiliesFolder : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var uiapp = commandData.Application;
                var app = uiapp.Application;

                var dialog = new OpenFolderDialog();
                var result = dialog.ShowDialog();

                if (result is null || result == false)
                {
                    return Result.Cancelled;
                }

                var files = Directory.GetFiles(dialog.FolderName).Where(f => Path.GetExtension(f).ToLowerInvariant() == ".rfa");

                var target = Path.Combine(dialog.FolderName, "out");
                Directory.CreateDirectory(target);

                var mapping = new Dictionary<string, string>();

                foreach (var file in files)
                {
                    var doc = app.OpenDocumentFile(file);
                    try
                    {
                        if (!doc.IsFamilyDocument) throw new InvalidOperationException("Document is not a family");
                        if (doc.FamilyManager is null) throw new InvalidOperationException("FamilyManager is null");

                        using var stencilIdParameter =
                            doc.FamilyManager.Parameters.Cast<FamilyParameter>().FirstOrDefault(p => p.Definition.Name == "06 Stencil ID")
                            ?? throw new InvalidOperationException("Missing StencilId parameter");

                        var stencilIds = doc.FamilyManager.Types.Cast<FamilyType>().Select(type => type.AsString(stencilIdParameter)).Distinct();
                        foreach (var stencilId in stencilIds)
                        {
                            mapping.Add(stencilId, doc.Title);
                        }

                        using var collector = new FilteredElementCollector(doc).OfClass(typeof(View3D));
                        using var view = collector.Cast<View3D>().FirstOrDefault() ?? throw new InvalidOperationException("Missing 3d view");

                        var serviceArea = doc.Settings.Categories.get_Item(BuiltInCategory.OST_SpecialityEquipment)?.SubCategories.Cast<Category>().FirstOrDefault(c => c.Name == "Working & Service Area");
                        if (serviceArea is not null)
                        {
                            using var tx = new Transaction(doc, "Hide service area");
                            tx.Start();
                            view.SetCategoryHidden(serviceArea.Id, true);
                            tx.Commit();
                        }

                        var path = Path.Combine(target, doc.Title);
                        Export.View(doc, view, path);
                    }
                    catch (Exception ex)
                    {
                        TaskDialog.Show("Error", doc.Title + Environment.NewLine + ex.ToString());
                    }
                    finally
                    {
                        doc.Close(false);
                    }
                }

                var serialized = JsonConvert.SerializeObject(mapping,
                    new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });

                var mappingFile = Path.Combine(target, "mapping.json");
                File.WriteAllText(mappingFile, serialized);

                TaskDialog.Show("Export folder", "Done");

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", ex.ToString());
                return Result.Failed;
            }
        }
    }

    public class Log(string path) : IDisposable
    {
        private readonly StreamWriter Stream = new(path);

        void Write(string message)
        {
            Stream.WriteLine(message);
            Stream.Flush();
        }

        public void Debug(string message) => Write($"[DEBUG] {message}");
        public void Info(string message) => Write($"[INFO] {message}");
        public void Warn(string message) => Write($"[WARN] {message}");
        public void Error(string message) => Write($"[ERROR] {message}");

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                Stream.Dispose();
            }
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ExportKinshipFolder : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var uiapp = commandData.Application;
                var app = uiapp.Application;

                var dialog = new OpenFolderDialog();
                var result = dialog.ShowDialog();

                if (result is null || result == false)
                {
                    return Result.Cancelled;
                }

                var target = Path.Combine(dialog.FolderName, "out");
                Directory.CreateDirectory(target);

                using var log = new Log(Path.Combine(target, "log.txt"));

                var versionDirectories =
                    Directory.GetDirectories(dialog.FolderName)
                    .Where(d => Regex.IsMatch(Path.GetFileName(d)!, @"^\d{4}$"))
                    .OrderDescending();

                /// NOTE: dedup, prefer latest Revit version
                var familyDirectories = new Dictionary<string, string>();
                foreach (var versionDirectory in versionDirectories)
                {
                    foreach (var path in Directory.GetDirectories(versionDirectory))
                    {
                        var name = Path.GetFileName(path);
                        if (name == "Flexible Generic Analyzer") continue;
                        familyDirectories.TryAdd(name, path);
                    }
                }

                var families = new List<string>();
                foreach (var familyDirectory in familyDirectories.Values)
                {
                    families.AddRange(
                        Directory.GetFiles(familyDirectory)
                        .Where(f => Path.GetExtension(f).ToLowerInvariant() == ".rfa"));
                }

                // NOTE: Sort list so 2023 families are processed first
                families.Sort();

                TaskDialog.Show("Kinship Families", $"Exporting {families.Count} families");
                log.Info($"Exporting {families.Count} families");

                using var openOptions = new OpenOptions();

                var index = 0;
                var mapping = new SortedDictionary<string, string>();
                var typeNames = new HashSet<string>();

                var failuresProcessing = Export.CreateFailureProcessing(log);
                app.FailuresProcessing += failuresProcessing;

                try
                {
                    foreach (var file in families)
                    {
                        log.Info($"{++index}/{families.Count} {file}");

                        var modelPath = ModelPathUtils.ConvertUserVisiblePathToModelPath(file);
                        var doc = app.OpenDocumentFile(modelPath, openOptions);
                        try
                        {
                            Export.Family(log, doc, target, mapping, typeNames);
                        }
                        catch (Exception ex)
                        {
                            log.Error($"Failed to export family '{doc.Title}' {ex.Message}");
                        }
                        finally
                        {
                            doc.Close(false);
                        }
                    }
                }
                finally
                {
                    app.FailuresProcessing -= failuresProcessing;
                }

                var serialized = JsonConvert.SerializeObject(mapping,
                    new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });

                var mappingFile = Path.Combine(target, "mapping.json");
                log.Info($"Write mapping {mappingFile}");
                File.WriteAllText(mappingFile, serialized);

                log.Info("Finished");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", ex.ToString());
                return Result.Failed;
            }
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ExportFamily : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var uiapp = commandData.Application;
                var app = uiapp.Application;
                var uidoc = uiapp.ActiveUIDocument;
                var doc = uidoc.Document;

                var view = doc.ActiveView;

                var target = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "family");
                Directory.CreateDirectory(target);

                using var log = new Log(Path.Combine(target, "log.txt"));

                var mapping = new SortedDictionary<string, string>();
                var typeNames = new HashSet<string>();

                var failuresProcessing = Export.CreateFailureProcessing(log);
                app.FailuresProcessing += failuresProcessing;

                try
                {
                    Export.Family(log, doc, target, mapping, typeNames);
                }
                catch (Exception ex)
                {
                    log.Error($"Failed to export family '{doc.Title}' {ex.Message}");
                }
                finally
                {
                    app.FailuresProcessing -= failuresProcessing;
                }

                var serialized = JsonConvert.SerializeObject(mapping,
                    new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });

                var mappingFile = Path.Combine(target, "mapping.json");
                log.Info($"Write mapping {mappingFile}");
                File.WriteAllText(mappingFile, serialized);

                log.Info("Finished");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", ex.ToString());
                return Result.Failed;
            }
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class AboutUs : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var mainWindow = new AboutUsWindow();
                mainWindow.ShowDialog();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Analytics.Send("Error", ex.Message).GetAwaiter();
                MessageWindow.Show("Error", ex.Message);
                return Result.Failed;
            }
        }
    }
}