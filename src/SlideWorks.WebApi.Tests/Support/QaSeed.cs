using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Polls;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Realtime;

namespace SlideWorks.WebApi.Tests.Support;

// A live deck with a Q&A slide and an open Q&A round (Q2), made through the public API: create_deck and add_slide as an
// agent (slide 1 a quote, slide 2 the qa layout), Go live as the owner, and the presenter put on the quote slide through
// SettablePresentations (no observer is told; a test moves the presenter itself). The app must run with
// SettablePresentations as its IPresentations (QaTestFixture). Go live opens the session's Q&A round once IQaRounds is
// real; when it is faked, or has opened none, QaRowSeed writes one, so a test gets a round either way. The slide ids and
// the round aren't on any route, so they are read back from the database.
public static class QaSeed
{
    public const string TITLE = "Questions from the room";

    public static async Task<SeededQa> CreateAsync(WebApplicationFactory<ApiMarker> factory, HttpClient owner, TestUser ownerUser)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(factory, ownerUser);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var quote = (await DeckSeed.AddQuoteAsync(agent, deck, "Small batches ship sooner")).GetProperty("slide").GetInt32();
        var qa = (await DeckSeed.AddSlideAsync(agent, deck, "qa", JsonSerializer.Serialize(new { title = TITLE }))).GetProperty("slide").GetInt32();
        var quoteSlideId = await SlideIdAsync(factory, deck, quote);
        var qaSlideId = await SlideIdAsync(factory, deck, qa);

        var response = await owner.PostAsync($"/api/decks/{deck}/live", null);
        response.EnsureSuccessStatusCode();
        var live = await response.Content.ReadFromJsonAsync<JsonElement>();
        var sessionId = Guid.Parse(live.GetProperty("sessionId").GetString()!);
        var code = live.GetProperty("code").GetString()!;

        factory.Services.GetRequiredService<SettablePresentations>().Set(SettablePresentations.At(deck, quote));

        var roundId = await OpenRoundIdAsync(factory, sessionId) ?? (await new QaRowSeed(factory.Services).RoundAsync(deck, sessionId)).Id;
        return new SeededQa(deck, sessionId, code, roundId, quote, quoteSlideId, qa, qaSlideId);
    }

    private static async Task<Guid> SlideIdAsync(WebApplicationFactory<ApiMarker> factory, string deckId, int slide)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Slides.AsNoTracking()
            .Where(s => s.DeckId == deckId && s.DeletedAt == null && s.Position == slide - 1)
            .Select(s => s.Id)
            .SingleAsync();
    }

    private static async Task<Guid?> OpenRoundIdAsync(WebApplicationFactory<ApiMarker> factory, Guid sessionId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.PollRounds.AsNoTracking()
            .Where(r => r.SessionId == sessionId && r.Kind == PollKind.Qa && r.ClosedAt == null)
            .Select(r => (Guid?)r.Id)
            .SingleOrDefaultAsync();
    }
}
