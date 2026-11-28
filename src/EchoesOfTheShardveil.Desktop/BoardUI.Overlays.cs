// 261127_code
// 261127_documentation

using EchoesOfTheShardveil.Core;
using Microsoft.Xna.Framework;

namespace EchoesOfTheShardveil.Desktop;

// Draws the panels that cover the table: the tutorial, the card inspector and the tooltips.
internal sealed partial class BoardUI
{
    private const int InspectorVisibleRows = 6;

    private static readonly Color OverlayFill = new(19, 34, 43);
    private static readonly Color OverlayEdge = new(73, 110, 120);

    /// <summary>Draws help for the badge or counter under the pointer.</summary>
    private void DrawBadgeTooltip()
    {
        if (handDrag is { Started: true } || hoverPointer is not { } pointer)
        {
            return;
        }

        var player = pointer.Y < layoutHeight / 2 ? Opponent : Human;
        var view = player == Human ? humanView : opponentView;
        var portrait = PortraitBounds(player);
        var (well, health) = PortraitBadges(player, view);
        var (removed, discard, deckCounter) = CardCounters(portrait, view);
        HudCounter? hovered =
            Geometry.IsInCircle(well.Bounds, pointer) ? well
            : Geometry.IsInCircle(health.Bounds, pointer) ? health
            : removed.Bounds.Contains(pointer) ? removed
            : discard.Bounds.Contains(pointer) ? discard
            : deckCounter.Bounds.Contains(pointer) ? deckCounter
            : null;
        if (hovered is not { Bounds: var badge, Tooltip: var (heading, body) })
        {
            return;
        }

        var deck = deckCounter.Bounds;

        const int padding = 12;
        const int bodySize = 16;
        var maxLines = Math.Max(1, (layoutHeight - 2 * padding - 36) / (bodySize + 4));
        var lines = BitmapText.Wrap(body, 34, maxLines);
        heading = BitmapText.Truncate(heading, 34);
        var longest = heading.Length;
        foreach (var line in lines)
        {
            longest = Math.Max(longest, line.Length);
        }
        var width = 2 * padding + BitmapText.Advance(bodySize) * longest;
        var height = 2 * padding + 20 + lines.Length * (bodySize + 4);
        // Show it beside the portrait, level with the badge, so it never covers the portrait.
        var y = Math.Clamp(badge.Center.Y - height / 2, 8, layoutHeight - height - 8);
        var bounds = new Rectangle(Math.Max(badge.Right, portrait.Right) + 10, y, width, height);
        if (bounds.Intersects(deck) && badge != deck)
        {
            // Keep the tooltip from covering the Deck counter.
            bounds.X = deck.Right + 10;
        }
        DrawBox(bounds, OverlayFill, OverlayEdge);
        DrawText(heading, bounds.X + padding, bounds.Y + padding, 15, Teal);
        for (var i = 0; i < lines.Length; i++)
        {
            DrawText(lines[i], bounds.X + padding, bounds.Y + padding + 20 + i * (bodySize + 4), bodySize, Ivory);
        }
    }

    /// <summary>Draws the tutorial prompt or the current lesson over a dimmed table.</summary>
    private void DrawTutorial()
    {
        FillRectangle(FullWidthBand(0, layoutHeight), new Color(0, 0, 0) * 0.75f);
        DrawBox(CenterInWindow(TutorialPanel), OverlayFill, OverlayEdge);
        var left = CenterInWindow(TutorialPanel).X + 36;
        if (tutorial.IsPrompt && BoardTutorial.Prompt is { } prompt)
        {
            DrawText(prompt.Label, left, 161 + TutorialOffset, 21, Teal);
            DrawText(prompt.Title, left, 207 + TutorialOffset, 43, Ivory);
            DrawText(prompt.Description, left, 280 + TutorialOffset, 23, Muted);
            DrawText(prompt.SkipHint, left, 325 + TutorialOffset, 21, Muted);
            DrawButton(CenterInWindow(TutorialSkip), prompt.SkipButton, false, quiet: true);
            DrawButton(CenterInWindow(TutorialNext), prompt.StartButton, true);
            return;
        }

        var lesson = BoardTutorial.Pages[tutorial.PageIndex];
        Span<char> text = stackalloc char[64];
        DrawText(BitmapText.Format(text, $"HOW TO PLAY  /  {tutorial.PageIndex + 1} OF {BoardTutorial.Pages.Count}"), left,
            151 + TutorialOffset, 19, Teal);
        DrawText(lesson.Title, left, 191 + TutorialOffset, 37, Ivory);
        var pointsX = left;
        var lineWidth = 64;
        if (lesson.Illustration != TutorialIllustration.None)
        {
            // Make room for the picture on the left.
            pointsX += 186;
            lineWidth = 48;
        }
        switch (lesson.Illustration)
        {
            case TutorialIllustration.Board:
                DrawTutorialBoard(left, 254 + TutorialOffset);
                break;
            case TutorialIllustration.Card when lesson.ExampleCard is { } card:
                var example = CardBounds(left + 12, 245 + TutorialOffset, 135);
                DrawCard(example, card, false);
                DrawCenteredText(BitmapText.Format(text, $"EXAMPLE {CardRulesText.CapitalTypeName(card.Type)}"),
                    example.Center.X, example.Bottom + 8, 14, Muted);
                break;
        }
        DrawTutorialPoints(lesson.Points, pointsX, lineWidth);

        var progressY = 503 + TutorialOffset;
        var progressStep = Math.Min(30, 390 / BoardTutorial.Pages.Count);
        if (progressStep >= 2)
        {
            for (var i = 0; i < BoardTutorial.Pages.Count; i++)
            {
                FillRectangle(new(left + i * progressStep, progressY, Math.Min(23, progressStep - 1), 3),
                    i <= tutorial.PageIndex ? Teal : Border);
            }
        }
        if (tutorial.CanGoBack)
        {
            DrawButton(CenterInWindow(TutorialBack), "Back", false, quiet: true);
        }
        DrawButton(CenterInWindow(TutorialSkip), "Skip guide", false, quiet: true);
        DrawButton(CenterInWindow(TutorialNext), tutorial.IsLastPage ? "Start duel" : "Next", true);
    }

