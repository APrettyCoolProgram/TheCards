// 261127_code
// 261127_documentation

using EchoesOfTheShardveil.Core;
using Microsoft.Xna.Framework;

namespace EchoesOfTheShardveil.Desktop;

// Positions of everything on the board, and the hit tests that use them. Static fields that depend on one
// another must stay in this file, because initialization order across partial class files is undefined.
internal sealed partial class BoardUI
{
    /// <summary>The board's design width.</summary>
    public const int Width = 1280;

    /// <summary>The board's design height.</summary>
    public const int Height = 720;

    /// <summary>The diameter of the round Hero and Opponent portraits.</summary>
    public const int HeroPortraitSize = 176;

    /// <summary>The diameter of the round number badges on a portrait.</summary>
    public const int BadgeSize = 36;

    private const int CardWidth = 95;
    private const int UnitRowLeft = 265;
    private const int RelicRowLeft = 1008;
    private const int UnitRowWidth = RelicRowLeft - UnitRowLeft - 18;
    private const int RelicRowWidth = Width - RelicRowLeft - 24;
    private const int MinimumSlotWidth = 55;
    private const int SlotGap = 6;
    // Card-shaped counters (Removed and Discard on the left, Deck on the right) flank the Hero portrait.
    // Both dimensions are odd so a digit with an odd size sits exactly in the middle (25 x 35 is 5:7 like a card).
    private const int CounterWidth = 25;
    private const int CounterHeight = 35;
    private const int CounterDigitHeight = 15;
    private const int CounterGap = 14;
    private const int CounterEdge = 24;
    // The portrait is round, so it tucks in until its rim is 5 pixels from the nearest corner of the
    // Removed and Discard counters. Deck mirrors that on the right.
    private const int PortraitClearance = 5;
    private static readonly int PortraitLeft = ComputePortraitLeft();
    private static readonly int DeckCounterLeft = 2 * (PortraitLeft + HeroPortraitSize / 2) - (CounterEdge + CounterWidth);
    private static readonly int HandAreaLeft = DeckCounterLeft + CounterWidth + CounterGap;
    private const int HandAreaRight = Width - 24;

    // Rectangles are in design coordinates. The Anchor methods move them when the window is larger.
    private static readonly Rectangle RematchButton = new(510, 502, 260, 40);
    private static readonly Rectangle DraftRematchButton = new(375, 502, 250, 40);
    private static readonly Rectangle DraftReturnButton = new(655, 502, 250, 40);
    private static readonly Rectangle SparkButton = new(328, 505, 124, 38);
    private static readonly Rectangle DiscardButton = new(460, 505, 125, 38);
    private static readonly Rectangle NextButton = new(1018, 505, 234, 38);
    private static readonly Rectangle CancelButton = new(858, 505, 152, 38);

    private static readonly Rectangle TutorialPanel = new(230, 118, 820, 484);
    private static readonly Rectangle TutorialBack = new(266, 528, 112, 40);
    private static readonly Rectangle TutorialSkip = new(648, 528, 151, 40);
    private static readonly Rectangle TutorialNext = new(813, 528, 201, 40);

    private static readonly Rectangle InspectorPanel = new(610, 82, 580, 258);
    private static readonly Rectangle InspectorClose = new(1090, 90, 84, 29);

    private int layoutHeight = Height;
    private int layoutWidth = Width;

    /// <summary>Gets the Play again button, which moves aside for a draft playtest.</summary>
    private Rectangle RematchBounds => draftPlaytest ? DraftRematchButton : RematchButton;

    /// <summary>Checks whether a finished draft playtest's return button was clicked.</summary>
    /// <param name="position">The pointer in board units.</param>
    /// <returns>Whether to return directly to the deckbuilder.</returns>
    public bool IsDraftReturn(Point position) => draftPlaytest && duel.Outcome != DuelOutcome.Ongoing &&
        AnchorBottomCenter(DraftReturnButton).Contains(position);

