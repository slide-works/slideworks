using System.Text.Json;
using System.Text.RegularExpressions;
using SlideWorks.AcceptanceTests.Journeys.Rebuild;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.AcceptanceTests.Support;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests.Journeys;

// SW-50 rebuild check, slides 23–44 of the 44-slide test deck, in made-up Northwind words at every slot's maximum (except the slots marked "shortened" in SecondHalfSlides.cs), on a template
// an agent uploaded a font to (the acceptance-test app stands in for prod, whose deck and brand are private). The slides come
// from Rebuild/SecondHalfSlides.cs; the test keeps every one of them as text, accepted without a warning, inside its frame,
// with its callout and its steps.
public class Sw50RebuildSecondHalfJourneyTests : AcceptanceTest
{
    private const string PROBE_IMAGES = "slide => slide.querySelectorAll('img').length";

    private const string FACE_LOADED = """
        family => [...document.fonts].some(f => f.family.replace(/["']/g, '') === family && f.status === 'loaded')
            && document.fonts.check(`16px "${family}"`)
        """;

    [Test]
    public async Task Agent_WhenRebuildingSlidesTwentyThreeToFortyFour_ShouldKeepEverySlideAsText()
    {
        await SignIn.AsAsync(Page, TestUser.Sam);
        await using var agent = await AgentSeed.ConnectAsync(TestUser.Sam);
        var deck = await RebuildDeck.MakeAsync(agent, Page, $"Northwind Studio {Guid.NewGuid():N}"[..30], "Northwind Rebuild");

        var slides = SecondHalfSlides.ALL;
        var problems = new List<string>();
        foreach (var slide in slides)
        {
            var args = new Dictionary<string, object?>
            {
                ["deck"] = deck,
                ["layout"] = slide.Layout,
                ["slots"] = JsonSerializer.SerializeToElement(slide.Slots(string.Empty)),
            };
            if (slide.Surface is { } surface)
            {
                args["surface"] = surface;
            }
            if (slide.Part is { } part)
            {
                args["part"] = part;
            }
            var (refused, text) = await RebuildDeck.TryAsync(agent, "add_slide", args);
            if (refused)
            {
                problems.Add($"slide {slide.Number} ({slide.Layout}) refused: {text}");
                continue;
            }
            var added = JsonDocument.Parse(text).RootElement;
            if (added.GetProperty("warnings").GetArrayLength() != 0)
            {
                problems.Add($"slide {slide.Number} ({slide.Layout}) warned: {added.GetProperty("warnings")}");
            }
            if (added.GetProperty("steps").GetInt32() != slide.Steps)
            {
                problems.Add($"slide {slide.Number} ({slide.Layout}) has {added.GetProperty("steps").GetInt32()} steps, expected {slide.Steps}");
            }
        }
        problems.ShouldBeEmpty();
        (await AgentSeed.CallAsync(agent, "get_deck", new() { ["deck"] = deck })).GetProperty("slides").GetArrayLength().ShouldBe(slides.Count);

        var player = new PlayerPage(Page);
        await Page.SetViewportSizeAsync(1920, 1080);
        await player.GotoAsync(deck);
        await Expect(player.Player).ToBeVisibleAsync();
        await Expect(player.Count).ToHaveTextAsync($"1 / {slides.Count}");
        await player.FontsReadyAsync();

        for (var i = 0; i < slides.Count; i++)
        {
            var (original, layout, _, _, isPhoto, callouts, steps, surface, _) = slides[i];
            var number = i + 1;
            var label = $"slide {original} ({layout}, deck position {number})";
            await Expect(player.Count).ToHaveTextAsync($"{number} / {slides.Count}");
            await Expect(player.Layer(number)).ToHaveAttributeAsync("data-active", "true");
            await Expect(player.Slide(number)).ToHaveAttributeAsync("data-layout", layout);

            // Every step of the slide is taken; the URL hash names the step the player stands on.
            for (var step = 1; step <= steps; step++)
            {
                await Page.Keyboard.PressAsync("ArrowRight");
                await Expect(Page).ToHaveURLAsync(new Regex($"#{number}\\.{step}$"));
            }

            // The probe has something to measure: the slide drew its text.
            await Expect(player.Slide(number)).ToBeVisibleAsync();
            (await player.Slide(number).EvaluateAsync<string>("slide => slide.innerText")).Trim().Length.ShouldBeGreaterThan(40, label);
            var overflow = await Eventually.ReadAsync(() => player.OverflowAsync(number), found => found.Length == 0);
            overflow.ShouldBeEmpty(label);

            var images = await player.Slide(number).EvaluateAsync<int>(PROBE_IMAGES);
            if (isPhoto)
            {
                images.ShouldBeGreaterThan(0, $"{label}: the probe finds the image on the photo slide");
            }
            else
            {
                images.ShouldBe(0, $"{label}: a slide that is not a photo draws no img");
            }

            // A slide with no Surface is light, except the layouts that draw dark by their own design (dividers and the closing slide).
            var expected = surface ?? (layout is "divider" or "close" ? "dark" : "light");
            (await new SlideProbe(Page, number).SurfaceAsync()).ShouldBe(expected, $"{label}: should draw the {expected} surface");

            var drawnCallouts = player.Slide(number).GetByTestId("block-callout");
            await Expect(drawnCallouts).ToHaveCountAsync(callouts);
            if (callouts > 0)
            {
                await Expect(drawnCallouts.First).ToBeVisibleAsync();
            }

            if (number < slides.Count)
            {
                await Page.Keyboard.PressAsync("ArrowRight");
            }
        }

        // The slides draw in the uploaded family, and that face is loaded.
        await player.GotoAsync(deck);
        await Expect(player.Player).ToBeVisibleAsync();
        await player.FontsReadyAsync();
        var drawn = await player.Slide(1).GetByTestId("cards-title").First.EvaluateAsync<string>("el => getComputedStyle(el).fontFamily");
        drawn.Trim().Replace("\"", "'").ShouldContain(RebuildDeck.FAMILY);
        var loaded = await Eventually.ReadAsync(() => Page.EvaluateAsync<bool>(FACE_LOADED, RebuildDeck.FAMILY), ok => ok);
        loaded.ShouldBeTrue();
    }
}
