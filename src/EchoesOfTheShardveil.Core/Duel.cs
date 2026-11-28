// 261127_code
// 261127_documentation

using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace EchoesOfTheShardveil.Core;

/// <summary>Runs a two-player duel under a <see cref="DuelFormat"/>. A seed makes it deterministic.</summary>
public sealed class Duel
{
    /// <summary>The default starting Health and healing cap.</summary>
    public const int StartingHealth = 20;

    /// <summary>The default required deck size.</summary>
    public const int DeckSize = 40;

    /// <summary>The default number of cards dealt before the mulligan.</summary>
    public const int OpeningHandSize = 5;

    /// <summary>The default hand limit at the end of a turn.</summary>
    public const int HandLimit = 10;

    /// <summary>The default number of Unit slots on each side.</summary>
    public const int UnitSlots = 5;

    /// <summary>The default number of Relic slots on each side.</summary>
    public const int RelicSlots = 2;

    /// <summary>The default Well capacity and maximum energy cost.</summary>
    public const int WellLimit = 10;

    /// <summary>The default copy limit for each card name.</summary>
    public const int MaxCopiesPerName = 3;

    private static readonly DuelFormat DefaultFormat = new(StartingHealth, DeckSize, MaxCopiesPerName);

    private readonly Player[] players;
    private readonly Random random;

    /// <summary>Validates and shuffles both decks, chooses the first player, and deals opening hands.</summary>
    /// <param name="playerOneDeck">The deck of the player at index 0.</param>
    /// <param name="playerTwoDeck">The deck of the player at index 1.</param>
    /// <param name="seed">The random seed, or <see langword="null"/> for an unpredictable duel.</param>
    /// <param name="format">The match limits, or <see langword="null"/> for the default format.</param>
    /// <param name="playerOneLeader">The first player's Leader, or null for a default identity.</param>
    /// <param name="playerTwoLeader">The second player's Leader, or null for a default identity.</param>
    /// <exception cref="ArgumentException">A deck or Leader violates the duel rules.</exception>
    public Duel(IEnumerable<CardDefinition> playerOneDeck, IEnumerable<CardDefinition> playerTwoDeck,
        int? seed = null, DuelFormat? format = null, CardDefinition? playerOneLeader = null,
        CardDefinition? playerTwoLeader = null)
    {
        if (playerOneLeader is { Type: not CardType.Leader } || playerTwoLeader is { Type: not CardType.Leader })
        {
            throw new ArgumentException("A duel leader must have the Leader card type.");
        }

        Format = format ?? DefaultFormat;
        random = seed is int value ? new Random(value) : new Random();
        players =
        [
            new(playerOneLeader ?? new("leader-one", "Leader One") { Type = CardType.Leader },
                ValidateDeck(playerOneDeck), Format),
            new(playerTwoLeader ?? new("leader-two", "Leader Two") { Type = CardType.Leader },
                ValidateDeck(playerTwoDeck), Format),
        ];
        var firstPlayer = random.Next(players.Length);
        ActivePlayer = firstPlayer;
        players[1 - firstPlayer].HasSpark = Format.SecondPlayerGetsSpark;
        foreach (var player in players)
        {
            random.Shuffle(CollectionsMarshal.AsSpan(player.Deck));
            Draw(player, Format.OpeningHandSize);
        }
    }

    /// <summary>Gets the format used by this duel.</summary>
    public DuelFormat Format { get; }

    /// <summary>Gets the step waiting to be resolved.</summary>
    public DuelPhase Phase { get; private set; } = DuelPhase.Mulligan;

    /// <summary>Gets the result of the duel.</summary>
    public DuelOutcome Outcome { get; private set; }

    /// <summary>Gets the index of the player whose turn it is.</summary>
    public int ActivePlayer { get; private set; }

    /// <summary>Gets the turn number, starting at 1 and counting both players' turns.</summary>
    public int Turn { get; private set; } = 1;

    /// <summary>Gets the attack waiting for the defender, or <see langword="null"/> if there is none.</summary>
    public AttackView? PendingAttack { get; private set; }

    /// <summary>Returns a snapshot, revealing only the viewer's hand and public information.</summary>
    /// <param name="player">The index of the player to describe.</param>
    /// <param name="viewer">The index of the player who will see the snapshot.</param>
    /// <returns>The state of <paramref name="player"/> as <paramref name="viewer"/> may see it.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when either index is not a player.</exception>
    public PlayerView View(int player, int viewer)
    {
        var owner = GetPlayer(player);
        GetPlayer(viewer); // Rejects an unknown viewer.
        var units = owner.Units.Select(unit => unit is null ? null : DescribeUnit(player, unit)).ToArray();
        return new(owner.Leader, owner.Health, owner.Deck.Count, owner.Hand.Count,
            player == viewer ? owner.Hand.ToArray() : null, owner.Well.Count, owner.ReadyShards,
            owner.TemporaryEnergy, owner.HasSpark, Phase == DuelPhase.Main && ActivePlayer == player &&
            CanReceiveShard(owner), units, [.. owner.Relics], [.. owner.Discard]);
    }

