using System.Collections.Generic;
using System.Text;

namespace LittleCastle.World
{
    public sealed class WorldConfigurationValidationReport
    {
        private readonly List<string> errors = new List<string>();
        private readonly List<string> warnings = new List<string>();
        private readonly List<string> info = new List<string>();

        public IReadOnlyList<string> Errors => errors;
        public IReadOnlyList<string> Warnings => warnings;
        public IReadOnlyList<string> Info => info;

        public bool IsValid => errors.Count == 0;

        public void AddError(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
                errors.Add(message);
        }

        public void AddWarning(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
                warnings.Add(message);
        }

        public void AddInfo(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
                info.Add(message);
        }

        public string ToMultilineString()
        {
            var builder = new StringBuilder();

            builder.AppendLine(
                IsValid
                    ? "World configuration validation passed."
                    : "World configuration validation failed.");

            builder.AppendLine(
                "Errors: " + errors.Count +
                ", warnings: " + warnings.Count +
                ", info: " + info.Count);

            AppendSection(builder, "ERROR", errors);
            AppendSection(builder, "WARN", warnings);
            AppendSection(builder, "INFO", info);

            return builder.ToString();
        }

        private static void AppendSection(
            StringBuilder builder,
            string label,
            IReadOnlyList<string> messages)
        {
            for (int i = 0; i < messages.Count; i++)
            {
                builder.Append("[");
                builder.Append(label);
                builder.Append("] ");
                builder.AppendLine(messages[i]);
            }
        }
    }
}
