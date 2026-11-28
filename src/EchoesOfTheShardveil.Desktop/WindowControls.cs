// 261127_code
// 261127_documentation

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace EchoesOfTheShardveil.Desktop;

/// <summary>What the window controls did with a click.</summary>
internal enum WindowControlResult
{
    /// <summary>The click was not on a control, so the screen may use it.</summary>
    Ignored,
    /// <summary>The controls used the click.</summary>
    Handled,
    /// <summary>The player confirmed leaving.</summary>
    ConfirmedExit,
}

/// <summary>Draws the upper-right controls and handles their modal exit confirmation.</summary>
/// <param name="audio">The sound preference the speaker icon toggles.</param>
/// <param name="isBoard">Whether leaving forfeits a duel rather than closing the application.</param>
/// <param name="returnDestination">The screen named in the forfeit prompt, or null for the main menu.</param>
internal sealed class WindowControls(GameAudio audio, bool isBoard, string? returnDestination = null)
{
    // Icon paths are authored in a 44-unit square and drawn at three quarters of that size.
    private const int IconDesignSize = 44;
    private const int IconSize = IconDesignSize * 3 / 4;
    private const int IconMargin = 20;
    private const int IconGap = 10;
    private const float IconScale = (float)IconSize / IconDesignSize;
    private readonly string forfeitMessage = $"You will return to {returnDestination ?? "the main menu"}.";

    private static readonly Color Border = new(82, 113, 120);
    private static readonly Color Panel = new(23, 38, 46);
    private static readonly Vector2[] Ring = [.. Enumerable.Range(0, 41)
        .Select(i => new Vector2(
            22f + 20f * MathF.Cos(i * MathHelper.TwoPi / 40),
            22f + 20f * MathF.Sin(i * MathHelper.TwoPi / 40)))];
    private static readonly Vector2[][] ExitMark =
    [
        [new(25, 12), new(12, 12), new(12, 32), new(25, 32)],
        [new(19, 22), new(33, 22)],
        [new(28, 17), new(33, 22), new(28, 27)],
    ];
    private static readonly Vector2[][] SpeakerMark =
    [
        [new(11, 18), new(16, 18), new(23, 12), new(23, 32), new(16, 26), new(11, 26), new(11, 18)],
    ];
    private static readonly Vector2[][] SoundWaves =
    [
        [new(28, 17), new(30, 22), new(28, 27)],
        [new(32, 14), new(35, 19), new(35, 25), new(32, 30)],
    ];
    private static readonly Vector2[][] MuteMark =
    [
        [new(28, 18), new(35, 25)],
        [new(35, 18), new(28, 25)],
    ];

    /// <summary>Gets whether the exit confirmation is open.</summary>
    public bool IsConfirming { get; private set; }

    /// <summary>Opens the exit confirmation, or closes it if it is open.</summary>
    public void ToggleExitPrompt() => IsConfirming = !IsConfirming;

    /// <summary>Opens the exit confirmation.</summary>
    public void RequestExitConfirmation() => IsConfirming = true;

    /// <summary>Handles a left click.</summary>
    /// <param name="mouse">The mouse position in client units.</param>
    /// <param name="client">The window's client size in mouse units.</param>
    /// <param name="viewport">The window viewport.</param>
    /// <returns>What the controls did with the click.</returns>
    public WindowControlResult Click(Point mouse, Rectangle client, Viewport viewport)
    {
        if (client.Width <= 0 || client.Height <= 0 || viewport.Width <= 0 || viewport.Height <= 0)
        {
            return IsConfirming ? WindowControlResult.Handled : WindowControlResult.Ignored;
        }

        var layout = new Layout(viewport);
        var pixel = ScreenLayout.ToViewportPixels(mouse, client, viewport);
        var pointer = new Point((int)(pixel.X / layout.Scale), (int)(pixel.Y / layout.Scale));
        if (IsConfirming)
        {
            if (layout.Confirm.Contains(pointer))
            {
                IsConfirming = false;
                return WindowControlResult.ConfirmedExit;
            }
            if (layout.Cancel.Contains(pointer))
            {
                IsConfirming = false;
            }
            return WindowControlResult.Handled;
        }

        if (IsOverIcon(layout.Exit, pointer))
        {
            RequestExitConfirmation();
            return WindowControlResult.Handled;
        }
        if (IsOverIcon(layout.Sound, pointer))
        {
            audio.Toggle();
            return WindowControlResult.Handled;
        }
        return WindowControlResult.Ignored;
    }

