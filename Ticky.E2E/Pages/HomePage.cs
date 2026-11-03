using Ticky.E2E.Infrastructure;
using static Microsoft.Playwright.Assertions;

namespace Ticky.E2E.Pages;

/// <summary>"/" - projects and their boards.</summary>
public class HomePage(IPage page)
{
    public ILocator Greeting => page.GetByRole(AriaRole.Heading, new() { Name = "What are you up to today?" });
    public ILocator NewProjectButton => page.GetByRole(AriaRole.Button, new() { Name = "New project" });
    public ILocator Dialog => page.GetByRole(AriaRole.Dialog);

    public ILocator ProjectSection(string projectName) =>
        page.Locator("section").Filter(new() { Has = page.GetByRole(AriaRole.Heading, new() { Name = projectName, Exact = true }) });

    public ILocator BoardCard(string boardName) =>
        page.Locator(".board-card").Filter(new() { Has = page.GetByRole(AriaRole.Heading, new() { Name = boardName, Exact = true }) });

    public async Task GotoAsync()
    {
        await page.GotoAsync("/");
        await page.WaitForBlazorAsync();
        await Expect(Greeting).ToBeVisibleAsync();
    }

    public async Task CreateProjectAsync(string name)
    {
        await NewProjectButton.ClickUntilVisibleAsync(Dialog.GetByRole(AriaRole.Heading, new() { Name = "New project" }));
        await Dialog.GetByPlaceholder("Project name").FillAndCommitAsync(name);
        await Dialog.GetByRole(AriaRole.Button, new() { Name = "Submit" }).ClickAsync();
        await Expect(ProjectSection(name)).ToBeVisibleAsync();
    }

    public async Task CreateBoardAsync(string projectName, string boardName, string code, string description = "Created by Playwright")
    {
        var addBoard = ProjectSection(projectName).GetByTitle("Create new board");
        await addBoard.ClickUntilVisibleAsync(Dialog.GetByRole(AriaRole.Heading, new() { Name = "New board" }));
        await Dialog.GetByPlaceholder("Board name").FillAndCommitAsync(boardName);
        await Dialog.GetByPlaceholder("What is this board for?").FillAndCommitAsync(description);
        await Dialog.GetByPlaceholder("e.g. TCK").FillAndCommitAsync(code);
        await Dialog.GetByRole(AriaRole.Button, new() { Name = "Submit" }).ClickAsync();
        await Expect(BoardCard(boardName)).ToBeVisibleAsync();
    }

    public async Task<BoardPage> OpenBoardAsync(string boardName)
    {
        await BoardCard(boardName).GetByRole(AriaRole.Heading, new() { Name = boardName }).ClickAsync();
        await page.WaitForURLAsync(new Regex(@"/boards/\d+"));
        var board = new BoardPage(page);
        await board.WaitUntilReadyAsync();
        return board;
    }

    /// <summary>Creates a fresh project + board and opens it. Each test gets its own so they can run in parallel.</summary>
    public async Task<BoardPage> CreateAndOpenFreshBoardAsync()
    {
        var project = TestSettings.Unique("Project");
        var board = TestSettings.Unique("Board");
        // Board codes must be 1-5 upper-case letters (no digits).
        var code = new string(Enumerable.Range(0, 4).Select(_ => (char)('A' + Random.Shared.Next(26))).ToArray());

        await GotoAsync();
        await CreateProjectAsync(project);
        await CreateBoardAsync(project, board, code);
        return await OpenBoardAsync(board);
    }
}