    /// <summary>Exchanges selected cards once during setup. An empty selection keeps the hand.</summary>
    /// <param name="player">The index of the player.</param>
    /// <param name="handSlots">The zero-based hand slots to exchange.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when setup is over or the player already chose their opening hand.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown when the slots are invalid or exceed the mulligan limit.</exception>
    public void Mulligan(int player, IEnumerable<int> handSlots)
    {
        Require(Phase == DuelPhase.Mulligan, "Mulligans are only available during setup.");
        var owner = GetPlayer(player);
        Require(!owner.MulliganComplete, "That player already chose their opening hand.");
        var slots = ValidateMulliganSlots(owner, handSlots);
        var returned = slots.Select(slot => owner.Hand[slot]).ToArray();
        random.Shuffle(returned);
        foreach (var slot in slots.OrderDescending())
        {
            owner.Hand.RemoveAt(slot);
        }
        // Cards are drawn from the end of the list, so the start of the list is the bottom of the deck.
        owner.Deck.InsertRange(0, returned);
        Draw(owner, returned.Length);
        owner.MulliganComplete = true;
        if (players.All(p => p.MulliganComplete))
        {
            Phase = DuelPhase.Ready;
        }
    }

    /// <summary>Resolves the current phase and progresses the turn.</summary>
    /// <param name="player">The index of the active player.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when it is not the player's turn, the phase cannot advance, an attack is waiting for the defender,
    /// or the hand is over the limit at the end of the turn.
    /// </exception>
    public void Advance(int player)
    {
        var owner = RequireActive(player);
        switch (Phase)
        {
            case DuelPhase.Ready:
                owner.ReadyShards = owner.Well.Count;
                owner.WellFeedsThisTurn = 0;
                foreach (var unit in owner.Units.OfType<Unit>())
                {
                    unit.Exhausted = false;
                }
                ResolveBoardTriggers(player, AbilityTrigger.StartTurn);
                SetNextPhase(DuelPhase.Draw);
                break;
            case DuelPhase.Draw:
                if (!Format.FirstPlayerSkipsFirstDraw || Turn != 1)
                {
                    Draw(owner, Format.CardsDrawnPerTurn);
                }
                SetNextPhase(DuelPhase.Main);
                break;
            case DuelPhase.Main:
                Phase = DuelPhase.Battle;
                break;
            case DuelPhase.Battle:
                Require(PendingAttack is null, "The defender must resolve the pending attack.");
                Phase = DuelPhase.End;
                ResolveBoardTriggers(player, AbilityTrigger.EndTurn);
                CheckOutcome();
                break;
            case DuelPhase.End:
                Require(owner.Hand.Count <= Format.HandLimit, "Discard down to the hand limit first.");
                owner.ReadyShards = 0;
                owner.TemporaryEnergy = 0;
                ActivePlayer = 1 - ActivePlayer;
                Turn++;
                Phase = DuelPhase.Ready;
                break;
            default:
                throw new InvalidOperationException("The phase cannot advance.");
        }
    }

    /// <summary>Discards a selected card during the end phase when hand size exceeds the limit.</summary>
    /// <param name="player">The index of the active player.</param>
    /// <param name="handSlot">The zero-based hand slot to discard.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when this is not the player's End phase or the hand is within the limit.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the hand slot is empty.</exception>
    public void DiscardToLimit(int player, int handSlot)
    {
        var owner = RequirePhase(player, DuelPhase.End);
        Require(owner.Hand.Count > Format.HandLimit, "The hand is within the limit.");
        owner.Discard.Add(GetHandCard(owner, handSlot));
        owner.Hand.RemoveAt(handSlot);
    }

    /// <summary>Turns a selected card in hand into a ready shard.</summary>
    /// <param name="player">The index of the active player.</param>
    /// <param name="handSlot">The zero-based hand slot of the card.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when this is not the player's Main phase or the Well cannot receive a shard.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the hand slot is empty.</exception>
    public void FeedWell(int player, int handSlot)
    {
        var owner = RequirePhase(player, DuelPhase.Main);
        Require(CanReceiveShard(owner), "The Well cannot receive another shard this turn.");
        owner.Well.Add(GetHandCard(owner, handSlot));
        owner.Hand.RemoveAt(handSlot);
        owner.ReadyShards++;
        owner.WellFeedsThisTurn++;
    }

    /// <summary>Spends the second player's once-per-duel Spark during Main.</summary>
    /// <param name="player">The index of the active player.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when this is not the player's Main phase or the player has no Spark.
    /// </exception>
    public void UseSpark(int player)
    {
        var owner = RequirePhase(player, DuelPhase.Main);
        Require(owner.HasSpark, "That player has no Spark left.");
        owner.HasSpark = false;
        owner.TemporaryEnergy += Format.SparkEnergy;
    }

