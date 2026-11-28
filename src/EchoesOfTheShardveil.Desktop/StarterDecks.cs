// 261127_code
// 261127_documentation

using EchoesOfTheShardveil.Core;

namespace EchoesOfTheShardveil.Desktop;

/// <summary>Loads the starter deck recipes and resolves their card IDs.</summary>
internal static class StarterDecks
{
    private static readonly Lazy<LoadedDeck> Player = new(() => Load(MatchSetup.Current.Player.Deck));
    private static readonly Lazy<LoadedDeck> Opponent = new(() => Load(MatchSetup.Current.Opponent.Deck));

    /// <summary>Creates a copy of the requested starter deck.</summary>
    /// <param name="opponentDeck">Whether to use the opponent's deck recipe.</param>
    /// <returns>The cards in recipe order, including copies.</returns>
    public static IReadOnlyList<CardDefinition> Create(bool opponentDeck = false) =>
        (CardDefinition[])(opponentDeck ? Opponent.Value : Player.Value).Cards.Clone();

    /// <summary>Gets the playable card pool from the sets named in the player recipe.</summary>
    public static IReadOnlyList<(string Set, CardDefinition Card)> PlayerPool => Player.Value.Pool;

    /// <summary>Reads a recipe and verifies its references and copy counts.</summary>
    /// <param name="file">The AppData-relative recipe path.</param>
    /// <returns>The expanded deck and its playable card pool.</returns>
    private static LoadedDeck Load(string file)
    {
        var path = AppDataJson.ResolveReference(file);
        var recipe = AppDataJson.Load<DeckRecipe>(path);
        if (recipe.Cards is null)
        {
            throw new InvalidDataException($"'{path}' must specify a card list.");
        }

        var pools = new Dictionary<string, IReadOnlyDictionary<string, CardDefinition>>(StringComparer.Ordinal);
        IReadOnlyDictionary<string, CardDefinition> GetPool(string? set)
        {
            if (string.IsNullOrWhiteSpace(set))
            {
                throw new InvalidDataException($"'{path}' contains a missing or empty set reference.");
            }
            if (!pools.TryGetValue(set, out var pool))
            {
                pool = CardSet.Load(set);
                pools.Add(set, pool);
            }
            return pool;
        }

        if (recipe.Set is not null)
        {
            GetPool(recipe.Set);
        }
        foreach (var set in recipe.Sets ?? [])
        {
            GetPool(set);
        }

        var format = MatchFormats.Active;
        var cards = new List<CardDefinition>();
        var seen = new HashSet<(string Set, string Id)>();
        foreach (var entry in recipe.Cards)
        {
            var set = entry?.Set ?? recipe.Set;
            if (entry is null || string.IsNullOrWhiteSpace(entry.Id) || string.IsNullOrWhiteSpace(set) ||
                !GetPool(set).TryGetValue(entry.Id, out var card) || !Duel.IsPlayableCard(card, format) ||
                !seen.Add((set, entry.Id)) || entry.Copies < 1 || entry.Copies > format.MaxCopiesPerName)
            {
                throw new InvalidDataException($"'{path}' has an invalid deck entry for '{entry?.Id}' in set '{set}'.");
            }
            cards.AddRange(Enumerable.Repeat(card, entry.Copies));
        }
        if (cards.Count != format.DeckSize)
        {
            throw new InvalidDataException($"'{path}' must contain exactly {format.DeckSize} cards.");
        }
        if (format.ExceedsCopyLimit(cards))
        {
            throw new InvalidDataException($"'{path}' exceeds the copy limit for a card name.");
        }
        var playablePool = pools.SelectMany(pool => pool.Value.Values
            .Where(card => Duel.IsPlayableCard(card, format))
            .Select(card => (Set: pool.Key, Card: card))).ToArray();
        return new LoadedDeck([.. cards], playablePool);
    }

    /// <summary>A starter deck and its collection.</summary>
    /// <param name="Cards">The expanded deck.</param>
    /// <param name="Pool">The playable cards and their set names.</param>
    private sealed record LoadedDeck(CardDefinition[] Cards, (string Set, CardDefinition Card)[] Pool);

    /// <summary>The set and card entries in a starter deck recipe.</summary>
    /// <param name="Set">The default set for entries that omit a set.</param>
    /// <param name="Cards">The card IDs and copy counts.</param>
    /// <param name="Sets">Additional sets to include in the card pool.</param>
    private sealed record DeckRecipe(DeckEntry[] Cards, string? Set = null, string[]? Sets = null);

    /// <summary>The copies of one card in a deck recipe.</summary>
    /// <param name="Id">The card's stable ID within the set.</param>
    /// <param name="Copies">The number of copies in the deck.</param>
    /// <param name="Set">The set containing this card, or null to use the deck's default.</param>
    private sealed record DeckEntry(string Id, int Copies, string? Set = null);
}
