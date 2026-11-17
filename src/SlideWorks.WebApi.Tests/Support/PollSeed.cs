using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Realtime.Presenting;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Realtime;

namespace SlideWorks.WebApi.Tests.Support;

// A live deck with a poll slide, made through the public API: create_deck and add_slide as an agent (slide 1 a quote,
// slide 2 the poll, which gets its default settings row), Go live as the owner, and the presenter put on the poll slide
// through SettablePresentations. The app must run with SettablePresentations as its IPresentations (PollTestFixture).
// The slide's id isn't on any route, so it is read back from the database; nothing here writes a row by hand.
public static class PollSeed
{
    public const string QUESTION = "How big was your last merge request?";

    public static readonly string[] OPTIONS = ["Under 50 lines", "50 to 200 lines", "Over 200 lines"];

    public static async Task<SeededPoll> CreateAsync(
        WebApplicationFactory<ApiMarker> factory,
        HttpClient owner,
        TestUser ownerUser,
        string question = QUESTION,
        string[]? options = null,
        bool notify = false)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(factory, ownerUser);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await DeckSeed.AddQuoteAsync(agent, deck, "Small batches ship sooner");
        var added = await DeckSeed.AddSlideAsync(agent, deck, "poll", JsonSerializer.Serialize(new { question, options = options ?? OPTIONS }));
        var slide = added.GetProperty("slide").GetInt32();

        Guid slideId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            slideId = await db.Slides.AsNoTracking()
                .Where(s => s.DeckId == deck && s.DeletedAt == null && s.Position == slide - 1)
                .Select(s => s.Id)
                .SingleAsync();
        }

        var response = await owner.PostAsync($"/api/decks/{deck}/live", null);
        response.EnsureSuccessStatusCode();
        var live = await response.Content.ReadFromJsonAsync<JsonElement>();
        var sessionId = Guid.Parse(live.GetProperty("sessionId").GetString()!);
        var code = live.GetProperty("code").GetString()!;

        if (notify)
        {
            await MoveAsync(factory.Services, deck, slide);
        }
        else
        {
            factory.Services.GetRequiredService<SettablePresentations>().Set(SettablePresentations.At(deck, slide));
        }
        return new SeededPoll(deck, slide, slideId, sessionId, code, OtherSlide: 1);
    }

    // What SW-4's registry does on an accepted change: record the state, then tell every observer.
    public static async Task MoveAsync(IServiceProvider services, string deckId, int slide, int step = 0)
    {
        var state = SettablePresentations.At(deckId, slide, step);
        services.GetRequiredService<SettablePresentations>().Set(state);
        foreach (var observer in services.GetServices<IPresentationObserver>())
        {
            await observer.OnChangedAsync(state);
        }
    }
}
