using Ticky.E2E.Infrastructure;
using Ticky.E2E.Pages;
using static Microsoft.Playwright.Assertions;

namespace Ticky.E2E.Tests;

/// <summary>
/// Ticky pushes board changes to every open copy of a board through a SignalR hub (UpdateHub).
/// These tests open the same board in two independent browser contexts - like two people on two laptops.
/// </summary>
[TestFixture]
[Category("Realtime")]
public class RealtimeSyncTests : TickyTest
{
    [Test]
    public async Task New_card_appears_on_a_second_open_copy_of_the_board_without_reload()
    {
        var first = await new HomePage(Page).CreateAndOpenFreshBoardAsync();
        await first.AddColumnAsync("Inbox");

        await using var secondContext = await Browser.NewContextAsync(ContextOptions());
        var secondPage = await secondContext.NewPageAsync();
        await secondPage.GotoAsync(Page.Url);
        var second = new BoardPage(secondPage);
        await second.WaitUntilReadyAsync();
        await Expect(second.Column("Inbox")).ToBeVisibleAsync();

        await first.AddCardAsync("Inbox", "Pushed over SignalR");

        await Expect(second.CardInColumn("Inbox", "Pushed over SignalR")).ToBeVisibleAsync(new() { Timeout = 15000 });
    }

    [Test]
    public async Task Moving_a_card_is_reflected_on_a_second_open_copy_of_the_board()
    {
        var first = await new HomePage(Page).CreateAndOpenFreshBoardAsync();
        await first.AddColumnAsync("Ready");
        await first.AddColumnAsync("Review");
        await first.AddCardAsync("Ready", "Review me");

        await using var secondContext = await Browser.NewContextAsync(ContextOptions());
        var secondPage = await secondContext.NewPageAsync();
        await secondPage.GotoAsync(Page.Url);
        var second = new BoardPage(secondPage);
        await second.WaitUntilReadyAsync();
        await Expect(second.CardInColumn("Ready", "Review me")).ToBeVisibleAsync();

        await first.DragCardAsync("Review me", "Review");
        await Expect(first.CardInColumn("Review", "Review me")).ToBeVisibleAsync();

        await Expect(second.CardInColumn("Review", "Review me")).ToBeVisibleAsync(new() { Timeout = 15000 });
    }
}
