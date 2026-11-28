// 261128_code
// 261128_documentation

using EchoesOfTheShardveil.Core;
using static EchoesOfTheShardveil.Tests.Core.DuelHarness;

namespace EchoesOfTheShardveil.Tests.Core;

/// <summary>Covers attacking, intercepting and the combat keywords.</summary>
public sealed class DuelCombatTests
{
    /// <summary>A new Unit cannot attack unless it has Rush.</summary>
    /// <param name="keywords">The Unit's keywords.</param>
    /// <param name="canAttack">Whether it may attack on arrival.</param>
    [Theory]
    [InlineData(CardKeyword.None, false)]
    [InlineData(CardKeyword.Rush, true)]
    public void OnlyRushAttacksOnArrival(CardKeyword keywords, bool canAttack)
    {
        var duel = Start([Unit("pup", keywords: keywords)]);
        duel.AdvanceTo(0, DuelPhase.Main);
        duel.PlayCard(0, "pup");
        duel.AdvanceTo(0, DuelPhase.Battle);

        Assert.Equal(canAttack, duel.Own(0).Units[0]!.CanAttack);
        if (!canAttack)
        {
            Assert.Throws<InvalidOperationException>(() => duel.Attack(0, 0));
        }
    }

    /// <summary>An unblocked attack damages the Leader, and the turn waits for the defender.</summary>
    [Fact]
    public void UnblockedAttackHitsTheLeader()
    {
        var duel = Faceoff(Unit("brute", power: 3), Unit("wall", health: 5));

        duel.Attack(0, 0);
        Assert.Throws<InvalidOperationException>(() => duel.Advance(0));
        Assert.Throws<InvalidOperationException>(() => duel.Defend(0));
        duel.Defend(1);

        Assert.Equal(17, duel.Own(1).Health);
        Assert.Null(duel.PendingAttack);
        Assert.True(duel.Own(0).Units[0]!.Exhausted);
        Assert.Throws<InvalidOperationException>(() => duel.Attack(0, 0));
    }

    /// <summary>Units deal damage to each other at the same time.</summary>
    [Fact]
    public void CombatIsSimultaneous()
    {
        var duel = Faceoff(Unit("left", power: 2, health: 2), Unit("right", power: 2, health: 2));

        duel.Attack(0, 0, new UnitTarget(1, 0));
        duel.Defend(1);

        Assert.Null(duel.Own(0).Units[0]);
        Assert.Null(duel.Own(1).Units[0]);
        Assert.Equal(20, duel.Own(1).Health);
    }

    /// <summary>A Vigilant Unit stays ready after attacking but still attacks once a turn.</summary>
    [Fact]
    public void VigilantStaysReady()
    {
        var duel = Faceoff(Unit("guard", keywords: CardKeyword.Vigilant), Unit("wall", health: 5));

        duel.Attack(0, 0);
        duel.Defend(1);

        Assert.False(duel.Own(0).Units[0]!.Exhausted);
        Assert.False(duel.Own(0).Units[0]!.CanAttack);
    }

    /// <summary>Only an Elusive Unit can intercept an Elusive attacker.</summary>
    /// <param name="defender">The interceptor's keywords.</param>
    /// <param name="allowed">Whether it may intercept.</param>
    [Theory]
    [InlineData(CardKeyword.None, false)]
    [InlineData(CardKeyword.Elusive, true)]
    public void ElusiveRestrictsInterceptors(CardKeyword defender, bool allowed)
    {
        var duel = Faceoff(Unit("scout", keywords: CardKeyword.Elusive), Unit("watcher", health: 5, keywords: defender));
        duel.Attack(0, 0);

        Assert.Equal(allowed, duel.Own(1).Units[0]!.CanIntercept);
        if (allowed)
        {
            duel.Defend(1, 0);
            Assert.Equal(20, duel.Own(1).Health);
        }
        else
        {
            Assert.Throws<InvalidOperationException>(() => duel.Defend(1, 0));
        }
    }

    /// <summary>An exhausted Unit can intercept only if it has Sentinel.</summary>
    /// <param name="keywords">The interceptor's keywords.</param>
    /// <param name="allowed">Whether it may intercept while exhausted.</param>
    [Theory]
    [InlineData(CardKeyword.None, false)]
    [InlineData(CardKeyword.Sentinel, true)]
    public void SentinelInterceptsWhileExhausted(CardKeyword keywords, bool allowed)
    {
        var duel = Faceoff(Unit("brute", health: 5), Unit("squire", health: 5, keywords: keywords));
        duel.AdvanceTo(1, DuelPhase.Battle);
        duel.Attack(1, 0);
        duel.Defend(0);
        // A Unit stays exhausted until its controller's next turn.
        duel.AdvanceTo(0, DuelPhase.Battle);
        Assert.True(duel.Own(1).Units[0]!.Exhausted);

        duel.Attack(0, 0);

        Assert.Equal(allowed, duel.Own(1).Units[0]!.CanIntercept);
    }

