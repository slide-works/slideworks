using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Tests.Sharing;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Live;

// IAudienceSessions.AnyLiveAsync: one answer for a list of decks, live only while a session is open.
[TestFixture]
public class AnyLiveTests : ApiTestFixture
{
    [Test]
    public async Task AnyLive_WhenOneDeckIsLive_ShouldBeTrueAndWhenNoneIsShouldBeFalse()
    {
        var quiet = await SharingApi.CreateDeckAsync(Client);
        var live = await SharingApi.CreateDeckAsync(Client);
        (await Client.PostAsync($"/api/decks/{live}/live", null)).EnsureSuccessStatusCode();

        var both = await AnyLiveAsync([quiet, live]);
        var onlyQuiet = await AnyLiveAsync([quiet]);
        var none = await AnyLiveAsync([]);
        await Client.DeleteAsync($"/api/decks/{live}/live");
        var ended = await AnyLiveAsync([quiet, live]);

        both.ShouldBeTrue();
        onlyQuiet.ShouldBeFalse();
        none.ShouldBeFalse();
        ended.ShouldBeFalse();
    }

    private async Task<bool> AnyLiveAsync(string[] deckIds)
    {
        using var scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IAudienceSessions>().AnyLiveAsync(deckIds);
    }
}
