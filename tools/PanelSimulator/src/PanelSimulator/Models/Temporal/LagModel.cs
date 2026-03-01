using System;

namespace PanelSimulator.Models.Temporal;

/// <summary>
/// Configuration for the lag (ghosting) model.
/// </summary>
/// <param name="LagCoefficient">Fraction of previous frame signal retained (typical: 0.01-0.03).</param>
/// <param name="DecayOrder">Number of previous frames contributing to lag (1 = single exponential).</param>
public sealed record LagConfig(
    double LagCoefficient = 0.02,
    int DecayOrder = 1);

/// <summary>
/// Simulates image lag (ghosting) - residual signal from previous frames.
/// Uses exponential decay model where a fraction of the previous frame's signal
/// carries over into the current frame.
/// </summary>
public sealed class LagModel
{
    private readonly LagConfig _config;
    private readonly ushort[,]?[] _frameHistory;
    private int _historyCount;
    private readonly object _lock = new();

    /// <summary>
    /// Initializes a new instance of the LagModel.
    /// </summary>
    /// <param name="config">Lag configuration. Uses defaults if null.</param>
    public LagModel(LagConfig? config = null)
    {
        _config = config ?? new LagConfig();

        if (_config.LagCoefficient < 0 || _config.LagCoefficient >= 1.0)
        {
            throw new ArgumentException(
                "Lag coefficient must be in [0, 1.0).", nameof(config));
        }

        if (_config.DecayOrder < 1)
        {
            throw new ArgumentException(
                "Decay order must be at least 1.", nameof(config));
        }

        _frameHistory = new ushort[,]?[_config.DecayOrder];
        _historyCount = 0;
    }

    /// <summary>
    /// Gets the current configuration.
    /// </summary>
    public LagConfig Config => _config;

    /// <summary>
    /// Gets the number of frames in history.
    /// </summary>
    public int HistoryCount
    {
        get
        {
            lock (_lock)
            {
                return _historyCount;
            }
        }
    }

    /// <summary>
    /// Applies lag effect to the current frame based on frame history.
    /// output[r,c] = current[r,c] + sum(lagCoeff^i * history[i][r,c]) for i in 1..N.
    /// The result is clamped to [0, 65535].
    /// </summary>
    /// <param name="currentFrame">Current frame before lag application.</param>
    /// <returns>New frame with ghosting artifacts from previous frames.</returns>
    public ushort[,] ApplyLag(ushort[,] currentFrame)
    {
        if (currentFrame == null)
        {
            throw new ArgumentNullException(nameof(currentFrame));
        }

        int rows = currentFrame.GetLength(0);
        int cols = currentFrame.GetLength(1);

        lock (_lock)
        {
            var result = new ushort[rows, cols];

            if (_config.LagCoefficient <= 0 || _historyCount == 0)
            {
                // No lag to apply, just copy and store
                Array.Copy(currentFrame, result, currentFrame.Length);
                PushHistory(currentFrame);
                return result;
            }

            // Apply exponential decay from history frames
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    double value = currentFrame[r, c];

                    // Add lag contributions from history (most recent first)
                    double decayFactor = _config.LagCoefficient;
                    int framesToProcess = Math.Min(_historyCount, _config.DecayOrder);

                    for (int i = 0; i < framesToProcess; i++)
                    {
                        var histFrame = _frameHistory[i];
                        if (histFrame != null &&
                            histFrame.GetLength(0) == rows &&
                            histFrame.GetLength(1) == cols)
                        {
                            value += decayFactor * histFrame[r, c];
                        }

                        // Each older frame contributes exponentially less
                        decayFactor *= _config.LagCoefficient;
                    }

                    result[r, c] = ClampToUShort(value);
                }
            }

            PushHistory(currentFrame);
            return result;
        }
    }

    /// <summary>
    /// Resets the frame history, clearing all lag state.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            Array.Clear(_frameHistory);
            _historyCount = 0;
        }
    }

    /// <summary>
    /// Pushes a frame into the history buffer (most recent at index 0).
    /// Oldest frame is discarded when the buffer is full.
    /// </summary>
    private void PushHistory(ushort[,] frame)
    {
        // Shift history: move all entries one position to the right
        for (int i = _frameHistory.Length - 1; i > 0; i--)
        {
            _frameHistory[i] = _frameHistory[i - 1];
        }

        // Clone the frame to prevent external mutation
        var clone = new ushort[frame.GetLength(0), frame.GetLength(1)];
        Array.Copy(frame, clone, frame.Length);
        _frameHistory[0] = clone;

        if (_historyCount < _config.DecayOrder)
        {
            _historyCount++;
        }
    }

    /// <summary>
    /// Clamps a double value to the ushort range [0, 65535].
    /// </summary>
    private static ushort ClampToUShort(double value)
    {
        if (value < 0) return 0;
        if (value > 65535) return 65535;
        return (ushort)Math.Round(value);
    }
}
