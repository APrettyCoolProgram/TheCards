// 261127_code
// 261127_documentation

using System.Globalization;
using EchoesOfTheShardveil.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace EchoesOfTheShardveil.Desktop;

// Draws the table: playmats, portraits and counters, board slots, the hand, cards and action buttons.
internal sealed partial class BoardUI
{
    // The badge atlas is a grid of numbered badges from 0 to 20, whatever the format's starting Health.
    private const int HealthAtlasColumns = 6;
    private const int HealthAtlasCount = 21;

    /// <summary>The color behind the board.</summary>
    public static readonly Color BackgroundColor = new(12, 21, 27);

    private static readonly Color Ivory = UiColors.Ivory;
    private static readonly Color Muted = new(163, 176, 179);
    private static readonly Color Teal = new(101, 208, 189);
    private static readonly Color Gold = new(235, 184, 107);
    private static readonly Color Rose = new(233, 145, 146);
    private static readonly Color Panel = new(25, 39, 45);
    private static readonly Color Border = new(57, 82, 90);
    private static readonly Color Recess = new(14, 25, 31);

    /// <summary>Draws the whole board.</summary>
    /// <param name="view">The mapping for the current window.</param>
    /// <param name="rasterizer">A rasterizer state with scissoring enabled.</param>
    public void Draw(BoardViewport view, RasterizerState rasterizer)
    {
        SetLayout(view);
        spriteBatch.Begin(samplerState: SamplerState.LinearClamp, rasterizerState: rasterizer,
            transformMatrix: view.Transform);
        FillRectangle(FullWidthBand(0, layoutHeight), BackgroundColor);
        var split = layoutHeight / 2 - 22;
        DrawPlaymat(opponentPlaymat, FullWidthBand(0, split), verticalAnchor: 0.5f);
        DrawPlaymat(heroPlaymat, FullWidthBand(split, layoutHeight - split), verticalAnchor: 1f);
        DrawSide(Opponent, opponentView);
        DrawSide(Human, humanView);
        DrawStatus();
        DrawHand();
        DrawActions();
        DrawDrag();
        if (tutorial.IsOpen)
        {
            DrawTutorial();
        }
        else
        {
            if (inspectedCard is { } card)
            {
                DrawInspector(card);
            }
            DrawBadgeTooltip();
        }
        spriteBatch.End();
    }

    /// <summary>Draws a playmat, cropped to the shape of its area.</summary>
    /// <param name="texture">The artwork.</param>
    /// <param name="bounds">The area to fill.</param>
    /// <param name="verticalAnchor">
    /// Which part survives a vertical crop, from 0 for the top to 1 for the bottom.
    /// </param>
    private void DrawPlaymat(Texture2D texture, Rectangle bounds, float verticalAnchor)
    {
        var scale = Math.Max((float)bounds.Width / texture.Width, (float)bounds.Height / texture.Height);
        var width = Math.Clamp((int)MathF.Round(bounds.Width / scale), 1, texture.Width);
        var height = Math.Clamp((int)MathF.Round(bounds.Height / scale), 1, texture.Height);
        var source = new Rectangle((texture.Width - width) / 2,
            (int)MathF.Round((texture.Height - height) * verticalAnchor), width, height);
        spriteBatch.Draw(texture, bounds, source, Color.White);
    }

    /// <summary>Draws one player's portrait, counters and board slots.</summary>
    /// <param name="player">The player index.</param>
    /// <param name="view">The player's snapshot.</param>
    private void DrawSide(int player, PlayerView view)
    {
        var portrait = PortraitBounds(player);
        var attackTarget = player == Opponent && selectedAttacker is not null;
        spriteBatch.Draw(player == Human ? heroPortrait : opponentPortrait, portrait, Color.White);
        if (attackTarget || (player == Human && humanView.CanFeedWell && handDrag is { Started: true, Position: not null }))
        {
            var ring = portrait;
            ring.Inflate(3, 3);
            var hovered = handDrag?.Position is { } pointer && IsOverPortrait(player, pointer);
            spriteBatch.Draw(heroDropRing, ring, attackTarget || hovered ? Gold : Teal);
        }

        var (well, health) = PortraitBadges(player, view);
        DrawBadge(well.Bounds, well.Value);
        DrawBadge(health.Bounds, health.Value);
        var (removed, discard, deck) = CardCounters(portrait, view);
        DrawCardCounter(removed.Bounds, removed.Value);
        DrawCardCounter(discard.Bounds, discard.Value);
        DrawCardCounter(deck.Bounds, deck.Value);

        var statusY = player == Human ? RowTop(player) + 86 : portrait.Bottom + 24;
        if (view.HasSpark)
        {
            DrawText("SPARK READY", 28, statusY, 12, Gold);
        }
        Span<char> text = stackalloc char[32];
        if (view.TemporaryEnergy > 0)
        {
            DrawText(BitmapText.Format(text, $"+{view.TemporaryEnergy} ENERGY"), 28, statusY + 18, 12, Gold);
        }

        for (var i = 0; i < view.Units.Count; i++)
        {
            var bounds = UnitBounds(player, i);
            if (view.Units[i] is { } unit)
            {
                var isAttacker = player == Human && selectedAttacker == i;
                DrawCard(bounds, unit.Card, isAttacker || attackTarget, unit);
            }
            else
            {
                DrawEmptySlot(bounds, BitmapText.Format(text, $"UNIT {i + 1}"));
            }
        }

        for (var i = 0; i < view.Relics.Count; i++)
        {
            var bounds = RelicBounds(player, i);
            if (view.Relics[i] is { } relic)
            {
                DrawCard(bounds, relic, false);
            }
            else
            {
                DrawEmptySlot(bounds, BitmapText.Format(text, $"RELIC {i + 1}"));
            }
        }
    }

