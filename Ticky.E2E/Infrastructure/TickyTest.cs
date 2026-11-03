namespace Ticky.E2E.Infrastructure;

/// <summary>
/// Base class for every Ticky UI test.
///  - Gives each test its own browser context (isolated cookies/storage) that is already logged in.
///  - Records a Playwright trace and keeps it only when the test fails.
///  - Fails any test that leaves Blazor's unhandled-error bar on screen.
/// </summary>
[Parallelizable(ParallelScope.Self)]
public abstract class TickyTest : PageTest
{
    /// <summary>Override to start a test logged out.</summary>
    protected virtual bool StartAuthenticated => true;

    public override BrowserNewContextOptions ContextOptions() => new()
    {
        BaseURL = TestSettings.BaseUrl,
        ViewportSize = new() { Width = 1440, Height = 900 },
        StorageStatePath = StartAuthenticated ? TestSettings.AuthStatePath : null,
    };

    [SetUp]
    public async Task StartTracing()
    {
        await Context.Tracing.StartAsync(new()
        {
            Title = TestContext.CurrentContext.Test.FullName,
            Screenshots = true,
            Snapshots = true,
            Sources = true,
        });
    }

    [TearDown]
    public async Task FinishTest()
    {
        var failed = TestContext.CurrentContext.Result.Outcome.Status == NUnit.Framework.Interfaces.TestStatus.Failed;

        if (failed)
        {
            Directory.CreateDirectory(TestSettings.ArtifactsDir);
            var name = string.Concat(TestContext.CurrentContext.Test.Name.Split(Path.GetInvalidFileNameChars()));
            var tracePath = Path.Combine(TestSettings.ArtifactsDir, $"{name}.zip");
            var screenshotPath = Path.Combine(TestSettings.ArtifactsDir, $"{name}.png");

            await Page.ScreenshotAsync(new() { Path = screenshotPath, FullPage = true });
            await Context.Tracing.StopAsync(new() { Path = tracePath });
            TestContext.AddTestAttachment(tracePath, "Playwright trace (open with: pwsh bin/Debug/net10.0/playwright.ps1 show-trace <file>)");
            TestContext.AddTestAttachment(screenshotPath, "Screenshot at failure");
            return;
        }

        await Context.Tracing.StopAsync();
        await Page.AssertNoBlazorErrorAsync();
    }
}
