using Ticky.E2E.Infrastructure;
using Ticky.E2E.Pages;
using static Microsoft.Playwright.Assertions;

namespace Ticky.E2E.Tests;

[TestFixture]
[Category("Cards")]
public class CardWorkflowTests : TickyTest
{
    private BoardPage _board = null!;

    [SetUp]
    public async Task CreateBoard() => _board = await new HomePage(Page).CreateAndOpenFreshBoardAsync();

    [Test]
    public async Task Columns_and_cards_can_be_created_and_counted()
    {
        await _board.AddColumnAsync("To do");
        await _board.AddColumnAsync("Doing");

        await _board.AddCardAsync("To do", "Write release notes");
        await _board.AddCardAsync("To do", "Fix login redirect");

        await Expect(_board.CardList("To do").Locator(".task-card")).ToHaveCountAsync(2);
        await Expect(_board.CardCounter("To do")).ToHaveTextAsync("2");
        await Expect(_board.CardCounter("Doing")).ToHaveTextAsync("0");
    }

    [Test]
    public async Task Cards_survive_a_page_reload()
    {
        await _board.AddColumnAsync("Backlog");
        await _board.AddCardAsync("Backlog", "Persisted card");

        await Page.ReloadAsync();
        await _board.WaitUntilReadyAsync();

        await Expect(_board.CardInColumn("Backlog", "Persisted card")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Column_card_limit_hides_new_card_button_when_full()
    {
        await _board.AddColumnAsync("WIP", maxCards: 1);

        await Expect(_board.CardCounter("WIP")).ToHaveTextAsync("0/1");
        await _board.AddCardAsync("WIP", "Only one at a time");

        await Expect(_board.CardCounter("WIP")).ToHaveTextAsync("1/1");
        await Expect(_board.NewCardButton("WIP")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Label_added_in_card_details_shows_on_the_board_and_is_saved()
    {
        // Arrange: a card on a fresh board (every new board starts with Bug / Feature / Enhancement labels).
        await _board.AddColumnAsync("Backlog");
        await _board.AddCardAsync("Backlog", "Card with a label");

        // Act: open the card and pick the "Bug" label.
        await _board.OpenCardAsync("Card with a label");
        await _board.AddLabelToOpenCardAsync("Bug");

        // Assert 1: the details window lists the label.
        await Expect(BoardPage.LabelChip(_board.Dialog, "Bug")).ToBeVisibleAsync();

        await _board.CloseCardAsync();

        // Assert 2: after a reload (so it really came from the database) the card on the board shows it.
        await Page.ReloadAsync();
        await _board.WaitUntilReadyAsync();
        await Expect(BoardPage.LabelChip(_board.CardInColumn("Backlog", "Card with a label"), "Bug")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Clicking_a_card_opens_its_details()
    {
        await _board.AddColumnAsync("Ideas");
        await _board.AddCardAsync("Ideas", "Card with details");

        await _board.Card("Card with details").Locator("h5").ClickUntilVisibleAsync(_board.Dialog);

        await Expect(_board.Dialog).ToContainTextAsync("Card with details");
    }
}
