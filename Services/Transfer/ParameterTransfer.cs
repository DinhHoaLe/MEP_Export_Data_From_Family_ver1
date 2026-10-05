using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Export_Data_From_Family.Models;
using System;
using System.Linq;

namespace Export_Data_From_Family.Services
{
    public static class ParameterTransfer
    {
        public static string Check(UIApplication uiapp, ParameterResult parameter, string groupTypeId)
        {
            return Check(uiapp, parameter, new TransferOptions
            {
                GroupTypeId = groupTypeId,
                IsInstance = parameter != null && parameter.IsInstance
            });
        }

        public static string Check(UIApplication uiapp, ParameterResult parameter, TransferOptions options)
        {
            Document doc = uiapp?.ActiveUIDocument == null ? null : uiapp.ActiveUIDocument.Document;
            if (doc == null || !doc.IsFamilyDocument)
                return "Open the destination family in Revit before transferring.";
            if (parameter == null || string.IsNullOrWhiteSpace(parameter.Name))
                return "The report has no parameter name.";
            if (string.IsNullOrWhiteSpace(parameter.DataTypeId))
                return "This JSON report lacks a data type. Export it again with the updated tool.";
            if (options == null || string.IsNullOrWhiteSpace(options.GroupTypeId) ||
                !ParameterUtils.IsBuiltInGroup(new ForgeTypeId(options.GroupTypeId)))
                return "Choose a valid destination parameter group.";
            if (options.CopyValue && options.CopyFormula)
                return "Choose either Copy value or Copy formula.";
            if (doc.FamilyManager.GetParameters().Any(p =>
                string.Equals(p.Definition.Name, parameter.Name, StringComparison.OrdinalIgnoreCase)))
                return "A parameter with this name already exists in the open family.";

            ForgeTypeId dataType = new ForgeTypeId(parameter.DataTypeId);
            if (!SpecUtils.IsValidDataType(dataType))
                return "This parameter data type cannot be created in the open family.";

            if (parameter.IsShared)
            {
                ExternalDefinition shared = FindSharedDefinition(uiapp, parameter);
                if (shared == null)
                    return "Shared parameter GUID was not found in Revit's configured shared parameter file.";
                if (shared.Name != parameter.Name || shared.GetDataType().TypeId != parameter.DataTypeId)
                    return "The shared parameter definition does not match the report.";
            }

            if (options.ChangeFormat && !string.IsNullOrWhiteSpace(options.UnitTypeId))
            {
                if (!UnitUtils.IsMeasurableSpec(dataType) || !Units.IsModifiableSpec(dataType))
                    return "Unit format cannot be changed for this data type.";
                ForgeTypeId unit = new ForgeTypeId(options.UnitTypeId);
                if (!UnitUtils.GetValidUnits(dataType).Any(x => x.TypeId == unit.TypeId))
                    return "Choose a unit supported by this data type.";
                FormatOptions format = new FormatOptions(unit);
                if (options.Accuracy.HasValue && !format.IsValidAccuracy(options.Accuracy.Value))
                    return "Choose a valid precision for the selected unit.";
            }

            if (options.CopyFormula)
            {
                if (string.IsNullOrWhiteSpace(options.FormulaText))
                    return "Enter a formula before transferring.";
                var destinationNames = doc.FamilyManager.GetParameters()
                    .Select(x => x.Definition.Name).Concat(new[] { parameter.Name });
                var missing = FormulaDependencyChecker.MissingParameters(options.FormulaText,
                    options.SourceParameterNames, destinationNames);
                if (missing.Count > 0)
                    return "Cannot copy formula. Missing parameters in destination: " +
                        string.Join(", ", missing) + ".";
            }

            if (options.CopyValue)
            {
                if (parameter.StorageType == StorageType.ElementId.ToString())
                    return "ElementId values refer to the source family and cannot be copied safely.";
                if (parameter.TypeValues == null)
                    return "This JSON report has no Type value data. Export it again or turn off Copy value.";
                if (!parameter.TypeValues.Any(x => x != null && x.HasValue))
                    return null; // Source family has no Types: keep the new parameter's default value.
                if (options.MatchTypes)
                {
                    if (!doc.FamilyManager.Types.Cast<FamilyType>().Any())
                        return "The destination family has no Types. Create a Type in Revit or turn off Copy value.";
                    bool match = doc.FamilyManager.Types.Cast<FamilyType>().Any(type =>
                        parameter.TypeValues.Any(value => value != null && value.HasValue && value.TypeName == type.Name));
                    if (!match) return "No source and destination Types with the same name have values.";
                }
                else
                {
                    FamilyTypeValueResult source = parameter.TypeValues.FirstOrDefault(x =>
                        x != null && x.TypeName == options.SourceTypeName);
                    if (source == null || !source.HasValue)
                        return "Choose a source Type with a value.";
                    if (!doc.FamilyManager.Types.Cast<FamilyType>().Any())
                        return null; // Destination has no Types: leave the parameter's value unset.
                    if (doc.FamilyManager.Types.Cast<FamilyType>().All(x =>
                        x.Name != options.DestinationTypeName))
                        return "Choose a destination Type to receive the value.";
                }
            }

            return null;
        }

