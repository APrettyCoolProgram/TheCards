// 261127_code
// 261127_documentation

using EchoesOfTheShardveil.Core;

namespace EchoesOfTheShardveil.Desktop;

/// <summary>References the content used for a single-player matchup.</summary>
/// <param name="Format">The AppData path of the format file.</param>
/// <param name="Player">The player side's content.</param>
/// <param name="Opponent">The opponent side's content.</param>
/// <param name="Tutorial">The AppData path of the walkthrough, or null to skip it.</param>
/// <param name="OpponentProfile">The optional AppData path for the computer opponent preferences.</param>
internal sealed record MatchSetup(string Format, MatchSide Player, MatchSide Opponent,
    string? Tutorial = "Tutorial/Alpha.json", string? OpponentProfile = null)
{
    private static readonly Lazy<MatchSetup> SinglePlayer = new(Load);

    /// <summary>Gets the active single-player matchup.</summary>
    public static MatchSetup Current => SinglePlayer.Value;

    /// <summary>Starts a duel with the current matchup's format, decks and leaders.</summary>
    /// <param name="playerDeck">An edited player deck, or null to use the configured recipe.</param>
    /// <returns>The duel waiting for mulligans.</returns>
    public static Duel CreateDuel(IReadOnlyList<CardDefinition>? playerDeck = null) =>
        new(playerDeck ?? StarterDecks.Create(), StarterDecks.Create(opponentDeck: true), format: MatchFormats.Active,
            playerOneLeader: Current.Player.LoadLeader(), playerTwoLeader: Current.Opponent.LoadLeader());

    /// <summary>Reads the configured matchup and verifies its required references.</summary>
    /// <returns>The matchup configuration.</returns>
    private static MatchSetup Load()
    {
        var path = AppDataJson.PathFor("Match", "AlphaSinglePlayer.json");
        var setup = AppDataJson.Load<MatchSetup>(path);
        if (string.IsNullOrWhiteSpace(setup.Format) ||
            setup.Player is not { IsComplete: true } ||
            setup.Opponent is not { IsComplete: true })
        {
            throw new InvalidDataException($"'{path}' must specify a format, deck, character, portrait and playmat for the matchup.");
        }
        if (setup.Tutorial is not null && string.IsNullOrWhiteSpace(setup.Tutorial))
        {
            throw new InvalidDataException($"'{path}' must use a tutorial path or null.");
        }
        return setup;
    }
}

/// <summary>References the deck, identity and artwork for one side of a matchup.</summary>
/// <param name="Deck">The AppData path of the deck recipe.</param>
/// <param name="Character">The AppData path of the character file.</param>
/// <param name="Portrait">An AppData image path or an embedded resource reference.</param>
/// <param name="Playmat">An AppData image path or an embedded resource reference.</param>
internal sealed record MatchSide(string Deck, string Character, string Portrait, string Playmat)
{
    /// <summary>Gets whether all content references are present.</summary>
    public bool IsComplete =>
        !string.IsNullOrWhiteSpace(Deck) && !string.IsNullOrWhiteSpace(Character) &&
        !string.IsNullOrWhiteSpace(Portrait) && !string.IsNullOrWhiteSpace(Playmat);

    /// <summary>Loads the character identity for use in the duel.</summary>
    /// <returns>The leader definition for this side.</returns>
    /// <exception cref="InvalidDataException">The character file is invalid or has no name.</exception>
    public CardDefinition LoadLeader()
    {
        var path = AppDataJson.ResolveReference(Character);
        var character = AppDataJson.Load<CharacterData>(path);
        if (string.IsNullOrWhiteSpace(character.Name))
        {
            throw new InvalidDataException($"'{path}' must contain a character name.");
        }
        return new CardDefinition(Path.GetFileNameWithoutExtension(path), character.Name) { Type = CardType.Leader };
    }

    /// <summary>The character fields a match uses.</summary>
    /// <param name="Name">The display name.</param>
    private sealed record CharacterData(string Name);
}
