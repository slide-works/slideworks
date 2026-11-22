using System.Text.Json;
using SlideWorks.AcceptanceTests.Journeys.Rebuild;
using SlideWorks.AcceptanceTests.Support;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests.Journeys;

// SW-768: the slides the second-half rebuild (Sw50RebuildSecondHalfJourneyTests) had to shorten. At every slot's maximum add_slide
// refuses each of them; this keeps the refusal asserted (it names the slot and gives the fit in px) and asserts that the one
// smallest change listed in Rebuild/RefusedSlides.cs is accepted without a warning, with one step past it still refused.
public class Sw50RefusedSlidesJourneyTests : AcceptanceTest
{
    [Test]
    public async Task Agent_WhenAddingSlidesRefusedAtMaximum_ShouldBeAcceptedWithTheirSmallestChange()
    {
        await SignIn.AsAsync(Page, TestUser.Sam);
        await using var agent = await AgentSeed.ConnectAsync(TestUser.Sam);
        var deck = await RebuildDeck.MakeAsync(agent, Page, $"Northwind Studio {Guid.NewGuid():N}"[..30], "Northwind Rebuild");

        async Task<(bool Refused, string Text)> AddAsync(RefusedSlides.Case slide, object slots) =>
            await RebuildDeck.TryAsync(agent, "add_slide", new()
            {
                ["deck"] = deck,
                ["layout"] = slide.Layout,
                ["part"] = slide.Part,
                ["slots"] = JsonSerializer.SerializeToElement(slots),
            });

        var problems = new List<string>();
        foreach (var slide in RefusedSlides.ALL)
        {
            var label = $"slide {slide.Slide} ({slide.Layout}), lever: {slide.Lever}";
            var (refused, message) = await AddAsync(slide, slide.AtMaximum());
            if (!refused || !message.Contains(slide.RefusedAt) || !message.Contains("too much text to fit"))
            {
                problems.Add($"{label}: at the maximum it should be refused naming {slide.RefusedAt}, got refused={refused}: {message}");
            }

            var (leverRefused, leverText) = await AddAsync(slide, slide.WithLever());
            if (leverRefused)
            {
                problems.Add($"{label}: with the lever it should be accepted, got: {leverText}");
            }
            else if (JsonDocument.Parse(leverText).RootElement.GetProperty("warnings").GetArrayLength() != 0)
            {
                problems.Add($"{label}: accepted with a warning: {leverText}");
            }

            if (slide.OneStepPast is { } past && !(await AddAsync(slide, past())).Refused)
            {
                problems.Add($"{label}: one step past the lever should still be refused");
            }
        }
        problems.ShouldBeEmpty();
    }
}
