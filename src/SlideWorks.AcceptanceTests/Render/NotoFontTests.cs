using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests.Render;

// SW-883: slides draw Thai and Japanese text in the Noto faces tokens.css imports. Inter and Plus Jakarta Sans have no
// such glyphs, so the browser walks the font stack to the Noto family and loads its subset for the characters on the slide.
public class NotoFontTests : AcceptanceTest
{
    [Test]
    public async Task Render_WhenThaiSlide_ShouldUseNotoSansThai()
    {
        await RenderQuoteAsync("การส่งมอบทีละน้อยทำให้เรียนรู้ได้เร็วขึ้น");

        await AssertFontLoadedAsync("Noto Sans Thai", "การส่งมอบ");
    }

    [Test]
    public async Task Render_WhenJapaneseSlide_ShouldUseNotoSansJp()
    {
        await RenderQuoteAsync("小さく出荷すれば、学びは早くなる。");

        await AssertFontLoadedAsync("Noto Sans JP", "小さく出荷");
    }

    private async Task RenderQuoteAsync(string quote)
    {
        await using var agent = await AgentSeed.ConnectAsync(TestUser.Sam);
        var deck = (await AgentSeed.CallAsync(agent, "create_deck", new() { ["title"] = "Noto" })).GetProperty("deck").GetString()!;
        await AgentSeed.AddQuotesAsync(TestUser.Sam, deck, quote);
        var preview = await AgentSeed.CallAsync(agent, "preview_deck", new() { ["deck"] = deck });

        await Page.GotoAsync(preview.GetProperty("render_url").GetString()!.Replace("{slide}", "1"));
        await Page.GetByTestId("render-slide").WaitForAsync();
    }

    // A family that no @font-face declares also passes document.fonts.check, so the face must be in the set and loaded.
    // The slide may draw in either weight (400 or 700), the only two tokens.css imports.
    private async Task AssertFontLoadedAsync(string family, string text)
    {
        await Page.WaitForFunctionAsync(
            "family => [...document.fonts].some(face => face.family.replaceAll(/['\"]/g, '') === family && face.status === 'loaded')",
            family);

        var checkedOk = await Page.EvaluateAsync<bool>("([family, text]) => [400, 700].some(weight => document.fonts.check(`${weight} 16px '${family}'`, text))", new object[] { family, text });

        checkedOk.ShouldBeTrue();
    }
}
