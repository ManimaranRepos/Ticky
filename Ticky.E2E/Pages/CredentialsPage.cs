using Ticky.E2E.Infrastructure;

namespace Ticky.E2E.Pages;

/// <summary>/settings/3 - the screen Ticky forces a user onto while they still have default credentials.</summary>
public class CredentialsPage(IPage page)
{
    public ILocator ForcedChangeWarning => page.GetByText("must be changed before going any further");
    public ILocator NewEmail => page.GetByPlaceholder("sample@email.com");
    public ILocator NewPassword => page.GetByPlaceholder("New password", new() { Exact = true });
    public ILocator RepeatPassword => page.GetByPlaceholder("Repeat new password");
    public ILocator SaveButton => page.GetByRole(AriaRole.Button, new() { Name = "Save" });

    public async Task SetNewCredentialsAsync(string email, string password)
    {
        await page.WaitForURLAsync("**/settings/3");
        await page.WaitForBlazorAsync();
        await Assertions.Expect(ForcedChangeWarning).ToBeVisibleAsync();

        // The form model is replaced after the first interactive render, so retry until our values stick.
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await NewEmail.FillAndCommitAsync(email);
            await NewPassword.FillAndCommitAsync(password);
            await RepeatPassword.FillAndCommitAsync(password);
            await SaveButton.ClickAsync();

            try
            {
                // On success Ticky navigates home (or to logout once the old session is invalidated).
                await page.WaitForURLAsync(url => !url.EndsWith("/settings/3"), new() { Timeout = 5000 });
                return;
            }
            catch (TimeoutException) when (attempt < 4) { }
        }
    }
}
