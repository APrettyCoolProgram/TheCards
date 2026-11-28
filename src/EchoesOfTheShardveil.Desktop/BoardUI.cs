// 261127_code
// 261127_documentation

using EchoesOfTheShardveil.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace EchoesOfTheShardveil.Desktop;

/// <summary>Renders the duel and routes single-player clicks to the rules engine.</summary>
/// <remarks>
/// This file holds the duel state and input. The other parts hold the layout (<c>BoardUI.Layout.cs</c>),
/// the table and cards (<c>BoardUI.Drawing.cs</c>), the panels drawn over them (<c>BoardUI.Overlays.cs</c>)
/// and the shape and text helpers (<c>BoardUI.Primitives.cs</c>).
/// </remarks>
internal sealed partial class BoardUI : IDisposable
{
    private const int Human = SinglePlayerOpponent.HumanSide;
    private const int Opponent = SinglePlayerOpponent.ComputerSide;
    private const double ComputerStepSeconds = 0.36;
    private const double HandLiftSeconds = 0.16;
    // The pointer must move this far, squared, before a press on a hand card becomes a drag.
    private const float DragThresholdSquared = 36f;

    private readonly SpriteBatch spriteBatch;
    private readonly Texture2D pixel;
    private readonly Texture2D font;
    private readonly Texture2D heroPortrait;
    private readonly Texture2D opponentPortrait;
    private readonly Texture2D heroPlaymat;
    private readonly Texture2D opponentPlaymat;
    private readonly Texture2D healthAtlas;
    private readonly Texture2D heroDropRing;
    private readonly Texture2D blankBadge;
    private readonly Texture2D[] counterDigits;
    private readonly HashSet<int> mulliganSlots = [];
    private readonly float[] handLift = new float[MatchFormats.Active.DeckSize];
    private int hoveredHand = -1;
    private CardDefinition? inspectedCard;
    private string[] inspectorRows = [];
    private string inspectorTypeLine = "";
    private string inspectorStats = "";
    private int inspectorScroll;
    private BoardTutorial tutorial;
    private readonly bool draftPlaytest;
    private readonly BoardHelp boardHelp;
    private readonly OpponentProfile opponentProfile = OpponentProfile.Current;
    private readonly IReadOnlyList<CardDefinition> playerDeck;
    private Duel duel;
    private PlayerView humanView;
    private PlayerView opponentView;
    private int? selectedHand;
    private int? selectedAttacker;
    private HandDrag? handDrag;
    private double computerDelay;
    private string message = "";
    private Point? hoverPointer;

    /// <summary>Initializes the board with its artwork and starts a duel.</summary>
    /// <param name="spriteBatch">The sprite batch to draw with.</param>
    /// <param name="pixel">A one-pixel white texture.</param>
    /// <param name="font">The bitmap font sheet.</param>
    /// <param name="heroPortrait">The framed Hero portrait.</param>
    /// <param name="healthAtlas">The numbered round badges, 0 to 20.</param>
    /// <param name="heroDropRing">The ring that highlights a portrait.</param>
    /// <param name="blankBadge">A badge without a number.</param>
    /// <param name="counterDigitSheet">The digit sheet. It is only read during construction.</param>
    /// <param name="heroPlaymat">The background of the lower half.</param>
    /// <param name="opponentPortrait">The framed Opponent portrait.</param>
    /// <param name="opponentPlaymat">The background of the upper half.</param>
    /// <param name="boardHelp">The resolved board tooltips.</param>
    /// <param name="playerDeck">The active player deck, or null to use the starter deck.</param>
    /// <param name="draftPlaytest">Whether this is a draft duel with a return to the deckbuilder.</param>
    public BoardUI(SpriteBatch spriteBatch, Texture2D pixel, Texture2D font,
        Texture2D heroPortrait, Texture2D healthAtlas, Texture2D heroDropRing, Texture2D blankBadge,
        Texture2D counterDigitSheet, Texture2D heroPlaymat, Texture2D opponentPortrait, Texture2D opponentPlaymat,
        BoardHelp boardHelp, IReadOnlyList<CardDefinition>? playerDeck = null, bool draftPlaytest = false)
    {
        this.spriteBatch = spriteBatch;
        this.boardHelp = boardHelp;
        this.pixel = pixel;
        this.font = font;
        this.heroPortrait = heroPortrait;
        this.opponentPortrait = opponentPortrait;
        this.heroPlaymat = heroPlaymat;
        this.opponentPlaymat = opponentPlaymat;
        this.healthAtlas = healthAtlas;
        this.heroDropRing = heroDropRing;
        this.blankBadge = blankBadge;
        counterDigits = CounterDigits.Build(counterDigitSheet, CounterDigitHeight);
        this.playerDeck = playerDeck ?? StarterDecks.Create();
        this.draftPlaytest = draftPlaytest;
        tutorial = new BoardTutorial(showGuide: !draftPlaytest);
        duel = NewDuel();
        humanView = duel.View(Human, Human);
        opponentView = duel.View(Opponent, Human);
    }

