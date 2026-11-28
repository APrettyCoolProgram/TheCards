// 261128_code
// 261128_documentation

using EchoesOfTheShardveil.Core;
using static EchoesOfTheShardveil.Tests.Core.DuelHarness;

namespace EchoesOfTheShardveil.Tests.Core;

/// <summary>Covers the turn sequence, energy, card plays and abilities.</summary>
public sealed class DuelTurnTests
{
    /// <summary>Only the active player can act, and only in the right phase.</summary>
    [Fact]
    public void ActionsRequireTheTurnAndPhase()
    {
        var duel = Start();

        Assert.Throws<InvalidOperationException>(() => duel.Advance(1));
        Assert.Throws<InvalidOperationException>(() => duel.FeedWell(0, 0));
        Assert.Throws<InvalidOperationException>(() => duel.Play(0, 0));
        Assert.Throws<InvalidOperationException>(() => duel.Attack(0, 0));
        Assert.Empty(duel.LegalPlays(0, 0));
    }

    /// <summary>The first player skips the opening draw and the second player does not.</summary>
    [Fact]
    public void FirstPlayerSkipsTheFirstDraw()
    {
        var deck = Enumerable.Range(0, Duel.DeckSize).Select(index => Unit($"card-{index}")).ToArray();
        var duel = new Duel(deck, deck, seed: 5);
        var first = duel.ActivePlayer;
        duel.Mulligan(0, []);
        duel.Mulligan(1, []);

        duel.AdvanceTo(first, DuelPhase.Main);
        Assert.Equal(Duel.OpeningHandSize, duel.Own(first).HandCount);

        duel.AdvanceTo(1 - first, DuelPhase.Main);
        Assert.Equal(Duel.OpeningHandSize + 1, duel.Own(1 - first).HandCount);
    }

    /// <summary>A player who must draw from an empty deck loses.</summary>
    [Fact]
    public void DrawingFromAnEmptyDeckLoses()
    {
        var duel = Start(cardsDrawnPerTurn: 1);
        duel.AdvanceTo(1, DuelPhase.Draw);

        duel.Advance(1);

        Assert.Equal(DuelOutcome.PlayerOneWins, duel.Outcome);
        Assert.Equal(DuelPhase.Finished, duel.Phase);
        Assert.Throws<InvalidOperationException>(() => duel.Advance(1));
    }

    /// <summary>The turn cannot end until the hand is within the limit.</summary>
    [Fact]
    public void HandLimitMustBeMetToEndTheTurn()
    {
        var duel = Start(handLimit: 9);
        duel.AdvanceTo(0, DuelPhase.End);
        Assert.Throws<InvalidOperationException>(() => duel.Advance(0));

        duel.DiscardToLimit(0, 0);

        Assert.Single(duel.Own(0).Discard);
        Assert.Throws<InvalidOperationException>(() => duel.DiscardToLimit(0, 0));
        duel.Advance(0);
        Assert.Equal(1, duel.ActivePlayer);
        Assert.Equal(2, duel.Turn);
    }

    /// <summary>The Well takes one shard per turn, pays costs, and refills next turn.</summary>
    [Fact]
    public void WellShardsPayCostsAndRefill()
    {
        var duel = Start([Unit("knight", cost: 1), Unit("giant", cost: 2)]);
        duel.AdvanceTo(0, DuelPhase.Main);
        Assert.Throws<InvalidOperationException>(() => duel.PlayCard(0, "knight"));

        duel.FeedWell(0, duel.Slot(0, "filler-0"));

        Assert.False(duel.Own(0).CanFeedWell);
        Assert.Throws<InvalidOperationException>(() => duel.FeedWell(0, 0));
        Assert.Empty(duel.LegalPlays(0, duel.Slot(0, "giant")));
        duel.PlayCard(0, "knight");
        Assert.Equal(0, duel.Own(0).ReadyShards);

        duel.AdvanceTo(1, DuelPhase.Main);
        duel.AdvanceTo(0, DuelPhase.Main);
        Assert.Equal(1, duel.Own(0).ReadyShards);
        Assert.True(duel.Own(0).CanFeedWell);
    }

