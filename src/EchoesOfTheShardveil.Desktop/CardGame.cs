// 261127_code
// 261127_documentation

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace EchoesOfTheShardveil.Desktop;

/// <summary>The single MonoGame game. It owns the window and shared resources and switches between screens.</summary>
internal sealed class CardGame : Game
{
    private readonly GraphicsDeviceManager graphics;
    private readonly GameAudio audio = new();
    private readonly Lazy<SavedDeck> savedDeck = new(() => new SavedDeck(SavedDeck.DefaultPath));
    private GameServices? services;
    private DesktopDrawable? drawable;
    private QuitWatcher? quitWatcher;
    private MenuScreen? menu;
    private DeckbuilderUI? deckbuilder;
    private IScreen? currentScreen;
    private bool exitConfirmed;

    /// <summary>Initializes the game with a resizable window at the board's design size.</summary>
    public CardGame()
    {
        graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = BoardUI.Width,
            PreferredBackBufferHeight = BoardUI.Height,
            HardwareModeSwitch = false,
        };
        Window.AllowUserResizing = true;
        IsMouseVisible = true;
    }

    protected override void LoadContent()
    {
        var spriteBatch = new SpriteBatch(GraphicsDevice);
        var pixel = new Texture2D(GraphicsDevice, 1, 1);
        pixel.SetData([Color.White]);
        var font = EmbeddedAssets.LoadPremultipliedTexture(GraphicsDevice, "Board-Font.png");
        services = new GameServices(GraphicsDevice, spriteBatch, pixel, font, audio);
        drawable = new DesktopDrawable();
        quitWatcher = new QuitWatcher();
        menu = new MenuScreen(services);
        Window.TextInput += HandleTextInput;
        Window.KeyDown += HandleKeyDown;
        Show(menu);
        audio.Start();
    }

    protected override void Update(GameTime gameTime)
    {
        if (currentScreen is { } screen)
        {
            var client = drawable?.Synchronize(GraphicsDevice, Window) ?? Window.ClientBounds;
            Apply(screen.Update(gameTime, client, IsActive));
        }
        base.Update(gameTime);
    }

    protected override void OnExiting(object sender, ExitingEventArgs args)
    {
        base.OnExiting(sender, args);
        // Quitting the application (such as from the macOS menu bar) exits at once.
        if (quitWatcher?.TakeApplicationQuit() == true)
        {
            return;
        }

        // The window's close button asks first: the menu offers to quit, the board offers to forfeit.
        if (!exitConfirmed && currentScreen is { } screen)
        {
            args.Cancel = true;
            screen.RequestClose();
        }
    }

    protected override void Draw(GameTime gameTime)
    {
        currentScreen?.Draw(gameTime);
        base.Draw(gameTime);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            CloseBoardScreen();
            Window.TextInput -= HandleTextInput;
            Window.KeyDown -= HandleKeyDown;
            deckbuilder?.Dispose();
            menu?.Dispose();
            audio.Dispose();
            services?.Font.Dispose();
            services?.Pixel.Dispose();
            services?.SpriteBatch.Dispose();
            quitWatcher?.Dispose();
            drawable?.Dispose();
        }
        base.Dispose(disposing);
    }

    /// <summary>Routes window text input to the deckbuilder when it is active.</summary>
    /// <param name="sender">The window receiving text.</param>
    /// <param name="args">The entered character.</param>
    private void HandleTextInput(object? sender, TextInputEventArgs args)
    {
        if (IsActive && currentScreen is DeckbuilderUI editor)
        {
            editor.EnterSearchText(args.Character);
        }
    }

    /// <summary>Routes collection navigation keys to the active deckbuilder.</summary>
    /// <param name="sender">The window receiving input.</param>
    /// <param name="args">The pressed key.</param>
    private void HandleKeyDown(object? sender, InputKeyEventArgs args)
    {
        if (IsActive && currentScreen is DeckbuilderUI editor)
        {
            editor.NavigateCollection(args.Key);
        }
    }

    /// <summary>Releases the current screen if it is a board. The menu and deckbuilder are kept for reuse.</summary>
    private void CloseBoardScreen()
    {
        if (currentScreen is BoardScreen board)
        {
            board.Dispose();
        }
    }

    /// <summary>Activates a screen and applies its window title.</summary>
    /// <param name="screen">The screen to show.</param>
    private void Show(IScreen screen)
    {
        currentScreen = screen;
        Window.Title = screen.Title;
    }

    /// <summary>Carries out what a screen asked for.</summary>
    /// <param name="transition">The requested change.</param>
    private void Apply(ScreenTransition transition)
    {
        try
        {
            ApplyTransition(transition);
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException &&
            currentScreen is MenuScreen or DeckbuilderUI)
        {
            // The screen that asked is still showing, so it can report the problem.
            Console.Error.WriteLine(error);
            if (currentScreen is MenuScreen currentMenu)
            {
                currentMenu.ShowContentError(transition);
            }
            else
            {
                ((DeckbuilderUI)currentScreen).ShowPlaytestError();
            }
        }
    }

    /// <summary>Creates, activates or closes the screens for a transition.</summary>
    /// <param name="transition">The requested change.</param>
    private void ApplyTransition(ScreenTransition transition)
    {
        switch (transition)
        {
            case ScreenTransition.OpenBoard when services is not null:
                Show(new BoardScreen(services, savedDeck.Value.ActiveDeck));
                break;
            case ScreenTransition.PlayDraft when services is not null &&
                deckbuilder?.CreatePlaytestDeck() is { } draft:
                Show(new BoardScreen(services, draft, draftPlaytest: true));
                break;
            case ScreenTransition.OpenDeckbuilder when services is not null:
                CloseBoardScreen();
                deckbuilder ??= new DeckbuilderUI(services, savedDeck.Value);
                deckbuilder.Activate();
                Show(deckbuilder);
                break;
            case ScreenTransition.ReturnToMenu when menu is not null:
                CloseBoardScreen();
                menu.Activate();
                Show(menu);
                break;
            case ScreenTransition.ExitGame:
                exitConfirmed = true;
                Exit();
                break;
        }
    }
}