        public static void Transfer(UIApplication uiapp, ParameterResult parameter, string groupTypeId)
        {
            Transfer(uiapp, parameter, new TransferOptions
            {
                GroupTypeId = groupTypeId,
                IsInstance = parameter != null && parameter.IsInstance
            });
        }

        public static void Transfer(UIApplication uiapp, ParameterResult parameter, TransferOptions options)
        {
            string issue = Check(uiapp, parameter, options);
            if (issue != null) throw new InvalidOperationException(issue);

            Document doc = uiapp.ActiveUIDocument.Document;
            ForgeTypeId groupType = new ForgeTypeId(options.GroupTypeId);
            bool isInstance = options.ChangeFormat ? options.IsInstance : parameter.IsInstance;
            using (Transaction transaction = new Transaction(doc, "Transfer family parameter"))
            {
                transaction.Start();
                try
                {
                    EnsureCurrentType(doc.FamilyManager, options.CopyValue || options.CopyFormula);
                    FamilyParameter added = AddParameter(uiapp, doc, parameter, groupType, isInstance);
                    if (options.ChangeFormat && !string.IsNullOrWhiteSpace(options.UnitTypeId))
                        ChangeFormat(doc, parameter, options);
                    if (options.CopyValue && parameter.TypeValues != null &&
                        parameter.TypeValues.Any(x => x != null && x.HasValue) &&
                        doc.FamilyManager.Types.Cast<FamilyType>().Any())
                    {
                        if (options.MatchTypes)
                        {
                            FamilyManager manager = doc.FamilyManager;
                            FamilyType original = manager.CurrentType;
                            try
                            {
                                foreach (FamilyType type in manager.Types)
                                {
                                    FamilyTypeValueResult source = parameter.TypeValues.FirstOrDefault(value =>
                                        value != null && value.HasValue && value.TypeName == type.Name);
                                    if (source == null) continue;
                                    manager.CurrentType = type;
                                    CopyValue(manager, added, source);
                                }
                            }
                            finally
                            {
                                if (original != null) manager.CurrentType = original;
                            }
                        }
                        else
                        {
                            FamilyManager manager = doc.FamilyManager;
                            FamilyType original = manager.CurrentType;
                            FamilyType destinationType = options.DestinationTypeName == null
                                ? manager.CurrentType
                                : manager.Types.Cast<FamilyType>().First(x =>
                                    x.Name == options.DestinationTypeName);
                            FamilyTypeValueResult source = parameter.TypeValues.First(value =>
                                value.TypeName == options.SourceTypeName);
                            try
                            {
                                manager.CurrentType = destinationType;
                                CopyValue(manager, added, source);
                            }
                            finally
                            {
                                if (original != null) manager.CurrentType = original;
                            }
                        }
                    }
                    // A Revit formula owns the final value. Apply any requested source values first.
                    if (options.CopyFormula) ApplyFormula(doc.FamilyManager, added, options.FormulaText);
                    transaction.Commit();
                }
                catch
                {
                    transaction.RollBack();
                    throw;
                }
            }
        }

        public static string CheckFormula(UIApplication uiapp, ParameterResult parameter, TransferOptions options)
        {
            if (options == null) return "Transfer options are missing.";
            TransferOptions trial = new TransferOptions
            {
                GroupTypeId = options.GroupTypeId,
                IsInstance = options.IsInstance,
                ChangeFormat = options.ChangeFormat,
                UnitTypeId = options.UnitTypeId,
                SymbolTypeId = options.SymbolTypeId,
                Accuracy = options.Accuracy,
                CopyFormula = true,
                FormulaText = options.FormulaText,
                SourceParameterNames = options.SourceParameterNames
            };
            string issue = Check(uiapp, parameter, trial);
            if (issue != null) return issue;
            Document doc = uiapp.ActiveUIDocument.Document;
            using (Transaction transaction = new Transaction(doc, "Check family parameter formula"))
            {
                transaction.Start();
                try
                {
                    EnsureCurrentType(doc.FamilyManager, true);
                    bool isInstance = trial.ChangeFormat ? trial.IsInstance : parameter.IsInstance;
                    FamilyParameter added = AddParameter(uiapp, doc, parameter,
                        new ForgeTypeId(trial.GroupTypeId), isInstance);
                    if (trial.ChangeFormat && !string.IsNullOrWhiteSpace(trial.UnitTypeId))
                        ChangeFormat(doc, parameter, trial);
                    ApplyFormula(doc.FamilyManager, added, trial.FormulaText);
                    return null;
                }
                catch (Exception error)
                {
                    return "Formula cannot be applied: " + error.Message;
                }
                finally
                {
                    if (transaction.GetStatus() == TransactionStatus.Started)
                        transaction.RollBack();
                }
            }
        }

