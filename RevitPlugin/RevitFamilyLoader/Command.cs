// (C) Copyright 2023 by Autodesk, Inc. 
//
// Permission to use, copy, modify, and distribute this software
// in object code form for any purpose and without fee is hereby
// granted, provided that the above copyright notice appears in
// all copies and that both that copyright notice and the limited
// warranty and restricted rights notice below appear in all
// supporting documentation.
//
// AUTODESK PROVIDES THIS PROGRAM "AS IS" AND WITH ALL FAULTS. 
// AUTODESK SPECIFICALLY DISCLAIMS ANY IMPLIED WARRANTY OF
// MERCHANTABILITY OR FITNESS FOR A PARTICULAR USE.  AUTODESK,
// INC. DOES NOT WARRANT THAT THE OPERATION OF THE PROGRAM WILL
// BE UNINTERRUPTED OR ERROR FREE.
//
// Use, duplication, or disclosure by the U.S. Government is
// subject to restrictions set forth in FAR 52.227-19 (Commercial
// Computer Software - Restricted Rights) and DFAR 252.227-7013(c)
// (1)(ii)(Rights in Technical Data and Computer Software), as
// applicable.
//

using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using DesignAutomationFramework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitFamilyLoader
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class Command : IExternalDBApplication
    {
        public ExternalDBApplicationResult OnStartup(ControlledApplication application)
        {
            DesignAutomationBridge.DesignAutomationReadyEvent += HandleDesignAutomationReadyEvent;
            return ExternalDBApplicationResult.Succeeded;
        }

        public ExternalDBApplicationResult OnShutdown(ControlledApplication application)
        {
            return ExternalDBApplicationResult.Succeeded;
        }

        [Obsolete]
        public void HandleApplicationInitializedEvent(object sender, Autodesk.Revit.DB.Events.ApplicationInitializedEventArgs e)
        {
            var app = sender as Autodesk.Revit.ApplicationServices.Application;
            DesignAutomationData data = new DesignAutomationData(app, "InputFile.rvt");
            this.DoTask(data);
        }

        private void HandleDesignAutomationReadyEvent(object sender, DesignAutomationReadyEventArgs e)
        {
            LogTrace("Design Automation Ready event triggered ...");

            e.Succeeded = true;
            e.Succeeded = this.DoTask(e.DesignAutomationData);
        }

        private bool DoTask(DesignAutomationData data)
        {
            if (data == null)
                return false;

            Application app = data.RevitApp;
            if (app == null)
            {
                LogTrace("Error occured");
                LogTrace("Invalid Revit App");
                return false;
            }


            //string modelPath = data.FilePath;
            //if (string.IsNullOrWhiteSpace(modelPath))
            //{
            //    LogTrace("Error occured");
            //    LogTrace("Invalid File Path");
            //    return false;
            //}

            //var doc = data.RevitDoc;
            //if (doc == null)
            //{
            //    LogTrace("Error occured");
            //    LogTrace("Invalid Revit DB Document");
            //    return false;
            //}

            LogTrace("Creating a new Revit model from template ... ");

            var doc = data.RevitApp.NewProjectDocument(UnitSystem.Metric);


            LogTrace("Getting RFA files ... ");

            var familiesFolder = new DirectoryInfo(Path.Combine(Directory.GetCurrentDirectory(), "Families"));
            FileInfo[] rfaFiles = familiesFolder.GetFiles("*.rfa", SearchOption.AllDirectories);

            if (rfaFiles.Length <= 0)
            {
                LogTrace("Error occurred");
                LogTrace("No RFA found to be loaded");
                return false;
            }

            LogTrace(" - DONE.");

            var loadedFamilies = new List<Family>();

           //foreach (FileInfo familyFile in rfaFiles)
             //handle one family file only
            {
                // var familyPathRelative = familyFile.FullName.Replace(Directory.GetCurrentDirectory(), string.Empty);
                var familyPathRelative = rfaFiles[0].FullName.Replace(Directory.GetCurrentDirectory(), string.Empty);
                LogTrace($"Loading family `{familyPathRelative}` ... ");
                //Family loadedFamily = null;
                //var loadResult = doc.LoadFamily(familyFile.FullName, out loadedFamily); //!<<< Not working
                //var loadResult = doc.LoadFamily(familyFile.FullName, new FamilyLoadOptions(), out loadedFamily); //!<<< Not working

                // Workaround
                //- 1. Opening RFA get Family document object
                //- 2. Loading Family by FamilyDocument.LoadFamily( ProjectDocument ) and related function overrrides instead
                try
                {
                    var familyDoc = app.OpenDocumentFile(rfaFiles[0].FullName);
                    if (familyDoc == null)
                        throw new InvalidOperationException("Failed to open RFA");

                    try
                    {
                        Family loadedFamily = familyDoc.LoadFamily(doc, new FamilyLoadOptions());
                        if (loadedFamily == null)
                        {
                            throw new InvalidOperationException("Failed to open RFA");
                        }

                        loadedFamilies.Add(loadedFamily);

                        LogTrace("insert family instance");
                        String name = loadedFamily.Name;
                        LogTrace("Family file has been loaded. Its name is " + name);

                        ISet<ElementId> familySymbolIds = loadedFamily.GetFamilySymbolIds();
                        using (Transaction transaction = new Transaction(doc))
                        {
                            transaction.Start("Start Insert Instance");


                            foreach (ElementId id in familySymbolIds)
                            {
                                FamilySymbol familySymbol = loadedFamily.Document.GetElement(id) as FamilySymbol;
                                LogTrace("Activating Symbol...");
                                if (!familySymbol.IsActive)
                                    familySymbol.Activate(); 

                                LogTrace("inser instance...");
                                FamilyInstance fi = doc.Create.NewFamilyInstance(new XYZ(0, 0, 0), familySymbol, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                            }
                            transaction.Commit();
                        }

                        LogTrace(" - DONE.");
                    }
                    catch(Exception ex)
                    {
                        throw ex;
                    }
                    finally
                    {
                        familyDoc.Close(false);
                    }
                }
                catch (Exception ex)
                {
                    LogTrace("Error occurred");
                    LogTrace($"Failed to load Family {familyPathRelative}");
                }
            }

            if (loadedFamilies.Count > 0)
            {
                LogTrace(" Saving changes to target RVT ... ");

                var saveModelPath = ModelPathUtils.ConvertUserVisiblePathToModelPath(Path.Combine(Directory.GetCurrentDirectory(), "outputFile.rvt")); //outputFile.rvt must be consistent to that in DA Acvitity
                var saveOptions = new SaveAsOptions();
                saveOptions.OverwriteExistingFile = true;

                doc.SaveAs(saveModelPath, saveOptions);

                LogTrace(" - DONE.");
                return true;
            }
            else
            {
                LogTrace("Error occurred");
                LogTrace("Cannot load all specifiyied RFA files to target RVT");
            }

            return false;
        }

        private void PrintError(Exception ex)
        {
            LogTrace("Error occurred");
            LogTrace(ex.Message);

            if (ex.InnerException != null)
                LogTrace(ex.InnerException.Message);
        }

        /// <summary>
        /// This will appear on the Design Automation output
        /// </summary>
        private static void LogTrace(string format, params object[] args)
        {
#if DEBUG
            System.Diagnostics.Trace.WriteLine(string.Format(format, args));
#endif
            System.Console.WriteLine(format, args);
        }
    }
}
