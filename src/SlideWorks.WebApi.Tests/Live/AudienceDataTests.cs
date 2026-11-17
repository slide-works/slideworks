using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Live;

namespace SlideWorks.WebApi.Tests.Live;

[TestFixture]
public class AudienceDataTests : ApiTestFixture
{
    [Test]
    public async Task CreateDeck_WhenCreated_ShouldHaveSixDigitJoinCode()
    {
        var first = await CreateDeckAsync("First");
        var second = await CreateDeckAsync("Second");

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var codes = await db.Decks.Where(d => d.Id == first || d.Id == second).Select(d => d.JoinCode).ToListAsync();
        codes.Count.ShouldBe(2);
        codes[0].ShouldNotBe(codes[1]);
        codes.ShouldAllBe(c => c.Length == 6 && c.All(char.IsAsciiDigit) && int.Parse(c) >= 100000 && int.Parse(c) <= 999999);
    }

    // The live routes that open sessions arrive with T0.2a, so the sessions here go in through the context, and the deck
    // is removed the only way the app removes one today: the row, with the database cascading to what hangs off it.
    [Test]
    public async Task DeleteDeck_WhenItHasSessions_ShouldRemoveThem()
    {
        var deckId = await CreateDeckAsync("With sessions");
        var otherDeckId = await CreateDeckAsync("Untouched");
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = Clock.GetUtcNow();
        var ended = new AudienceSession { Id = Guid.NewGuid(), DeckId = deckId, StartedAt = now, EndedAt = now.AddMinutes(30), PeakJoined = 2 };
        var open = new AudienceSession { Id = Guid.NewGuid(), DeckId = deckId, StartedAt = now.AddHours(1) };
        var other = new AudienceSession { Id = Guid.NewGuid(), DeckId = otherDeckId, StartedAt = now };
        db.AudienceSessions.AddRange(ended, open, other);
        db.AudienceJoins.AddRange(
            new AudienceJoin { SessionId = ended.Id, GuestId = "guest-a", JoinedAt = now },
            new AudienceJoin { SessionId = ended.Id, GuestId = "guest-b", JoinedAt = now },
            new AudienceJoin { SessionId = open.Id, GuestId = "guest-a", JoinedAt = now },
            new AudienceJoin { SessionId = other.Id, GuestId = "guest-a", JoinedAt = now });
        await db.SaveChangesAsync();

        await db.Decks.Where(d => d.Id == deckId).ExecuteDeleteAsync();

        (await db.AudienceSessions.CountAsync(s => s.DeckId == deckId)).ShouldBe(0);
        (await db.AudienceJoins.CountAsync(j => j.SessionId == ended.Id || j.SessionId == open.Id)).ShouldBe(0);
        (await db.AudienceSessions.CountAsync(s => s.DeckId == otherDeckId)).ShouldBe(1);
        (await db.AudienceJoins.CountAsync(j => j.SessionId == other.Id)).ShouldBe(1);
    }

    private async Task<string> CreateDeckAsync(string title)
    {
        var response = await Client.PostAsJsonAsync("/api/decks", new { title });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
    }
}
