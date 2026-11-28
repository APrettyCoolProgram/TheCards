// 261127_code
// 261127_documentation

using Microsoft.Xna.Framework;

namespace EchoesOfTheShardveil.Desktop;

// Shapes and text drawn from the one-pixel texture and the bitmap font.
internal sealed partial class BoardUI
{
    /// <summary>Draws a button.</summary>
    /// <param name="bounds">The button bounds.</param>
    /// <param name="label">The text.</param>
    /// <param name="enabled">Whether to draw it as available.</param>
    /// <param name="quiet">Whether a button that is not highlighted keeps bright text.</param>
    private void DrawButton(Rectangle bounds, ReadOnlySpan<char> label, bool enabled, bool quiet = false)
    {
        DrawBox(bounds, enabled ? new Color(43, 83, 88) : Panel, enabled ? Teal : Border);
        DrawCenteredText(label, bounds.Center.X, bounds.Y + 8, 21, enabled || quiet ? Ivory : Muted);
    }

    /// <summary>Draws a rounded box with a one-pixel edge.</summary>
    /// <param name="bounds">The outer bounds.</param>
    /// <param name="fill">The inside color.</param>
    /// <param name="edge">The edge color.</param>
    private void DrawBox(Rectangle bounds, Color fill, Color edge)
    {
        FillRoundedRectangle(bounds, edge);
        bounds.Inflate(-1, -1);
        FillRoundedRectangle(bounds, fill);
    }

    /// <summary>Fills a rectangle with small rounded corners.</summary>
    /// <param name="bounds">The area.</param>
    /// <param name="color">The color.</param>
    private void FillRoundedRectangle(Rectangle bounds, Color color)
    {
        FillRectangle(new(bounds.X + 4, bounds.Y, bounds.Width - 8, 1), color);
        FillRectangle(new(bounds.X + 2, bounds.Y + 1, bounds.Width - 4, 1), color);
        FillRectangle(new(bounds.X + 1, bounds.Y + 2, bounds.Width - 2, 1), color);
        FillRectangle(new(bounds.X, bounds.Y + 3, bounds.Width, bounds.Height - 6), color);
        FillRectangle(new(bounds.X + 1, bounds.Bottom - 3, bounds.Width - 2, 1), color);
        FillRectangle(new(bounds.X + 2, bounds.Bottom - 2, bounds.Width - 4, 1), color);
        FillRectangle(new(bounds.X + 4, bounds.Bottom - 1, bounds.Width - 8, 1), color);
    }

    /// <summary>Draws a two-pixel outline.</summary>
    /// <param name="bounds">The outlined area.</param>
    /// <param name="color">The color.</param>
    private void DrawOutline(Rectangle bounds, Color color)
    {
        FillRectangle(new(bounds.Left, bounds.Top, bounds.Width, 2), color);
        FillRectangle(new(bounds.Left, bounds.Bottom - 2, bounds.Width, 2), color);
        FillRectangle(new(bounds.Left, bounds.Top, 2, bounds.Height), color);
        FillRectangle(new(bounds.Right - 2, bounds.Top, 2, bounds.Height), color);
    }

    /// <summary>Draws a one-pixel dashed border.</summary>
    /// <param name="bounds">The bordered area.</param>
    /// <param name="color">The color.</param>
    private void DrawDashedBorder(Rectangle bounds, Color color)
    {
        const int step = 12;
        const int length = 6;
        const int inset = 6;
        for (var x = bounds.Left + inset; x < bounds.Right - inset; x += step)
        {
            var width = Math.Min(length, bounds.Right - inset - x);
            FillRectangle(new(x, bounds.Top, width, 1), color);
            FillRectangle(new(x, bounds.Bottom - 1, width, 1), color);
        }
        for (var y = bounds.Top + inset; y < bounds.Bottom - inset; y += step)
        {
            var height = Math.Min(length, bounds.Bottom - inset - y);
            FillRectangle(new(bounds.Left, y, 1, height), color);
            FillRectangle(new(bounds.Right - 1, y, 1, height), color);
        }
    }

    /// <summary>Fills a rectangle.</summary>
    /// <param name="bounds">The area.</param>
    /// <param name="color">The color.</param>
    private void FillRectangle(Rectangle bounds, Color color) => spriteBatch.Draw(pixel, bounds, color);

    /// <summary>Draws text from its upper-left corner.</summary>
    /// <param name="text">The text.</param>
    /// <param name="x">The left edge.</param>
    /// <param name="y">The top edge.</param>
    /// <param name="height">The text height.</param>
    /// <param name="color">The color.</param>
    private void DrawText(ReadOnlySpan<char> text, int x, int y, int height, Color color) =>
        BitmapText.Draw(spriteBatch, font, text, x, y, height, color);

    /// <summary>Draws text centered horizontally.</summary>
    /// <param name="text">The text.</param>
    /// <param name="centerX">The horizontal center.</param>
    /// <param name="y">The top edge.</param>
    /// <param name="height">The text height.</param>
    /// <param name="color">The color.</param>
    private void DrawCenteredText(ReadOnlySpan<char> text, int centerX, int y, int height, Color color) =>
        BitmapText.DrawCentered(spriteBatch, font, text, centerX, y, height, color);
}
