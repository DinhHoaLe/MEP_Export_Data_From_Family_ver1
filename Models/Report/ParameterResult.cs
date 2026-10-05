using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Export_Data_From_Family.Models
{
    public class ParameterResult
    {
        public string Name { get; set; }

        public string StorageType { get; set; }
        public string DataTypeId { get; set; }
        public string GroupTypeId { get; set; }
        public string SharedGuid { get; set; }

        public bool IsInstance { get; set; }

        public bool IsShared { get; set; }

        public string Formula { get; set; }

        public string Value { get; set; }
        public ParameterFormatResult Format { get; set; }
        public List<FamilyTypeValueResult> TypeValues { get; set; }
    }
}
