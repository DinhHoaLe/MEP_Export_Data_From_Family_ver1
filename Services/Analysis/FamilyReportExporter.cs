using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Export_Data_From_Family.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace Export_Data_From_Family.Services
{
    public static class FamilyReportExporter
    {
        public static string Export(UIApplication uiapp, string folder)
        {
            string[] files = Directory.GetFiles(folder, "*.rfa", SearchOption.TopDirectoryOnly);
            List<FamilyHealthResult> results = new List<FamilyHealthResult>();
            foreach (string file in files)
                results.Add(AnalyzeFile(uiapp, file));
            return WriteReport(folder, results);
        }

        // Yield between files so the WPF progress screen can repaint. Revit API calls
        // still run on the command's UI thread because no Task.Run is used here.
        public static async Task<string> ExportAsync(UIApplication uiapp, string folder,
            Action<int, int> progress, CancellationToken cancellationToken)
        {
            string[] files = Directory.GetFiles(folder, "*.rfa", SearchOption.TopDirectoryOnly);
            List<FamilyHealthResult> results = new List<FamilyHealthResult>();
            for (int index = 0; index < files.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Invoke(index, files.Length);
                await Dispatcher.Yield(DispatcherPriority.Background);
                cancellationToken.ThrowIfCancellationRequested();
                results.Add(AnalyzeFile(uiapp, files[index]));
            }
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Invoke(files.Length, files.Length);
            return WriteReport(folder, results);
        }

        private static FamilyHealthResult AnalyzeFile(UIApplication uiapp, string file)
        {
            Document active = uiapp.ActiveUIDocument == null ? null : uiapp.ActiveUIDocument.Document;
            Document document = null;
            bool openedByTool = false;
            try
            {
                bool alreadyOpen = active != null && !string.IsNullOrEmpty(active.PathName) &&
                    string.Equals(Path.GetFullPath(active.PathName), Path.GetFullPath(file),
                        StringComparison.OrdinalIgnoreCase);
                document = alreadyOpen ? active : uiapp.Application.OpenDocumentFile(file);
                openedByTool = !alreadyOpen;
                if (document == null || !document.IsFamilyDocument) return null;

                FamilyHealthResult result = FamilyAnalyzer.Analyze(document, file);
                RuleChecker.Run(result);
                return result;
            }
            catch (Exception error)
            {
                return new FamilyHealthResult
                {
                    FileName = Path.GetFileName(file),
                    FilePath = file,
                    Status = "ERROR",
                    Checks = new List<CheckResult>
                    {
                        new CheckResult { Rule = "Analyze Family", Status = "FAIL", Message = error.Message }
                    }
                };
            }
            finally
            {
                if (openedByTool && document != null && document.IsValidObject)
                    document.Close(false);
            }
        }

        private static string WriteReport(string folder, List<FamilyHealthResult> results)
        {
            string reportFolder = Path.Combine(folder, "FamilyHealthReport");
            Directory.CreateDirectory(reportFolder);
            string output = Path.Combine(reportFolder, "summary.json");
            File.WriteAllText(output, JsonConvert.SerializeObject(results.FindAll(x => x != null), Formatting.Indented));
            return output;
        }
    }
}
