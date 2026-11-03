using Ticky.E2E.Infrastructure;
using Ticky.E2E.Pages;
using static Microsoft.Playwright.Assertions;

namespace Ticky.E2E.Tests;

[TestFixture]
[Category("DragAndDrop")]
public class DragAndDropTests : TickyTest
{
    private BoardPage _board = null!;

    [SetUp]
    public async Task CreateBoard() => _board = await new HomePage(Page).CreateAndOpenFreshBoardAsync();

    [Test]
    public async Task Card_can_be_dragged_to_another_column_and_the_move_is_saved()
    {
        await _board.AddColumnAsync("Ready");
        await _board.AddColumnAsync("Done", finished: true);
        await _board.AddCardAsync("Ready", "Ship it");

        await _board.DragCardAsync("Ship it", "Done");

        await Expect(_board.CardInColumn("Done", "Ship it")).ToBeVisibleAsync();
        await Expect(_board.CardInColumn("Ready", "Ship it")).ToHaveCountAsync(0);

        // The move must be persisted, not just a DOM change.
        await Page.ReloadAsync();
        await _board.WaitUntilReadyAsync();
        await Expect(_board.CardInColumn("Done", "Ship it")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Card_cannot_be_dragged_into_a_full_column()
    {
        await _board.AddColumnAsync("Ready");
        await _board.AddColumnAsync("WIP", maxCards: 1);
        await _board.AddCardAsync("Ready", "Waiting card");
        await _board.AddCardAsync("WIP", "Busy card");

        await _board.DragCardAsync("Waiting card", "WIP");

        await Expect(_board.CardInColumn("Ready", "Waiting card")).ToBeVisibleAsync();
        await Expect(_board.CardCounter("WIP")).ToHaveTextAsync("1/1");
    }
}
