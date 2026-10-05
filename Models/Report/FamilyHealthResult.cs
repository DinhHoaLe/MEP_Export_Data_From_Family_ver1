using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Export_Data_From_Family.Models
{
    public class FamilyHealthResult
    {
        public string FileName { get; set; }
        public string FilePath { get; set; }

        public double FileSizeMB { get; set; }

        public string Category { get; set; }
        public string PlacementType { get; set; }

        public int TypeCount { get; set; }
        public int ParameterCount { get; set; }
        public int NestedFamilyCount { get; set; }
        public int ConnectorCount { get; set; }

        public List<ParameterResult> Parameters { get; set; }

        public List<CheckResult> Checks { get; set; }

        public string Status { get; set; }
    }
}