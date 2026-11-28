// 261128_code
// 261128_documentation

using EchoesOfTheShardveil.Core;
using EchoesOfTheShardveil.Desktop;
using EchoesOfTheShardveil.Tests.Core;

namespace EchoesOfTheShardveil.Tests.Desktop;

/// <summary>Plays many seeded duels with the shipped decks and checks that the rules stay consistent.</summary>
public sealed class SeededDuelTests
{
    private const int Duels = 2000;
    private const int MaxSteps = 5000;

    /// <summary>
    /// The computer plays against a random player. Every duel must finish, with no rejected action and no card
    /// lost or created.
    /// </summary>
    [Fact]
    public void SeededDuelsFinishConsistently()
    {
        var format = MatchFormats.Active;
        var playerDeck = StarterDecks.Create();
        var opponentDeck = StarterDecks.Create(opponentDeck: true);
        var profiles = Enum.GetValues<OpponentTargetPriority>()
            .Select(priority => OpponentProfile.Current with { TargetPriority = priority }).ToArray();

        for (var seed = 0; seed < Duels; seed++)
        {
            var duel = new Duel(playerDeck, opponentDeck, seed, format);
            var random = new Random(seed);
            duel.Mulligan(0, []);
            duel.Mulligan(1, []);
            var steps = 0;
            try
            {
                for (; duel.Outcome == DuelOutcome.Ongoing && steps < MaxSteps; steps++)
                {
                    var computerActs = duel.PendingAttack is null
                        ? duel.ActivePlayer == SinglePlayerOpponent.ComputerSide
                        : duel.ActivePlayer == SinglePlayerOpponent.HumanSide;
                    if (computerActs)
                    {
                        SinglePlayerOpponent.TakeStep(duel, profiles[seed % profiles.Length]);
                    }
                    else
                    {
                        TakeRandomStep(duel, random);
                    }
                    CheckState(duel);
                }
            }
            catch (Exception error)
            {
                Assert.Fail($"Seed {seed} failed at step {steps}: {error}");
            }
            Assert.True(duel.Outcome != DuelOutcome.Ongoing, $"Seed {seed} did not finish.");
        }
    }

    /// <summary>Takes one random legal action for the human side.</summary>
    /// <param name="duel">A duel waiting for the human side.</param>
    /// <param name="random">The source of choices.</param>
    private static void TakeRandomStep(Duel duel, Random random)
    {
        const int player = SinglePlayerOpponent.HumanSide;
        var own = duel.Own(player);
        if (duel.PendingAttack is not null)
        {
            var blockers = Slots(own, unit => unit.CanIntercept);
            duel.Defend(player, blockers.Length > 0 && random.Next(2) == 0 ? Pick(blockers, random) : null);
            return;
        }

        switch (duel.Phase)
        {
            case DuelPhase.Main:
                if (own.HandCount > 0 && own.CanFeedWell && random.Next(2) == 0)
                {
                    duel.FeedWell(player, random.Next(own.HandCount));
                    return;
                }
                var plays = Enumerable.Range(0, own.HandCount)
                    .SelectMany(slot => duel.LegalPlays(player, slot), (slot, play) => (slot, play)).ToArray();
                if (plays.Length > 0 && random.Next(4) != 0)
                {
                    var (slot, play) = Pick(plays, random);
                    duel.Play(player, slot, play.Target, play.BoardSlot);
                    return;
                }
                break;
            case DuelPhase.Battle:
                var attackers = Slots(own, unit => unit.CanAttack);
                if (attackers.Length > 0 && random.Next(4) != 0)
                {
                    var targets = Slots(duel.View(SinglePlayerOpponent.ComputerSide, player), _ => true);
                    UnitTarget? target = targets.Length > 0 && random.Next(2) == 0
                        ? new(SinglePlayerOpponent.ComputerSide, Pick(targets, random))
                        : null;
                    duel.Attack(player, Pick(attackers, random), target);
                    return;
                }
                break;
            case DuelPhase.End when own.HandCount > duel.Format.HandLimit:
                duel.DiscardToLimit(player, random.Next(own.HandCount));
                return;
        }
        duel.Advance(player);
    }

    /// <summary>Checks card conservation and value ranges on both sides.</summary>
    /// <param name="duel">The duel.</param>
    private static void CheckState(Duel duel)
    {
        for (var player = 0; player < 2; player++)
        {
            var view = duel.Own(player);
            var cards = view.DeckCount + view.HandCount + view.Shards + view.Discard.Count +
                view.Units.Count(unit => unit is not null) + view.Relics.Count(relic => relic is not null);
            Assert.Equal(duel.Format.DeckSize, cards);
            Assert.InRange(view.Health, 0, duel.Format.StartingHealth);
            Assert.InRange(view.ReadyShards, 0, view.Shards);
            Assert.InRange(view.Shards, 0, duel.Format.WellLimit);
            Assert.True(view.TemporaryEnergy >= 0);
            Assert.DoesNotContain(view.Units, unit => unit is not null && unit.Damage >= unit.Card.Health);
        }
    }

    /// <summary>Lists the Unit slots that match.</summary>
    /// <param name="view">The side to search.</param>
    /// <param name="predicate">The condition.</param>
    /// <returns>The matching slots.</returns>
    private static int[] Slots(PlayerView view, Func<UnitView, bool> predicate) =>
        [.. Enumerable.Range(0, view.Units.Count).Where(slot => view.Units[slot] is { } unit && predicate(unit))];

    /// <summary>Picks one item.</summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="items">A non-empty list.</param>
    /// <param name="random">The source of choices.</param>
    /// <returns>The item.</returns>
    private static T Pick<T>(T[] items, Random random) => items[random.Next(items.Length)];
}
