// 261128_code
// 261128_documentation

using EchoesOfTheShardveil.Core;

namespace EchoesOfTheShardveil.Tests.Core;

/// <summary>Builds small duels whose hands are fully known, and moves them through turns.</summary>
internal static class DuelHarness
{
    /// <summary>Creates a Unit card.</summary>
    /// <param name="id">The card ID, also used as its name.</param>
    /// <param name="power">The Power.</param>
    /// <param name="health">The Health.</param>
    /// <param name="keywords">The keywords.</param>
    /// <param name="cost">The energy cost.</param>
    /// <param name="abilities">The abilities.</param>
    /// <returns>The card.</returns>
    public static CardDefinition Unit(string id, int power = 1, int health = 1,
        CardKeyword keywords = CardKeyword.None, int cost = 0, params CardAbility[] abilities) =>
        new(id, id) { Power = power, Health = health, Keywords = keywords, Cost = cost, Abilities = abilities };

    /// <summary>Creates a Spell card.</summary>
    /// <param name="id">The card ID, also used as its name.</param>
    /// <param name="abilities">The abilities.</param>
    /// <returns>The card.</returns>
    public static CardDefinition Spell(string id, params CardAbility[] abilities) =>
        new(id, id) { Type = CardType.Spell, Abilities = abilities };

    /// <summary>Creates a Relic card.</summary>
    /// <param name="id">The card ID, also used as its name.</param>
    /// <param name="abilities">The abilities.</param>
    /// <returns>The card.</returns>
    public static CardDefinition Relic(string id, params CardAbility[] abilities) =>
        new(id, id) { Type = CardType.Relic, Abilities = abilities };

    /// <summary>
    /// Starts a duel past its mulligans in which player 0 goes first. Each deck is the listed cards padded with
    /// vanilla Units, and the opening hand holds the whole deck, so every card is available.
    /// </summary>
    /// <param name="first">The cards player 0 needs.</param>
    /// <param name="second">The cards player 1 needs.</param>
    /// <param name="startingHealth">The starting Health.</param>
    /// <param name="cardsDrawnPerTurn">The regular draw. The deck is empty, so any draw loses the duel.</param>
    /// <param name="handLimit">The hand limit.</param>
    /// <returns>The duel, waiting in player 0's Ready phase.</returns>
    public static Duel Start(CardDefinition[]? first = null, CardDefinition[]? second = null,
        int startingHealth = 20, int cardsDrawnPerTurn = 0, int handLimit = 20)
    {
        const int size = 10;
        var format = new DuelFormat(startingHealth, size, size, openingHandSize: size, handLimit: handLimit,
            cardsDrawnPerTurn: cardsDrawnPerTurn);
        for (var seed = 0; ; seed++)
        {
            var duel = new Duel(Pad(first ?? [], size), Pad(second ?? [], size), seed, format);
            if (duel.ActivePlayer == 0)
            {
                duel.Mulligan(0, []);
                duel.Mulligan(1, []);
                return duel;
            }
        }
    }

    /// <summary>Advances until the given player is in the given phase.</summary>
    /// <param name="duel">The duel.</param>
    /// <param name="player">The player.</param>
    /// <param name="phase">The phase.</param>
    public static void AdvanceTo(this Duel duel, int player, DuelPhase phase)
    {
        for (var step = 0; duel.ActivePlayer != player || duel.Phase != phase; step++)
        {
            Assert.True(step < 20, $"The duel never reached player {player}'s {phase} phase.");
            duel.Advance(duel.ActivePlayer);
        }
    }

    /// <summary>Gets a player's own view.</summary>
    /// <param name="duel">The duel.</param>
    /// <param name="player">The player.</param>
    /// <returns>The view including the hand.</returns>
    public static PlayerView Own(this Duel duel, int player) => duel.View(player, player);

    /// <summary>Finds the hand slot holding a card.</summary>
    /// <param name="duel">The duel.</param>
    /// <param name="player">The player.</param>
    /// <param name="id">The card ID.</param>
    /// <returns>The hand slot.</returns>
    public static int Slot(this Duel duel, int player, string id)
    {
        var hand = duel.Own(player).Hand!;
        var slot = Enumerable.Range(0, hand.Count).FirstOrDefault(index => hand[index].Id == id, -1);
        Assert.True(slot >= 0, $"'{id}' is not in player {player}'s hand.");
        return slot;
    }

    /// <summary>Plays a card by ID.</summary>
    /// <param name="duel">The duel in the player's Main phase.</param>
    /// <param name="player">The player.</param>
    /// <param name="id">The card ID.</param>
    /// <param name="target">The chosen Unit, if the card needs one.</param>
    public static void PlayCard(this Duel duel, int player, string id, UnitTarget? target = null) =>
        duel.Play(player, duel.Slot(player, id), target);

    /// <summary>Pads a deck to size with distinct vanilla Units.</summary>
    /// <param name="cards">The required cards.</param>
    /// <param name="size">The deck size.</param>
    /// <returns>The deck.</returns>
    private static IEnumerable<CardDefinition> Pad(CardDefinition[] cards, int size) =>
        cards.Concat(Enumerable.Range(0, size - cards.Length).Select(index => Unit($"filler-{index}")));
}
