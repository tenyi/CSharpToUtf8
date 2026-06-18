using Avalonia;
using System;
using CSharpToUtf8.Conversion;

namespace CSharpToUtf8;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // 在 Avalonia 啟動前先註冊 CodePages provider，
        // 確保 Big5/GBK/Shift_JIS 等東亞 codepage 可用。
        // 此處為同步、極輕量操作，符合 Avalonia 啟動前的限制。
        EncodingBootstrap.Register();

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
