// 261127_code
// 261127_documentation

using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace EchoesOfTheShardveil.Desktop;

/// <summary>Synchronizes the DesktopGL back buffer with the SDL window's actual pixel size.</summary>
internal sealed class DesktopDrawable : IDisposable
{
    /// <summary>The shape of SDL's functions that report a window size.</summary>
    /// <param name="window">The SDL window handle.</param>
    /// <param name="width">Receives the width.</param>
    /// <param name="height">Receives the height.</param>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void WindowSizeQuery(nint window, out int width, out int height);

    private nint library;
    private readonly WindowSizeQuery? getDrawableSize;
    private readonly WindowSizeQuery? getClientSize;

    /// <summary>Initializes the SDL size queries. Without them the window's own bounds are used.</summary>
    public DesktopDrawable()
    {
        if (SdlLibrary.TryLoad(out library))
        {
            getDrawableSize = LoadQuery("SDL_GL_GetDrawableSize");
            getClientSize = LoadQuery("SDL_GetWindowSize");
        }
    }

    /// <summary>Resizes the back buffer to the window's pixel size when they differ.</summary>
    /// <param name="device">The graphics device to resize.</param>
    /// <param name="window">The game window.</param>
    /// <returns>The window's client bounds in the units used by mouse positions.</returns>
    public Rectangle Synchronize(GraphicsDevice device, GameWindow window)
    {
        var client = window.ClientBounds;
        if (window.Handle == 0)
        {
            return client;
        }

        if (getClientSize is not null)
        {
            getClientSize(window.Handle, out var clientWidth, out var clientHeight);
            if (clientWidth > 0 && clientHeight > 0)
            {
                client.Width = clientWidth;
                client.Height = clientHeight;
            }
        }

        if (getDrawableSize is null)
        {
            return client;
        }

        getDrawableSize(window.Handle, out var width, out var height);
        var presentation = device.PresentationParameters;
        var viewport = device.Viewport;
        if (width <= 0 || height <= 0
            || (width == presentation.BackBufferWidth && height == presentation.BackBufferHeight
                && width == viewport.Width && height == viewport.Height))
        {
            return client;
        }

        // DesktopGL uses the back-buffer height to convert both viewport and scissor coordinates.
        presentation.BackBufferWidth = width;
        presentation.BackBufferHeight = height;
        device.Viewport = new Viewport(0, 0, width, height);
        device.ScissorRectangle = device.Viewport.Bounds;
        return client;
    }

    /// <inheritdoc />
    public void Dispose() => SdlLibrary.Free(ref library);

    /// <summary>Looks up an SDL function that reports a window size.</summary>
    /// <param name="name">The exported function name.</param>
    /// <returns>The function, or <see langword="null"/> if SDL does not export it.</returns>
    private WindowSizeQuery? LoadQuery(string name) => NativeLibrary.TryGetExport(library, name, out var function)
        ? Marshal.GetDelegateForFunctionPointer<WindowSizeQuery>(function)
        : null;
}
