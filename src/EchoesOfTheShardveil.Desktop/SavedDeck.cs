// 261127_code
// 261127_documentation

using System.Text.Json;
using EchoesOfTheShardveil.Core;

namespace EchoesOfTheShardveil.Desktop;

/// <summary>Stores the applied single-player deck separately from the shipped starter recipes.</summary>
internal sealed class SavedDeck
{
    private static readonly JsonSerializerOptions WriteOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly string path;

    /// <summary>Loads the applied deck or starts with the configured starter.</summary>
    /// <param name="path">The full path to the player's save file.</param>
    public SavedDeck(string path)
    {
        this.path = path;
        ActiveDeck = StarterDecks.Create();
        try
        {
            var data = AppDataJson.Load<DeckData>(path);
            ActiveDeck = Resolve(data.Cards);
        }
        catch (FileNotFoundException)
        {
            // The player has not saved a deck yet.
        }
        catch (DirectoryNotFoundException)
        {
            // The save directory is created on the first successful save.
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            UsedStarterFallback = true;
        }
    }

    /// <summary>Gets the save path for the current single-player matchup.</summary>
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.DoNotVerify),
        "EchoesOfTheShardveil", "Decks", "AlphaSinglePlayer.json");

    /// <summary>Gets the deck to use when starting a duel.</summary>
    public IReadOnlyList<CardDefinition> ActiveDeck { get; private set; }

    /// <summary>Gets whether an existing save could not be read or is incompatible with the current matchup.</summary>
    public bool UsedStarterFallback { get; private set; }

    /// <summary>Saves the draft and updates the active deck after the file has been committed.</summary>
    /// <param name="entries">The set, card and copy count for each card in the draft.</param>
    /// <returns>Whether the deck was saved and applied.</returns>
    public bool TryApply(IEnumerable<(string Set, CardDefinition Card, int Copies)> entries)
    {
        var data = new DeckData([.. entries.Select(entry => new DeckEntry(entry.Set, entry.Card.Id, entry.Copies))]);
        try
        {
            var deck = Resolve(data.Cards);
            var directory = Path.GetDirectoryName(path)!;
            Directory.CreateDirectory(directory);
            var temporaryPath = Path.Combine(directory, Path.GetRandomFileName());
            var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            try
            {
                using (stream)
                {
                    JsonSerializer.Serialize(stream, data, WriteOptions);
                    stream.Flush(flushToDisk: true);
                }
                File.Move(temporaryPath, path, overwrite: true);
            }
            finally
            {
                File.Delete(temporaryPath);
            }
            ActiveDeck = deck;
            UsedStarterFallback = false;
            return true;
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Resolves saved set and card IDs against the current collection and verifies deck legality.</summary>
    /// <param name="entries">The saved card references.</param>
    /// <returns>The resolved deck.</returns>
    private static CardDefinition[] Resolve(DeckEntry[] entries)
    {
        var format = MatchFormats.Active;
        var pool = StarterDecks.PlayerPool.ToDictionary(entry => (entry.Set, entry.Card.Id), entry => entry.Card);
        var seen = new HashSet<(string Set, string Id)>();
        var cards = new List<CardDefinition>();
        if (entries is null)
        {
            throw new InvalidDataException("The saved deck is missing its card list.");
        }
        foreach (var entry in entries)
        {
            if (entry is null || !pool.TryGetValue((entry.Set, entry.Id), out var card) ||
                !seen.Add((entry.Set, entry.Id)) || entry.Copies < 1 || entry.Copies > format.MaxCopiesPerName ||
                entry.Copies > format.DeckSize - cards.Count)
            {
                throw new InvalidDataException("The saved deck contains unavailable cards or invalid copy counts.");
            }
            cards.AddRange(Enumerable.Repeat(card, entry.Copies));
        }
        if (cards.Count != format.DeckSize || format.ExceedsCopyLimit(cards))
        {
            throw new InvalidDataException("The saved deck exceeds the current format's deck or copy limits.");
        }
        return [.. cards];
    }

    /// <summary>The saved deck document.</summary>
    /// <param name="Cards">The saved entries.</param>
    private sealed record DeckData(DeckEntry[] Cards);

    /// <summary>The saved copies of a card within a set.</summary>
    /// <param name="Set">The set name.</param>
    /// <param name="Id">The stable card ID in the set.</param>
    /// <param name="Copies">The number of copies.</param>
    private sealed record DeckEntry(string Set, string Id, int Copies);
}