    /// <summary>Pierce carries only the excess damage through to the Leader.</summary>
    [Fact]
    public void PierceDealsExcessDamage()
    {
        var duel = Faceoff(Unit("lancer", power: 5, health: 5, keywords: CardKeyword.Pierce), Unit("pup", health: 2));

        duel.Attack(0, 0, new UnitTarget(1, 0));
        duel.Defend(1);

        Assert.Equal(17, duel.Own(1).Health);
    }

    /// <summary>Drain heals only by the damage that had Health to remove.</summary>
    [Fact]
    public void DrainHealsByDamageDealt()
    {
        var duel = Faceoff(Unit("brute", power: 8, health: 20),
            Unit("bat", power: 9, health: 3, keywords: CardKeyword.Drain));
        duel.Attack(0, 0);
        duel.Defend(1);
        Assert.Equal(12, duel.Own(1).Health);

        duel.AdvanceTo(1, DuelPhase.Battle);
        duel.Attack(1, 0, new UnitTarget(0, 0));
        duel.Defend(0);
        // All 9 damage landed on a Unit with 20 Health, healing up to the cap, though the bat is destroyed.
        Assert.Equal(20, duel.Own(1).Health);
        Assert.Null(duel.Own(1).Units[0]);
    }

    /// <summary>Overkill on a Unit does not add to Drain.</summary>
    [Fact]
    public void DrainIgnoresOverkill()
    {
        var duel = Faceoff(Unit("brute", power: 8, health: 2),
            Unit("bat", power: 9, health: 20, keywords: CardKeyword.Drain));
        duel.Attack(0, 0);
        duel.Defend(1);

        duel.AdvanceTo(1, DuelPhase.Battle);
        duel.Attack(1, 0, new UnitTarget(0, 0));
        duel.Defend(0);

        Assert.Equal(14, duel.Own(1).Health);
    }

    /// <summary>Lethal damage to the Leader ends the duel.</summary>
    [Fact]
    public void LethalDamageWins()
    {
        var duel = Faceoff(Unit("brute", power: 9), Unit("wall", health: 5), startingHealth: 5);

        duel.Attack(0, 0);
        duel.Defend(1);

        Assert.Equal(0, duel.Own(1).Health);
        Assert.Equal(DuelOutcome.PlayerOneWins, duel.Outcome);
        Assert.Equal(DuelPhase.Finished, duel.Phase);
    }

    /// <summary>Both Leaders falling in one exchange is a draw.</summary>
    [Fact]
    public void SimultaneousDefeatIsADraw()
    {
        var duel = Faceoff(Unit("left", power: 9, keywords: CardKeyword.Pierce),
            Unit("right", power: 9, keywords: CardKeyword.Pierce), startingHealth: 5);

        duel.Attack(0, 0, new UnitTarget(1, 0));
        duel.Defend(1);

        Assert.Equal(DuelOutcome.Draw, duel.Outcome);
    }

    /// <summary>An attack can only be aimed at an enemy Unit that exists.</summary>
    [Fact]
    public void AttackTargetMustBeAnEnemyUnit()
    {
        var duel = Faceoff(Unit("brute"), Unit("wall"));

        Assert.Throws<InvalidOperationException>(() => duel.Attack(0, 0, new UnitTarget(0, 0)));
        Assert.Throws<InvalidOperationException>(() => duel.Attack(0, 0, new UnitTarget(1, 3)));
        Assert.Throws<InvalidOperationException>(() => duel.Attack(0, 4));
    }

    /// <summary>Puts one Unit in slot 0 on each side, then stops in player 0's Battle phase on a later turn.</summary>
    /// <param name="attacker">Player 0's Unit.</param>
    /// <param name="defender">Player 1's Unit.</param>
    /// <param name="startingHealth">The starting Health.</param>
    /// <returns>The duel.</returns>
    private static Duel Faceoff(CardDefinition attacker, CardDefinition defender, int startingHealth = 20)
    {
        var duel = Start([attacker], [defender], startingHealth);
        duel.AdvanceTo(0, DuelPhase.Main);
        duel.PlayCard(0, attacker.Id);
        duel.AdvanceTo(1, DuelPhase.Main);
        duel.PlayCard(1, defender.Id);
        duel.AdvanceTo(0, DuelPhase.Battle);
        return duel;
    }
}
