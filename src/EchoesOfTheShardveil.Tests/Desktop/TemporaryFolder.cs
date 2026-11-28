// 261128_code
// 261128_documentation

namespace EchoesOfTheShardveil.Tests.Desktop;

/// <summary>A folder for a test's own files, deleted when the test ends.</summary>
internal sealed class TemporaryFolder : IDisposable
{
    /// <summary>Creates an empty folder.</summary>
    /// <param name="path">The folder to create, or null for a new one in the system temporary directory.</param>
    public TemporaryFolder(string? path = null)
    {
        Root = path ?? Path.Combine(Path.GetTempPath(), "EchoesTests-" + Path.GetRandomFileName());
        Directory.CreateDirectory(Root);
    }

    /// <summary>Gets the folder path.</summary>
    public string Root { get; }

    /// <summary>Writes a file in the folder.</summary>
    /// <param name="name">The file name.</param>
    /// <param name="content">The file content.</param>
    /// <returns>The full path of the file.</returns>
    public string Write(string name, string content)
    {
        var path = Path.Combine(Root, name);
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>Deletes the folder and its files.</summary>
    public void Dispose() => Directory.Delete(Root, recursive: true);
}
