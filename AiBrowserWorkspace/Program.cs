using Avalonia;
using Avalonia.X11;

namespace AiBrowserWorkspace;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (OperatingSystem.IsLinux())
        {
            PreventCefFromLoadingGtk();
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // On Linux, CEF detects the desktop environment (GNOME here) from these variables
    // and, on recognizing one, loads GTK/Pango for native theming — which drags in the
    // system's libharfbuzz.so. That's a different build than the private copy bundled
    // in SkiaSharp's libHarfBuzzSharp.so that Avalonia already loaded for its own text
    // rendering: both export the same hb_* symbol names with an incompatible internal
    // ABI, so a call resolved into the "wrong" one corrupts memory. Observed as either
    // a SIGSEGV in hb_face_reference_table the moment a browser is added, or — if that
    // memory corruption happens to land somewhere non-fatal instead — glyphs shaping
    // into garbage across the whole app's own UI, not just the browser. Clearing these
    // before the first browser starts stops CEF from ever loading GTK/Pango, so the
    // system harfbuzz never enters the process and the two copies never collide.
    private static void PreventCefFromLoadingGtk()
    {
        Environment.SetEnvironmentVariable("XDG_CURRENT_DESKTOP", null);
        Environment.SetEnvironmentVariable("DESKTOP_SESSION", null);
        Environment.SetEnvironmentVariable("GNOME_DESKTOP_SESSION_ID", null);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            // VMware's virtual SVGA3D GPU is blacklisted for GL; force software rendering
            // on X11/XWayland so we don't depend on a working GPU driver in this VM.
            .With(new X11PlatformOptions
            {
                RenderingMode = new[] { X11RenderingMode.Software },
                // The IBus/DBus input-method bridge throws repeatedly on this desktop
                // ("UnknownMethod: Destroy is not implemented on IBus.Service") right
                // around window-activation, correlating with the app being torn down.
                // Disabling it removes that whole failure path; CJK/IME input isn't a
                // requirement for this app.
                EnableIme = false
            })
            .LogToTrace();
}
