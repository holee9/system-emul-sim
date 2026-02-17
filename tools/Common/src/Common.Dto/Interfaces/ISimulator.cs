namespace Common.Dto.Interfaces;

/// <summary>
/// Defines the standard interface for all simulator implementations.
/// REQ-SIM-050: Common.Dto shall define the ISimulator interface.
/// </summary>
public interface ISimulator
{
    /// <summary>
    /// Initializes the simulator with the specified configuration.
    /// </summary>
    /// <param name="config">Configuration object for the simulator.</param>
    void Initialize(object config);

    /// <summary>
    /// Processes the input data through the simulator.
    /// </summary>
    /// <param name="input">Input data to process.</param>
    /// <returns>Processed output data.</returns>
    object Process(object input);

    /// <summary>
    /// Resets the simulator to its initial state.
    /// </summary>
    void Reset();

    /// <summary>
    /// Gets the current status of the simulator.
    /// </summary>
    /// <returns>Status description string.</returns>
    string GetStatus();
}
