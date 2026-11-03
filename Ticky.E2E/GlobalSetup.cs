using Ticky.E2E.Infrastructure;
using Ticky.E2E.Pages;

namespace Ticky.E2E;

/// <summary>
/// Runs once before any test in the assembly:
///  1. makes sure Ticky is running (starts it if needed),
///  2. walks the default admin through Ticky's forced "change your credentials" screen,
///  3. saves the logged-in browser state so every test starts already authenticated.
/// </summary>
[SetUpFixture]
public class GlobalSetup
{
    private static TickyAppHost? _app;

    [OneTimeSetUp]
    public async Task BeforeAllTests()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

        if (!await TickyAppHost.IsUpAsync(http))
        {
            if (!TestSettings.StartApp)
                Assert.Fail($"Ticky is not running at {TestSettings.BaseUrl} and TICKY_START_APP=false.");

            TestContext.Progress.WriteLine($"Starting Ticky at {TestSettings.BaseUrl} ...");
            _app = await TickyAppHost.StartAsync(http, TimeSpan.FromMinutes(4));
        }

        TestContext.Progress.WriteLine("Ticky is up. Preparing an authenticated admin session ...");

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync();
        var context = await browser.NewContextAsync(new() { BaseURL = TestSettings.BaseUrl });
        await context.AddBlazorProbeAsync();
        var page = await context.NewPageAsync();
        var login = new LoginPage(page);

        // Fresh database: the default admin exists and must change credentials first.
        // Re-run against the same database: the e2e admin already exists.
        if (!await login.TryLoginAsync(TestSettings.AdminEmail, TestSettings.AdminPassword))
        {
            if (!await login.TryLoginAsync(TestSettings.DefaultAdminEmail, TestSettings.DefaultAdminPassword))
            {
                Directory.CreateDirectory(TestSettings.ArtifactsDir);
                var shot = Path.Combine(TestSettings.ArtifactsDir, "global-setup-login.png");
                await page.ScreenshotAsync(new() { Path = shot, FullPage = true });
                var errors = string.Join(" | ", await page.Locator(".field-validation-error, .validation-summary-errors").AllInnerTextsAsync());
                Assert.Fail(
                    $"Could neither log in with {TestSettings.AdminEmail} nor with Ticky's default {TestSettings.DefaultAdminEmail}. " +
                    $"The admin login in this database was probably changed by hand - start from an empty database (run RUN-ME.cmd again).\n" +
                    $"Last URL: {page.Url}\nPage errors: {(errors.Length > 0 ? errors : "(none)")}\nScreenshot: {shot}");
            }

            try
            {
                await new CredentialsPage(page).SetNewCredentialsAsync(TestSettings.AdminEmail, TestSettings.AdminPassword);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Changing the default admin's credentials failed.\n" + await DescribePageAsync(page, "global-setup-credentials"), ex);
            }

            // Changing the e-mail invalidates the session; log in again with the new credentials.
            await page.GotoAsync("/auth/logout");
            Assert.That(await login.TryLoginAsync(TestSettings.AdminEmail, TestSettings.AdminPassword), Is.True,
                "Could not log in with the newly set e2e admin credentials.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(TestSettings.AuthStatePath)!);
        await context.StorageStateAsync(new() { Path = TestSettings.AuthStatePath });
        await context.CloseAsync();
    }

    /// <summary>Screenshot + a text summary of what the page shows, so a setup failure in CI explains itself.</summary>
    private static async Task<string> DescribePageAsync(IPage page, string name)
    {
        try
        {
            Directory.CreateDirectory(TestSettings.ArtifactsDir);
            var shot = Path.Combine(TestSettings.ArtifactsDir, $"{name}.png");
            await page.ScreenshotAsync(new() { Path = shot, FullPage = true });

            var inputs = await page.EvaluateAsync<string>(
                "() => [...document.querySelectorAll('input,textarea,button')].map(e => `${e.tagName}[type=${e.type}][placeholder=${e.placeholder ?? ''}][disabled=${e.disabled}] ${(e.innerText||'').trim()}`).join('\\n')");
            var text = await page.EvaluateAsync<string>("() => document.body.innerText");
            var errorUi = await page.Locator("#blazor-error-ui").IsVisibleAsync();

            return $"URL: {page.Url}\nBlazor error UI visible: {errorUi}\nScreenshot: {shot}\n" +
                   $"--- form elements ---\n{inputs}\n--- page text (first 1500 chars) ---\n{text[..Math.Min(1500, text.Length)]}";
        }
        catch (Exception ex)
        {
            return $"(could not describe page: {ex.Message})";
        }
    }

    [OneTimeTearDown]
    public async Task AfterAllTests()
    {
        if (_app is not null)
            await _app.DisposeAsync();
    }
}
