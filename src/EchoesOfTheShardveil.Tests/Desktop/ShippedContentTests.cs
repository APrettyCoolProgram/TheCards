// 261128_code
// 261128_documentation

using System.Numerics;
using EchoesOfTheShardveil.Core;
using EchoesOfTheShardveil.Desktop;

namespace EchoesOfTheShardveil.Tests.Desktop;

/// <summary>Checks that the AppData shipped with the game loads and is usable.</summary>
public sealed class ShippedContentTests
{
    /// <summary>The configured matchup creates a duel with named Leaders.</summary>
    [Fact]
    public void ConfiguredMatchupStarts()
    {
        var duel = MatchSetup.CreateDuel();

        Assert.Equal(DuelPhase.Mulligan, duel.Phase);
        Assert.False(string.IsNullOrWhiteSpace(duel.View(0, 0).Leader.Name));
        Assert.False(string.IsNullOrWhiteSpace(duel.View(1, 0).Leader.Name));
        Assert.True(OpponentProfile.Current.CardsToKeepWhenFeeding >= 0);
    }

    /// <summary>Both starter decks have the format's size and respect the copy limit.</summary>
    /// <param name="opponent">Whether to check the opponent's deck.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StarterDeckIsLegal(bool opponent)
    {
        var deck = StarterDecks.Create(opponent);

        Assert.Equal(MatchFormats.Active.DeckSize, deck.Count);
        Assert.False(MatchFormats.Active.ExceedsCopyLimit(deck));
        Assert.All(deck, card => Assert.True(Duel.IsPlayableCard(card, MatchFormats.Active), card.Id));
    }

    /// <summary>Every card has a face summary and rules text with all placeholders filled in.</summary>
    [Fact]
    public void EveryCardHasRulesText()
    {
        CardRulesText.LoadCatalogs();

        Assert.All(CardSet.Load("Alpha").Values, card =>
        {
            var rules = CardRulesText.Describe(card).ToArray();
            var summary = CardRulesText.Summarize(card);

            Assert.Equal(BitOperations.PopCount((uint)card.Keywords) + card.Abilities.Count, rules.Length);
            Assert.All(rules.Append(summary), line => Assert.DoesNotContain('{', line));
            Assert.True(rules.Length == 0 || !string.IsNullOrWhiteSpace(summary), card.Id);
        });
    }
}
