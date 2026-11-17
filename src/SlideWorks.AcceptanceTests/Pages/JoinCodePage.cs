using static Microsoft.Playwright.Assertions;

namespace SlideWorks.AcceptanceTests.Pages;

// The keypad at /join (SW-5 J5): six digit boxes that go to /j/{code} on the sixth digit. The guest page shows it too when
// a code has no session. One member for every id in JOIN_CODE_TEST_IDS in src/SlideWorks.Client/src/guest/testIds.ts.
public sealed class JoinCodePage(IPage page)
{
    public const string PATH = "/join";

    public const int CODE_LENGTH = 6;

    public ILocator Root => page.GetByTestId("join-code-page");

    // The advice shown after a wrong or ended code, or too many wrong tries. Focus moves here.
    public ILocator Error => page.GetByTestId("join-code-error");

    // Box {position}, counted from 1 to 6.
    public ILocator Box(int position) => page.GetByTestId($"join-code-box-{position}");

    public async Task GotoAsync() => await page.GotoAsync(PATH);

    // Waits until the keypad has rendered, and fails the test if it doesn't.
    public async Task IsLoadedAsync() => await Expect(Root).ToBeVisibleAsync();

    // Types the digits one box at a time, as a guest does on a numeric keypad.
    public async Task TypeAsync(string code)
    {
        for (var position = 1; position <= code.Length && position <= CODE_LENGTH; position++)
            await Box(position).PressSequentiallyAsync(code[position - 1].ToString());
    }
}