    /// <summary>Draws the icons and, when open, the confirmation dialog.</summary>
    /// <param name="spriteBatch">A sprite batch that has not begun.</param>
    /// <param name="pixel">A one-pixel white texture.</param>
    /// <param name="font">The bitmap font sheet.</param>
    /// <param name="viewport">The window viewport.</param>
    public void Draw(SpriteBatch spriteBatch, Texture2D pixel, Texture2D font, Viewport viewport)
    {
        if (viewport.Width <= 0 || viewport.Height <= 0)
        {
            return;
        }

        var layout = new Layout(viewport);
        spriteBatch.Begin(samplerState: SamplerState.LinearClamp,
            transformMatrix: Matrix.CreateScale(layout.Scale, layout.Scale, 1));
        DrawIcon(spriteBatch, pixel, layout.Exit, 1f / layout.Scale, ExitMark);
        DrawIcon(spriteBatch, pixel, layout.Sound, 1f / layout.Scale, SpeakerMark,
            audio.Enabled ? SoundWaves : MuteMark);
        if (IsConfirming)
        {
            spriteBatch.Draw(pixel, new Rectangle(0, 0, layout.Width, layout.Height), Color.Black * 0.72f);
            DrawBox(spriteBatch, pixel, layout.Dialog, Panel);
            var left = layout.Dialog.Left + 30;
            BitmapText.Draw(spriteBatch, font, isBoard ? "Forfeit the game?" : "Exit Echoes of the Shardveil?",
                left, layout.Dialog.Top + 23, 32, UiColors.Ivory);
            BitmapText.Draw(spriteBatch, font,
                isBoard ? forfeitMessage : "The application will close.",
                left, layout.Dialog.Top + 69, 19, new Color(177, 192, 196));
            DrawButton(spriteBatch, pixel, font, layout.Cancel, "Cancel", Panel);
            DrawButton(spriteBatch, pixel, font, layout.Confirm, isBoard ? "Forfeit & return" : "Exit",
                new Color(117, 57, 61));
        }
        spriteBatch.End();
    }

    /// <summary>
    /// Hit tests a round icon. Its size is odd, so this uses the exact center rather than whole pixels.
    /// </summary>
    /// <param name="bounds">The icon bounds.</param>
    /// <param name="pointer">The pointer in layout units.</param>
    /// <returns><see langword="true"/> if the pointer is on the icon.</returns>
    private static bool IsOverIcon(Rectangle bounds, Point pointer)
    {
        var radius = bounds.Width / 2f;
        var delta = new Vector2(pointer.X - bounds.X - radius, pointer.Y - bounds.Y - radius);
        return delta.LengthSquared() <= radius * radius;
    }

    /// <summary>Draws a round icon: its background, ring and strokes.</summary>
    /// <param name="spriteBatch">A sprite batch that has already begun.</param>
    /// <param name="pixel">A one-pixel white texture.</param>
    /// <param name="bounds">The icon bounds.</param>
    /// <param name="ringWidth">The thickness of the ring.</param>
    /// <param name="mark">The strokes of the symbol.</param>
    /// <param name="badge">Additional strokes, if any.</param>
    private static void DrawIcon(SpriteBatch spriteBatch, Texture2D pixel, Rectangle bounds, float ringWidth,
        Vector2[][] mark, Vector2[][]? badge = null)
    {
        FillIconBackground(spriteBatch, pixel, bounds);
        var origin = new Vector2(bounds.X, bounds.Y);
        DrawPath(spriteBatch, pixel, Ring, origin, ringWidth);
        DrawPaths(spriteBatch, pixel, mark, origin);
        if (badge is not null)
        {
            DrawPaths(spriteBatch, pixel, badge, origin);
        }
    }

    /// <summary>Draws several strokes of an icon.</summary>
    /// <param name="spriteBatch">A sprite batch that has already begun.</param>
    /// <param name="pixel">A one-pixel white texture.</param>
    /// <param name="paths">The strokes.</param>
    /// <param name="origin">The icon's upper-left corner.</param>
    private static void DrawPaths(SpriteBatch spriteBatch, Texture2D pixel, Vector2[][] paths, Vector2 origin)
    {
        foreach (var path in paths)
        {
            DrawPath(spriteBatch, pixel, path, origin, 1.8f * IconScale);
        }
    }

