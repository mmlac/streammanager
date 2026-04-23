namespace StreamManager.App;

// Static bridge so Avalonia's XAML-loaded App instance can reach the DI container
// built in Program.Main. Populated once at startup; read-only afterwards.
internal static class AppHost
{
    public static IServiceProvider? Services { get; set; }
}
