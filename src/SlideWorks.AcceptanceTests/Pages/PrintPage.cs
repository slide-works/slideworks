namespace SlideWorks.AcceptanceTests.Pages;

// The print view at /print/{id}#token=…: every slide of the deck in one column, one section per slide, which the PDF engine
// saves as one page per slide. data-ready is "true" once fonts, images and word clouds have settled.
public sealed class PrintPage(IPage page)
{
    public static string PathFor(string deckId) => $"/print/{Uri.EscapeDataString(deckId)}";

    public ILocator Root => page.GetByTestId("print-deck");

    public ILocator Slides => page.GetByTestId("print-slide");

    // The section of slide {number}, counted from 1.
    public ILocator Slide(int number) => Slides.Nth(number - 1);

    // The poll slide in section {number}, with the results the print view drew.
    public PollSlide Poll(int number) => new(Slide(number));

    public async Task GotoAsync(string deckId, string token) => await page.GotoAsync($"{PathFor(deckId)}#token={token}");
}
