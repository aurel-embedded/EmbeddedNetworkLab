using System.Globalization;
using System.Windows.Controls;

namespace EmbeddedNetworkLab.UI.Validation
{
    public class PortRangeValidationRule : ValidationRule
    {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            string input = value as string ?? value?.ToString() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(input))
                return new ValidationResult(false, "Port is required.");

            if (!int.TryParse(input, out int port))
                return new ValidationResult(false, "Port must be a number.");

            if (port < 1 || port > 65535)
                return new ValidationResult(false, "Port must be between 1 and 65535.");

            return ValidationResult.ValidResult;
        }
    }
}