    /// <summary>Lists available placements and targets without changing the duel.</summary>
    /// <param name="player">The index of the player.</param>
    /// <param name="handSlot">The zero-based hand slot of the card.</param>
    /// <param name="includeSpark">Whether to include plays affordable by spending an available Spark first.</param>
    /// <returns>
    /// Every way the card can be played with the allowed energy. The list is empty outside the player's Main phase, for an empty hand
    /// slot, or when the card cannot be paid for.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is not a player.</exception>
    public IReadOnlyList<CardPlay> LegalPlays(int player, int handSlot, bool includeSpark = false)
    {
        var owner = GetPlayer(player);
        if (Phase != DuelPhase.Main || player != ActivePlayer || (uint)handSlot >= owner.Hand.Count)
        {
            return [];
        }

        var card = owner.Hand[handSlot];
        if (card.Cost > owner.AvailableEnergy + (includeSpark && owner.HasSpark ? Format.SparkEnergy : 0))
        {
            return [];
        }

        var slots = card.Type switch
        {
            CardType.Unit => EmptySlots(owner.Units),
            CardType.Relic => EmptySlots(owner.Relics),
            _ => [null],
        };
        var abilities = card.GetPlayAbilities();
        if (IsTargetLegal(player, abilities, null))
        {
            return [.. slots.Select(slot => new CardPlay(slot, null))];
        }

        var plays = new List<CardPlay>();
        for (var side = 0; side < players.Length; side++)
        {
            for (var unit = 0; unit < players[side].Units.Length; unit++)
            {
                var target = new UnitTarget(side, unit);
                if (IsTargetLegal(player, abilities, target))
                {
                    plays.AddRange(slots.Select(slot => new CardPlay(slot, target)));
                }
            }
        }
        return plays;
    }

    /// <summary>Pays for a card and resolves its Main-phase play.</summary>
    /// <param name="player">The index of the active player.</param>
    /// <param name="handSlot">The zero-based hand slot of the card.</param>
    /// <param name="target">The Unit the card's ability applies to, if it needs one.</param>
    /// <param name="boardSlot">
    /// The slot for a Unit or Relic, or <see langword="null"/> to use the first open one. Must be
    /// <see langword="null"/> for a Spell.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when this is not the player's Main phase, the slot or target is not legal, or there is not enough energy.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the hand slot is empty.</exception>
    public void Play(int player, int handSlot, UnitTarget? target = null, int? boardSlot = null)
    {
        var owner = RequirePhase(player, DuelPhase.Main);
        var card = GetHandCard(owner, handSlot);
        var slot = card.Type switch
        {
            CardType.Unit => ChooseSlot(owner.Units, boardSlot),
            CardType.Relic => ChooseSlot(owner.Relics, boardSlot),
            _ when boardSlot is null => 0, // Unused: a Spell takes no slot.
            _ => throw new InvalidOperationException("That card does not occupy a board slot."),
        };
        var abilities = card.GetPlayAbilities();
        RequireLegalTarget(player, abilities, target);
        Pay(owner, card.Cost);
        owner.Hand.RemoveAt(handSlot);
        switch (card.Type)
        {
            case CardType.Unit:
                owner.Units[slot] = new Unit(card, Turn);
                break;
            case CardType.Relic:
                owner.Relics[slot] = card;
                break;
            case CardType.Spell:
                owner.Discard.Add(card);
                break;
        }
        ResolveAbilities(player, abilities, target);
        RemoveDefeatedUnits();
    }

    /// <summary>Uses an activated ability on one of the active player's Units or Relics.</summary>
    /// <param name="player">The index of the active player.</param>
    /// <param name="relic">
    /// <see langword="true"/> if the card is in a Relic slot; <see langword="false"/> if it is in a Unit slot.
    /// </param>
    /// <param name="slot">The zero-based slot of the card.</param>
    /// <param name="abilityIndex">The index of the ability in <see cref="CardDefinition.Abilities"/>.</param>
    /// <param name="target">The Unit the ability applies to, if it needs one.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when this is not the player's Main phase, the slot is empty, the ability is not activated, the target
    /// is not legal, or there is not enough energy.
    /// </exception>
    public void Activate(int player, bool relic, int slot, int abilityIndex, UnitTarget? target = null)
    {
        var owner = RequirePhase(player, DuelPhase.Main);
        var card = relic ? (uint)slot < owner.Relics.Length ? owner.Relics[slot] : null
            : GetUnit(new(player, slot))?.Card;
        Require(card is not null, "There is no card in that slot.");
        var abilities = card.Abilities;
        Require((uint)abilityIndex < abilities.Count && abilities[abilityIndex].Trigger == AbilityTrigger.Activated,
            "That ability is not activated.");
        var ability = abilities[abilityIndex];
        RequireLegalTarget(player, [ability], target);
        Pay(owner, ability.Cost);
        ResolveAbilities(player, [ability], target);
        RemoveDefeatedUnits();
    }

