using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Export_Data_From_Family.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class BatchFamilyHealthCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return ShowFamilyReportCommand.ShowWizard(commandData.Application, false, ref message);
        }
    }
}