    /// <summary>Gets the Well and Health badges on a portrait.</summary>
    /// <param name="player">The player index.</param>
    /// <param name="view">The player's snapshot.</param>
    /// <returns>Each badge's position, number and help.</returns>
    private (HudCounter Well, HudCounter Health) PortraitBadges(int player, PlayerView view)
    {
        var portrait = PortraitBounds(player);
        return (
            new(RimBadgeBounds(portrait, 12), view.Shards, player == Human ? boardHelp.Well : boardHelp.OpponentWell),
            new(RimBadgeBounds(portrait, 6), view.Health, player == Human ? boardHelp.Health : boardHelp.OpponentHealth));
    }

    /// <summary>Gets the Removed, Discard and Deck counters. Removed is not in the rules yet and shows zero.</summary>
    /// <param name="portrait">The portrait the counters flank.</param>
    /// <param name="view">The player's snapshot.</param>
    /// <returns>Each counter's position, number and help.</returns>
    private (HudCounter Removed, HudCounter Discard, HudCounter Deck) CardCounters(Rectangle portrait, PlayerView view)
    {
        var bottom = portrait.Bottom - CounterHeight;
        return (
            new(new(CounterEdge, portrait.Top, CounterWidth, CounterHeight), 0, boardHelp.Removed),
            new(new(CounterEdge, bottom, CounterWidth, CounterHeight), view.Discard.Count, boardHelp.Discard),
            new(new(DeckCounterLeft, bottom, CounterWidth, CounterHeight), view.DeckCount, boardHelp.Deck));
    }

    /// <summary>Draws a card-shaped counter with a centered number.</summary>
    /// <param name="bounds">The counter bounds.</param>
    /// <param name="value">The number.</param>
    private void DrawCardCounter(Rectangle bounds, int value)
    {
        DrawBox(bounds, Color.Black, Color.White);
        const int gap = 1; // With odd digit widths this keeps the whole number's width odd.
        Span<char> label = stackalloc char[11];
        Math.Max(0, value).TryFormat(label, out var length, provider: CultureInfo.InvariantCulture);
        var total = gap * (length - 1);
        foreach (var character in label[..length])
        {
            total += counterDigits[character - '0'].Width;
        }
        var x = bounds.X + (bounds.Width - total) / 2;
        var y = bounds.Y + (bounds.Height - CounterDigitHeight) / 2;
        foreach (var character in label[..length])
        {
            var digit = counterDigits[character - '0'];
            spriteBatch.Draw(digit, new Vector2(x, y), Color.White);
            x += digit.Width + gap;
        }
    }

    /// <summary>Draws a number from the round badge atlas shared by the portrait counters.</summary>
    /// <param name="bounds">The badge bounds.</param>
    /// <param name="value">The number to show.</param>
    private void DrawBadge(Rectangle bounds, int value)
    {
        if (value >= HealthAtlasCount)
        {
            // The atlas stops at 20, so larger numbers are drawn as text on a blank badge.
            spriteBatch.Draw(blankBadge, bounds, Color.White);
            Span<char> label = stackalloc char[11];
            value.TryFormat(label, out var length, provider: CultureInfo.InvariantCulture);
            DrawCenteredText(label[..length], bounds.Center.X, bounds.Center.Y - 9, 18, Color.White);
            return;
        }

        var index = Math.Max(0, value);
        var cellSize = healthAtlas.Width / HealthAtlasColumns;
        var tile = new Rectangle(index % HealthAtlasColumns * cellSize, index / HealthAtlasColumns * cellSize,
            cellSize, cellSize);
        spriteBatch.Draw(healthAtlas, bounds, tile, Color.White);
    }