    /// <summary>Draws a lesson's bulleted paragraphs.</summary>
    /// <param name="points">The paragraphs.</param>
    /// <param name="left">The left edge.</param>
    /// <param name="lineWidth">The most characters on a line.</param>
    private void DrawTutorialPoints(IReadOnlyList<string> points, int left, int lineWidth)
    {
        var y = 250 + TutorialOffset;
        for (var i = 0; i < points.Count; i++)
        {
            var point = points[i];
            FillRectangle(new(left + 1, y + 8, 5, 5), Teal);
            foreach (var line in BitmapText.Wrap(point, lineWidth, 5))
            {
                DrawText(line, left + 18, y, 21, Ivory);
                y += 23;
            }
            y += 10;
        }
    }

    /// <summary>Draws a miniature of the board: each side's portrait and slots, then the actions and hand.</summary>
    /// <param name="left">The left edge.</param>
    /// <param name="top">The top edge.</param>
    private void DrawTutorialBoard(int left, int top)
    {
        const int width = 160;
        DrawBox(new(left, top, width, 199), Recess, Border);
        DrawCenteredText("EXIT / SOUND", left + width / 2, top + 9, 14, Muted);
        for (var side = 0; side < 2; side++)
        {
            var y = top + 36 + side * 77;
            var accent = side == 0 ? Rose : Teal;
            DrawText(side == 0 ? "OPPONENT" : "YOU", left + 11, y, 14, accent);
            FillRectangle(new(left + 12, y + 19, 19, 24), accent);
            var units = duel.Format.UnitSlots;
            var slots = units + duel.Format.RelicSlots;
            var step = Math.Min(15, 114 / Math.Max(1, slots));
            for (var slot = 0; slot < slots; slot++)
            {
                var x = left + 40 + slot * step + (slot >= units ? 3 : 0);
                FillRectangle(new(x, y + 23, Math.Min(11, step - 1), 16), slot < units ? Border : Gold * 0.65f);
            }
        }
        DrawCenteredText("ACTIONS", left + width / 2, top + 159, 14, Muted);
        DrawCenteredText("YOUR HAND", left + width / 2, top + 177, 14, Teal);
    }

    /// <summary>Draws the details of a card.</summary>
    /// <param name="card">The card being inspected.</param>
    private void DrawInspector(CardDefinition card)
    {
        DrawBox(AnchorRight(InspectorPanel), OverlayFill, OverlayEdge);
        DrawText("CARD DETAILS", 631 + ExtraWidth, 94, 15, Teal);
        DrawButton(AnchorRight(InspectorClose), "Close", false, quiet: true);
        var preview = CardBounds(628 + ExtraWidth, 121, 140);
        DrawCard(preview, card, true, enlarged: true);

        var left = 789 + ExtraWidth;
        DrawText(card.Name, left, 107, 30, Ivory);
        DrawText(inspectorTypeLine, left, 144, 18, Teal);
        DrawText(inspectorStats, left, 166, 17, Muted);

        if (inspectorRows.Length == 0)
        {
            DrawText("No keyword or ability text.", left, 205, 18, Muted);
        }
        for (var i = 0; i < InspectorVisibleRows && i + inspectorScroll < inspectorRows.Length; i++)
        {
            DrawText(inspectorRows[i + inspectorScroll], left, 190 + i * 20, 18, Ivory);
        }
        if (inspectorRows.Length > InspectorVisibleRows)
        {
            DrawText("Scroll for more", left, 313, 14, Muted);
        }
    }
}