    /// <summary>Declares an attack against a Unit, or against the enemy Leader if the target is null.</summary>
    /// <param name="player">The index of the active player.</param>
    /// <param name="attackerSlot">The Unit slot of the attacker.</param>
    /// <param name="target">The enemy Unit to attack, or <see langword="null"/> for the enemy Leader.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when this is not the player's Battle phase, an attack is already pending, the Unit cannot attack, or
    /// the target is not an enemy Unit.
    /// </exception>
    public void Attack(int player, int attackerSlot, UnitTarget? target = null)
    {
        RequirePhase(player, DuelPhase.Battle);
        Require(PendingAttack is null, "Resolve the previous attack first.");
        var attackerAddress = new UnitTarget(player, attackerSlot);
        var attacker = GetUnit(attackerAddress);
        Require(attacker is not null && CanAttack(attacker), "That Unit cannot attack.");
        Require(target is null || (target.Value.Player == 1 - player && GetUnit(target.Value) is not null),
            "Choose the enemy Leader or a Unit on the enemy side.");
        attacker.LastAttackTurn = Turn;
        if (!attacker.Card.HasKeyword(CardKeyword.Vigilant))
        {
            attacker.Exhausted = true;
        }
        PendingAttack = new(attackerAddress, target);
    }

    /// <summary>Accepts the attack or redirects it to an eligible defending Unit, then resolves combat.</summary>
    /// <param name="player">The index of the defending player.</param>
    /// <param name="interceptSlot">
    /// The Unit slot of the interceptor, or <see langword="null"/> to accept the attack as declared.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when there is no attack for the player to defend or the Unit cannot intercept it.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is not a player.</exception>
    public void Defend(int player, int? interceptSlot = null)
    {
        var defender = GetPlayer(player);
        Require(player == 1 - ActivePlayer && PendingAttack is not null, "There is no attack for this player to defend.");
        var attack = PendingAttack;
        var attacker = GetUnit(attack.Attacker)!;
        var target = attack.Target;
        if (interceptSlot is int slot)
        {
            var address = new UnitTarget(player, slot);
            var interceptor = GetUnit(address);
            Require(interceptor is not null && CanIntercept(interceptor, attacker),
                "That Unit cannot intercept this attack.");
            interceptor.Exhausted = true;
            interceptor.LastInterceptTurn = Turn;
            target = address;
        }

        PendingAttack = null;
        if (target is { } blockerAddress)
        {
            ResolveUnitCombat(attacker, player, GetUnit(blockerAddress)!);
        }
        else
        {
            ApplyDrain(ActivePlayer, attacker, DamageLeader(defender, attacker.Power));
        }
        RemoveDefeatedUnits();
    }

    /// <summary>Lists the indexes of the empty slots in a row.</summary>
    /// <typeparam name="T">The kind of occupant.</typeparam>
    /// <param name="row">The Unit or Relic slots.</param>
    /// <returns>The empty slot indexes in order.</returns>
    private static int?[] EmptySlots<T>(T?[] row) where T : class =>
        [.. Enumerable.Range(0, row.Length).Where(slot => row[slot] is null).Select(slot => (int?)slot)];

    /// <summary>Picks the slot a card will occupy.</summary>
    /// <typeparam name="T">The kind of occupant.</typeparam>
    /// <param name="row">The Unit or Relic slots.</param>
    /// <param name="requested">The requested slot, or <see langword="null"/> for the first open one.</param>
    /// <returns>The chosen slot.</returns>
    /// <exception cref="InvalidOperationException">Thrown when there is no such open slot.</exception>
    private static int ChooseSlot<T>(T?[] row, int? requested) where T : class
    {
        var slot = requested ?? Array.IndexOf(row, null);
        Require((uint)slot < row.Length && row[slot] is null, "Choose an open slot for that card.");
        return slot;
    }

    /// <summary>Determines whether a player's Well can still be fed this turn.</summary>
    /// <param name="owner">The player.</param>
    /// <returns><see langword="true"/> if the Well has room and the turn's feed limit is not used up.</returns>
    private bool CanReceiveShard(Player owner) =>
        owner.WellFeedsThisTurn < Format.WellFeedsPerTurn && owner.Well.Count < Format.WellLimit;

    /// <summary>Takes a public snapshot of a Unit.</summary>
    /// <param name="player">The index of the Unit's controller.</param>
    /// <param name="unit">The Unit.</param>
    /// <returns>The snapshot, including what the Unit can do now.</returns>
    private UnitView DescribeUnit(int player, Unit unit) => new(unit.Card, unit.Power, unit.Damage, unit.Exhausted,
        Phase == DuelPhase.Battle && player == ActivePlayer && PendingAttack is null && CanAttack(unit),
        PendingAttack is { } attack && player != ActivePlayer && CanIntercept(unit, GetUnit(attack.Attacker)!));

