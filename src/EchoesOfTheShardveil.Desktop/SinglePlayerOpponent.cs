// 261127_code
// 261127_documentation

using EchoesOfTheShardveil.Core;

namespace EchoesOfTheShardveil.Desktop;

/// <summary>Plays one legal action for the computer opponent.</summary>
internal static class SinglePlayerOpponent
{
    /// <summary>The player index the computer controls.</summary>
    public const int ComputerSide = 1;

    /// <summary>The player index of the person it plays against.</summary>
    public const int HumanSide = 0;

    /// <summary>Takes the computer's next single action, so the player can follow along.</summary>
    /// <param name="duel">A duel waiting for the computer.</param>
    /// <param name="profile">The computer opponent preferences.</param>
    /// <returns>A message describing the action, or an empty string when there is nothing worth showing.</returns>
    public static string TakeStep(Duel duel, OpponentProfile profile)
    {
        var own = duel.View(ComputerSide, ComputerSide);
        var enemy = duel.View(HumanSide, ComputerSide);
        var hand = own.Hand!;
        if (duel.PendingAttack is not null)
        {
            var defender = FindFirstUnitSlot(own, unit => unit.CanIntercept);
            duel.Defend(ComputerSide, defender >= 0 ? defender : null);
            return defender >= 0 ? $"{own.Units[defender]!.Card.Name} intercepted." : "Opponent took the attack.";
        }

        switch (duel.Phase)
        {
            case DuelPhase.Main:
                if (own.CanFeedWell && hand.Count > profile.CardsToKeepWhenFeeding)
                {
                    var shard = Enumerable.Range(0, hand.Count).MaxBy(slot => hand[slot].Cost);
                    duel.FeedWell(ComputerSide, shard);
                    return "Opponent fed the Well.";
                }
                if (TryPlayCard(duel, own, enemy, profile) is { } played)
                {
                    return $"Opponent played {played.Name}.";
                }
                break;
            case DuelPhase.Battle:
                var attacker = FindFirstUnitSlot(own, unit => unit.CanAttack);
                if (attacker >= 0)
                {
                    duel.Attack(ComputerSide, attacker);
                    return $"{own.Units[attacker]!.Card.Name} attacks your Hero.";
                }
                break;
            case DuelPhase.End when hand.Count > duel.Format.HandLimit:
                duel.DiscardToLimit(ComputerSide, hand.Count - 1);
                return "Opponent discarded to the hand limit.";
        }

        duel.Advance(ComputerSide);
        return duel.ActivePlayer == HumanSide ? "Your turn begins." : "";
    }

    /// <summary>Plays the most expensive card that is affordable and has somewhere to go.</summary>
    /// <param name="duel">The duel in the computer's Main phase.</param>
    /// <param name="own">The computer's side.</param>
    /// <param name="enemy">The human's side.</param>
    /// <param name="profile">The computer opponent preferences.</param>
    /// <returns>The card played, or <see langword="null"/> if none could be.</returns>
    private static CardDefinition? TryPlayCard(Duel duel, PlayerView own, PlayerView enemy, OpponentProfile profile)
    {
        var hand = own.Hand!;
        var energy = own.ReadyShards + own.TemporaryEnergy;
        foreach (var slot in Enumerable.Range(0, hand.Count).OrderByDescending(index => hand[index].Cost))
        {
            var card = hand[slot];
            var plays = duel.LegalPlays(ComputerSide, slot, includeSpark: profile.SpendSpark);
            var friendly = RequiredTarget(card) == AbilityTarget.FriendlyUnit;
            var preferred = friendly ? own : enemy;
            var preferredSide = friendly ? ComputerSide : HumanSide;
            var choices = plays.Where(play => play.Target is null || play.Target.Value.Player == preferredSide).ToArray();
            if (choices.Length == 0)
            {
                continue;
            }

            var choice = choices.MaxBy(play => play.Target is { } target && preferred.Units[target.Slot] is { } unit
                ? profile.TargetPriority switch
                {
                    OpponentTargetPriority.LowestHealth => -(unit.Card.Health - unit.Damage),
                    OpponentTargetPriority.HighestPower => unit.Power,
                    _ => 0,
                }
                : 0);
            if (card.Cost > energy)
            {
                duel.UseSpark(ComputerSide);
            }
            duel.Play(ComputerSide, slot, choice.Target, choice.BoardSlot);
            return card;
        }
        return null;
    }

    /// <summary>Gets the kind of Unit a card needs as a target when played. A friendly-only requirement wins.</summary>
    /// <param name="card">The card.</param>
    /// <returns>The target kind, or <see langword="null"/> if none is needed.</returns>
    private static AbilityTarget? RequiredTarget(CardDefinition card)
    {
        var abilities = card.GetPlayAbilities();
        if (abilities.Any(ability => ability.Target == AbilityTarget.FriendlyUnit))
        {
            return AbilityTarget.FriendlyUnit;
        }
        return abilities.Any(ability => ability.Target == AbilityTarget.ChosenUnit) ? AbilityTarget.ChosenUnit : null;
    }

    /// <summary>Finds the slot of the first Unit that matches.</summary>
    /// <param name="view">The side to search.</param>
    /// <param name="predicate">The condition.</param>
    /// <returns>The Unit slot, or -1.</returns>
    private static int FindFirstUnitSlot(PlayerView view, Func<UnitView, bool> predicate)
    {
        for (var slot = 0; slot < view.Units.Count; slot++)
        {
            if (view.Units[slot] is { } unit && predicate(unit))
            {
                return slot;
            }
        }
        return -1;
    }
}
