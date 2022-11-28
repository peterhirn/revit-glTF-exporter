﻿using System;
using System.IO;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;
using Autodesk.Windows;
using RibbonPanel = Autodesk.Revit.UI.RibbonPanel;

namespace Revit_glTF_Exporter
{
    class App : IExternalApplication
    {
        private static string RIBBON_TAB = "e-verse";
        private static string RIBBON_PANEL = "glTF";
        private static string PUSH_BUTTON_NAME = "glTF Exporter";
        private static string PUSH_BUTTON_TEXT = "glTF Exporter";
        private static string AddInPath = typeof(App).Assembly.Location;
        private static string ButtonIconsFolder = Path.GetDirectoryName(AddInPath) + "\\Images\\";

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }

        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                CreateRibbonTab(application, RIBBON_TAB);
            }
            catch
            {

            }

            RibbonPanel panel = null;
            //look for XXXXXX RibbonPanel, or create it if not already created
            foreach (RibbonPanel existingPanel in application.GetRibbonPanels())
            {
                if (existingPanel.Name.Equals(RIBBON_PANEL))
                {
                    //existingPanel.AddSeparator();
                    panel = existingPanel;
                    break;
                }
            }
            if (panel == null) panel = application.CreateRibbonPanel(RIBBON_TAB, RIBBON_PANEL);

            PushButtonData pushDataButton = new PushButtonData(PUSH_BUTTON_NAME, PUSH_BUTTON_TEXT, AddInPath, "Revit_glTF_Exporter.ExternalCommand");
            pushDataButton.LargeImage = new BitmapImage(new Uri(Path.Combine(ButtonIconsFolder, "gltf.png"), UriKind.Absolute));

            panel.AddItem(pushDataButton);

            return Result.Succeeded;
        }

        public static void CreateRibbonTab(UIControlledApplication application, string ribbonTabName)
        {
            RibbonControl ribbon = ComponentManager.Ribbon;
            RibbonTab tab = ribbon.FindTab(ribbonTabName);

            if (tab == null)
            {
                application.CreateRibbonTab(ribbonTabName);
            }  
        }
    }
}