    /// <summary>The Spark gives one temporary energy once, and it expires with the turn.</summary>
    [Fact]
    public void SparkIsTemporaryAndSingleUse()
    {
        var duel = Start(second: [Unit("knight", cost: 1)]);
        duel.AdvanceTo(1, DuelPhase.Main);
        var slot = duel.Slot(1, "knight");
        Assert.Empty(duel.LegalPlays(1, slot));
        Assert.NotEmpty(duel.LegalPlays(1, slot, includeSpark: true));

        duel.UseSpark(1);

        Assert.Equal(1, duel.Own(1).TemporaryEnergy);
        Assert.Throws<InvalidOperationException>(() => duel.UseSpark(1));
        duel.AdvanceTo(0, DuelPhase.Main);
        Assert.Equal(0, duel.Own(1).TemporaryEnergy);
    }

    /// <summary>Units fill slots, occupied slots are refused, and a Spell takes no slot.</summary>
    [Fact]
    public void CardsGoToTheirZones()
    {
        var duel = Start([Unit("one"), Unit("two"), Spell("bolt"), Relic("idol")]);
        duel.AdvanceTo(0, DuelPhase.Main);

        duel.Play(0, duel.Slot(0, "one"), boardSlot: 2);
        Assert.Throws<InvalidOperationException>(() => duel.Play(0, duel.Slot(0, "two"), boardSlot: 2));
        Assert.Throws<InvalidOperationException>(() => duel.Play(0, duel.Slot(0, "bolt"), boardSlot: 0));
        duel.PlayCard(0, "two");
        duel.PlayCard(0, "bolt");
        duel.PlayCard(0, "idol");

        var view = duel.Own(0);
        Assert.Equal("two", view.Units[0]!.Card.Id);
        Assert.Equal("one", view.Units[2]!.Card.Id);
        Assert.Equal("idol", view.Relics[0]!.Id);
        Assert.Equal("bolt", Assert.Single(view.Discard).Id);
        Assert.Throws<ArgumentOutOfRangeException>(() => duel.Play(0, view.HandCount));
    }

    /// <summary>A damage Spell needs an existing target and destroys a Unit with no Health left.</summary>
    [Fact]
    public void DamageSpellNeedsATarget()
    {
        var bolt = Spell("bolt", new CardAbility(AbilityTrigger.Spell, AbilityTarget.ChosenUnit, AbilityAction.Damage, 2));
        var duel = Start([bolt], [Unit("pup", health: 2)]);
        duel.AdvanceTo(0, DuelPhase.Main);
        Assert.Empty(duel.LegalPlays(0, duel.Slot(0, "bolt")));
        Assert.Throws<InvalidOperationException>(() => duel.PlayCard(0, "bolt"));
        Assert.Throws<InvalidOperationException>(() => duel.PlayCard(0, "bolt", new UnitTarget(1, 0)));

        duel.AdvanceTo(1, DuelPhase.Main);
        duel.PlayCard(1, "pup");
        duel.AdvanceTo(0, DuelPhase.Main);
        var target = new UnitTarget(1, 0);
        Assert.Equal(target, Assert.Single(duel.LegalPlays(0, duel.Slot(0, "bolt"))).Target);
        duel.PlayCard(0, "bolt", target);

        Assert.Null(duel.Own(1).Units[0]);
        Assert.Equal("pup", Assert.Single(duel.Own(1).Discard).Id);
    }

    /// <summary>A friendly-only effect cannot target an enemy Unit.</summary>
    [Fact]
    public void FriendlyEffectRefusesEnemies()
    {
        var banner = Spell("banner",
            new CardAbility(AbilityTrigger.Spell, AbilityTarget.FriendlyUnit, AbilityAction.IncreasePower, 2));
        var duel = Start([Unit("ally"), banner], [Unit("foe")]);
        duel.AdvanceTo(0, DuelPhase.Main);
        duel.PlayCard(0, "ally");
        duel.AdvanceTo(1, DuelPhase.Main);
        duel.PlayCard(1, "foe");
        duel.AdvanceTo(0, DuelPhase.Main);

        Assert.Throws<InvalidOperationException>(() => duel.PlayCard(0, "banner", new UnitTarget(1, 0)));
        duel.PlayCard(0, "banner", new UnitTarget(0, 0));

        Assert.Equal(3, duel.Own(0).Units[0]!.Power);
    }