    /// <summary>Releases the textures the board created. Textures passed in stay with the caller.</summary>
    public void Dispose()
    {
        foreach (var digit in counterDigits)
        {
            digit.Dispose();
        }
    }

    /// <summary>Advances animations, automatic phases and the computer opponent.</summary>
    /// <param name="elapsed">The time since the previous update.</param>
    /// <param name="pointer">The pointer in board units, or <see langword="null"/> when it is off the board.</param>
    public void Update(TimeSpan elapsed, Point? pointer)
    {
        hoverPointer = pointer;
        if (tutorial.IsOpen)
        {
            return;
        }

        MoveDrag(pointer);
        UpdateHandLift(elapsed, pointer);

        while (duel.Phase is DuelPhase.Ready or DuelPhase.Draw)
        {
            duel.Advance(duel.ActivePlayer);
            RefreshViews();
        }

        var computerActs = duel.PendingAttack is null ? duel.ActivePlayer == Opponent : duel.ActivePlayer == Human;
        if (duel.Outcome != DuelOutcome.Ongoing || duel.Phase == DuelPhase.Mulligan || !computerActs)
        {
            computerDelay = 0;
            return;
        }

        computerDelay += elapsed.TotalSeconds;
        if (computerDelay < ComputerStepSeconds)
        {
            return;
        }

        computerDelay = 0;
        var update = SinglePlayerOpponent.TakeStep(duel, opponentProfile);
        if (update.Length > 0)
        {
            message = update;
        }
        RefreshViews();
    }

    /// <summary>Handles the left button going down, which may start dragging a hand card.</summary>
    /// <param name="position">The pointer in board units.</param>
    public void PointerPressed(Point position)
    {
        var slot = !tutorial.IsOpen && duel.Phase == DuelPhase.Main && duel.ActivePlayer == Human
            ? FindHandSlotAt(position) : -1;
        if (slot >= 0)
        {
            handDrag = new HandDrag(slot, position, position - PresentedHandBounds(slot).Location);
            return;
        }

        Click(position);
    }

    /// <summary>Handles the left button coming up, which drops a dragged card.</summary>
    /// <param name="position">The pointer in board units, or <see langword="null"/> when it is off the board.</param>
    public void PointerReleased(Point? position)
    {
        if (handDrag is not { } drag)
        {
            return;
        }

        MoveDrag(position);
        handDrag = null;
        if (position is not { } point)
        {
            return;
        }

        // A press that never became a drag is an ordinary click.
        if (drag.Started)
        {
            DropCard(drag, point);
        }
        else
        {
            Click(drag.Start);
        }
    }

    /// <summary>Abandons a card drag without playing the card.</summary>
    public void CancelDrag() => handDrag = null;

    /// <summary>Opens the inspector for the hand card under the pointer.</summary>
    /// <param name="position">The pointer in board units.</param>
    public void RightClick(Point position)
    {
        var slot = !tutorial.IsOpen && handDrag is null ? FindHandSlotAt(position) : -1;
        if (slot >= 0)
        {
            Inspect(humanView.Hand![slot]);
        }
    }

    /// <summary>Handles a click on the tutorial, the inspector, the buttons or the table.</summary>
    /// <param name="position">The pointer in board units.</param>
    private void Click(Point position)
    {
        if (tutorial.IsOpen)
        {
            ClickTutorial(position);
            return;
        }

        if (inspectedCard is not null && AnchorRight(InspectorPanel).Contains(position))
        {
            if (AnchorRight(InspectorClose).Contains(position))
            {
                CloseInspector();
            }
            return;
        }

        if (duel.Outcome != DuelOutcome.Ongoing)
        {
            if (AnchorBottomCenter(RematchBounds).Contains(position))
            {
                Rematch();
            }
            return;
        }

        try
        {
            ClickDuel(position);
            RefreshViews();
        }
        catch (InvalidOperationException error)
        {
            message = error.Message;
        }
    }