    /// <summary>Draws the result, the pending attack or the latest message.</summary>
    private void DrawStatus()
    {
        scoped ReadOnlySpan<char> status = duel.Outcome switch
        {
            DuelOutcome.PlayerOneWins => "You win.",
            DuelOutcome.PlayerTwoWins => "Opponent wins.",
            DuelOutcome.Draw => "The duel is a draw.",
            _ => message,
        };
        Span<char> text = stackalloc char[256];
        if (duel.Outcome == DuelOutcome.Ongoing && duel.PendingAttack is { } attack && duel.ActivePlayer == Opponent)
        {
            var defender = attack.Target is { } unit ? humanView.Units[unit.Slot]!.Card.Name : "your Hero";
            status = BitmapText.Format(text, $"Attack on {defender}.");
        }
        if (status.Length > 0)
        {
            DrawCenteredText(status, layoutWidth / 2, layoutHeight - 172, 16, Ivory);
        }
    }

    /// <summary>
    /// Draws the hand, with the hovered card on top. A dragged card is drawn by <see cref="DrawDrag"/>.
    /// </summary>
    private void DrawHand()
    {
        for (var i = 0; i < humanView.HandCount; i++)
        {
            if (i != hoveredHand && !(handDrag is { Started: true } drag && drag.Slot == i))
            {
                DrawHandCard(i);
            }
        }
        if (hoveredHand >= 0)
        {
            DrawHandCard(hoveredHand);
        }
    }

    /// <summary>Draws one card of the hand, with a shadow when raised.</summary>
    /// <param name="slot">The hand slot.</param>
    private void DrawHandCard(int slot)
    {
        var bounds = PresentedHandBounds(slot);
        if (handLift[slot] > 0)
        {
            var shadow = bounds;
            shadow.Offset(0, 5);
            shadow.Inflate(2, 2);
            FillRoundedRectangle(shadow, Color.Black * (0.48f * handLift[slot]));
        }
        DrawCard(bounds, humanView.Hand![slot], slot == selectedHand || mulliganSlots.Contains(slot));
    }

    /// <summary>Highlights where the dragged card can go and draws it under the pointer.</summary>
    private void DrawDrag()
    {
        if (handDrag is not { Started: true, Position: { } position } drag)
        {
            return;
        }

        var card = humanView.Hand![drag.Slot];
        // Indexing avoids the enumerator that foreach would create each frame.
        for (var i = 0; i < drag.Plays.Count; i++)
        {
            var play = drag.Plays[i];
            var bounds = DropBounds(card, play);
            var highlight = bounds.Contains(position) ? Gold : Teal;
            if (card.Type == CardType.Spell && play.Target is null)
            {
                DrawBox(bounds, Panel, highlight);
                DrawCenteredText("CAST SPELL", bounds.Center.X, bounds.Y + 12, 19, Ivory);
            }
            else
            {
                DrawOutline(bounds, highlight);
            }
        }

        // The dragged card shrinks slightly, keeping the grabbed point under the pointer.
        var handWidth = HandBounds(drag.Slot, humanView.HandCount).Width;
        var previewWidth = SnapCardWidth((int)(handWidth * 0.8f));
        var scale = (float)previewWidth / handWidth;
        var location = position.ToVector2() - drag.Offset.ToVector2() * scale;
        DrawCard(CardBounds((int)location.X, (int)location.Y, previewWidth), card, true);
    }

    /// <summary>Draws the buttons that apply to the current step.</summary>
    private void DrawActions()
    {
        if (duel.Outcome != DuelOutcome.Ongoing)
        {
            DrawButton(AnchorBottomCenter(RematchBounds), "Play again", true);
            if (draftPlaytest)
            {
                DrawButton(AnchorBottomCenter(DraftReturnButton), "Back to deckbuilder", true);
            }
            return;
        }

        if (duel.Phase == DuelPhase.Mulligan)
        {
            Span<char> text = stackalloc char[32];
            DrawButton(AnchorBottomRight(NextButton),
                mulliganSlots.Count == 0 ? "Keep hand" : BitmapText.Format(text, $"Replace {mulliganSlots.Count} cards"),
                true);
            return;
        }

        if (duel.PendingAttack is not null)
        {
            if (duel.ActivePlayer == Opponent)
            {
                DrawButton(AnchorBottomRight(NextButton), "Take the hit", true);
            }
            return;
        }

        if (duel.ActivePlayer != Human)
        {
            return;
        }

        switch (duel.Phase)
        {
            case DuelPhase.Main:
                DrawButton(AnchorBottomLeft(SparkButton), "Use Spark", humanView.HasSpark);
                DrawButton(AnchorBottomRight(NextButton), "Go to battle", true);
                break;
            case DuelPhase.Battle:
                DrawButton(AnchorBottomRight(NextButton), "End battle", true);
                break;
            case DuelPhase.End:
                DrawButton(AnchorBottomLeft(DiscardButton), "Discard",
                    selectedHand is not null && humanView.HandCount > duel.Format.HandLimit);
                DrawButton(AnchorBottomRight(NextButton), "End turn", humanView.HandCount <= duel.Format.HandLimit);
                break;
        }
        if (selectedAttacker is not null || selectedHand is not null)
        {
            DrawButton(AnchorBottomRight(CancelButton), "Cancel selection", true);
        }
    }

