using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;

namespace ZaneTask.Desktop;

/// <summary>
/// Runs the ZaneTask server (API + web UI) as a hidden child process on 127.0.0.1 with a local SQLite
/// database. The server also watches this process and exits with it, so nothing is left running.
/// </summary>
internal sealed class LocalServer : IDisposable
{
    /// <summary>Kept stable so the browser session (stored per origin) survives restarts.</summary>
    private const int PreferredPort = 5288;

    /// <summary>Stay signed in for 30 days.</summary>
    private const int SessionMinutes = 30 * 24 * 60;

    private readonly Process _process;
    private readonly StreamWriter _log;

    private LocalServer(Process process, StreamWriter log, Uri url)
    {
        _process = process;
        _log = log;
        Url = url;
    }

    public Uri Url { get; }

    public static async Task<LocalServer> StartAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(AppPaths.ServerExe))
            throw new InvalidOperationException($"The server is missing: {AppPaths.ServerExe}. Reinstall ZaneTask.");

        Directory.CreateDirectory(AppPaths.DataDir);
        Directory.CreateDirectory(Path.GetDirectoryName(AppPaths.LogFile)!);

        var url = new Uri($"http://127.0.0.1:{PickPort()}/");
        var startInfo = new ProcessStartInfo(AppPaths.ServerExe)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(AppPaths.ServerExe)!,
        };
        var env = startInfo.Environment;
        env["ASPNETCORE_URLS"] = url.ToString().TrimEnd('/');
        env["ASPNETCORE_ENVIRONMENT"] = "Production";
        env["Database__Provider"] = "Sqlite";
        env["ConnectionStrings__Default"] = $"Data Source={AppPaths.DatabaseFile}";
        env["Database__MigrateOnStartup"] = "true";
        env["Jwt__SigningKey"] = GetOrCreateSigningKey();
        env["Jwt__AccessTokenLifetimeMinutes"] = SessionMinutes.ToString();
        env["Hosting__UseHttpsRedirection"] = "false";
        env["Hosting__ParentProcessId"] = Environment.ProcessId.ToString();

        var log = new StreamWriter(AppPaths.LogFile, append: false) { AutoFlush = true };
        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, e) => WriteLog(log, e.Data);
        process.ErrorDataReceived += (_, e) => WriteLog(log, e.Data);

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var server = new LocalServer(process, log, url);
        try
        {
            await server.WaitUntilReadyAsync(cancellationToken);
            return server;
        }
        catch
        {
            server.Dispose();
            throw;
        }
    }

    private async Task WaitUntilReadyAsync(CancellationToken cancellationToken)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var deadline = DateTime.UtcNow.AddSeconds(90); // first start creates the database

        while (DateTime.UtcNow < deadline)
        {
            if (_process.HasExited)
                throw new InvalidOperationException($"The server stopped while starting. Details: {AppPaths.LogFile}");

            try
            {
                using var response = await http.GetAsync(new Uri(Url, "api/health"), cancellationToken);
                if (response.IsSuccessStatusCode)
                    return;
            }
            catch (HttpRequestException)
            {
                // Not listening yet.
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Request timed out; try again.
            }

            await Task.Delay(250, cancellationToken);
        }

        throw new TimeoutException($"The server did not start in time. Details: {AppPaths.LogFile}");
    }

    private static int PickPort()
    {
        if (IsFree(PreferredPort))
            return PreferredPort;

        // Another program uses our port: fall back to any free one (you'll need to sign in again).
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static bool IsFree(int port)
    {
        try
        {
            using var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    /// <summary>A random key per install; tokens from one computer are useless on another.</summary>
    private static string GetOrCreateSigningKey()
    {
        if (File.Exists(AppPaths.SigningKeyFile))
        {
            var existing = File.ReadAllText(AppPaths.SigningKeyFile).Trim();
            if (existing.Length >= 32)
                return existing;
        }

        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        File.WriteAllText(AppPaths.SigningKeyFile, key);
        return key;
    }

    private static void WriteLog(StreamWriter log, string? line)
    {
        if (line is null)
            return;
        lock (log)
        {
            try { log.WriteLine(line); } catch (ObjectDisposedException) { }
        }
    }

    public void Dispose()
    {
        try
        {
            if (!_process.HasExited)
                _process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Already exited.
        }

        _process.Dispose();
        lock (_log)
            _log.Dispose();
    }
}
