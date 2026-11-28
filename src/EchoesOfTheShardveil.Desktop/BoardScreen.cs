// 261127_code
// 261127_documentation

using EchoesOfTheShardveil.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace EchoesOfTheShardveil.Desktop;

/// <summary>Hosts one single-player duel. A new screen is created for each visit from the menu.</summary>
internal sealed class BoardScreen : IScreen
{
    private readonly GameServices services;
    private readonly bool draftPlaytest;
    private readonly WindowControls controls;
    // Everything this screen creates, so that one path releases it after a duel or a failed load.
    private readonly List<IDisposable> resources = [];
    private readonly RasterizerState boardRasterizer;
    private readonly BoardUI board;
    private readonly InputTracker input = new();

    /// <summary>Loads the board artwork and starts a duel.</summary>
    /// <param name="services">The shared graphics and audio.</param>
    /// <param name="playerDeck">The active player deck, or null to use the starter deck.</param>
    /// <param name="draftPlaytest">Whether to return to the deckbuilder after this duel.</param>
    public BoardScreen(GameServices services, IReadOnlyList<CardDefinition>? playerDeck = null, bool draftPlaytest = false)
    {
        this.services = services;
        this.draftPlaytest = draftPlaytest;
        var device = services.Device;
        var match = MatchSetup.Current;
        var help = BoardHelp.Current;
        CardRulesText.LoadCatalogs();
        BoardUI.ValidateSlotLayout(MatchFormats.Active);
        controls = new WindowControls(services.Audio, isBoard: true,
            returnDestination: draftPlaytest ? "the deckbuilder" : null);
        try
        {
            boardRasterizer = Own(new RasterizerState { ScissorTestEnable = true });
            var heroPortrait = Own(LoadFramedPortrait(device, match.Player.Portrait));
            var heroPlaymat = Own(LoadMatchTexture(device, match.Player.Playmat));
            var healthAtlas = Own(EmbeddedAssets.LoadPremultipliedTexture(device, "Board-Hero-Health.png"));
            var opponentPortrait = Own(LoadFramedPortrait(device, match.Opponent.Portrait));
            var opponentPlaymat = Own(LoadMatchTexture(device, match.Opponent.Playmat));
            var heroDropRing = Own(CircleTexture.Create(device, BoardUI.HeroPortraitSize + 6, borderWidth: 3,
                Color.Transparent));
            var blankBadge = Own(CircleTexture.Create(device, BoardUI.BadgeSize, borderWidth: 1, Color.Black));
            using var digitSheet = EmbeddedAssets.LoadTexture(device, "Counter-Digits.png");
            board = Own(new BoardUI(services.SpriteBatch, services.Pixel, services.Font, heroPortrait, healthAtlas,
                heroDropRing, blankBadge, digitSheet, heroPlaymat, opponentPortrait, opponentPlaymat, help, playerDeck,
                draftPlaytest));
        }
        catch
        {
            // The caller never receives a half-built screen, so it cannot dispose it.
            Dispose();
            throw;
        }
    }

    /// <inheritdoc />
    public string Title => draftPlaytest ? "Echoes of the Shardveil - Draft playtest"
        : "Echoes of the Shardveil - Single player";

    /// <inheritdoc />
    public ScreenTransition Update(GameTime gameTime, Rectangle client, bool isActive)
    {
        var frame = input.Poll();
        if (!isActive)
        {
            board.CancelDrag();
            return ScreenTransition.None;
        }

        var viewport = services.Device.Viewport;
        var boardViewport = new BoardViewport(viewport);
        board.SetLayout(boardViewport);

        var action = WindowControlResult.Ignored;
        if (frame.EscapePressed)
        {
            controls.ToggleExitPrompt();
            action = WindowControlResult.Handled;
        }
        else if (frame.LeftPressed)
        {
            action = controls.Click(frame.Pointer, client, viewport);
        }

        if (action == WindowControlResult.ConfirmedExit)
        {
            return draftPlaytest ? ScreenTransition.OpenDeckbuilder : ScreenTransition.ReturnToMenu;
        }
        if (controls.IsConfirming)
        {
            board.CancelDrag();
            return ScreenTransition.None;
        }

        return UpdateBoard(gameTime.ElapsedGameTime, frame, boardViewport, client, action);
    }

    /// <inheritdoc />
    public void RequestClose() => controls.RequestExitConfirmation();

