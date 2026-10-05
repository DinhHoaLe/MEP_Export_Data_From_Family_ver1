using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Export_Data_From_Family.Services
{
    internal static class FormulaDependencyChecker
    {
        internal static List<string> ReferencedParameters(string formula, IEnumerable<string> sourceNames)
        {
            string remaining = MaskQuotedText(formula ?? "");
            List<string> referenced = new List<string>();
            foreach (string name in (sourceNames ?? Enumerable.Empty<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(x => x.Length))
            {
                string pattern = @"(?<![\p{L}\p{N}_])" + Regex.Escape(name) +
                    @"(?![\p{L}\p{N}_]|\s*\()";
                remaining = Regex.Replace(remaining, pattern, match =>
                {
                    referenced.Add(name);
                    return new string(' ', match.Length);
                }, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            }
            return referenced.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        internal static List<string> MissingParameters(string formula, IEnumerable<string> sourceNames,
            IEnumerable<string> destinationNames)
        {
            HashSet<string> available = new HashSet<string>(
                destinationNames ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            return ReferencedParameters(formula, sourceNames)
                .Where(name => !available.Contains(name)).ToList();
        }

        private static string MaskQuotedText(string formula)
        {
            StringBuilder result = new StringBuilder(formula.Length);
            bool quoted = false;
            for (int index = 0; index < formula.Length; index++)
            {
                char current = formula[index];
                if (current == '"')
                {
                    if (quoted && index + 1 < formula.Length && formula[index + 1] == '"')
                    {
                        result.Append("  ");
                        index++;
                        continue;
                    }
                    quoted = !quoted;
                    result.Append(' ');
                }
                else result.Append(quoted ? ' ' : current);
            }
            return result.ToString();
        }
    }
}