    /// <summary>Gets how much taller the layout is than the design.</summary>
    private int ExtraHeight => layoutHeight - Height;
    /// <summary>Gets how much wider the layout is than the design.</summary>
    private int ExtraWidth => layoutWidth - Width;
    /// <summary>Gets how far the tutorial moves down to stay centered.</summary>
    private int TutorialOffset => ExtraHeight / 2;

    /// <summary>Adapts the layout to the window's shape.</summary>
    /// <param name="viewport">The mapping for the current window.</param>
    public void SetLayout(BoardViewport viewport)
    {
        layoutHeight = viewport.Height;
        layoutWidth = viewport.Width;
    }

    /// <summary>Finds the leftmost portrait position that keeps the round rim clear of the counters.</summary>
    /// <returns>The portrait's left edge.</returns>
    private static int ComputePortraitLeft()
    {
        // Removed's lower inner corner is (HeroPortraitSize / 2 - CounterHeight) above the portrait's center line.
        var radius = HeroPortraitSize / 2;
        var vertical = radius - CounterHeight;
        var horizontal = (int)MathF.Ceiling(MathF.Sqrt((radius + PortraitClearance) * (radius + PortraitClearance)
            - vertical * vertical));
        return CounterEdge + CounterWidth + horizontal - radius;
    }

    /// <summary>Returns a moved copy of a rectangle.</summary>
    /// <param name="bounds">The rectangle.</param>
    /// <param name="x">The horizontal movement.</param>
    /// <param name="y">The vertical movement.</param>
    /// <returns>The moved rectangle.</returns>
    private static Rectangle Offset(Rectangle bounds, int x, int y)
    {
        bounds.Offset(x, y);
        return bounds;
    }

    /// <summary>Keeps a design rectangle at the same distance from the bottom-left corner.</summary>
    /// <param name="bounds">The design rectangle.</param>
    /// <returns>The rectangle in the current layout.</returns>
    private Rectangle AnchorBottomLeft(Rectangle bounds) => Offset(bounds, 0, ExtraHeight);

    /// <summary>Keeps a design rectangle centered and at the same distance from the bottom.</summary>
    /// <param name="bounds">The design rectangle.</param>
    /// <returns>The rectangle in the current layout.</returns>
    private Rectangle AnchorBottomCenter(Rectangle bounds) => Offset(bounds, ExtraWidth / 2, ExtraHeight);

    /// <summary>Keeps a design rectangle at the same distance from the bottom-right corner.</summary>
    /// <param name="bounds">The design rectangle.</param>
    /// <returns>The rectangle in the current layout.</returns>
    private Rectangle AnchorBottomRight(Rectangle bounds) => Offset(bounds, ExtraWidth, ExtraHeight);

    /// <summary>Keeps a design rectangle at the same distance from the top-right corner.</summary>
    /// <param name="bounds">The design rectangle.</param>
    /// <returns>The rectangle in the current layout.</returns>
    private Rectangle AnchorRight(Rectangle bounds) => Offset(bounds, ExtraWidth, 0);

    /// <summary>Keeps a design rectangle centered in the window.</summary>
    /// <param name="bounds">The design rectangle.</param>
    /// <returns>The rectangle in the current layout.</returns>
    private Rectangle CenterInWindow(Rectangle bounds) => Offset(bounds, ExtraWidth / 2, TutorialOffset);

    /// <summary>Gets a band across the whole layout.</summary>
    /// <param name="y">The top edge.</param>
    /// <param name="height">The height.</param>
    /// <returns>The band.</returns>
    private Rectangle FullWidthBand(int y, int height) => new(0, y, layoutWidth, height);

    /// <summary>Gets the top of a player's row of slots.</summary>
    /// <param name="player">The player index.</param>
    /// <returns>The vertical position.</returns>
    private int RowTop(int player) => player == Human
        ? 352 + ExtraHeight * 3 / 4
        : 122 + ExtraHeight / 4;

