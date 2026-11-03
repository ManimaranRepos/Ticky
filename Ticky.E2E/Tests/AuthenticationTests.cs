using Ticky.E2E.Infrastructure;
using Ticky.E2E.Pages;
using static Microsoft.Playwright.Assertions;

namespace Ticky.E2E.Tests;

[TestFixture]
[Category("Auth")]
public class AuthenticationTests : TickyTest
{
    protected override bool StartAuthenticated => false;

    [Test]
    public async Task Anonymous_user_is_redirected_to_login_with_return_url()
    {
        await Page.GotoAsync("/settings");

        await Expect(Page).ToHaveURLAsync(new Regex(@"/auth/login\?returnUrl=%2Fsettings"));
        await Expect(new LoginPage(Page).Heading).ToBeVisibleAsync();
    }

    [Test]
    public async Task Wrong_password_shows_error_and_stays_on_login()
    {
        var login = new LoginPage(Page);
        await login.GotoAsync();

        await login.SubmitAsync(TestSettings.AdminEmail, "definitely-not-the-password");

        await Expect(login.InvalidCredentialsError).ToBeVisibleAsync();
        await Expect(Page).ToHaveURLAsync(new Regex("/auth/login"));
    }

    [Test]
    public async Task Default_credentials_no_longer_work_after_first_setup()
    {
        // GlobalSetup changed the seeded admin@ticky.com / abc123 - the default must now be rejected.
        var login = new LoginPage(Page);
        await login.GotoAsync();

        await login.SubmitAsync(TestSettings.DefaultAdminEmail, TestSettings.DefaultAdminPassword);

        await Expect(login.InvalidCredentialsError).ToBeVisibleAsync();
    }

    [Test]
    public async Task Valid_login_lands_on_home_and_logout_ends_the_session()
    {
        var login = new LoginPage(Page);
        await login.GotoAsync();
        await login.SubmitAsync(TestSettings.AdminEmail, TestSettings.AdminPassword);

        var home = new HomePage(Page);
        await Expect(home.Greeting).ToBeVisibleAsync();

        await Page.GotoAsync("/auth/logout");
        await Expect(login.Heading).ToBeVisibleAsync();

        // The session cookie is gone: protected pages bounce back to login.
        await Page.GotoAsync("/");
        await Expect(Page).ToHaveURLAsync(new Regex("/auth/login"));
    }

    [Test]
    public async Task Login_form_requires_email_and_password()
    {
        var login = new LoginPage(Page);
        await login.GotoAsync();

        await login.SubmitButton.ClickAsync();

        await Expect(Page).ToHaveURLAsync(new Regex("/auth/login"));
        await Expect(Page.GetByText("field is required").First).ToBeVisibleAsync();
    }
}