    /// <summary>Draws a card face.</summary>
    /// <param name="bounds">The card bounds.</param>
    /// <param name="card">The card.</param>
    /// <param name="selected">Whether to highlight it.</param>
    /// <param name="unit">The Unit in play, or <see langword="null"/> to show printed values.</param>
    /// <param name="enlarged">Whether to allow larger text.</param>
    private void DrawCard(Rectangle bounds, CardDefinition card, bool selected, UnitView? unit = null, bool enlarged = false)
    {
        var accent = card.Type switch
        {
            CardType.Unit => Teal,
            CardType.Spell => Rose,
            _ => Gold,
        };
        var ready = unit is { CanAttack: true } or { CanIntercept: true };
        var fill = unit is { Exhausted: true } ? new Color(33, 41, 44) : new Color(30, 49, 54);
        DrawBox(bounds, fill, selected ? Gold : ready ? Teal : Border);
        var inset = Math.Max(4, bounds.Width / 12);
        var nameFont = Math.Clamp(bounds.Width / 5, 10, enlarged ? 29 : 18);
        var detailFont = Math.Clamp(bounds.Width / 7, 9, enlarged ? 20 : 14);
        var interior = bounds.Width - 2 * inset;
        FillRectangle(new(bounds.X + inset, bounds.Y + 6, interior, 2), selected ? Gold : accent);
        Span<char> text = stackalloc char[32];
        DrawText(BitmapText.Format(text, $"{CardRulesText.CapitalTypeName(card.Type)} {card.Cost}"), bounds.X + inset,
            bounds.Y + 11, detailFont, accent);
        var nameRows = BitmapText.Wrap(card.Name, Math.Max(5, interior / BitmapText.Advance(nameFont)), 2);
        for (var i = 0; i < nameRows.Length; i++)
        {
            DrawText(nameRows[i], bounds.X + inset, bounds.Y + 13 + detailFont + i * nameFont, nameFont, Ivory);
        }

        var descriptionRows = BitmapText.Wrap(CardRulesText.Summarize(card),
            Math.Max(5, interior / BitmapText.Advance(detailFont)), 2);
        for (var i = 0; i < descriptionRows.Length; i++)
        {
            var y = bounds.Y + 15 + detailFont + nameFont * 2 + i * detailFont;
            if (y + detailFont <= bounds.Bottom - detailFont - 7)
            {
                DrawText(descriptionRows[i], bounds.X + inset, y, detailFont, Muted);
            }
        }

        if (card.Type == CardType.Unit)
        {
            DrawText(BitmapText.Format(text, $"{unit?.Power ?? card.Power}/{card.Health - (unit?.Damage ?? 0)}"),
                bounds.X + inset, bounds.Bottom - nameFont - 5, nameFont, Ivory);
        }
        if (ready || unit is { Exhausted: true })
        {
            var label = ready ? unit!.CanIntercept ? "BLOCK" : "ATTACK" : "USED";
            var labelSize = Math.Max(9, detailFont - 1);
            DrawText(label, bounds.Right - inset - BitmapText.Advance(labelSize) * label.Length,
                bounds.Bottom - labelSize - 6, labelSize, ready ? Teal : Muted);
        }
    }

    /// <summary>Draws an empty board slot.</summary>
    /// <param name="bounds">The slot bounds.</param>
    /// <param name="label">The slot's name.</param>
    private void DrawEmptySlot(Rectangle bounds, ReadOnlySpan<char> label)
    {
        FillRoundedRectangle(bounds, Recess);
        DrawDashedBorder(bounds, new Color(72, 104, 112));
        var fontHeight = Math.Min(16, 2 * (bounds.Width - 6) / label.Length);
        DrawCenteredText(label, bounds.Center.X, bounds.Center.Y - fontHeight / 2 - 1, fontHeight, Muted);
    }
}
