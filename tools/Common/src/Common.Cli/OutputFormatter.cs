using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Common.Cli;

/// <summary>
/// Output format for CLI results.
/// </summary>
public enum OutputFormat
{
    /// <summary>JSON text output.</summary>
    Json,

    /// <summary>Comma-separated values.</summary>
    Csv,

    /// <summary>Raw binary output.</summary>
    Binary,

    /// <summary>Human-readable table.</summary>
    Table
}

/// <summary>
/// Provides formatting utilities for CLI output in JSON, CSV, binary, and table formats.
/// </summary>
public static class OutputFormatter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Writes an object as formatted JSON to a file or stdout.
    /// </summary>
    /// <typeparam name="T">Type of data to serialize.</typeparam>
    /// <param name="data">Data object.</param>
    /// <param name="path">Output file path. If null, writes to stdout.</param>
    public static void WriteJson<T>(T data, string? path = null)
    {
        string json = JsonSerializer.Serialize(data, JsonOptions);
        if (path is not null)
        {
            File.WriteAllText(path, json, Encoding.UTF8);
        }
        else
        {
            Console.WriteLine(json);
        }
    }

    /// <summary>
    /// Writes a 2D ushort frame as CSV to a file or stdout.
    /// </summary>
    /// <param name="frame">2D pixel array [rows, cols].</param>
    /// <param name="path">Output file path. If null, writes to stdout.</param>
    public static void WriteCsv(ushort[,] frame, string? path = null)
    {
        ArgumentNullException.ThrowIfNull(frame);

        int rows = frame.GetLength(0);
        int cols = frame.GetLength(1);

        using TextWriter writer = path is not null
            ? new StreamWriter(path, false, Encoding.UTF8)
            : Console.Out;

        for (int r = 0; r < rows; r++)
        {
            var sb = new StringBuilder(cols * 6);
            for (int c = 0; c < cols; c++)
            {
                if (c > 0) sb.Append(',');
                sb.Append(frame[r, c].ToString(CultureInfo.InvariantCulture));
            }

            writer.WriteLine(sb.ToString());
        }
    }

    /// <summary>
    /// Writes raw binary data with a file header (magic + version).
    /// </summary>
    /// <param name="data">Raw binary payload.</param>
    /// <param name="path">Output file path.</param>
    /// <param name="magic">4-character magic string for file identification.</param>
    /// <param name="version">Format version number.</param>
    /// <exception cref="ArgumentException">Thrown when magic is not exactly 4 characters.</exception>
    public static void WriteBinary(byte[] data, string path, string magic, uint version)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(path);

        if (magic.Length != 4)
            throw new ArgumentException("Magic string must be exactly 4 characters.", nameof(magic));

        using var fs = File.Create(path);
        // Write magic
        fs.Write(Encoding.ASCII.GetBytes(magic));
        // Write version (little-endian)
        Span<byte> versionBytes = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(versionBytes, version);
        fs.Write(versionBytes);
        // Write payload
        fs.Write(data);
    }

    /// <summary>
    /// Writes a key-value statistics table to a TextWriter.
    /// </summary>
    /// <param name="stats">Dictionary of stat name to value.</param>
    /// <param name="output">Target TextWriter. If null, writes to stdout.</param>
    public static void WriteTable(Dictionary<string, object> stats, TextWriter? output = null)
    {
        ArgumentNullException.ThrowIfNull(stats);

        output ??= Console.Out;

        if (stats.Count == 0)
        {
            output.WriteLine("(no data)");
            return;
        }

        int maxKeyLen = 0;
        foreach (var key in stats.Keys)
        {
            if (key.Length > maxKeyLen) maxKeyLen = key.Length;
        }

        string separator = new('-', maxKeyLen + 20);
        output.WriteLine(separator);
        output.WriteLine($"{"Property".PadRight(maxKeyLen + 2)}Value");
        output.WriteLine(separator);

        foreach (var (key, value) in stats)
        {
            string valueStr = value switch
            {
                double d => d.ToString("F4", CultureInfo.InvariantCulture),
                float f => f.ToString("F4", CultureInfo.InvariantCulture),
                _ => value?.ToString() ?? "(null)"
            };
            output.WriteLine($"{key.PadRight(maxKeyLen + 2)}{valueStr}");
        }

        output.WriteLine(separator);
    }
}
