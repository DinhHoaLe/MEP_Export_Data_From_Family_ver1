namespace Export_Data_From_Family.Models
{
    public class FamilyTypeValueResult
    {
        public string TypeName { get; set; }
        public bool HasValue { get; set; }
        public double? DoubleValue { get; set; }
        public int? IntegerValue { get; set; }
        public string StringValue { get; set; }
        public string DisplayValue { get; set; }
    }
}