    /// <summary>Healing never goes past the starting Health.</summary>
    [Fact]
    public void HealingStopsAtStartingHealth()
    {
        var heal = new CardAbility(AbilityTrigger.Arrival, AbilityTarget.Controller, AbilityAction.HealLeader, 3);
        var duel = Start([Unit("healer", abilities: heal)]);
        duel.AdvanceTo(0, DuelPhase.Main);

        duel.PlayCard(0, "healer");

        Assert.Equal(20, duel.Own(0).Health);
    }

    /// <summary>Relic abilities resolve at the start and end of their controller's turns.</summary>
    [Fact]
    public void RelicTriggersResolveOnTheirTurn()
    {
        var idol = Relic("idol",
            new CardAbility(AbilityTrigger.StartTurn, AbilityTarget.RandomFriendlyUnit, AbilityAction.IncreasePower),
            new CardAbility(AbilityTrigger.EndTurn, AbilityTarget.RandomFriendlyUnit, AbilityAction.Damage));
        var duel = Start([Unit("ally", health: 5), idol]);
        duel.AdvanceTo(0, DuelPhase.Main);
        duel.PlayCard(0, "ally");
        duel.PlayCard(0, "idol");

        duel.AdvanceTo(0, DuelPhase.End);
        Assert.Equal(1, duel.Own(0).Units[0]!.Damage);
        Assert.Equal(1, duel.Own(0).Units[0]!.Power);

        duel.AdvanceTo(1, DuelPhase.Main);
        duel.AdvanceTo(0, DuelPhase.Main);
        Assert.Equal(2, duel.Own(0).Units[0]!.Power);
    }

    /// <summary>Last Breath resolves when a Unit is destroyed.</summary>
    [Fact]
    public void LastBreathResolvesOnDestruction()
    {
        var breath = new CardAbility(AbilityTrigger.LastBreath, AbilityTarget.Controller, AbilityAction.Draw);
        var bolt = Spell("bolt", new CardAbility(AbilityTrigger.Spell, AbilityTarget.ChosenUnit, AbilityAction.Damage));
        var duel = Start([Unit("ghost", abilities: breath), bolt]);
        duel.AdvanceTo(0, DuelPhase.Main);
        duel.PlayCard(0, "ghost");

        duel.PlayCard(0, "bolt", new UnitTarget(0, 0));

        // The deck is empty, so the Last Breath draw loses the duel.
        Assert.Equal(DuelOutcome.PlayerTwoWins, duel.Outcome);
    }

    /// <summary>An activated ability costs energy and only activated abilities can be used.</summary>
    [Fact]
    public void ActivatedAbilityIsPaidFor()
    {
        var arrive = new CardAbility(AbilityTrigger.Arrival, AbilityTarget.Controller, AbilityAction.HealLeader);
        var pump = new CardAbility(AbilityTrigger.Activated, AbilityTarget.FriendlyUnit, AbilityAction.IncreasePower, Cost: 1);
        var duel = Start([Unit("ally"), Relic("forge", arrive, pump)]);
        duel.AdvanceTo(0, DuelPhase.Main);
        duel.PlayCard(0, "ally");
        duel.PlayCard(0, "forge");
        var ally = new UnitTarget(0, 0);
        Assert.Throws<InvalidOperationException>(() => duel.Activate(0, relic: true, 0, 1, ally));
        duel.FeedWell(0, 0);
        Assert.Throws<InvalidOperationException>(() => duel.Activate(0, relic: true, 0, 0));
        Assert.Throws<InvalidOperationException>(() => duel.Activate(0, relic: true, 1, 0));

        duel.Activate(0, relic: true, 0, 1, ally);

        Assert.Equal(2, duel.Own(0).Units[0]!.Power);
        Assert.Equal(0, duel.Own(0).ReadyShards);
    }
}
