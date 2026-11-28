// 261127_code
// 261127_documentation

using System.Text.Json;
using System.Text.Json.Serialization;

namespace EchoesOfTheShardveil.Desktop;

/// <summary>Reads JSON files distributed with the desktop app.</summary>
internal static class AppDataJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        RespectRequiredConstructorParameters = true,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },
    };

    // Fully extracted single-file builds can report the extraction cache as BaseDirectory.
    // Their AppData is shipped beside the app host. With dotnet MyApp.dll it lives beside the DLL.
    private static readonly string AppDataDirectory = Path.Combine(GetDeploymentDirectory(), "AppData");

    /// <summary>Gets the directory from which the desktop app was deployed.</summary>
    /// <returns>The app host directory, or the managed application's base directory under dotnet.</returns>
    private static string GetDeploymentDirectory()
    {
        var executable = Environment.ProcessPath;
        return executable is not null &&
            !string.Equals(Path.GetFileNameWithoutExtension(executable), "dotnet", StringComparison.OrdinalIgnoreCase)
            ? Path.GetDirectoryName(executable) ?? AppContext.BaseDirectory
            : AppContext.BaseDirectory;
    }

    /// <summary>Resolves a file in the deployed AppData directory.</summary>
    /// <param name="parts">The path segments relative to AppData.</param>
    /// <returns>The absolute path of the data file.</returns>
    public static string PathFor(params string[] parts) =>
        Path.Combine([AppDataDirectory, .. parts]);

    /// <summary>Resolves an AppData-relative content reference from a configuration file.</summary>
    /// <param name="reference">A relative path using forward slashes.</param>
    /// <returns>The absolute path inside AppData.</returns>
    /// <exception cref="InvalidDataException">The reference is not a relative path inside AppData.</exception>
    public static string ResolveReference(string reference)
    {
        var parts = reference.Split('/');
        if (!parts.All(IsSafeName))
        {
            throw new InvalidDataException($"Invalid AppData reference '{reference}'.");
        }
        return PathFor(parts);
    }

    /// <summary>Determines whether text names a single file or folder that cannot leave its parent.</summary>
    /// <param name="name">The file or folder name.</param>
    /// <returns><see langword="true"/> if the name is not blank, relative or a path.</returns>
    public static bool IsSafeName(string name) =>
        !string.IsNullOrWhiteSpace(name) && name is not ("." or "..") && name.IndexOfAny(['/', '\\', ':']) < 0;

    /// <summary>Reads a JSON document, reporting its file path if parsing fails.</summary>
    /// <typeparam name="T">The document type.</typeparam>
    /// <param name="path">The path of the data file.</param>
    /// <returns>The deserialized document.</returns>
    /// <exception cref="InvalidDataException">The file does not hold a valid document.</exception>
    /// <exception cref="IOException">The file cannot be read.</exception>
    public static T Load<T>(string path) where T : class
    {
        using var stream = File.OpenRead(path);
        try
        {
            return JsonSerializer.Deserialize<T>(stream, Options)
                ?? throw new InvalidDataException($"'{path}' must contain a JSON document.");
        }
        catch (JsonException error)
        {
            throw new InvalidDataException($"Invalid JSON in '{path}': {error.Message}", error);
        }
    }

    /// <summary>Reads one object from a larger AppData document.</summary>
    /// <typeparam name="T">The object type.</typeparam>
    /// <param name="element">The JSON element to read.</param>
    /// <param name="path">The containing file path for diagnostics.</param>
    /// <returns>The deserialized object.</returns>
    /// <exception cref="InvalidDataException">The element is not a valid object.</exception>
    public static T ReadElement<T>(JsonElement element, string path) where T : class
    {
        try
        {
            return element.Deserialize<T>(Options)
                ?? throw new InvalidDataException($"'{path}' contains an empty object.");
        }
        catch (JsonException error)
        {
            throw new InvalidDataException($"Invalid JSON object in '{path}': {error.Message}", error);
        }
    }
}
