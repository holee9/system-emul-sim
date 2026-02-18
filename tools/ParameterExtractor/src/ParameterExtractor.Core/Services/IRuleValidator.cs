using ParameterExtractor.Core.Models;

namespace ParameterExtractor.Core.Services;

/// <summary>
/// Service for validating extracted parameters against known constraints.
/// </summary>
public interface IRuleValidator
{
    /// <summary>
    /// Validates a single parameter against applicable rules.
    /// </summary>
    /// <param name="parameter">The parameter to validate.</param>
    /// <returns>The same parameter with updated validation status and message.</returns>
    ParameterInfo Validate(ParameterInfo parameter);

    /// <summary>
    /// Validates all parameters in a collection.
    /// </summary>
    /// <param name="parameters">The parameters to validate.</param>
    /// <returns>Dictionary mapping parameter names to validation results.</returns>
    IDictionary<string, ValidationStatus> ValidateMany(IEnumerable<ParameterInfo> parameters);

    /// <summary>
    /// Adds a custom validation rule.
    /// </summary>
    /// <param name="ruleName">Name of the rule.</param>
    /// <param name="rule">Validation function that returns status and message.</param>
    void AddRule(string ruleName, Func<ParameterInfo, (ValidationStatus Status, string Message)> rule);
}
