// 261127_code
// 261127_documentation

using System.Collections.Concurrent;
using System.Text.Json;
using EchoesOfTheShardveil.Core;

namespace EchoesOfTheShardveil.Desktop;

/// <summary>Loads card definitions from an AppData set file for use by the desktop game.</summary>
internal static class CardSet
{
    private static readonly ConcurrentDictionary<string, LoadedSet> Sets =
        new(StringComparer.Ordinal);

    /// <summary>Reads the named set and indexes its cards by their stable IDs.</summary>
    /// <param name="setName">The name shared by the set folder and its .cards file.</param>
    /// <returns>The set's cards keyed by ID.</returns>
    /// <exception cref="InvalidDataException">The file does not contain a valid card set.</exception>
    public static IReadOnlyDictionary<string, CardDefinition> Load(string setName) => GetSet(setName).Cards;

    /// <summary>Gets optional presentation metadata for a card.</summary>
    /// <param name="setName">The set containing the card.</param>
    /// <param name="cardId">The card's stable ID within the set.</param>
    /// <returns>The presentation fields, or null if none are present.</returns>
    public static CardPresentation? GetPresentation(string setName, string cardId) =>
        GetSet(setName).Presentation.GetValueOrDefault(cardId);

    /// <summary>Gets the cached set and its desktop metadata.</summary>
    /// <param name="setName">The set name.</param>
    /// <returns>The loaded set.</returns>
    private static LoadedSet GetSet(string setName)
    {
        if (!AppDataJson.IsSafeName(setName))
        {
            throw new InvalidDataException($"Invalid card set name '{setName}'.");
        }
        return Sets.GetOrAdd(setName, Read);
    }

    /// <summary>Reads and validates a set that has not been loaded yet.</summary>
    /// <param name="setName">The set name.</param>
    /// <returns>The set's cards and presentation metadata.</returns>
    private static LoadedSet Read(string setName)
    {
        var path = AppDataJson.PathFor("Set", setName, setName + ".cards");
        var set = AppDataJson.Load<SetData>(path);
        if (set.Name != setName || set.Cards is not { Length: > 0 })
        {
            throw new InvalidDataException($"'{path}' must contain the '{setName}' set and its cards.");
        }

        var cards = new Dictionary<string, CardDefinition>(StringComparer.Ordinal);
        var metadata = new Dictionary<string, CardPresentation>(StringComparer.Ordinal);
        foreach (var entry in set.Cards)
        {
            var card = AppDataJson.ReadElement<CardDefinition>(entry, path);
            if (string.IsNullOrWhiteSpace(card.Id) || string.IsNullOrWhiteSpace(card.Name) ||
                card.Abilities is null || card.Abilities.Any(ability => ability is null))
            {
                throw new InvalidDataException($"'{path}' contains a card with missing data.");
            }
            if (!cards.TryAdd(card.Id, card))
            {
                throw new InvalidDataException($"Duplicate card ID '{card.Id}' in '{path}'.");
            }
            var presentation = AppDataJson.ReadElement<CardPresentation>(entry, path);
            if (presentation.Artwork is { } image)
            {
                presentation = presentation with { Artwork = AppDataJson.ResolveReference($"Set/{setName}/{image}") };
            }
            if (presentation.Artwork is not null || !string.IsNullOrWhiteSpace(presentation.FlavorText))
            {
                metadata.Add(card.Id, presentation);
            }
        }

        return new LoadedSet(cards, metadata);
    }

    /// <summary>The top-level fields in a .cards file.</summary>
    /// <param name="Name">The set name.</param>
    /// <param name="Cards">The card definitions.</param>
    private sealed record SetData(string Name, JsonElement[] Cards);

    /// <summary>Optional desktop metadata stored alongside a card definition.</summary>
    /// <param name="Artwork">The image path relative to the set folder.</param>
    /// <param name="FlavorText">Optional card lore shown in the deckbuilder.</param>
    internal sealed record CardPresentation(string? Artwork = null, string? FlavorText = null);

    /// <summary>A set's engine definitions and desktop metadata.</summary>
    /// <param name="Cards">The card definitions keyed by ID.</param>
    /// <param name="Presentation">Presentation metadata keyed by card ID.</param>
    private sealed record LoadedSet(IReadOnlyDictionary<string, CardDefinition> Cards,
        IReadOnlyDictionary<string, CardPresentation> Presentation);
}
