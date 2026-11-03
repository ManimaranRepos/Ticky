namespace Ticky.E2E.Infrastructure;

/// <summary>
/// All knobs come from environment variables so the same suite runs locally,
/// against Docker, and in CI without code changes.
/// </summary>
public static class TestSettings
{
    /// <summary>Where Ticky is (or will be) listening. Release builds bind to :8080.</summary>
    public static string BaseUrl =>
        (Environment.GetEnvironmentVariable("TICKY_BASE_URL") ?? "http://localhost:8080").TrimEnd('/');

    /// <summary>When true (default) the suite starts Ticky itself if nothing answers at <see cref="BaseUrl"/>.</summary>
    public static bool StartApp =>
        !string.Equals(Environment.GetEnvironmentVariable("TICKY_START_APP"), "false", StringComparison.OrdinalIgnoreCase);

    // Database used when the suite starts the app itself.
    public static string DbHost => Env("TICKY_DB_HOST", "localhost");
    public static string DbName => Env("TICKY_DB_NAME", "ticky_e2e");
    public static string DbUser => Env("TICKY_DB_USER", "ticky");
    public static string DbPassword => Env("TICKY_DB_PASSWORD", "ticky_e2e_pw");

    // Seeded by Ticky on first start (Ticky.Base/Constants.cs).
    public const string DefaultAdminEmail = "admin@ticky.com";
    public const string DefaultAdminPassword = "abc123";

    // What the suite changes the default admin to during global setup.
    public static string AdminEmail => Env("TICKY_ADMIN_EMAIL", "e2e.admin@ticky.test");
    public static string AdminPassword => Env("TICKY_ADMIN_PASSWORD", "E2e-Passw0rd!");

    public static string ArtifactsDir => Path.Combine(TestContext.CurrentContext.WorkDirectory, "playwright-artifacts");
    public static string AuthStatePath => Path.Combine(TestContext.CurrentContext.WorkDirectory, ".auth", "admin.json");

    private static string Env(string key, string fallback) =>
        Environment.GetEnvironmentVariable(key) is { Length: > 0 } v ? v : fallback;

    /// <summary>Short unique suffix so parallel tests never collide on names.</summary>
    public static string Unique(string prefix) => $"{prefix} {Guid.NewGuid().ToString("N")[..6]}";
}
