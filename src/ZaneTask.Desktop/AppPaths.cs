namespace ZaneTask.Desktop;

/// <summary>Where the desktop app keeps its program files and your data.</summary>
internal static class AppPaths
{
    /// <summary>The published server, installed next to the desktop app.</summary>
    public static string ServerExe => Path.Combine(AppContext.BaseDirectory, "server", "ZaneTask.Api.exe");

    /// <summary>Your data lives here, separate from the program files, so reinstalling keeps it.</summary>
    public static string DataDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZaneTask");

    public static string DatabaseFile => Path.Combine(DataDir, "zanetask.db");
    public static string SigningKeyFile => Path.Combine(DataDir, "signing.key");
    public static string LogFile => Path.Combine(DataDir, "logs", "server.log");
    public static string WebViewDir => Path.Combine(DataDir, "webview");
}
