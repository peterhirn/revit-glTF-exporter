namespace Revit_glTF_Exporter
{
    using Autodesk.Revit.ApplicationServices;
    using Autodesk.Revit.Attributes;
    using Autodesk.Revit.DB;
    using Autodesk.Revit.UI;
    using Common_glTF_Exporter.Core;
    using Common_glTF_Exporter.Utils;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Immutable;
    using System.Text;

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
                    TaskDialog.Show("Wrong View", "You must be in a 3D view to export");
                    return Result.Succeeded;
                }

                /*
                var programDataLocation = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                var appSettingsFile = string.Concat(programDataLocation, "\\Autodesk\\ApplicationPlugins\\leia.bundle\\Contents\\2026\\Leia_glTF_Exporter.dll.config");

                if (!File.Exists(appSettingsFile))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(appSettingsFile));
                    File.WriteAllText(appSettingsFile, "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n<configuration />");
                }
                */

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
                SettingsConfig.SetValue("path", Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\out");
                SettingsConfig.SetValue("fileName", "out");
                SettingsConfig.SetValue("user", app.Username);
                SettingsConfig.SetValue("release", app.VersionName);
                SettingsConfig.SetValue("isRFA", "false");

                var ctx = new GLTFExportContext(doc, view, true);
                var exporter = new CustomExporter(doc, ctx)
                {
                    ShouldStopOnError = false
                };
                exporter.Export(view);

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

                foreach(var item in symbols)
                {
                    sb.AppendLine(item.Family.Name);
                }

                TaskDialog.Show("Families", sb.ToString());

                foreach (var symbol in symbols)
                {
                    var family = symbol.Family;
                    var familyDoc = doc.EditFamily(family);
                    try
                    {
                        using var collector = new FilteredElementCollector(familyDoc)
                            .OfClass(typeof(View3D));

                        using var view = collector.Cast<View3D>().FirstOrDefault() ?? throw new InvalidOperationException("Missing 3d view");

                        var serviceArea = familyDoc.Settings.Categories.get_Item(BuiltInCategory.OST_SpecialityEquipment)?.SubCategories.Cast<Category>().FirstOrDefault(c => c.Name == "Working & Service Area");
                        if (serviceArea is not null)
                        {
                            view.SetCategoryHidden(serviceArea.Id, true);
                        }

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
                        SettingsConfig.SetValue("path", Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\" + family.Name);
                        SettingsConfig.SetValue("fileName", family.Name);
                        SettingsConfig.SetValue("user", app.Username);
                        SettingsConfig.SetValue("release", app.VersionName);
                        SettingsConfig.SetValue("isRFA", "false");

                        var ctx = new GLTFExportContext(familyDoc, view, true);
                        var exporter = new CustomExporter(familyDoc, ctx)
                        {
                            ShouldStopOnError = false
                        };
                        exporter.Export(view);

                        //TaskDialog.Show("View", view.Name);
                    }
                    finally
                    {
                        familyDoc.Close(false);
                    }
                }

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