    /// <inheritdoc />
    public void Draw(GameTime gameTime)
    {
        var device = services.Device;
        device.Clear(BoardUI.BackgroundColor);
        var viewport = device.Viewport;
        var boardViewport = new BoardViewport(viewport);
        if (boardViewport.IsDrawable)
        {
            var previousScissor = device.ScissorRectangle;
            var previousRasterizer = device.RasterizerState;
            device.ScissorRectangle = viewport.Bounds;
            try
            {
                board.Draw(boardViewport, boardRasterizer);
            }
            finally
            {
                device.RasterizerState = previousRasterizer;
                device.ScissorRectangle = previousScissor;
            }
        }
        controls.Draw(services.SpriteBatch, services.Pixel, services.Font, viewport);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // Newest first, so the board is released before the textures it draws.
        for (var i = resources.Count - 1; i >= 0; i--)
        {
            resources[i].Dispose();
        }
        resources.Clear();
    }

    /// <summary>Records something this screen must release.</summary>
    /// <typeparam name="T">The kind of resource.</typeparam>
    /// <param name="resource">The newly created resource.</param>
    /// <returns>The same resource.</returns>
    private T Own<T>(T resource) where T : IDisposable
    {
        resources.Add(resource);
        return resource;
    }

    /// <summary>Loads a portrait and frames it in a circle.</summary>
    /// <param name="device">The graphics device.</param>
    /// <param name="reference">The AppData image path or embedded resource reference.</param>
    /// <returns>A texture the caller must dispose.</returns>
    /// <exception cref="InvalidDataException">The reference does not name a readable, square image.</exception>
    /// <exception cref="IOException">The image file cannot be read.</exception>
    private static Texture2D LoadFramedPortrait(GraphicsDevice device, string reference)
    {
        using var portrait = LoadMatchTexture(device, reference, premultiply: true);
        if (portrait.Width != portrait.Height)
        {
            throw new InvalidDataException($"The portrait '{reference}' must be square.");
        }
        return CircleTexture.FramePortrait(portrait, BoardUI.HeroPortraitSize, borderWidth: 2);
    }

    /// <summary>Loads artwork referenced by the matchup from AppData or embedded content.</summary>
    /// <param name="device">The graphics device.</param>
    /// <param name="reference">The image path or embedded resource name prefixed with embedded:.</param>
    /// <param name="premultiply">Whether to premultiply alpha for transparent artwork.</param>
    /// <returns>The texture owned by the board screen.</returns>
    /// <exception cref="InvalidDataException">The reference is invalid or does not name a readable image.</exception>
    /// <exception cref="IOException">The image file cannot be read.</exception>
    private static Texture2D LoadMatchTexture(GraphicsDevice device, string reference, bool premultiply = false)
    {
        const string embedded = "embedded:";
        try
        {
            using var stream = reference.StartsWith(embedded, StringComparison.Ordinal)
                ? EmbeddedAssets.Open(reference[embedded.Length..])
                : File.OpenRead(AppDataJson.ResolveReference(reference));
            return premultiply ? Texture2D.FromStream(device, stream, DefaultColorProcessors.PremultiplyAlpha)
                : Texture2D.FromStream(device, stream);
        }
        catch (InvalidOperationException error)
        {
            // Both a missing embedded name and an undecodable image arrive as this type.
            throw new InvalidDataException($"Could not load the matchup image '{reference}'.", error);
        }
    }

    /// <summary>
    /// Forwards this frame's input to the board. A click the window controls already used is not repeated.
    /// </summary>
    /// <param name="elapsed">The time since the previous update.</param>
    /// <param name="frame">This frame's input events.</param>
    /// <param name="boardViewport">The mapping for the current window.</param>
    /// <param name="client">The window's client size in mouse units.</param>
    /// <param name="action">What the window controls did with a click.</param>
    /// <returns>The transition requested by a finished playtest, if any.</returns>
    private ScreenTransition UpdateBoard(TimeSpan elapsed, InputFrame frame, BoardViewport boardViewport, Rectangle client,
        WindowControlResult action)
    {
        Point? pointer = null;
        if (boardViewport.TryMap(frame.Pointer, client, out var position))
        {
            pointer = position;
            if (action == WindowControlResult.Ignored && frame.LeftPressed)
            {
                if (board.IsDraftReturn(position))
                {
                    return ScreenTransition.OpenDeckbuilder;
                }
                board.PointerPressed(position);
            }
            if (frame.RightPressed)
            {
                board.RightClick(position);
            }
            if (frame.ScrollDirection != 0)
            {
                board.ScrollInspector(position, frame.ScrollDirection);
            }
        }
        if (frame.LeftReleased)
        {
            board.PointerReleased(pointer);
        }
        board.Update(elapsed, pointer);
        return ScreenTransition.None;
    }
}
