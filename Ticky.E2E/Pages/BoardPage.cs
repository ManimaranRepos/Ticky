using Ticky.E2E.Infrastructure;
using static Microsoft.Playwright.Assertions;

namespace Ticky.E2E.Pages;

/// <summary>/boards/{id} - the kanban board.</summary>
public class BoardPage(IPage page)
{
    public IPage Page => page;
    public ILocator NewColumnButton => page.GetByTitle("Add new column");
    public ILocator Dialog => page.GetByRole(AriaRole.Dialog);

    /// <summary>A column container (div.board-column) identified by its heading.</summary>
    public ILocator Column(string name) =>
        page.Locator(".board-column").Filter(new() { Has = page.Locator("h6", new() { HasTextString = name }) });

    /// <summary>The Sortable list inside a column that holds the cards (id="column_{id}").</summary>
    public ILocator CardList(string columnName) => Column(columnName).Locator("[id^='column_']");

    public ILocator Card(string cardName) => page.Locator(".task-card").Filter(new() { HasTextString = cardName });

    public ILocator CardInColumn(string columnName, string cardName) =>
        CardList(columnName).Locator(".task-card").Filter(new() { HasTextString = cardName });

    public ILocator NewCardButton(string columnName) => Column(columnName).GetByTitle("New card");

    /// <summary>The "3" or "1/1" badge next to the column name.</summary>
    public ILocator CardCounter(string columnName) => Column(columnName).Locator("span.rounded-full").First;

    public ILocator Notification(string text) => page.GetByText(text);

    public async Task WaitUntilReadyAsync()
    {
        await page.WaitForBlazorAsync();
        await Expect(NewColumnButton).ToBeVisibleAsync();
    }

    public async Task AddColumnAsync(string name, int maxCards = 0, bool finished = false)
    {
        await NewColumnButton.ClickUntilVisibleAsync(Dialog.GetByRole(AriaRole.Heading, new() { Name = "New column" }));
        await Dialog.GetByPlaceholder("e.g. In progress").FillAndCommitAsync(name);

        if (maxCards > 0)
            await Dialog.GetByPlaceholder("0").FillAndCommitAsync(maxCards.ToString());

        if (finished)
            await Dialog.Locator("input[type='checkbox']").CheckAsync();

        await Dialog.GetByRole(AriaRole.Button, new() { Name = "Submit" }).ClickAsync();
        await Expect(Column(name)).ToBeVisibleAsync();
        await Expect(Dialog).ToBeHiddenAsync();
    }

    public async Task AddCardAsync(string columnName, string cardName)
    {
        var form = Column(columnName).Locator(".new-card-form");
        await NewCardButton(columnName).ClickUntilVisibleAsync(form.Locator("textarea"));

        await form.Locator("textarea").FillAsync(cardName);
        await form.GetByRole(AriaRole.Button, new() { Name = "Create" }).ClickAsync();

        await Expect(CardInColumn(columnName, cardName)).ToBeVisibleAsync();
    }

    public Task DragCardAsync(string cardName, string targetColumn) =>
        page.SortableDragAsync(Card(cardName).Locator("h5"), CardList(targetColumn));
}
