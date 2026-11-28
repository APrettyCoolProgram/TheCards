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
    private readonly Texture2D heroPortrait;
    private readonly Texture2D opponentPortrait;
    private readonly Texture2D heroPlaymat;
    private readonly Texture2D opponentPlaymat;
    private readonly Texture2D healthAtlas;
    private readonly Texture2D heroDropRing;
    private readonly Texture2D blankBadge;
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
        boardRasterizer = new RasterizerState { ScissorTestEnable = true };
        heroPortrait = LoadFramedPortrait(device, match.Player.Portrait);
        heroPlaymat = LoadMatchTexture(device, match.Player.Playmat);
        healthAtlas = EmbeddedAssets.LoadPremultipliedTexture(device, "Board-Hero-Health.png");
        opponentPortrait = LoadFramedPortrait(device, match.Opponent.Portrait);
        opponentPlaymat = LoadMatchTexture(device, match.Opponent.Playmat);
        heroDropRing = CircleTexture.Create(device, BoardUI.HeroPortraitSize + 6, borderWidth: 3, Color.Transparent);
        blankBadge = CircleTexture.Create(device, BoardUI.BadgeSize, borderWidth: 1, Color.Black);
        using var digitSheet = EmbeddedAssets.LoadTexture(device, "Counter-Digits.png");
        board = new BoardUI(services.SpriteBatch, services.Pixel, services.Font, heroPortrait, healthAtlas,
            heroDropRing, blankBadge, digitSheet, heroPlaymat, opponentPortrait, opponentPlaymat, help, playerDeck, draftPlaytest);
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
        heroPortrait.Dispose();
        opponentPortrait.Dispose();
        heroPlaymat.Dispose();
        opponentPlaymat.Dispose();
        healthAtlas.Dispose();
        heroDropRing.Dispose();
        blankBadge.Dispose();
        board.Dispose();
        boardRasterizer.Dispose();
    }

    /// <summary>Loads a portrait and frames it in a circle.</summary>
    /// <param name="device">The graphics device.</param>
    /// <param name="reference">The AppData image path or embedded resource reference.</param>
    /// <returns>A texture the caller must dispose.</returns>
    private static Texture2D LoadFramedPortrait(GraphicsDevice device, string reference)
    {
        using var portrait = LoadMatchTexture(device, reference, premultiply: true);
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