    /// <summary>
    /// Gets where a portrait sits. The Hero's is low in the lower-left corner with its Health badge inside the window.
    /// </summary>
    /// <param name="player">The player index.</param>
    /// <returns>The portrait bounds.</returns>
    private Rectangle PortraitBounds(int player)
    {
        var top = player == Human ? layoutHeight - HeroPortraitSize - BadgeSize / 2 - 10 : BadgeSize / 2 + 10;
        return new(PortraitLeft, top, HeroPortraitSize, HeroPortraitSize);
    }

    /// <summary>Hit tests a round portrait.</summary>
    /// <param name="player">The player index.</param>
    /// <param name="position">The pointer in board units.</param>
    /// <returns><see langword="true"/> if the pointer is on the portrait.</returns>
    private bool IsOverPortrait(int player, Point position) => Geometry.IsInCircle(PortraitBounds(player), position);

    /// <summary>Gets a rectangle with the 5:7 shape of a card.</summary>
    /// <param name="x">The left edge.</param>
    /// <param name="y">The top edge.</param>
    /// <param name="width">The width, ideally a multiple of 5.</param>
    /// <returns>The card bounds.</returns>
    private static Rectangle CardBounds(int x, int y, int width = CardWidth) =>
        new(x, y, width, CardHeight(width));

    /// <summary>Gets the height that gives a width the 5:7 shape of a card.</summary>
    /// <param name="width">The card width.</param>
    /// <returns>The card height.</returns>
    private static int CardHeight(int width) => width / 5 * 7;

    /// <summary>Rounds a card width down to a multiple of 5, so its 5:7 height is exact.</summary>
    /// <param name="width">The available width.</param>
    /// <returns>The rounded width, at least 5.</returns>
    private static int SnapCardWidth(int width) => Math.Max(5, width / 5 * 5);

    /// <summary>Checks that the configured rows fit within the board's layout at a readable size.</summary>
    /// <param name="format">The match format.</param>
    /// <exception cref="InvalidDataException">The format has more slots than a row can show.</exception>
    public static void ValidateSlotLayout(DuelFormat format)
    {
        const int maxUnits = (UnitRowWidth + SlotGap) / (MinimumSlotWidth + SlotGap);
        const int maxRelics = (RelicRowWidth + SlotGap) / (MinimumSlotWidth + SlotGap);
        if (format.UnitSlots > maxUnits || format.RelicSlots > maxRelics)
        {
            throw new InvalidDataException($"The current board layout fits up to {maxUnits} Unit and {maxRelics} Relic slots per side.");
        }
    }

    /// <summary>Gets the position of a Unit slot.</summary>
    /// <param name="player">The player index.</param>
    /// <param name="slot">The Unit slot.</param>
    /// <returns>The slot bounds.</returns>
    private Rectangle UnitBounds(int player, int slot) =>
        SlotBounds(UnitRowLeft + ExtraWidth * (slot + 1) / (duel.Format.UnitSlots + 1),
            RowTop(player), UnitRowWidth, 150, duel.Format.UnitSlots, slot);

    /// <summary>Gets the position of a Relic slot.</summary>
    /// <param name="player">The player index.</param>
    /// <param name="slot">The Relic slot.</param>
    /// <returns>The slot bounds.</returns>
    private Rectangle RelicBounds(int player, int slot) =>
        SlotBounds(RelicRowLeft + ExtraWidth, RowTop(player), RelicRowWidth, 131, duel.Format.RelicSlots, slot);

    /// <summary>Fits cards into a row, preserving its preferred spacing when there is room.</summary>
    /// <param name="left">The row's left edge.</param>
    /// <param name="top">The row's top edge.</param>
    /// <param name="width">The space available to the row.</param>
    /// <param name="preferredStep">The usual distance between cards.</param>
    /// <param name="count">The number of slots in the row.</param>
    /// <param name="slot">The slot to position.</param>
    /// <returns>The card bounds for that slot.</returns>
    private static Rectangle SlotBounds(int left, int top, int width, int preferredStep, int count, int slot)
    {
        var cardWidth = Math.Min(CardWidth, (width - SlotGap * (count - 1)) / count / 5 * 5);
        var step = Math.Min(preferredStep, (width - cardWidth) / Math.Max(1, count - 1));
        return CardBounds(left + slot * step, top, cardWidth);
    }

