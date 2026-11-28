// 261128_code
// 261128_documentation

using EchoesOfTheShardveil.Core;
using static EchoesOfTheShardveil.Tests.Core.DuelHarness;

namespace EchoesOfTheShardveil.Tests.Core;

/// <summary>Covers format limits, deck validation, opening hands, mulligans and views.</summary>
public sealed class DuelSetupTests
{
    /// <summary>A wrong size, a Leader, or an impossible card is rejected.</summary>
    [Fact]
    public void IllegalDecksAreRejected()
    {
        CardDefinition[] illegal =
        [
            new("leader", "leader") { Type = CardType.Leader },
            Unit("costly", cost: Duel.WellLimit + 1),
            Unit("negative", cost: -1),
            Unit("lifeless", health: 0),
            Spell("bad-amount", new CardAbility(AbilityTrigger.Spell, AbilityTarget.Controller, AbilityAction.Draw, -1)),
        ];

        Assert.Throws<ArgumentException>(() => new Duel(Deck().Skip(1), Deck()));
        foreach (var card in illegal)
        {
            var deck = Deck();
            deck[0] = card;
            Assert.Throws<ArgumentException>(() => new Duel(Deck(), deck));
        }
    }

    /// <summary>The copy limit counts names without regard to case.</summary>
    [Fact]
    public void CopyLimitIgnoresNameCase()
    {
        var deck = Deck();
        deck[0] = deck[1] = deck[2] = Unit("twin");
        _ = new Duel(deck, Deck());

        deck[3] = Unit("TWIN");
        Assert.Throws<ArgumentException>(() => new Duel(deck, Deck()));
    }

    /// <summary>A Leader argument must be a Leader card.</summary>
    [Fact]
    public void NonLeaderCannotLead() =>
        Assert.Throws<ArgumentException>(() => new Duel(Deck(), Deck(), playerOneLeader: Unit("imposter")));

    /// <summary>Formats reject limits that cannot work.</summary>
    [Fact]
    public void FormatRejectsInvalidLimits()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DuelFormat(0, 40, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DuelFormat(20, 4, 3, openingHandSize: 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DuelFormat(20, 40, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DuelFormat(20, 40, 3, sparkEnergy: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DuelFormat(20, 40, 3, mulliganSwapLimit: -1));
    }

    /// <summary>Both players get an opening hand and only the second player gets the Spark.</summary>
    [Fact]
    public void OpeningDealGivesSecondPlayerTheSpark()
    {
        var duel = new Duel(Deck(), Deck(), seed: 1);
        var second = 1 - duel.ActivePlayer;

        Assert.Equal(DuelPhase.Mulligan, duel.Phase);
        Assert.Equal(Duel.OpeningHandSize, duel.Own(0).HandCount);
        Assert.Equal(Duel.OpeningHandSize, duel.Own(1).HandCount);
        Assert.True(duel.Own(second).HasSpark);
        Assert.False(duel.Own(duel.ActivePlayer).HasSpark);
    }

    /// <summary>The same seed and decks give the same first player and hands.</summary>
    [Fact]
    public void SeedMakesTheDealRepeatable()
    {
        var one = new Duel(Deck(), Deck(), seed: 42);
        var two = new Duel(Deck(), Deck(), seed: 42);

        Assert.Equal(one.ActivePlayer, two.ActivePlayer);
        Assert.Equal(one.Own(0).Hand, two.Own(0).Hand);
        Assert.Equal(one.Own(1).Hand, two.Own(1).Hand);
    }

    /// <summary>A view hides the other player's hand and rejects unknown players.</summary>
    [Fact]
    public void ViewHidesTheOpposingHand()
    {
        var duel = new Duel(Deck(), Deck(), seed: 1);

        Assert.Null(duel.View(1, 0).Hand);
        Assert.Equal(Duel.OpeningHandSize, duel.View(1, 0).HandCount);
        Assert.Throws<ArgumentOutOfRangeException>(() => duel.View(2, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => duel.View(0, -1));
    }

    /// <summary>A mulligan replaces the chosen cards once, and setup ends after both players choose.</summary>
    [Fact]
    public void MulliganReplacesChosenCardsOnce()
    {
        var duel = new Duel(Deck(), Deck(), seed: 3);
        var before = duel.Own(0).Hand!.ToArray();

        duel.Mulligan(0, [0, 1]);

        var after = duel.Own(0).Hand!;
        Assert.Equal(before.Length, after.Count);
        Assert.DoesNotContain(before[0], after);
        Assert.DoesNotContain(before[1], after);
        Assert.Equal(Duel.DeckSize - Duel.OpeningHandSize, duel.Own(0).DeckCount);
        Assert.Throws<InvalidOperationException>(() => duel.Mulligan(0, []));
        Assert.Equal(DuelPhase.Mulligan, duel.Phase);

        duel.Mulligan(1, []);
        Assert.Equal(DuelPhase.Ready, duel.Phase);
    }

    /// <summary>Repeated, missing or too many mulligan slots are rejected.</summary>
    [Fact]
    public void MulliganRejectsBadSlots()
    {
        var duel = new Duel(Deck(), Deck(), seed: 3,
            format: new DuelFormat(20, Duel.DeckSize, 3, mulliganSwapLimit: 2));

        Assert.Throws<ArgumentException>(() => duel.Mulligan(0, [1, 1]));
        Assert.Throws<ArgumentException>(() => duel.Mulligan(0, [Duel.OpeningHandSize]));
        Assert.Throws<ArgumentException>(() => duel.Mulligan(0, [0, 1, 2]));
    }

    /// <summary>Changing a card's ability list after the duel starts does not change the duel.</summary>
    [Fact]
    public void DuelCopiesAbilityLists()
    {
        var abilities = new[] { new CardAbility(AbilityTrigger.Arrival, AbilityTarget.Controller, AbilityAction.Draw) };
        var duel = Start([Unit("scribe", abilities: abilities)]);

        abilities[0] = abilities[0] with { Amount = 99 };

        var card = duel.Own(0).Hand!.Single(card => card.Id == "scribe");
        Assert.Equal(1, card.Abilities[0].Amount);
    }

    /// <summary>Builds a default-sized deck with distinct names.</summary>
    /// <returns>The deck.</returns>
    private static List<CardDefinition> Deck() =>
        [.. Enumerable.Range(0, Duel.DeckSize).Select(index => Unit($"card-{index}"))];
}
