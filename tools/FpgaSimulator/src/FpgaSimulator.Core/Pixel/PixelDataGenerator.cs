namespace FpgaSimulator.Core.Pixel;

/// <summary>
/// Generates test pattern pixel data for FpgaSimulator testing.
/// Supports multiple patterns: counter, constant, random, checkerboard.
/// Implements AC-SIM-001 (counter pattern) and AC-SIM-012 (deterministic output).
/// </summary>
public sealed class PixelDataGenerator
{
    private readonly object _lock = new();
    private Random _random;
    private ushort _constantValue;
    private int _bitDepth;
    private PatternMode _patternMode;
    private int _seed;

    /// <summary>
    /// Initializes a new instance with 16-bit depth and counter pattern.
    /// </summary>
    public PixelDataGenerator() : this(bitDepth: 16, patternMode: PatternMode.Counter)
    {
    }

    /// <summary>
    /// Initializes a new instance with specified bit depth and pattern mode.
    /// </summary>
    /// <param name="bitDepth">Pixel bit depth (14 or 16)</param>
    /// <param name="patternMode">Test pattern mode</param>
    public PixelDataGenerator(int bitDepth, PatternMode patternMode)
    {
        _bitDepth = Math.Clamp(bitDepth, 14, 16);
        _patternMode = patternMode;
        _seed = Environment.TickCount;
        _random = new Random(_seed);
        _constantValue = 0x8000; // Mid-scale default
    }

    /// <summary>Current pixel bit depth</summary>
    public int BitDepth
    {
        get
        {
            lock (_lock)
            {
                return _bitDepth;
            }
        }
    }

    /// <summary>Current pattern mode</summary>
    public PatternMode PatternMode
    {
        get
        {
            lock (_lock)
            {
                return _patternMode;
            }
        }
    }

    /// <summary>
    /// Sets the seed for random number generation.
    /// Ensures deterministic output for AC-SIM-012 compliance.
    /// </summary>
    /// <param name="seed">Random seed value</param>
    public void SetSeed(int seed)
    {
        lock (_lock)
        {
            _seed = seed;
            _random = new Random(_seed);
        }
    }

    /// <summary>
    /// Sets the constant value used in Constant pattern mode.
    /// </summary>
    /// <param name="value">Constant pixel value</param>
    public void SetConstantValue(ushort value)
    {
        lock (_lock)
        {
            _constantValue = value;
        }
    }

    /// <summary>
    /// Generates a frame of pixel data using the current pattern mode.
    /// </summary>
    /// <param name="rows">Number of rows (max 3072)</param>
    /// <param name="cols">Number of columns (max 3072)</param>
    /// <returns>2D pixel array [rows, cols]</returns>
    public ushort[,] GenerateFrame(int rows, int cols)
    {
        rows = Math.Min(rows, 3072);
        cols = Math.Min(cols, 3072);

        var frame = new ushort[rows, cols];
        var maxValue = (1 << _bitDepth) - 1;

        lock (_lock)
        {
            switch (_patternMode)
            {
                case PatternMode.Counter:
                    GenerateCounterPattern(frame, rows, cols, maxValue);
                    break;

                case PatternMode.Constant:
                    GenerateConstantPattern(frame, rows, cols);
                    break;

                case PatternMode.Random:
                    GenerateRandomPattern(frame, rows, cols, maxValue);
                    break;

                case PatternMode.Checkerboard:
                    GenerateCheckerboardPattern(frame, rows, cols, maxValue);
                    break;
            }
        }

        return frame;
    }

    private void GenerateCounterPattern(ushort[,] frame, int rows, int cols, int maxValue)
    {
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                // pixel[r][c] = (r * cols + c) % 2^bit_depth
                var value = (row * cols + col) % (maxValue + 1);
                frame[row, col] = (ushort)value;
            }
        }
    }

    private void GenerateConstantPattern(ushort[,] frame, int rows, int cols)
    {
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                frame[row, col] = _constantValue;
            }
        }
    }

    private void GenerateRandomPattern(ushort[,] frame, int rows, int cols, int maxValue)
    {
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                var value = _random.Next(maxValue + 1);
                frame[row, col] = (ushort)value;
            }
        }
    }

    private void GenerateCheckerboardPattern(ushort[,] frame, int rows, int cols, int maxValue)
    {
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                // Even positions = 0, Odd positions = max
                var isEven = (row + col) % 2 == 0;
                frame[row, col] = isEven ? (ushort)0 : (ushort)maxValue;
            }
        }
    }
}
