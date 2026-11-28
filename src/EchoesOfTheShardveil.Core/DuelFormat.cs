// 261127_code
// 261127_documentation

namespace EchoesOfTheShardveil.Core;

/// <summary>Defines the setup and limits for a duel.</summary>
public sealed class DuelFormat
{
    /// <summary>Creates a format with limits that can support an opening hand.</summary>
    /// <param name="startingHealth">Starting Health and the healing cap.</param>
    /// <param name="deckSize">The required number of cards per deck.</param>
    /// <param name="maxCopiesPerName">The maximum number of copies of a card name per deck.</param>
    /// <param name="openingHandSize">The number of cards dealt before mulligans.</param>
    /// <param name="handLimit">The maximum hand size at the end of a turn.</param>
    /// <param name="secondPlayerGetsSpark">Whether the player going second receives a Spark.</param>
    /// <param name="unitSlots">The number of Unit slots per side.</param>
    /// <param name="relicSlots">The number of Relic slots per side.</param>
    /// <param name="wellLimit">The shard capacity and maximum card or ability energy cost.</param>
    /// <param name="firstPlayerSkipsFirstDraw">Whether the first player skips the opening turn's draw.</param>
    /// <param name="sparkEnergy">The temporary energy granted by a Spark.</param>
    /// <param name="cardsDrawnPerTurn">The number of cards drawn in the regular Draw phase.</param>
    /// <param name="wellFeedsPerTurn">The number of times each player may feed the Well on their turn.</param>
    /// <param name="mulliganSwapLimit">The maximum swaps, or null to allow the whole opening hand.</param>
    /// <exception cref="ArgumentOutOfRangeException">A limit is invalid or exceeds the deck size.</exception>
    public DuelFormat(int startingHealth, int deckSize, int maxCopiesPerName,
        int openingHandSize = Duel.OpeningHandSize, int handLimit = Duel.HandLimit, bool secondPlayerGetsSpark = true,
        int unitSlots = Duel.UnitSlots, int relicSlots = Duel.RelicSlots, int wellLimit = Duel.WellLimit,
        bool firstPlayerSkipsFirstDraw = true, int sparkEnergy = 1, int cardsDrawnPerTurn = 1, int wellFeedsPerTurn = 1,
        int? mulliganSwapLimit = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(startingHealth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(deckSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(openingHandSize);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(openingHandSize, deckSize);
        ArgumentOutOfRangeException.ThrowIfNegative(handLimit);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCopiesPerName);
        ArgumentOutOfRangeException.ThrowIfNegative(unitSlots);
        ArgumentOutOfRangeException.ThrowIfNegative(relicSlots);
        ArgumentOutOfRangeException.ThrowIfNegative(wellLimit);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sparkEnergy);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(sparkEnergy, int.MaxValue - wellLimit);
        ArgumentOutOfRangeException.ThrowIfNegative(cardsDrawnPerTurn);
        ArgumentOutOfRangeException.ThrowIfNegative(wellFeedsPerTurn);
        ArgumentOutOfRangeException.ThrowIfNegative(mulliganSwapLimit.GetValueOrDefault());
        StartingHealth = startingHealth;
        DeckSize = deckSize;
        MaxCopiesPerName = maxCopiesPerName;
        OpeningHandSize = openingHandSize;
        HandLimit = handLimit;
        SecondPlayerGetsSpark = secondPlayerGetsSpark;
        UnitSlots = unitSlots;
        RelicSlots = relicSlots;
        WellLimit = wellLimit;
        FirstPlayerSkipsFirstDraw = firstPlayerSkipsFirstDraw;
        SparkEnergy = sparkEnergy;
        CardsDrawnPerTurn = cardsDrawnPerTurn;
        WellFeedsPerTurn = wellFeedsPerTurn;
        MulliganSwapLimit = mulliganSwapLimit;
    }

    /// <summary>Gets the starting Health and healing cap.</summary>
    public int StartingHealth { get; }

    /// <summary>Gets the required number of cards per deck.</summary>
    public int DeckSize { get; }

    /// <summary>Gets the copy limit for each card name.</summary>
    public int MaxCopiesPerName { get; }

    /// <summary>Gets the number of cards dealt before mulligans.</summary>
    public int OpeningHandSize { get; }

    /// <summary>Gets the maximum hand size at the end of a turn.</summary>
    public int HandLimit { get; }

    /// <summary>Gets whether the player going second receives a Spark.</summary>
    public bool SecondPlayerGetsSpark { get; }

    /// <summary>Gets the number of Unit slots per side.</summary>
    public int UnitSlots { get; }

    /// <summary>Gets the number of Relic slots per side.</summary>
    public int RelicSlots { get; }

    /// <summary>Gets the maximum number of shards in a Well and the maximum energy cost.</summary>
    public int WellLimit { get; }

    /// <summary>Gets whether the first player skips the opening turn's draw.</summary>
    public bool FirstPlayerSkipsFirstDraw { get; }

    /// <summary>Gets the temporary energy granted by a Spark.</summary>
    public int SparkEnergy { get; }

    /// <summary>Gets the number of cards drawn during a regular Draw phase.</summary>
    public int CardsDrawnPerTurn { get; }

    /// <summary>Gets the maximum number of times a player may feed the Well on their turn.</summary>
    public int WellFeedsPerTurn { get; }

    /// <summary>Gets the configured mulligan limit, or null if the full opening hand may be replaced.</summary>
    public int? MulliganSwapLimit { get; }

    /// <summary>Gets the maximum swaps possible with the configured opening hand size.</summary>
    public int MaxMulliganSwaps => Math.Min(OpeningHandSize, MulliganSwapLimit ?? OpeningHandSize);

    /// <summary>Determines whether a deck has more copies of any card name than the format allows.</summary>
    /// <param name="deck">The cards in the deck. Names are compared without regard to case.</param>
    /// <returns><see langword="true"/> if a name exceeds <see cref="MaxCopiesPerName"/>.</returns>
    public bool ExceedsCopyLimit(IEnumerable<CardDefinition> deck) =>
        deck.GroupBy(card => card.Name, StringComparer.OrdinalIgnoreCase)
            .Any(group => group.Count() > MaxCopiesPerName);
}