    /// <summary>Scrolls the inspector text when the pointer is over it.</summary>
    /// <param name="position">The pointer in board units.</param>
    /// <param name="direction">1 to scroll down, -1 to scroll up.</param>
    public void ScrollInspector(Point position, int direction)
    {
        if (!tutorial.IsOpen && inspectedCard is not null && AnchorRight(InspectorPanel).Contains(position))
        {
            inspectorScroll = Math.Clamp(inspectorScroll + direction * 2, 0,
                Math.Max(0, inspectorRows.Length - InspectorVisibleRows));
        }
    }

    /// <summary>Creates a duel against the computer, which always keeps its opening hand.</summary>
    /// <returns>The duel, waiting for the human's mulligan.</returns>
    private Duel NewDuel()
    {
        var match = MatchSetup.CreateDuel(playerDeck);
        // The computer always keeps its opening hand.
        match.Mulligan(Opponent, []);
        return match;
    }

    /// <summary>Starts over with the same deck and walkthrough preference.</summary>
    private void Rematch()
    {
        duel = NewDuel();
        tutorial = new BoardTutorial(showGuide: !draftPlaytest);
        mulliganSlots.Clear();
        ResetSelection();
        ResetHandLift();
        computerDelay = 0;
        message = "";
        RefreshViews();
    }

    /// <summary>Takes new snapshots of both sides after the duel changes.</summary>
    private void RefreshViews()
    {
        var next = duel.View(Human, Human);
        if (next.HandCount != humanView.HandCount)
        {
            ResetHandLift();
        }
        humanView = next;
        opponentView = duel.View(Opponent, Human);
    }

    /// <summary>Feeds the Well or plays the card, depending on where it was dropped.</summary>
    /// <param name="drag">The finished drag.</param>
    /// <param name="point">Where the card was dropped.</param>
    private void DropCard(HandDrag drag, Point point)
    {
        if (IsOverPortrait(Human, point))
        {
            if (humanView.CanFeedWell)
            {
                duel.FeedWell(Human, drag.Slot);
                message = "Added a face-down shard to your Well.";
                ResetSelection();
                RefreshViews();
            }
            else
            {
                message = "Your Well cannot accept another card this turn.";
            }
            return;
        }

        var card = humanView.Hand![drag.Slot];
        foreach (var play in drag.Plays)
        {
            if (!DropBounds(card, play).Contains(point))
            {
                continue;
            }

            try
            {
                duel.Play(Human, drag.Slot, play.Target, play.BoardSlot);
                message = $"You played {card.Name}.";
                ResetSelection();
                RefreshViews();
            }
            catch (InvalidOperationException error)
            {
                message = error.Message;
            }
            return;
        }
        message = drag.Plays.Count == 0 && !humanView.CanFeedWell
            ? "No legal destination for that card." : "Drop the card on a highlighted space.";
    }

    /// <summary>Follows the pointer, and turns a press into a drag once it has moved far enough.</summary>
    /// <param name="pointer">The pointer, or <see langword="null"/> when it is off the board.</param>
    private void MoveDrag(Point? pointer)
    {
        if (handDrag is not { } drag)
        {
            return;
        }

        drag.Position = pointer;
        if (!drag.Started && pointer is { } position &&
            Vector2.DistanceSquared(position.ToVector2(), drag.Start.ToVector2()) >= DragThresholdSquared)
        {
            drag.Started = true;
            drag.Plays = duel.LegalPlays(Human, drag.Slot);
            CloseInspector();
        }
    }

    /// <summary>Handles the tutorial's buttons.</summary>
    /// <param name="position">The pointer in board units.</param>
    private void ClickTutorial(Point position)
    {
        if (CenterInWindow(TutorialNext).Contains(position))
        {
            tutorial.Next();
        }
        else if (CenterInWindow(TutorialSkip).Contains(position))
        {
            tutorial.Close();
        }
        else if (tutorial.CanGoBack && CenterInWindow(TutorialBack).Contains(position))
        {
            tutorial.Back();
        }
    }