    /// <summary>Determines whether a Unit is able to attack, ignoring the phase.</summary>
    /// <param name="unit">The Unit.</param>
    /// <returns>
    /// <see langword="true"/> if the Unit is ready, has not attacked this turn, and has been in play or has Rush.
    /// </returns>
    private bool CanAttack(Unit unit) => !unit.Exhausted && unit.LastAttackTurn != Turn &&
        (unit.ArrivalTurn < Turn || unit.Card.HasKeyword(CardKeyword.Rush));

    /// <summary>Determines whether a Unit is able to intercept an attacker.</summary>
    /// <param name="unit">The defending Unit.</param>
    /// <param name="attacker">The attacking Unit.</param>
    /// <returns><see langword="true"/> if the Unit may intercept.</returns>
    private bool CanIntercept(Unit unit, Unit attacker) => unit.LastInterceptTurn != Turn &&
        (!unit.Exhausted || unit.Card.HasKeyword(CardKeyword.Sentinel)) &&
        (!attacker.Card.HasKeyword(CardKeyword.Elusive) || unit.Card.HasKeyword(CardKeyword.Elusive));

    /// <summary>
    /// Makes two Units strike at once. Excess damage and Drain are based on each side's Health before the exchange.
    /// </summary>
    /// <param name="attacker">The attacking Unit of the active player.</param>
    /// <param name="defendingPlayer">The index of the player being attacked.</param>
    /// <param name="blocker">The Unit being attacked or intercepting.</param>
    private void ResolveUnitCombat(Unit attacker, int defendingPlayer, Unit blocker)
    {
        var blockerHealth = blocker.RemainingHealth;
        var attackerHealth = attacker.RemainingHealth;
        var dealtToBlocker = Math.Min(attacker.Power, blockerHealth);
        var dealtToAttacker = Math.Min(blocker.Power, attackerHealth);
        blocker.Damage += attacker.Power;
        attacker.Damage += blocker.Power;
        var piercedByAttacker = DamageLeader(players[defendingPlayer], PierceDamage(attacker, blockerHealth));
        var piercedByBlocker = DamageLeader(players[ActivePlayer], PierceDamage(blocker, attackerHealth));
        ApplyDrain(ActivePlayer, attacker, dealtToBlocker + piercedByAttacker);
        ApplyDrain(defendingPlayer, blocker, dealtToAttacker + piercedByBlocker);
    }

    /// <summary>Gets the damage that carries through a Unit to its player.</summary>
    /// <param name="unit">The Unit dealing damage.</param>
    /// <param name="opposingHealth">The remaining Health of the Unit it strikes.</param>
    /// <returns>The excess Power if the Unit has Pierce, otherwise 0.</returns>
    private static int PierceDamage(Unit unit, int opposingHealth) =>
        unit.Card.HasKeyword(CardKeyword.Pierce) ? Math.Max(0, unit.Power - opposingHealth) : 0;

    /// <summary>Heals a player if the Unit that dealt damage has Drain.</summary>
    /// <param name="player">The index of the Unit's controller.</param>
    /// <param name="source">The Unit that dealt the damage.</param>
    /// <param name="damageDealt">The damage actually dealt.</param>
    private void ApplyDrain(int player, Unit source, int damageDealt)
    {
        if (source.Card.HasKeyword(CardKeyword.Drain))
        {
            HealLeader(players[player], damageDealt);
        }
    }

    /// <summary>Removes Health from a player.</summary>
    /// <param name="player">The player.</param>
    /// <param name="damage">The damage dealt.</param>
    /// <returns>The Health actually lost, which is less than the damage when it is lethal.</returns>
    private static int DamageLeader(Player player, int damage)
    {
        var dealt = Math.Min(player.Health, damage);
        player.Health -= dealt;
        return dealt;
    }

    /// <summary>Restores Health, up to the starting Health. A player at 0 cannot be healed.</summary>
    /// <param name="player">The player.</param>
    /// <param name="amount">The Health to restore.</param>
    private void HealLeader(Player player, int amount)
    {
        if (player.Health > 0)
        {
            player.Health = (int)Math.Min(Format.StartingHealth, (long)player.Health + amount);
        }
    }

    /// <summary>Resolves a trigger on all of a player's Units and then Relics.</summary>
    /// <param name="player">The index of the player.</param>
    /// <param name="trigger">The trigger to resolve.</param>
    private void ResolveBoardTriggers(int player, AbilityTrigger trigger)
    {
        var owner = players[player];
        var cards = owner.Units.OfType<Unit>().Select(unit => unit.Card)
            .Concat(owner.Relics.OfType<CardDefinition>());
        var abilities = cards.SelectMany(card => card.Abilities).Where(ability => ability.Trigger == trigger).ToArray();
        ResolveAbilities(player, abilities, null);
        RemoveDefeatedUnits();
    }

