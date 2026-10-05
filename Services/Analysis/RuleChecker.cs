using Export_Data_From_Family.Models;

using System.Collections.Generic;
using System.Linq;

namespace Export_Data_From_Family.Services
{
    public static class RuleChecker
    {
        public static void Run(
            FamilyHealthResult family)
        {
            family.Checks =
                new List<CheckResult>();

            CheckFileSize(family);

            CheckParameter(
                family,
                "A",
                true
            );

            CheckParameter(
                family,
                "B",
                true
            );

            CheckParameter(
                family,
                "Độ dày",
                false
            );

            CheckNestedFamily(family);

            CalculateStatus(family);
        }


        private static void CheckFileSize(
            FamilyHealthResult family)
        {
            if (family.FileSizeMB <= 2.0)
            {
                family.Checks.Add(
                    new CheckResult
                    {
                        Rule = "File Size",
                        Status = "PASS",
                        Message =
                            family.FileSizeMB +
                            " MB"
                    }
                );
            }
            else
            {
                family.Checks.Add(
                    new CheckResult
                    {
                        Rule = "File Size",
                        Status = "WARNING",
                        Message =
                            "Family > 2 MB"
                    }
                );
            }
        }


        private static void CheckParameter(
            FamilyHealthResult family,
            string parameterName,
            bool shouldBeInstance)
        {
            ParameterResult parameter =
                family.Parameters
                    .FirstOrDefault(
                        x =>
                        x.Name ==
                        parameterName
                    );

            if (parameter == null)
            {
                family.Checks.Add(
                    new CheckResult
                    {
                        Rule =
                            "Parameter: " +
                            parameterName,

                        Status = "FAIL",

                        Message =
                            "Parameter missing"
                    }
                );

                return;
            }


            if (
                parameter.IsInstance !=
                shouldBeInstance)
            {
                family.Checks.Add(
                    new CheckResult
                    {
                        Rule =
                            "Parameter: " +
                            parameterName,

                        Status = "FAIL",

                        Message =
                            shouldBeInstance
                            ? "Should be Instance"
                            : "Should be Type"
                    }
                );

                return;
            }


            family.Checks.Add(
                new CheckResult
                {
                    Rule =
                        "Parameter: " +
                        parameterName,

                    Status = "PASS",

                    Message = "OK"
                }
            );
        }


        private static void CheckNestedFamily(
            FamilyHealthResult family)
        {
            if (
                family.NestedFamilyCount == 0)
            {
                family.Checks.Add(
                    new CheckResult
                    {
                        Rule =
                            "Nested Families",

                        Status =
                            "PASS",

                        Message =
                            "No nested family"
                    }
                );
            }
            else
            {
                family.Checks.Add(
                    new CheckResult
                    {
                        Rule =
                            "Nested Families",

                        Status =
                            "WARNING",

                        Message =
                            family
                            .NestedFamilyCount +
                            " nested families"
                    }
                );
            }
        }


        private static void CalculateStatus(
            FamilyHealthResult family)
        {
            if (
                family.Checks.Any(
                    x =>
                    x.Status == "FAIL"
                ))
            {
                family.Status =
                    "FAIL";

                return;
            }

            if (
                family.Checks.Any(
                    x =>
                    x.Status ==
                    "WARNING"
                ))
            {
                family.Status =
                    "WARNING";

                return;
            }

            family.Status =
                "PASS";
        }
    }
}