    /// <summary>Handles a click on a running duel.</summary>
    /// <param name="position">The pointer in board units.</param>
    /// <exception cref="InvalidOperationException">
    /// Not caught here; the rules engine throws it for a move it rejects.
    /// </exception>
    private void ClickDuel(Point position)
    {
        var handSlot = FindHandSlotAt(position);
        if (handSlot >= 0)
        {
            ClickHandCard(handSlot);
        }
        else if (duel.Phase == DuelPhase.Mulligan)
        {
            ClickMulligan(position);
        }
        else if (duel.PendingAttack is not null)
        {
            ClickDefense(position);
        }
        else if (duel.ActivePlayer == Human)
        {
            ClickTurn(position);
        }
    }

    /// <summary>Selects a card to exchange during setup, or to discard at the end of a turn.</summary>
    /// <param name="handSlot">The clicked hand slot.</param>
    private void ClickHandCard(int handSlot)
    {
        if (duel.Phase == DuelPhase.Mulligan)
        {
            if (mulliganSlots.Remove(handSlot))
            {
                message = "";
            }
            else if (mulliganSlots.Count < duel.Format.MaxMulliganSwaps)
            {
                mulliganSlots.Add(handSlot);
                message = "";
            }
            else
            {
                message = duel.Format.MaxMulliganSwaps == 0 ? "This format keeps the opening hand."
                    : $"You may replace up to {duel.Format.MaxMulliganSwaps} opening cards.";
            }
        }
        if (duel.ActivePlayer == Human && duel.Phase == DuelPhase.End)
        {
            selectedHand = handSlot;
            message = "";
        }
    }

    /// <summary>Confirms the opening hand if the button was clicked.</summary>
    /// <param name="position">The pointer in board units.</param>
    private void ClickMulligan(Point position)
    {
        if (AnchorBottomRight(NextButton).Contains(position))
        {
            duel.Mulligan(Human, mulliganSlots);
            message = duel.ActivePlayer == Human ? "You go first."
                : duel.Format.SecondPlayerGetsSpark ? "You go second and receive a Spark."
                : "You go second.";
            mulliganSlots.Clear();
            CloseInspector();
            ResetHandLift();
        }
    }

    /// <summary>Takes the hit or intercepts while the computer is attacking.</summary>
    /// <param name="position">The pointer in board units.</param>
    private void ClickDefense(Point position)
    {
        if (duel.ActivePlayer != Opponent)
        {
            return;
        }

        var unit = FindUnitSlotAt(Human, position);
        if (AnchorBottomRight(NextButton).Contains(position))
        {
            duel.Defend(Human);
            message = "Attack resolved.";
        }
        else if (unit >= 0 && humanView.Units[unit]?.CanIntercept == true)
        {
            var name = humanView.Units[unit]!.Card.Name;
            duel.Defend(Human, unit);
            message = $"{name} intercepted.";
        }
    }

    /// <summary>Handles the buttons of the human's own turn.</summary>
    /// <param name="position">The pointer in board units.</param>
    private void ClickTurn(Point position)
    {
        if (AnchorBottomRight(CancelButton).Contains(position))
        {
            ResetSelection();
            message = "";
            return;
        }

        switch (duel.Phase)
        {
            case DuelPhase.Main:
                if (AnchorBottomRight(NextButton).Contains(position))
                {
                    EndStep();
                }
                else if (AnchorBottomLeft(SparkButton).Contains(position) && humanView.HasSpark)
                {
                    duel.UseSpark(Human);
                    message = $"Spark added {duel.Format.SparkEnergy} temporary energy.";
                }
                break;
            case DuelPhase.Battle:
                ClickBattle(position);
                break;
            case DuelPhase.End:
                if (AnchorBottomLeft(DiscardButton).Contains(position) && selectedHand is { } discard &&
                    humanView.HandCount > duel.Format.HandLimit)
                {
                    duel.DiscardToLimit(Human, discard);
                    ResetSelection();
                }
                if (AnchorBottomRight(NextButton).Contains(position) && humanView.HandCount <= duel.Format.HandLimit)
                {
                    EndStep();
                }
                break;
        }
    }

