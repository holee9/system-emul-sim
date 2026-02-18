using ParameterExtractor.Core.Models;

namespace ParameterExtractor.Core.Services;

/// <summary>
/// Rule validator implementation for detector parameters.
/// </summary>
public class RuleValidator : IRuleValidator
{
    private readonly Dictionary<string, Func<ParameterInfo, (ValidationStatus Status, string Message)>> _rules = new();

    public RuleValidator()
    {
        RegisterDefaultRules();
    }

    /// <inheritdoc />
    public ParameterInfo Validate(ParameterInfo parameter)
    {
        parameter.ValidationStatus = ValidationStatus.Valid;
        parameter.ValidationMessage = string.Empty;

        // Apply numeric range validation if min/max specified
        if (parameter.NumericValue.HasValue)
        {
            if (parameter.Min.HasValue && parameter.NumericValue < parameter.Min)
            {
                parameter.ValidationStatus = ValidationStatus.Error;
                parameter.ValidationMessage = $"Value {parameter.NumericValue} is below minimum {parameter.Min}.";
                return parameter;
            }

            if (parameter.Max.HasValue && parameter.NumericValue > parameter.Max)
            {
                parameter.ValidationStatus = ValidationStatus.Error;
                parameter.ValidationMessage = $"Value {parameter.NumericValue} exceeds maximum {parameter.Max}.";
                return parameter;
            }
        }

        // Apply registered rules by parameter name pattern
        var applicableRules = _rules.Where(r =>
            parameter.Name.Contains(r.Key, StringComparison.OrdinalIgnoreCase));

        foreach (var rule in applicableRules)
        {
            var (status, message) = rule.Value(parameter);
            if (status != ValidationStatus.Valid)
            {
                parameter.ValidationStatus = status;
                parameter.ValidationMessage = message;
                return parameter;
            }
        }

        return parameter;
    }

    /// <inheritdoc />
    public IDictionary<string, ValidationStatus> ValidateMany(IEnumerable<ParameterInfo> parameters)
    {
        var results = new Dictionary<string, ValidationStatus>();

        foreach (var param in parameters)
        {
            Validate(param);
            results[param.Name] = param.ValidationStatus;
        }

        return results;
    }

    /// <inheritdoc />
    public void AddRule(string ruleName, Func<ParameterInfo, (ValidationStatus, string)> rule)
    {
        _rules[ruleName] = rule;
    }

    private void RegisterDefaultRules()
    {
        // Panel-specific rules
        AddRule("pixel", p =>
        {
            if (p.NumericValue.HasValue && p.NumericValue.Value <= 0)
            {
                return (ValidationStatus.Error, "Pixel pitch must be greater than 0.");
            }

            if (p.NumericValue.HasValue && p.NumericValue.Value > 500)
            {
                return (ValidationStatus.Warning, "Pixel pitch exceeds typical range (50-500 um).");
            }

            return (ValidationStatus.Valid, string.Empty);
        });

        AddRule("bit", p =>
        {
            if (p.NumericValue.HasValue)
            {
                var bitDepth = (int)p.NumericValue.Value;
                if (bitDepth is not 14 and not 16)
                {
                    return (ValidationStatus.Error, "Bit depth must be 14 or 16.");
                }
            }

            return (ValidationStatus.Valid, string.Empty);
        });

        AddRule("row", p =>
        {
            if (p.NumericValue.HasValue)
            {
                var rows = (int)p.NumericValue.Value;
                if (rows < 256 || rows > 4096)
                {
                    return (ValidationStatus.Error, "Rows must be between 256 and 4096.");
                }
            }

            return (ValidationStatus.Valid, string.Empty);
        });

        AddRule("col", p =>
        {
            if (p.NumericValue.HasValue)
            {
                var cols = (int)p.NumericValue.Value;
                if (cols < 256 || cols > 4096)
                {
                    return (ValidationStatus.Error, "Columns must be between 256 and 4096.");
                }
            }

            return (ValidationStatus.Valid, string.Empty);
        });

        // Timing rules
        AddRule("gate", p =>
        {
            if (p.NumericValue.HasValue && p.NumericValue.Value <= 0)
            {
                return (ValidationStatus.Error, "Gate timing must be greater than 0.");
            }

            if (p.NumericValue.HasValue && p.NumericValue.Value > 1000)
            {
                return (ValidationStatus.Warning, "Gate timing exceeds 1000 us, verify this is correct.");
            }

            return (ValidationStatus.Valid, string.Empty);
        });

        // CSI-2 rules
        AddRule("lane", p =>
        {
            if (p.NumericValue.HasValue)
            {
                var lanes = (int)p.NumericValue.Value;
                if (lanes is not 1 and not 2 and not 4)
                {
                    return (ValidationStatus.Error, "Lane count must be 1, 2, or 4.");
                }
            }

            return (ValidationStatus.Valid, string.Empty);
        });

        AddRule("mbps", p =>
        {
            if (p.NumericValue.HasValue)
            {
                var speed = p.NumericValue.Value;
                if (speed < 400 || speed > 1250)
                {
                    return (ValidationStatus.Warning, "Lane speed outside verified range (400-1250 Mbps).");
                }
            }

            return (ValidationStatus.Valid, string.Empty);
        });
    }
}
