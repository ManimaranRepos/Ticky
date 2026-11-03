using System.Diagnostics;

namespace Ticky.E2E.Infrastructure;

/// <summary>
/// Starts Ticky.Web as a child process when nothing is listening at <see cref="TestSettings.BaseUrl"/>.
/// Runs a Release build with the Development environment because:
///  - Release binds to :8080 and points the board's SignalR hub at :8080 (Debug hard-codes https://localhost:7225),
///  - the Development environment keeps static web assets from referenced projects working under `dotnet run`.
/// </summary>
public sealed class TickyAppHost : IAsyncDisposable
{
    private readonly Process _process;
    private readonly List<string> _log = [];

    private TickyAppHost(Process process) => _process = process;

    public IReadOnlyList<string> Log => _log;

    public static async Task<bool> IsUpAsync(HttpClient http)
    {
        try
        {
            using var res = await http.GetAsync($"{TestSettings.BaseUrl}/auth/login");
            return res.IsSuccessStatusCode;
        }
        catch (HttpRequestException) { return false; }
        catch (TaskCanceledException) { return false; }
    }

    public static async Task<TickyAppHost> StartAsync(HttpClient http, TimeSpan timeout)
    {
        var webProject = Path.Combine(FindSolutionRoot(), "Ticky.Web", "Ticky.Web.csproj");

        var psi = new ProcessStartInfo("dotnet",
            ["run", "-c", "Release", "--project", webProject, "--no-launch-profile"])
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        psi.Environment["DB_HOST"] = TestSettings.DbHost;
        psi.Environment["DB_NAME"] = TestSettings.DbName;
        psi.Environment["DB_USERNAME"] = TestSettings.DbUser;
        psi.Environment["DB_PASSWORD"] = TestSettings.DbPassword;
        psi.Environment["SMTP_ENABLED"] = "false";
        psi.Environment["FULLY_OFFLINE"] = "true";
        psi.Environment["BASE_URL"] = TestSettings.BaseUrl;

        var process = Process.Start(psi) ?? throw new InvalidOperationException("Could not start `dotnet run` for Ticky.Web.");
        var host = new TickyAppHost(process);
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) lock (host._log) host._log.Add(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) lock (host._log) host._log.Add(e.Data); };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (process.HasExited)
                throw new InvalidOperationException(
                    $"Ticky.Web exited with code {process.ExitCode} before it was ready.\n{string.Join('\n', host._log.TakeLast(40))}");

            if (await IsUpAsync(http))
                return host;

            await Task.Delay(1000);
        }

        await host.DisposeAsync();
        throw new TimeoutException(
            $"Ticky.Web did not answer at {TestSettings.BaseUrl} within {timeout.TotalSeconds:0}s. Is MySQL running?\n{string.Join('\n', host._log.TakeLast(40))}");
    }

    public static string FindSolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Ticky.sln")))
            dir = dir.Parent;

        return dir?.FullName ?? throw new DirectoryNotFoundException("Could not find Ticky.sln above the test output folder.");
    }

    public async ValueTask DisposeAsync()
    {
        if (_process.HasExited)
            return;

        _process.Kill(entireProcessTree: true);
        await _process.WaitForExitAsync();
        _process.Dispose();
    }
}
