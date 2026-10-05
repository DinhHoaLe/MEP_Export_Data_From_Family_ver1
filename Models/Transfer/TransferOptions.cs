using System.Collections.Generic;

namespace Export_Data_From_Family.Models
{
    public class TransferOptions
    {
        public string GroupTypeId { get; set; }
        public bool IsInstance { get; set; }
        public bool ChangeFormat { get; set; }
        public string UnitTypeId { get; set; }
        public string SymbolTypeId { get; set; }
        public double? Accuracy { get; set; }
        public bool CopyValue { get; set; }
        public bool MatchTypes { get; set; }
        public string SourceTypeName { get; set; }
        public string DestinationTypeName { get; set; }
        public bool CopyFormula { get; set; }
        public string FormulaText { get; set; }
        public List<string> SourceParameterNames { get; set; }
    }
}
