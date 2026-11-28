// 261128_code
// 261128_documentation

using EchoesOfTheShardveil.Core;
using EchoesOfTheShardveil.Desktop;

namespace EchoesOfTheShardveil.Tests.Desktop;

/// <summary>Checks that broken data files are reported rather than half loaded.</summary>
public sealed class LoadErrorTests
{
    /// <summary>References cannot leave AppData.</summary>
    /// <param name="reference">An unsafe reference.</param>
    [Theory]
    [InlineData("")]
    [InlineData("..")]
    [InlineData("../secret.json")]
    [InlineData("Deck/../../secret.json")]
    [InlineData("/rooted.json")]
    [InlineData("C:/rooted.json")]
    [InlineData("Deck\\AlphaPlayer.json")]
    [InlineData("Deck//AlphaPlayer.json")]
    public void UnsafeReferencesAreRejected(string reference) =>
        Assert.Throws<InvalidDataException>(() => AppDataJson.ResolveReference(reference));

    /// <summary>A malformed or incomplete document names its file in the error.</summary>
    /// <param name="json">The file content.</param>
    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("""{ "id": "a" }""")]
    [InlineData("""{ "id": "a", "name": "A", "type": 1 }""")]
    [InlineData("""{ "id": "a", "name": "A", "type": "Dragon" }""")]
    public void InvalidDocumentNamesTheFile(string json)
    {
        using var folder = new TemporaryFolder();
        var path = folder.Write("broken.json", json);

        var error = Assert.Throws<InvalidDataException>(() => AppDataJson.Load<CardDefinition>(path));

        Assert.Contains(path, error.Message, StringComparison.Ordinal);
    }

    /// <summary>A missing file is an I/O error.</summary>
    [Fact]
    public void MissingFileIsReported()
    {
        using var folder = new TemporaryFolder();

        Assert.Throws<FileNotFoundException>(() =>
            AppDataJson.Load<CardDefinition>(Path.Combine(folder.Root, "absent.json")));
    }

    /// <summary>A card set with a bad name, no cards, or a bad card is rejected.</summary>
    /// <param name="content">The set file, with <c>$NAME</c> standing for the set name.</param>
    [Theory]
    [InlineData("""{ "name": "Other", "cards": [ { "id": "a", "name": "A" } ] }""")]
    [InlineData("""{ "name": "$NAME", "cards": [] }""")]
    [InlineData("""{ "name": "$NAME", "cards": [ { "id": "a", "name": " " } ] }""")]
    [InlineData("""{ "name": "$NAME", "cards": [ { "id": "a", "name": "A", "abilities": null } ] }""")]
    [InlineData("""{ "name": "$NAME", "cards": [ { "id": "a", "name": "A" }, { "id": "a", "name": "B" } ] }""")]
    [InlineData("""{ "name": "$NAME", "cards": [ { "id": "a", "name": "A", "artwork": "../a.png" } ] }""")]
    public void BrokenCardSetIsRejected(string content)
    {
        // Sets load only from AppData, so this writes a throwaway set into the test output's copy.
        var name = "Test" + Guid.NewGuid().ToString("N");
        using var folder = new TemporaryFolder(AppDataJson.PathFor("Set", name));
        folder.Write(name + ".cards", content.Replace("$NAME", name, StringComparison.Ordinal));

        Assert.Throws<InvalidDataException>(() => CardSet.Load(name));
    }

    /// <summary>A set name cannot be a path.</summary>
    [Fact]
    public void CardSetNameCannotBeAPath() =>
        Assert.Throws<InvalidDataException>(() => CardSet.Load("../Alpha"));
}
