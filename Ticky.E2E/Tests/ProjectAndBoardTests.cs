using Ticky.E2E.Infrastructure;
using Ticky.E2E.Pages;
using static Microsoft.Playwright.Assertions;

namespace Ticky.E2E.Tests;

[TestFixture]
[Category("Boards")]
public class ProjectAndBoardTests : TickyTest
{
    [Test]
    public async Task Admin_can_create_a_project_and_a_board_in_it()
    {
        var home = new HomePage(Page);
        var project = TestSettings.Unique("Website relaunch");
        var board = TestSettings.Unique("Sprint board");

        await home.GotoAsync();
        await home.CreateProjectAsync(project);
        await home.CreateBoardAsync(project, board, "WEB", "Tasks for the relaunch");

        await Expect(home.ProjectSection(project).GetByText("1 board")).ToBeVisibleAsync();
        await Expect(home.BoardCard(board).GetByText("Tasks for the relaunch")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Board_code_must_be_upper_case()
    {
        var home = new HomePage(Page);
        var project = TestSettings.Unique("Validation");

        await home.GotoAsync();
        await home.CreateProjectAsync(project);

        await home.ProjectSection(project).GetByTitle("Create new board")
            .ClickUntilVisibleAsync(home.Dialog.GetByRole(AriaRole.Heading, new() { Name = "New board" }));
        await home.Dialog.GetByPlaceholder("Board name").FillAndCommitAsync("Lowercase code board");
        await home.Dialog.GetByPlaceholder("e.g. TCK").FillAndCommitAsync("abc");
        await home.Dialog.GetByRole(AriaRole.Button, new() { Name = "Submit" }).ClickAsync();

        await Expect(home.Dialog.GetByText("The code must be in upper-case.")).ToBeVisibleAsync();
        await Expect(home.Dialog).ToBeVisibleAsync();
    }

    [Test]
    public async Task Project_name_is_required()
    {
        var home = new HomePage(Page);
        await home.GotoAsync();

        await home.NewProjectButton.ClickUntilVisibleAsync(home.Dialog.GetByRole(AriaRole.Heading, new() { Name = "New project" }));
        await home.Dialog.GetByRole(AriaRole.Button, new() { Name = "Submit" }).ClickAsync();

        await Expect(home.Dialog.GetByText("field is required")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Opening_a_board_shows_it_and_remembers_it_as_recent()
    {
        var home = new HomePage(Page);
        var board = await home.CreateAndOpenFreshBoardAsync();

        await Expect(board.NewColumnButton).ToBeVisibleAsync();

        await home.GotoAsync();
        // The nav menu has its own "Recent board" link too; check the one on the home page itself.
        await Expect(Page.Locator("main").GetByRole(AriaRole.Link, new() { Name = "Recent board" })).ToBeVisibleAsync();
    }
}
