namespace Ticky.E2E.Pages;

/// <summary>/auth/login - a plain Razor Page (no Blazor circuit), so normal form interaction works.</summary>
public class LoginPage(IPage page)
{
    public ILocator Email => page.Locator("#Input_Email");
    public ILocator Password => page.Locator("#Input_Password");
    public ILocator SubmitButton => page.GetByRole(AriaRole.Button, new() { Name = "Log in" });
    public ILocator Heading => page.GetByRole(AriaRole.Heading, new() { Name = "Hey, there" });
    public ILocator InvalidCredentialsError => page.GetByText("The provided e-mail/password combination does not exist.");

    public Task GotoAsync() => page.GotoAsync("/auth/login");

    public async Task SubmitAsync(string email, string password)
    {
        await Email.FillAsync(email);
        await Password.FillAsync(password);
        // The form posts back to the server: wait for that round trip, not just for the click.
        await page.RunAndWaitForNavigationAsync(() => SubmitButton.ClickAsync(), new() { Timeout = 15000 });
        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
    }

    /// <summary>Returns true when the credentials were accepted (we left the login page).</summary>
    public async Task<bool> TryLoginAsync(string email, string password)
    {
        await GotoAsync();
        await SubmitAsync(email, password);

        // Success = the server redirected us away from the login page; failure = it re-rendered it with an error.
        try
        {
            await page.WaitForURLAsync(url => !url.Contains("/auth/login", StringComparison.OrdinalIgnoreCase),
                new() { Timeout = 5000 });
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }
}
