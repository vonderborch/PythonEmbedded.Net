using System.Text.Json;
using System.Text.Json.Serialization;

namespace PythonEmbedded.Net.Helpers;

/// <summary>
/// Provides utility methods for working with JSON data, offering functionalities
/// for parsing, serialization, and deserialization of JSON strings and objects.
/// </summary>
public static class JsonHelpers
{
    /// <summary>
    /// A pre-configured instance of <see cref="JsonSerializerOptions"/> used globally
    /// for JSON serialization and deserialization operations within the application.
    /// </summary>
    public static readonly JsonSerializerOptions JsonSerializeOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        IncludeFields = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    
    /// <summary>
    ///     Deserializes a JSON file to the specified type.
    /// </summary>
    /// <param name="path">The path to the JSON file.</param>
    /// <param name="options">Options to use when deserializing the file.</param>
    /// <typeparam name="TValue">The expected type.</typeparam>
    /// <returns>The type, or the default for the type.</returns>
    public static TValue? DeserializeFromFile<TValue>(string path, JsonSerializerOptions? options = null)
    {
        if (!File.Exists(path))
        {
            return default;
        }

        var rawContents = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(rawContents))
        {
            return default;
        }

        return DeserializeString<TValue>(rawContents, options);
    }

    /// <summary>
    ///     Deserializes a JSON string to the specified type.
    /// </summary>
    /// <param name="contents">The contents to deserialize.</param>
    /// <param name="options">Options to use when deserializing the file.</param>
    /// <param name="raiseError">Whether to raise an error if the deserialization fails.</param>
    /// <typeparam name="TValue">The expected type.</typeparam>
    /// <returns>The type, or the default for the type.</returns>
    public static TValue? DeserializeString<TValue>(string contents, JsonSerializerOptions? options = null,
        bool raiseError = false)
    {
        if (string.IsNullOrWhiteSpace(contents))
        {
            return default;
        }

        var actualOptions = options ?? JsonSerializeOptions;

        try
        {
            return JsonSerializer.Deserialize<TValue>(contents, actualOptions);
        }
        catch
        {
            if (raiseError)
            {
                throw;
            }

            return default;
        }
    }

    /// <summary>
    ///     Serializes an object to a JSON file.
    /// </summary>
    /// <param name="path">The file to output the JSON string to.</param>
    /// <param name="obj">The object to serialize.</param>
    /// <param name="options">Options to use when serializing the file.</param>
    public static void SerializeToFile(string path, object obj, JsonSerializerOptions? options = null)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        var contents = SerializeToString(obj, options);
        File.WriteAllText(path, contents);
    }

    /// <summary>
    ///     Serializes an object to a JSON string.
    /// </summary>
    /// <param name="obj">The object to serialize.</param>
    /// <param name="options">Options to use when serializing the file.</param>
    /// <returns>The JSON string representing the object.</returns>
    public static string SerializeToString(object obj, JsonSerializerOptions? options = null)
    {
        var actualOptions = options ?? JsonSerializeOptions;
        var contents = JsonSerializer.Serialize(obj, actualOptions);

        return contents;
    }
}