    /// <summary>Applies abilities in order. An ability that needs a Unit and has none does nothing.</summary>
    /// <param name="player">The index of the controller.</param>
    /// <param name="abilities">The abilities to apply.</param>
    /// <param name="target">The chosen Unit, if any.</param>
    private void ResolveAbilities(int player, IReadOnlyList<CardAbility> abilities, UnitTarget? target)
    {
        foreach (var ability in abilities)
        {
            var owner = players[player];
            var unit = ability.Target switch
            {
                AbilityTarget.ChosenUnit or AbilityTarget.FriendlyUnit when target is UnitTarget address => GetUnit(address),
                AbilityTarget.RandomFriendlyUnit => RandomUnit(owner),
                _ => null,
            };
            switch (ability.Action)
            {
                case AbilityAction.HealLeader:
                    HealLeader(owner, ability.Amount);
                    break;
                case AbilityAction.Draw:
                    Draw(owner, ability.Amount);
                    break;
                case AbilityAction.Damage when unit is not null:
                    unit.Damage += ability.Amount;
                    break;
                case AbilityAction.Ready when unit is not null:
                    unit.Exhausted = false;
                    break;
                case AbilityAction.IncreasePower when unit is not null:
                    unit.Power += ability.Amount;
                    break;
            }
        }
    }

    /// <summary>Picks one of a player's Units at random.</summary>
    /// <param name="owner">The player.</param>
    /// <returns>A Unit, or <see langword="null"/> if the player has none.</returns>
    private Unit? RandomUnit(Player owner)
    {
        var units = owner.Units.OfType<Unit>().ToArray();
        return units.Length == 0 ? null : units[random.Next(units.Length)];
    }

    /// <summary>Rejects a target the abilities cannot use.</summary>
    /// <param name="player">The index of the controller.</param>
    /// <param name="abilities">The abilities being resolved.</param>
    /// <param name="target">The chosen Unit, if any.</param>
    /// <exception cref="InvalidOperationException">Thrown when the target is not legal.</exception>
    private void RequireLegalTarget(int player, IReadOnlyList<CardAbility> abilities, UnitTarget? target) =>
        Require(IsTargetLegal(player, abilities, target), "Choose a valid target for this effect.");

    /// <summary>Determines whether a target satisfies abilities that resolve together.</summary>
    /// <param name="player">The index of the controller.</param>
    /// <param name="abilities">The abilities being resolved.</param>
    /// <param name="target">The chosen Unit, if any.</param>
    /// <returns>
    /// <see langword="true"/> if a needed target is present and eligible, or none is needed and none is given.
    /// </returns>
    private bool IsTargetLegal(int player, IReadOnlyList<CardAbility> abilities, UnitTarget? target)
    {
        var friendly = abilities.Any(ability => ability.Target == AbilityTarget.FriendlyUnit);
        if (!friendly && !abilities.Any(ability => ability.Target == AbilityTarget.ChosenUnit))
        {
            return target is null;
        }

        return target is UnitTarget address && (uint)address.Player < players.Length &&
            (!friendly || address.Player == player) && GetUnit(address) is not null;
    }

    /// <summary>Discards destroyed Units, resolves their Last Breath abilities, and checks for a winner.</summary>
    private void RemoveDefeatedUnits()
    {
        // Remove every lethal Unit before resolving any Last Breath from that group.
        while (true)
        {
            var lastBreaths = new List<(int Player, CardAbility Ability)>();
            var destroyed = false;
            for (var side = 0; side < players.Length; side++)
            {
                var player = (ActivePlayer + side) % players.Length;
                var owner = players[player];
                for (var slot = 0; slot < owner.Units.Length; slot++)
                {
                    var unit = owner.Units[slot];
                    if (unit is null || unit.RemainingHealth > 0)
                    {
                        continue;
                    }
                    destroyed = true;
                    owner.Units[slot] = null;
                    owner.Discard.Add(unit.Card);
                    lastBreaths.AddRange(unit.Card.Abilities.Where(ability => ability.Trigger == AbilityTrigger.LastBreath)
                        .Select(ability => (player, ability)));
                }
            }
            if (!destroyed)
            {
                break;
            }
            foreach (var (player, ability) in lastBreaths)
            {
                ResolveAbilities(player, [ability], null);
            }
        }
        CheckOutcome();
    }

    /// <summary>Draws cards. A player who must draw from an empty deck loses at the next outcome check.</summary>
    /// <param name="owner">The player.</param>
    /// <param name="count">The number of cards.</param>
    private static void Draw(Player owner, int count)
    {
        for (var i = 0; i < count; i++)
        {
            if (owner.Deck.Count == 0)
            {
                owner.DeckOut = true;
                return;
            }
            owner.Hand.Add(owner.Deck[^1]);
            owner.Deck.RemoveAt(owner.Deck.Count - 1);
        }
    }

    /// <summary>Moves to a phase unless the duel has just ended.</summary>
    /// <param name="next">The phase to enter.</param>
    private void SetNextPhase(DuelPhase next)
    {
        CheckOutcome();
        if (Outcome == DuelOutcome.Ongoing)
        {
            Phase = next;
        }
    }

