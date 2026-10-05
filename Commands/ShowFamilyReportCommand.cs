using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Export_Data_From_Family.UI.Wpf;
using System;

namespace Export_Data_From_Family.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ShowFamilyReportCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return ShowWizard(commandData.Application, true, ref message);
        }

        internal static Result ShowWizard(UIApplication uiapp, bool preferJson, ref string message)
        {
            try
            {
                FamilyWizardWindow window = new FamilyWizardWindow(uiapp, preferJson);
                new System.Windows.Interop.WindowInteropHelper(window).Owner = uiapp.MainWindowHandle;
                window.ShowDialog();
                if (window.LoadError != null) throw window.LoadError;
                if (!window.LoadCompleted) return Result.Cancelled;
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Family Data Studio", ex.Message);
                return Result.Failed;
            }
        }
    }
}