        private static void EnsureCurrentType(FamilyManager manager, bool needed)
        {
            if (!needed) return;
            FamilyType first = manager.Types.Cast<FamilyType>().FirstOrDefault();
            if (first == null)
                manager.NewType("Default");
            else if (manager.CurrentType == null)
                manager.CurrentType = first;
        }

        private static FamilyParameter AddParameter(UIApplication uiapp, Document doc,
            ParameterResult parameter, ForgeTypeId groupType, bool isInstance)
        {
            if (parameter.IsShared)
                return doc.FamilyManager.AddParameter(FindSharedDefinition(uiapp, parameter), groupType, isInstance);
            ForgeTypeId dataType = new ForgeTypeId(parameter.DataTypeId);
            if (Category.IsBuiltInCategory(dataType))
            {
                BuiltInCategory builtIn = Category.GetBuiltInCategory(dataType);
                Category category = Category.GetCategory(doc, builtIn);
                if (category == null)
                    throw new InvalidOperationException("Family Type category is unavailable in this family.");
                return doc.FamilyManager.AddParameter(parameter.Name, groupType, category, isInstance);
            }
            return doc.FamilyManager.AddParameter(parameter.Name, groupType, dataType, isInstance);
        }

        private static void ApplyFormula(FamilyManager manager, FamilyParameter added, string formula)
        {
            if (!added.CanAssignFormula)
                throw new InvalidOperationException("Revit does not allow a formula on this parameter.");
            FamilyType original = manager.CurrentType;
            try
            {
                if (original == null)
                    manager.CurrentType = manager.Types.Cast<FamilyType>().First();
                manager.SetFormula(added, formula);
            }
            catch (Exception error)
            {
                throw new InvalidOperationException("Formula could not be applied. Revit: " + error.Message, error);
            }
            finally
            {
                if (original != null) manager.CurrentType = original;
            }
        }

        private static void CopyValue(FamilyManager manager, FamilyParameter target,
            FamilyTypeValueResult source)
        {
            switch (target.StorageType)
            {
                case StorageType.Double:
                    if (!source.DoubleValue.HasValue) throw new InvalidOperationException("Source double value is missing.");
                    manager.Set(target, source.DoubleValue.Value);
                    break;
                case StorageType.Integer:
                    if (!source.IntegerValue.HasValue) throw new InvalidOperationException("Source integer value is missing.");
                    manager.Set(target, source.IntegerValue.Value);
                    break;
                case StorageType.String:
                    manager.Set(target, source.StringValue ?? "");
                    break;
                default:
                    throw new InvalidOperationException("This value type cannot be copied safely.");
            }
        }

        private static void ChangeFormat(Document doc, ParameterResult parameter, TransferOptions options)
        {
            ForgeTypeId spec = new ForgeTypeId(parameter.DataTypeId);
            Units units = doc.GetUnits();
            FormatOptions existing = units.GetFormatOptions(spec);
            ForgeTypeId unit = new ForgeTypeId(options.UnitTypeId);
            FormatOptions format = existing.GetUnitTypeId().TypeId == options.UnitTypeId
                ? new FormatOptions(existing) : new FormatOptions(unit);
            if (options.Accuracy.HasValue) format.Accuracy = options.Accuracy.Value;
            if (options.SymbolTypeId != null)
            {
                ForgeTypeId symbol = options.SymbolTypeId.Length == 0
                    ? new ForgeTypeId() : new ForgeTypeId(options.SymbolTypeId);
                if (format.IsValidSymbol(symbol)) format.SetSymbolTypeId(symbol);
            }
            units.SetFormatOptions(spec, format);
            doc.SetUnits(units);
        }

        private static ExternalDefinition FindSharedDefinition(UIApplication uiapp, ParameterResult parameter)
        {
            Guid guid;
            if (!Guid.TryParse(parameter.SharedGuid, out guid)) return null;
            DefinitionFile file;
            try
            {
                file = uiapp.Application.OpenSharedParameterFile();
            }
            catch
            {
                return null;
            }
            if (file == null) return null;
            foreach (DefinitionGroup group in file.Groups)
                foreach (Definition definition in group.Definitions)
                {
                    ExternalDefinition external = definition as ExternalDefinition;
                    if (external != null && external.GUID == guid)
                        return external;
                }
            return null;
        }
    }
}