    /// <summary>Chooses an attacker, then its target.</summary>
    /// <param name="position">The pointer in board units.</param>
    private void ClickBattle(Point position)
    {
        if (AnchorBottomRight(NextButton).Contains(position))
        {
            EndStep();
            return;
        }

        var ownSlot = FindUnitSlotAt(Human, position);
        if (ownSlot >= 0 && humanView.Units[ownSlot]?.CanAttack == true)
        {
            selectedAttacker = selectedAttacker == ownSlot ? null : ownSlot;
            CloseInspector();
            return;
        }

        if (selectedAttacker is not int attacker)
        {
            return;
        }

        var enemySlot = FindUnitSlotAt(Opponent, position);
        if (enemySlot >= 0 && opponentView.Units[enemySlot] is not null)
        {
            duel.Attack(Human, attacker, new(Opponent, enemySlot));
        }
        else if (IsOverPortrait(Opponent, position))
        {
            duel.Attack(Human, attacker);
        }
        else
        {
            return;
        }
        message = $"{humanView.Units[attacker]!.Card.Name} attacked.";
        ResetSelection();
    }

    /// <summary>Advances the human's turn and clears the selection.</summary>
    private void EndStep()
    {
        duel.Advance(Human);
        ResetSelection();
    }

    /// <summary>Opens the inspector for a card.</summary>
    /// <param name="card">The card.</param>
    private void Inspect(CardDefinition card)
    {
        inspectedCard = card;
        inspectorRows = [.. CardRulesText.Describe(card).SelectMany(rule => BitmapText.Wrap(rule, 42))];
        inspectorScroll = 0;
        inspectorTypeLine = $"{CardRulesText.TypeLine(card)}   |   {card.Cost} energy";
        inspectorStats = card.Type == CardType.Unit ? $"{card.Power} Power / {card.Health} Health" : "";
        if (card.Aspect is { Length: > 0 } aspect)
        {
            inspectorStats = inspectorStats.Length > 0 ? $"{inspectorStats}   |   Aspect: {aspect}" : $"Aspect: {aspect}";
        }
    }

    /// <summary>Closes the inspector.</summary>
    private void CloseInspector() => inspectedCard = null;

    /// <summary>Clears the selected cards, any drag and the inspector.</summary>
    private void ResetSelection()
    {
        selectedHand = null;
        selectedAttacker = null;
        CancelDrag();
        CloseInspector();
    }

    /// <summary>Lowers every hand card.</summary>
    private void ResetHandLift()
    {
        Array.Clear(handLift);
        hoveredHand = -1;
    }

    /// <summary>Raises the hovered hand card and lowers the others.</summary>
    /// <param name="elapsed">The time since the previous update.</param>
    /// <param name="pointer">The pointer, or <see langword="null"/> when it is off the board.</param>
    private void UpdateHandLift(TimeSpan elapsed, Point? pointer)
    {
        hoveredHand = handDrag is not { Started: true } && pointer is { } position ? FindHandSlotAt(position) : -1;
        var step = (float)Math.Min(elapsed.TotalSeconds / HandLiftSeconds, 1);
        for (var i = 0; i < humanView.HandCount; i++)
        {
            handLift[i] = i == hoveredHand
                ? Math.Min(1f, handLift[i] + step)
                : Math.Max(0f, handLift[i] - step);
        }
    }

    /// <summary>A number shown beside or on a portrait, with its hover help.</summary>
    /// <param name="Bounds">Where it is drawn.</param>
    /// <param name="Value">The number.</param>
    /// <param name="Tooltip">The help shown on hover.</param>
    private readonly record struct HudCounter(Rectangle Bounds, int Value, Tooltip Tooltip);

    /// <summary>A press on a hand card that may turn into a drag.</summary>
    /// <param name="slot">The hand slot of the card.</param>
    /// <param name="start">Where the button went down.</param>
    /// <param name="offset">The press position relative to the card's corner.</param>
    private sealed class HandDrag(int slot, Point start, Point offset)
    {
        /// <summary>Gets the hand slot of the card.</summary>
        public int Slot { get; } = slot;

        /// <summary>Gets where the button went down.</summary>
        public Point Start { get; } = start;

        /// <summary>Gets the press position relative to the card's corner.</summary>
        public Point Offset { get; } = offset;

        /// <summary>Gets or sets the current pointer, or <see langword="null"/> when it is off the board.</summary>
        public Point? Position { get; set; }

        /// <summary>Gets or sets whether the pointer has moved far enough to count as a drag.</summary>
        public bool Started { get; set; }

        /// <summary>Gets or sets the places the card can be dropped.</summary>
        public IReadOnlyList<CardPlay> Plays { get; set; } = [];
    }
}