    /// <summary>Draws one stroke as connected line segments.</summary>
    /// <param name="spriteBatch">A sprite batch that has already begun.</param>
    /// <param name="pixel">A one-pixel white texture.</param>
    /// <param name="path">The points in the icon's design square.</param>
    /// <param name="origin">The icon's upper-left corner.</param>
    /// <param name="width">The line thickness.</param>
    private static void DrawPath(SpriteBatch spriteBatch, Texture2D pixel, Vector2[] path, Vector2 origin, float width)
    {
        for (var i = 1; i < path.Length; i++)
        {
            var delta = (path[i] - path[i - 1]) * IconScale;
            spriteBatch.Draw(pixel, origin + path[i - 1] * IconScale, null, UiColors.Ivory,
                MathF.Atan2(delta.Y, delta.X), new Vector2(0f, 0.5f),
                new Vector2(delta.Length() + width * 0.40f, width), SpriteEffects.None, 0f);
        }
    }

    /// <summary>Fills the black disc behind an icon.</summary>
    /// <param name="spriteBatch">A sprite batch that has already begun.</param>
    /// <param name="pixel">A one-pixel white texture.</param>
    /// <param name="bounds">The icon bounds.</param>
    private static void FillIconBackground(SpriteBatch spriteBatch, Texture2D pixel, Rectangle bounds)
    {
        var center = bounds.Width / 2f;
        var radius = 20f * IconScale;
        for (var y = 0; y < bounds.Height; y++)
        {
            var distance = y + 0.5f - center;
            if (MathF.Abs(distance) >= radius)
            {
                continue;
            }

            var halfSpan = MathF.Sqrt(radius * radius - distance * distance);
            var left = (int)MathF.Round(center - halfSpan);
            var right = (int)MathF.Round(center + halfSpan);
            spriteBatch.Draw(pixel, new Rectangle(bounds.X + left, bounds.Y + y, right - left, 1), Color.Black);
        }
    }

    /// <summary>Draws a dialog box with a one-pixel border.</summary>
    /// <param name="spriteBatch">A sprite batch that has already begun.</param>
    /// <param name="pixel">A one-pixel white texture.</param>
    /// <param name="bounds">The box bounds.</param>
    /// <param name="fill">The inside color.</param>
    private static void DrawBox(SpriteBatch spriteBatch, Texture2D pixel, Rectangle bounds, Color fill)
    {
        spriteBatch.Draw(pixel, bounds, Border);
        bounds.Inflate(-1, -1);
        spriteBatch.Draw(pixel, bounds, fill);
    }

    /// <summary>Draws a dialog button.</summary>
    /// <param name="spriteBatch">A sprite batch that has already begun.</param>
    /// <param name="pixel">A one-pixel white texture.</param>
    /// <param name="font">The bitmap font sheet.</param>
    /// <param name="bounds">The button bounds.</param>
    /// <param name="label">The text.</param>
    /// <param name="fill">The button color.</param>
    private static void DrawButton(SpriteBatch spriteBatch, Texture2D pixel, Texture2D font,
        Rectangle bounds, string label, Color fill)
    {
        DrawBox(spriteBatch, pixel, bounds, fill);
        const int height = 20;
        BitmapText.DrawCentered(spriteBatch, font, label, bounds.Center.X, bounds.Center.Y - height / 2, height,
            UiColors.Ivory);
    }

    /// <summary>Positions the controls in units scaled from the board's design size.</summary>
    private readonly record struct Layout
    {
        /// <summary>Initializes the layout for a viewport.</summary>
        /// <param name="viewport">The window viewport, which must not be empty.</param>
        public Layout(Viewport viewport)
        {
            Scale = Math.Min(viewport.Width / (float)BoardUI.Width, viewport.Height / (float)BoardUI.Height);
            Width = (int)MathF.Ceiling(viewport.Width / Scale);
            Height = (int)MathF.Ceiling(viewport.Height / Scale);
        }

        /// <summary>Gets the pixels per layout unit.</summary>
        public float Scale { get; }

        /// <summary>Gets the window width in layout units.</summary>
        public int Width { get; }

        /// <summary>Gets the window height in layout units.</summary>
        public int Height { get; }

        /// <summary>Gets the exit icon bounds.</summary>
        public Rectangle Exit => new(Width - IconMargin - IconSize, IconMargin, IconSize, IconSize);

        /// <summary>Gets the sound icon bounds.</summary>
        public Rectangle Sound => new(Exit.X, Exit.Bottom + IconGap, IconSize, IconSize);

        /// <summary>Gets the confirmation dialog bounds.</summary>
        public Rectangle Dialog => new((Width - 500) / 2, (Height - 172) / 2, 500, 172);

        /// <summary>Gets the dialog's cancel button bounds.</summary>
        public Rectangle Cancel => new(Dialog.Right - 324, Dialog.Bottom - 58, 108, 37);

        /// <summary>Gets the dialog's confirm button bounds.</summary>
        public Rectangle Confirm => new(Dialog.Right - 203, Dialog.Bottom - 58, 176, 37);
    }
}
