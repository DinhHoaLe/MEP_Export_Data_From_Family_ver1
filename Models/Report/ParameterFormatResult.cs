namespace Export_Data_From_Family.Models
{
    public class ParameterFormatResult
    {
        public string UnitTypeId { get; set; }
        public string SymbolTypeId { get; set; }
        public double Accuracy { get; set; }
        public string RoundingMethod { get; set; }
        public bool UseDigitGrouping { get; set; }
        public bool UsePlusPrefix { get; set; }
        public bool SuppressSpaces { get; set; }
        public bool SuppressLeadingZeros { get; set; }
        public bool SuppressTrailingZeros { get; set; }
    }
}
