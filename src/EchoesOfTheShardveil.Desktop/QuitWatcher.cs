// 261127_code
// 261127_documentation

using System.Runtime.InteropServices;

namespace EchoesOfTheShardveil.Desktop;

/// <summary>
/// Tells an application quit (the macOS menu's Quit or Cmd+Q) apart from the window's close button.
/// SDL reports a quit request on its own, but a close button first reports a window close, then the quit.
/// </summary>
internal sealed class QuitWatcher : IDisposable
{
    private const int QuitEvent = 0x100;
    private const int WindowEvent = 0x200;
    private const byte WindowClose = 14;
    // SDL_Event layout: type (uint32), timestamp (uint32), windowID (uint32), then the window event id (byte).
    private const int WindowEventIdOffset = 12;

    /// <summary>The callback SDL runs for each event.</summary>
    /// <param name="userdata">The value given at registration, which is unused.</param>
    /// <param name="sdlEvent">A pointer to the SDL event.</param>
    /// <returns>A value SDL ignores for event watchers.</returns>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int EventWatch(nint userdata, nint sdlEvent);

    /// <summary>The shape of SDL's functions that add and remove an event watcher.</summary>
    /// <param name="filter">A pointer to the callback.</param>
    /// <param name="userdata">A value passed back to the callback.</param>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void WatchRegistration(nint filter, nint userdata);

    private nint library;
    // Held in a field so the delegate is not collected while SDL still calls it.
    private readonly EventWatch? watch;
    private readonly WatchRegistration? removeWatch;
    private readonly nint filter;
    private volatile bool windowCloseSeen;
    private volatile bool applicationQuit;

    /// <summary>Starts watching SDL events. If SDL is unavailable, no quit is ever reported.</summary>
    public QuitWatcher()
    {
        if (!SdlLibrary.TryLoad(out library) ||
            !NativeLibrary.TryGetExport(library, "SDL_AddEventWatch", out var add) ||
            !NativeLibrary.TryGetExport(library, "SDL_DelEventWatch", out var delete))
        {
            return;
        }

        watch = OnEvent;
        filter = Marshal.GetFunctionPointerForDelegate(watch);
        removeWatch = Marshal.GetDelegateForFunctionPointer<WatchRegistration>(delete);
        Marshal.GetDelegateForFunctionPointer<WatchRegistration>(add)(filter, 0);
    }

    /// <summary>Returns whether the latest exit request was an application quit, and clears it.</summary>
    /// <returns><see langword="true"/> if the application was quit other than by the close button.</returns>
    public bool TakeApplicationQuit()
    {
        var quit = applicationQuit;
        applicationQuit = false;
        return quit;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        removeWatch?.Invoke(filter, 0);
        SdlLibrary.Free(ref library);
    }

    /// <summary>Records window close and quit events.</summary>
    /// <param name="userdata">Unused.</param>
    /// <param name="sdlEvent">A pointer to the SDL event.</param>
    /// <returns>1, which SDL ignores for event watchers.</returns>
    private int OnEvent(nint userdata, nint sdlEvent)
    {
        var type = Marshal.ReadInt32(sdlEvent);
        if (type == WindowEvent && Marshal.ReadByte(sdlEvent, WindowEventIdOffset) == WindowClose)
        {
            windowCloseSeen = true;
        }
        else if (type == QuitEvent)
        {
            // A quit that no window close came before is the application being quit from elsewhere.
            applicationQuit = !windowCloseSeen;
            windowCloseSeen = false;
        }
        return 1;
    }
}