    /// <summary>Records the result, and finishes the duel if either player has lost.</summary>
    private void CheckOutcome()
    {
        var firstLost = players[0].HasLost;
        var secondLost = players[1].HasLost;
        Outcome = (firstLost, secondLost) switch
        {
            (true, true) => DuelOutcome.Draw,
            (true, false) => DuelOutcome.PlayerTwoWins,
            (false, true) => DuelOutcome.PlayerOneWins,
            _ => DuelOutcome.Ongoing,
        };
        if (Outcome != DuelOutcome.Ongoing)
        {
            Phase = DuelPhase.Finished;
        }
    }

    /// <summary>Spends energy, using temporary energy first because it expires at the end of the turn.</summary>
    /// <param name="owner">The player.</param>
    /// <param name="cost">The energy to spend.</param>
    /// <exception cref="InvalidOperationException">Thrown when there is not enough energy.</exception>
    private static void Pay(Player owner, int cost)
    {
        Require(owner.AvailableEnergy >= cost, "Insufficient energy.");
        var temporarySpent = Math.Min(cost, owner.TemporaryEnergy);
        owner.TemporaryEnergy -= temporarySpent;
        owner.ReadyShards -= cost - temporarySpent;
    }

    /// <summary>Gets the player who is entitled to take a turn action.</summary>
    /// <param name="player">The player index.</param>
    /// <returns>The player's state.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the duel is over or it is not the player's turn.
    /// </exception>
    private Player RequireActive(int player)
    {
        var owner = GetPlayer(player);
        Require(Outcome == DuelOutcome.Ongoing && player == ActivePlayer, "It is not that player's turn.");
        return owner;
    }

    /// <summary>Gets the active player, requiring a particular phase.</summary>
    /// <param name="player">The player index.</param>
    /// <param name="phase">The required phase.</param>
    /// <returns>The player's state.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when it is not the player's turn or the duel is in another phase.
    /// </exception>
    private Player RequirePhase(int player, DuelPhase phase)
    {
        var owner = RequireActive(player);
        Require(Phase == phase, $"This action requires the {phase} phase.");
        return owner;
    }

    /// <summary>Gets a player's state.</summary>
    /// <param name="player">The player index, 0 or 1.</param>
    /// <returns>The player's state.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is not a player.</exception>
    private Player GetPlayer(int player) => (uint)player < players.Length ? players[player]
        : throw new ArgumentOutOfRangeException(nameof(player));

    /// <summary>Gets the Unit at an address.</summary>
    /// <param name="address">The side and slot.</param>
    /// <returns>The Unit, or <see langword="null"/> if the slot is empty or does not exist.</returns>
    private Unit? GetUnit(UnitTarget address) => (uint)address.Slot < Format.UnitSlots
        ? GetPlayer(address.Player).Units[address.Slot]
        : null;

    /// <summary>Gets a card from a hand.</summary>
    /// <param name="player">The player.</param>
    /// <param name="slot">The zero-based hand slot.</param>
    /// <returns>The card.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when there is no card in the slot.</exception>
    private static CardDefinition GetHandCard(Player player, int slot) => (uint)slot < player.Hand.Count
        ? player.Hand[slot]
        : throw new ArgumentOutOfRangeException(nameof(slot));

    /// <summary>Checks the hand slots chosen for a mulligan.</summary>
    /// <param name="owner">The player.</param>
    /// <param name="handSlots">The chosen slots.</param>
    /// <returns>The slots as an array.</returns>
    /// <exception cref="ArgumentException">Thrown when the slots are invalid or exceed the mulligan limit.</exception>
    private int[] ValidateMulliganSlots(Player owner, IEnumerable<int> handSlots)
    {
        var slots = handSlots.ToArray();
        if (slots.Length > Format.MaxMulliganSwaps)
        {
            throw new ArgumentException($"You may replace at most {Format.MaxMulliganSwaps} opening cards.", nameof(handSlots));
        }
        if (slots.Distinct().Count() != slots.Length || slots.Any(slot => (uint)slot >= owner.Hand.Count))
        {
            throw new ArgumentException("Choose distinct slots from the opening hand.", nameof(handSlots));
        }
        return slots;
    }

    /// <summary>Checks the card's type, stats and costs against the supplied format.</summary>
    /// <param name="card">The card to check.</param>
    /// <param name="format">The format whose Well capacity limits costs.</param>
    /// <returns>Whether the card can be included in a deck for that format.</returns>
    public static bool IsPlayableCard(CardDefinition card, DuelFormat format) =>
        card.Type is CardType.Unit or CardType.Spell or CardType.Relic &&
        card.Cost >= 0 && card.Cost <= format.WellLimit &&
        (card.Type != CardType.Unit || (card.Power >= 0 && card.Health > 0)) &&
        card.Abilities.All(ability => ability.Amount >= 0 && ability.Cost >= 0 && ability.Cost <= format.WellLimit);

