using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using ConfigConverter.Models;

namespace ConfigConverter.Services;

/// <summary>
/// YAML parser for detector configuration files.
/// </summary>
public class YamlParser
{
    private readonly IDeserializer _deserializer;

    public YamlParser()
    {
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    /// <summary>
    /// Parses YAML content into a DetectorConfig object.
    /// </summary>
    /// <param name="yamlContent">YAML content as string</param>
    /// <returns>Parsed DetectorConfig object</returns>
    /// <exception cref="ArgumentException">Thrown when YAML content is null or empty</exception>
    /// <exception cref="InvalidOperationException">Thrown when YAML parsing fails</exception>
    public DetectorConfig Parse(string yamlContent)
    {
        if (string.IsNullOrWhiteSpace(yamlContent))
        {
            throw new ArgumentException("YAML content cannot be empty or null.", nameof(yamlContent));
        }

        try
        {
            return _deserializer.Deserialize<DetectorConfig>(yamlContent)
                ?? throw new InvalidOperationException("Deserialization returned null.");
        }
        catch (Exception ex) when (ex is not ArgumentException and not InvalidOperationException)
        {
            throw new InvalidOperationException($"Failed to parse YAML content: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Parses YAML file into a DetectorConfig object.
    /// </summary>
    /// <param name="filePath">Path to YAML file</param>
    /// <returns>Parsed DetectorConfig object</returns>
    public DetectorConfig ParseFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"YAML file not found: {filePath}");
        }

        var yamlContent = File.ReadAllText(filePath);
        return Parse(yamlContent);
    }
}
