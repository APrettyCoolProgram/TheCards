// 261128_code
// 261128_documentation

using EchoesOfTheShardveil.Core;
using EchoesOfTheShardveil.Desktop;

namespace EchoesOfTheShardveil.Tests.Desktop;

/// <summary>Covers saving the player's deck and falling back to the starter deck.</summary>
public sealed class SavedDeckTests
{
    /// <summary>With no save file, the starter deck is used without a warning.</summary>
    [Fact]
    public void MissingSaveUsesTheStarter()
    {
        using var folder = new TemporaryFolder();

        var saved = new SavedDeck(Path.Combine(folder.Root, "Decks", "deck.json"));

        Assert.False(saved.UsedStarterFallback);
        Assert.Equal(StarterDecks.Create(), saved.ActiveDeck);
    }

    /// <summary>An unreadable or incompatible save falls back to the starter deck and says so.</summary>
    /// <param name="content">The save file.</param>
    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("""{ "cards": null }""")]
    [InlineData("""{ "cards": [] }""")]
    [InlineData("""{ "cards": [ null ] }""")]
    [InlineData("""{ "cards": [ { "set": "Alpha", "id": "no-such-card", "copies": 1 } ] }""")]
    [InlineData("""{ "cards": [ { "set": "Alpha", "id": "ember-pup", "copies": 99 } ] }""")]
    public void BadSaveFallsBackToTheStarter(string content)
    {
        using var folder = new TemporaryFolder();

        var saved = new SavedDeck(folder.Write("deck.json", content));

        Assert.True(saved.UsedStarterFallback);
        Assert.Equal(StarterDecks.Create(), saved.ActiveDeck);
    }

    /// <summary>An applied deck is read back the same, replacing a broken save.</summary>
    [Fact]
    public void AppliedDeckRoundTrips()
    {
        using var folder = new TemporaryFolder();
        var path = folder.Write("deck.json", "not json");
        var saved = new SavedDeck(path);

        Assert.True(saved.TryApply(Entries(StarterDecks.Create().Reverse())));

        Assert.False(saved.UsedStarterFallback);
        var reloaded = new SavedDeck(path);
        Assert.False(reloaded.UsedStarterFallback);
        Assert.Equal(saved.ActiveDeck, reloaded.ActiveDeck);
        // The temporary file used for the write is gone.
        Assert.Single(Directory.GetFiles(folder.Root));
    }

    /// <summary>An illegal draft is refused and nothing is written.</summary>
    [Fact]
    public void IllegalDraftIsNotSaved()
    {
        using var folder = new TemporaryFolder();
        var path = Path.Combine(folder.Root, "deck.json");
        var saved = new SavedDeck(path);

        Assert.False(saved.TryApply(Entries(StarterDecks.Create().Skip(1))));

        Assert.False(File.Exists(path));
        Assert.Equal(StarterDecks.Create(), saved.ActiveDeck);
    }

    /// <summary>Groups a deck into the entries the deckbuilder saves.</summary>
    /// <param name="deck">The cards.</param>
    /// <returns>The set, card and copy count of each card.</returns>
    private static (string Set, CardDefinition Card, int Copies)[] Entries(IEnumerable<CardDefinition> deck) =>
        [.. deck.GroupBy(card => card).Select(group =>
            (StarterDecks.PlayerPool.First(entry => entry.Card == group.Key).Set, group.Key, group.Count()))];
}