    /// <summary>Checks the deck rules and copies the deck.</summary>
    /// <param name="deck">The cards supplied by the caller.</param>
    /// <returns>A copy the duel can change freely.</returns>
    /// <exception cref="ArgumentException">Thrown when the deck breaks a rule.</exception>
    private List<CardDefinition> ValidateDeck(IEnumerable<CardDefinition> deck)
    {
        var cards = deck.ToList();
        if (cards.Count != Format.DeckSize ||
            cards.Any(card => !IsPlayableCard(card, Format)) ||
            Format.ExceedsCopyLimit(cards))
        {
            throw new ArgumentException($"A deck needs {Format.DeckSize} valid playable cards, with at most {Format.MaxCopiesPerName} of each name.",
                nameof(deck));
        }
        // Copy the ability lists so a caller cannot change the cards of a running duel.
        return [.. cards.Select(card => card with { Abilities = Array.AsReadOnly(card.Abilities.ToArray()) })];
    }

    /// <summary>Enforces a rule.</summary>
    /// <param name="condition">What must be true.</param>
    /// <param name="message">The explanation shown to the player.</param>
    /// <exception cref="InvalidOperationException">Thrown when the condition is false.</exception>
    private static void Require([DoesNotReturnIf(false)] bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    /// <summary>The mutable state of one side of the duel.</summary>
    /// <param name="leader">The player's Leader card.</param>
    /// <param name="deck">The validated deck, with its top card last.</param>
    /// <param name="format">The match format used to initialize the side.</param>
    private sealed class Player(CardDefinition leader, List<CardDefinition> deck, DuelFormat format)
    {
        /// <summary>Gets the player's Leader card.</summary>
        public CardDefinition Leader { get; } = leader;

        /// <summary>Gets the deck, with its top card last.</summary>
        public List<CardDefinition> Deck { get; } = deck;

        /// <summary>Gets the cards in hand.</summary>
        public List<CardDefinition> Hand { get; } = [];

        /// <summary>Gets the discard pile.</summary>
        public List<CardDefinition> Discard { get; } = [];

        /// <summary>Gets the cards turned into shards.</summary>
        public List<CardDefinition> Well { get; } = [];

        /// <summary>Gets the Unit slots.</summary>
        public Unit?[] Units { get; } = new Unit?[format.UnitSlots];

        /// <summary>Gets the Relic slots.</summary>
        public CardDefinition?[] Relics { get; } = new CardDefinition?[format.RelicSlots];

        /// <summary>Gets or sets the remaining Health.</summary>
        public int Health { get; set; } = format.StartingHealth;

        /// <summary>Gets or sets the shards not yet spent this turn.</summary>
        public int ReadyShards { get; set; }

        /// <summary>Gets or sets the extra energy that expires at the end of the turn.</summary>
        public int TemporaryEnergy { get; set; }

        /// <summary>Gets the energy that can be spent now.</summary>
        public int AvailableEnergy => ReadyShards + TemporaryEnergy;

        /// <summary>Gets or sets whether the Spark is still unused.</summary>
        public bool HasSpark { get; set; }

        /// <summary>Gets or sets how many times this player fed the Well this turn.</summary>
        public int WellFeedsThisTurn { get; set; }

        /// <summary>Gets or sets whether the opening hand has been chosen.</summary>
        public bool MulliganComplete { get; set; }

        /// <summary>Gets or sets whether the player had to draw from an empty deck.</summary>
        public bool DeckOut { get; set; }

        /// <summary>Gets whether the player has met a losing condition.</summary>
        public bool HasLost => Health == 0 || DeckOut;
    }

    /// <summary>The mutable state of a Unit in play.</summary>
    /// <param name="card">The card the Unit was played from.</param>
    /// <param name="arrivalTurn">The turn on which the Unit entered play.</param>
    private sealed class Unit(CardDefinition card, int arrivalTurn)
    {
        /// <summary>Gets the card the Unit was played from.</summary>
        public CardDefinition Card { get; } = card;

        /// <summary>Gets the turn on which the Unit entered play.</summary>
        public int ArrivalTurn { get; } = arrivalTurn;

        /// <summary>Gets or sets the current Power.</summary>
        public int Power { get; set; } = card.Power;

        /// <summary>Gets or sets the damage taken.</summary>
        public int Damage { get; set; }

        /// <summary>Gets the Health left after damage. Zero or less means the Unit is destroyed.</summary>
        public int RemainingHealth => Card.Health - Damage;

        /// <summary>Gets or sets whether the Unit is exhausted.</summary>
        public bool Exhausted { get; set; }

        /// <summary>Gets or sets the last turn on which the Unit attacked.</summary>
        public int LastAttackTurn { get; set; }

        /// <summary>Gets or sets the last turn on which the Unit intercepted.</summary>
        public int LastInterceptTurn { get; set; }
    }
}
