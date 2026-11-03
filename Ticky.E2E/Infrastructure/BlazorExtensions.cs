using static Microsoft.Playwright.Assertions;

namespace Ticky.E2E.Infrastructure;

/// <summary>
/// Helpers for the quirks of Blazor Interactive Server apps.
/// </summary>
public static class BlazorExtensions
{
    /// <summary>
    /// Ticky prerenders pages, so for a moment after navigation the HTML is visible but the
    /// SignalR circuit isn't connected yet and clicks do nothing. Instead of sleeping, retry the
    /// click until the element we expect to appear actually appears.
    /// </summary>
    public static async Task ClickUntilVisibleAsync(this ILocator trigger, ILocator expected, int attempts = 10)
    {
        for (var i = 0; i < attempts; i++)
        {
            await trigger.ClickAsync();
            try
            {
                await expected.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 1500 });
                return;
            }
            catch (TimeoutException) when (i < attempts - 1)
            {
                // Circuit not ready yet (or the click landed during a re-render) - try again.
            }
        }
    }

    /// <summary>
    /// Blazor InputText binds on the DOM "change" event, which only fires on blur.
    /// Fill, then move focus away so the value really reaches the server.
    /// </summary>
    public static async Task FillAndCommitAsync(this ILocator input, string value)
    {
        await input.FillAsync(value);
        await input.DispatchEventAsync("change");
    }

    /// <summary>
    /// Init script (add with <see cref="AddBlazorProbeAsync"/>) that counts messages on Blazor's SignalR
    /// WebSocket. Until the circuit has exchanged messages, the page is only prerendered HTML: a form
    /// submitted then is a plain POST that ASP.NET rejects with "A valid antiforgery token was not provided".
    /// </summary>
    public const string CircuitProbeScript = """
        (() => {
          const Native = window.WebSocket;
          if (!Native || Native.__tickyProbe) return;
          class ProbedWebSocket extends Native {
            constructor(url, protocols) {
              super(url, protocols);
              if (String(url).includes('_blazor')) {
                window.__blazorMessages = 0;
                this.addEventListener('message', () => { window.__blazorMessages = (window.__blazorMessages || 0) + 1; });
              }
            }
          }
          ProbedWebSocket.__tickyProbe = true;
          window.WebSocket = ProbedWebSocket;
        })();
        """;

    public static Task AddBlazorProbeAsync(this IBrowserContext context) => context.AddInitScriptAsync(CircuitProbeScript);

    /// <summary>Waits until the Blazor Server circuit is connected and has sent its first render batch.</summary>
    public static async Task WaitForBlazorAsync(this IPage page)
    {
        await page.WaitForFunctionAsync(
            "() => window.Blazor !== undefined && document.readyState === 'complete' && (window.__blazorMessages || 0) >= 2",
            null, new() { Timeout = 30000 });

        // Let the first interactive render settle (layouts re-render once the current user is loaded).
        await page.WaitForTimeoutAsync(300);
    }

    /// <summary>Fails the test if Blazor showed its "An unhandled error has occurred" bar.</summary>
    public static Task AssertNoBlazorErrorAsync(this IPage page) =>
        Expect(page.Locator("#blazor-error-ui")).ToBeHiddenAsync(new() { Timeout = 2000 });

    /// <summary>
    /// Ticky uses Sortable.js with forceFallback=true, i.e. it listens to plain pointer events
    /// (not HTML5 drag-and-drop). Playwright's DragToAsync moves too fast for Sortable to notice,
    /// so walk the mouse there in small steps.
    /// </summary>
    public static async Task SortableDragAsync(this IPage page, ILocator source, ILocator target)
    {
        await source.ScrollIntoViewIfNeededAsync();
        var from = await source.BoundingBoxAsync() ?? throw new InvalidOperationException("Drag source is not visible.");
        var to = await target.BoundingBoxAsync() ?? throw new InvalidOperationException("Drop target is not visible.");

        var startX = from.X + from.Width / 2;
        var startY = from.Y + Math.Min(20, from.Height / 2);
        var endX = to.X + to.Width / 2;
        var endY = to.Y + Math.Min(30, Math.Max(to.Height / 2, 8));

        await page.Mouse.MoveAsync(startX, startY);
        await page.Mouse.DownAsync();
        // Small nudge so Sortable starts the drag before we travel.
        await page.Mouse.MoveAsync(startX + 5, startY + 5, new() { Steps = 5 });
        await page.Mouse.MoveAsync(endX, endY, new() { Steps = 25 });
        await page.Mouse.MoveAsync(endX, endY + 2, new() { Steps = 3 });
        await page.Mouse.UpAsync();
    }
}