    /// <summary>Gets where a hand card rests: partly below the bottom edge, shrinking as the hand grows.</summary>
    /// <param name="slot">The hand slot.</param>
    /// <param name="count">The number of cards in the hand.</param>
    /// <returns>The resting bounds.</returns>
    private Rectangle HandBounds(int slot, int count)
    {
        const int spacing = 8;
        // Cards are centered in the space to the right of the Hero portrait.
        var available = HandAreaRight + ExtraWidth - HandAreaLeft;
        var width = Math.Min(CardWidth, (available - spacing * (count - 1)) / Math.Max(1, count));
        width = SnapCardWidth(width);
        var total = width * count + spacing * (count - 1);
        return CardBounds(HandAreaLeft + (available - total) / 2 + (width + spacing) * slot,
            layoutHeight - CardHeight(width) / 2, width);
    }

    /// <summary>Gets where a hand card is drawn, including how far hovering has raised it.</summary>
    /// <param name="slot">The hand slot.</param>
    /// <returns>The drawn bounds.</returns>
    private Rectangle PresentedHandBounds(int slot)
    {
        var bounds = HandBounds(slot, humanView.HandCount);
        var lift = MathHelper.SmoothStep(0f, 1f, handLift[slot]);
        bounds.Offset(0, -(int)MathF.Round((bounds.Height / 2f + 14f) * lift));
        return bounds;
    }

    /// <summary>Gets the area that accepts a dragged card for one of its legal plays.</summary>
    /// <param name="card">The dragged card.</param>
    /// <param name="play">The play.</param>
    /// <returns>The drop area.</returns>
    private Rectangle DropBounds(CardDefinition card, CardPlay play)
    {
        if (play.Target is { } target)
        {
            return UnitBounds(target.Player, target.Slot);
        }
        return card.Type switch
        {
            CardType.Unit => UnitBounds(Human, play.BoardSlot!.Value),
            CardType.Relic => RelicBounds(Human, play.BoardSlot!.Value),
            _ => new Rectangle(1026 + ExtraWidth, 283 + ExtraHeight / 2, 210, 46),
        };
    }

    /// <summary>
    /// Finds the hand card under the pointer. The resting area counts too, so a raised card does not drop away.
    /// </summary>
    /// <param name="position">The pointer in board units.</param>
    /// <returns>The hand slot, or -1.</returns>
    private int FindHandSlotAt(Point position)
    {
        for (var i = 0; i < humanView.HandCount; i++)
        {
            if (Rectangle.Union(HandBounds(i, humanView.HandCount), PresentedHandBounds(i)).Contains(position))
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>Finds the Unit slot under the pointer.</summary>
    /// <param name="player">The player index.</param>
    /// <param name="position">The pointer in board units.</param>
    /// <returns>The Unit slot, or -1.</returns>
    private int FindUnitSlotAt(int player, Point position)
    {
        for (var i = 0; i < duel.Format.UnitSlots; i++)
        {
            if (UnitBounds(player, i).Contains(position))
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>Returns a badge centered on the portrait's rim at a clock position (12 is the top).</summary>
    /// <param name="portrait">The portrait bounds.</param>
    /// <param name="hour">The clock position.</param>
    /// <returns>The badge bounds.</returns>
    private static Rectangle RimBadgeBounds(Rectangle portrait, float hour)
    {
        var angle = hour / 12f * MathHelper.TwoPi;
        var radius = HeroPortraitSize / 2f;
        var x = portrait.Center.X + (int)MathF.Round(radius * MathF.Sin(angle));
        var y = portrait.Center.Y - (int)MathF.Round(radius * MathF.Cos(angle));
        return new(x - BadgeSize / 2, y - BadgeSize / 2, BadgeSize, BadgeSize);
    }
}
