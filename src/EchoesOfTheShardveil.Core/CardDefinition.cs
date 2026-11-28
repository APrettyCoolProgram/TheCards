// 261127_code
// 261127_documentation

namespace EchoesOfTheShardveil.Core;

/// <summary>Defines a card independently of its state in a duel.</summary>
/// <param name="Id">The stable identifier of the card, such as <c>ember-pup</c>.</param>
/// <param name="Name">The player-facing name. Deck copy limits are applied by name.</param>
public sealed record CardDefinition(string Id, string Name)
{
    /// <summary>Gets the kind of card, which decides how it is played.</summary>
    public CardType Type { get; init; } = CardType.Unit;

    /// <summary>Gets the energy needed to play the card.</summary>
    public int Cost { get; init; }

    /// <summary>Gets the printed combat damage of a Unit.</summary>
    public int Power { get; init; }

    /// <summary>Gets the printed Health of a Unit.</summary>
    public int Health { get; init; }

    /// <summary>Gets the descriptive subtype, such as Beast or Knight, if the card has one.</summary>
    public string? Subtype { get; init; }

    /// <summary>Gets the Aspect of the card, if it has one. No rule uses it yet.</summary>
    public string? Aspect { get; init; }

    /// <summary>Gets the combat keywords of the card.</summary>
    public CardKeyword Keywords { get; init; }

    /// <summary>Gets the triggered and activated effects of the card.</summary>
    public IReadOnlyList<CardAbility> Abilities { get; init; } = [];

    /// <summary>Determines whether the card has a keyword.</summary>
    /// <param name="keyword">The keyword to look for.</param>
    /// <returns><see langword="true"/> if the card has the keyword; otherwise, <see langword="false"/>.</returns>
    public bool HasKeyword(CardKeyword keyword) => (Keywords & keyword) != 0;

    /// <summary>Gets the abilities that resolve when the card is played from hand.</summary>
    /// <returns>
    /// The <see cref="AbilityTrigger.Spell"/> abilities of a Spell, or the <see cref="AbilityTrigger.Arrival"/>
    /// abilities of any other card.
    /// </returns>
    public IReadOnlyList<CardAbility> GetPlayAbilities()
    {
        var trigger = Type == CardType.Spell ? AbilityTrigger.Spell : AbilityTrigger.Arrival;
        return [.. Abilities.Where(ability => ability.Trigger == trigger)];
    }
}

/// <summary>The kind of a card.</summary>
public enum CardType
{
    /// <summary>A player's Leader. It cannot be put in a deck.</summary>
    Leader,
    /// <summary>A creature that occupies a Unit slot, attacks and intercepts.</summary>
    Unit,
    /// <summary>A one-time effect that goes to the discard pile when played.</summary>
    Spell,
    /// <summary>A permanent that occupies a Relic slot.</summary>
    Relic,
}

/// <summary>Combat keywords a card can combine.</summary>
[Flags]
public enum CardKeyword
{
    /// <summary>No keywords.</summary>
    None = 0,
    /// <summary>Can attack on the turn it enters play.</summary>
    Rush = 1,
    /// <summary>Does not become exhausted by attacking.</summary>
    Vigilant = 2,
    /// <summary>Can intercept while exhausted.</summary>
    Sentinel = 4,
    /// <summary>Can only be intercepted by Elusive Units.</summary>
    Elusive = 8,
    /// <summary>Combat damage beyond a Unit's remaining Health hits that Unit's player.</summary>
    Pierce = 16,
    /// <summary>Restores its controller's Health by the combat damage it deals.</summary>
    Drain = 32,
}

/// <summary>When a card ability resolves.</summary>
public enum AbilityTrigger
{
    /// <summary>When a Spell is played.</summary>
    Spell,
    /// <summary>When a Unit or Relic enters play.</summary>
    Arrival,
    /// <summary>When a Unit is destroyed.</summary>
    LastBreath,
    /// <summary>At the start of its controller's turn.</summary>
    StartTurn,
    /// <summary>At the end of its controller's turn.</summary>
    EndTurn,
    /// <summary>When its controller pays to use it during the Main phase.</summary>
    Activated,
}

/// <summary>What a card ability affects.</summary>
public enum AbilityTarget
{
    /// <summary>The player who controls the card.</summary>
    Controller,
    /// <summary>A Unit on either side, chosen by the controller.</summary>
    ChosenUnit,
    /// <summary>A Unit of the controller, chosen by the controller.</summary>
    FriendlyUnit,
    /// <summary>A Unit of the controller, chosen at random.</summary>
    RandomFriendlyUnit,
}

/// <summary>What a card ability does.</summary>
public enum AbilityAction
{
    /// <summary>Deals damage to a Unit.</summary>
    Damage,
    /// <summary>Restores the controller's Health.</summary>
    HealLeader,
    /// <summary>Draws cards for the controller.</summary>
    Draw,
    /// <summary>Removes exhaustion from a Unit.</summary>
    Ready,
    /// <summary>Permanently raises the Power of a Unit.</summary>
    IncreasePower,
}

/// <summary>Describes a card effect and its optional activation cost.</summary>
/// <param name="Trigger">When the effect resolves.</param>
/// <param name="Target">What the effect applies to.</param>
/// <param name="Action">What the effect does.</param>
/// <param name="Amount">
/// The damage, healing, number of cards or Power gained. <see cref="AbilityAction.Ready"/> ignores it.
/// </param>
/// <param name="Cost">The energy needed to use an <see cref="AbilityTrigger.Activated"/> ability.</param>
public sealed record CardAbility(
    AbilityTrigger Trigger, AbilityTarget Target, AbilityAction Action, int Amount = 1, int Cost = 0);
