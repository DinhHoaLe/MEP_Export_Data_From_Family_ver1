using Autodesk.Revit.DB;
using Export_Data_From_Family.Models;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Export_Data_From_Family.Services
{
    public static class FamilyAnalyzer
    {
        public static FamilyHealthResult Analyze(
            Document doc,
            string filePath)
        {
            FamilyManager fm = doc.FamilyManager;
            Family family = doc.OwnerFamily;

            FileInfo fileInfo =
                new FileInfo(filePath);

            FamilyHealthResult result =
                new FamilyHealthResult();

            result.FileName =
                fileInfo.Name;

            result.FilePath =
                filePath;

            result.FileSizeMB =
                Math.Round(
                    fileInfo.Length /
                    1024.0 /
                    1024.0,
                    3
                );

            result.Category =
                family?.FamilyCategory?.Name;

            result.PlacementType =
                family?.FamilyPlacementType
                    .ToString();

            // Types
            result.TypeCount =
                fm.Types
                    .Cast<FamilyType>()
                    .Count();

            // Parameters
            IList<FamilyParameter> parameters =
                fm.GetParameters();

            result.ParameterCount =
                parameters.Count;

            result.Parameters =
                new List<ParameterResult>();

            foreach (
                FamilyParameter parameter
                in parameters)
            {
                ParameterResult p =
                    new ParameterResult();

                p.Name =
                    parameter.Definition?.Name;

                p.StorageType =
                    parameter.StorageType
                        .ToString();

                p.DataTypeId =
                    parameter.Definition?.GetDataType()?.TypeId;

                p.GroupTypeId =
                    parameter.Definition?.GetGroupTypeId()?.TypeId;

                p.IsInstance =
                    parameter.IsInstance;

                p.IsShared =
                    parameter.IsShared;

                if (parameter.IsShared)
                    p.SharedGuid = parameter.GUID.ToString();

                p.Formula =
                    parameter.Formula;

                ForgeTypeId dataType = parameter.Definition?.GetDataType();
                if (dataType != null && UnitUtils.IsMeasurableSpec(dataType) &&
                    Units.IsModifiableSpec(dataType))
                {
                    try
                    {
                        FormatOptions format = doc.GetUnits().GetFormatOptions(dataType);
                        p.Format = new ParameterFormatResult
                        {
                            UnitTypeId = format.GetUnitTypeId().TypeId,
                            SymbolTypeId = format.GetSymbolTypeId().TypeId,
                            Accuracy = format.Accuracy,
                            RoundingMethod = format.RoundingMethod.ToString(),
                            UseDigitGrouping = format.UseDigitGrouping,
                            UsePlusPrefix = format.CanUsePlusPrefix() && format.UsePlusPrefix,
                            SuppressSpaces = format.CanSuppressSpaces() && format.SuppressSpaces,
                            SuppressLeadingZeros = format.CanSuppressLeadingZeros() && format.SuppressLeadingZeros,
                            SuppressTrailingZeros = format.CanSuppressTrailingZeros() && format.SuppressTrailingZeros
                        };
                    }
                    catch (Exception)
                    {
                        p.Format = null;
                    }
                }

                p.TypeValues = new List<FamilyTypeValueResult>();
                foreach (FamilyType familyType in fm.Types)
                {
                    FamilyTypeValueResult value = new FamilyTypeValueResult
                    {
                        TypeName = familyType.Name
                    };
                    try
                    {
                        value.HasValue = familyType.HasValue(parameter);
                        if (value.HasValue)
                        {
                            switch (parameter.StorageType)
                            {
                                case StorageType.Double:
                                    value.DoubleValue = familyType.AsDouble(parameter);
                                    value.DisplayValue = familyType.AsValueString(parameter);
                                    break;
                                case StorageType.Integer:
                                    value.IntegerValue = familyType.AsInteger(parameter);
                                    value.DisplayValue = value.IntegerValue?.ToString();
                                    break;
                                case StorageType.String:
                                    value.StringValue = familyType.AsString(parameter);
                                    value.DisplayValue = value.StringValue;
                                    break;
                            }
                        }
                    }
                    catch (Exception) { value.HasValue = false; }
                    p.TypeValues.Add(value);
                }

                result.Parameters.Add(p);
            }

            // Nested Families
            result.NestedFamilyCount =
                new FilteredElementCollector(doc)
                    .OfClass(typeof(Family))
                    .Cast<Family>()
                    .Count(x =>
                        !x.IsOwnerFamily
                    );

            // Connectors
            result.ConnectorCount =
                new FilteredElementCollector(doc)
                    .OfClass(
                        typeof(ConnectorElement)
                    )
                    .GetElementCount();

            return result;
        }
    }
}
