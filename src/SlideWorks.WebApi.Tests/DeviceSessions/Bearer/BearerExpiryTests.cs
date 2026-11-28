using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.DeviceSessions;
using SlideWorks.WebApi.Features.DeviceSessions.Data;
using DeviceSessionStore = SlideWorks.WebApi.Features.DeviceSessions.DeviceSessions;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.DeviceSessions.Bearer;

// A device session's lifetime under the bearer (K5): 90 days sliding from last use, touched at most once a minute, and an
// expired session ended as expired on first sight and never revived. Its own fixture, because it steps the clock.
[TestFixture]
public class BearerExpiryTests : ApiTestFixture
{
    private DeviceSessionRowSeed Seed => new(Factory.Services);

    [Test]
    public async Task Bearer_WhenExpired_ShouldAnswerSessionEndedAndNeverRevive()
    {
        var (_, secret) = await Seed.CreateAsync(TestUser.Sam);
        using var bearer = BearerClient(secret);
        Clock.Advance(DeviceSessionStore.LIFETIME + TimeSpan.FromMinutes(1));

        using var first = await bearer.GetAsync("/api/me");
        var ended = await StoredAsync(secret);
        Clock.Advance(TimeSpan.FromMinutes(5));
        using var again = await bearer.GetAsync("/api/me");

        (first.StatusCode, again.StatusCode).ShouldBe((HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized));
        (await ErrorOfAsync(first), await ErrorOfAsync(again)).ShouldBe(("session_ended", "session_ended"));
        ended.EndReason.ShouldBe(DeviceSessionEndReason.Expired);
        var later = await StoredAsync(secret);
        (later.EndedAt, later.ExpiresAt, later.LastUsedAt).ShouldBe((ended.EndedAt, ended.ExpiresAt, ended.LastUsedAt));
    }

    [Test]
    public async Task Bearer_WhenUsed_ShouldSlideExpiryAtMostOncePerMinute()
    {
        var (session, secret) = await Seed.CreateAsync(TestUser.Sam);
        using var bearer = BearerClient(secret);

        Clock.Advance(TimeSpan.FromSeconds(30));
        (await bearer.GetAsync("/api/me")).StatusCode.ShouldBe(HttpStatusCode.OK);
        var early = await StoredAsync(secret);
        Clock.Advance(TimeSpan.FromSeconds(31));
        (await bearer.GetAsync("/api/me")).StatusCode.ShouldBe(HttpStatusCode.OK);
        var touched = await StoredAsync(secret);

        (early.LastUsedAt, early.ExpiresAt).ShouldBe((session.LastUsedAt, session.ExpiresAt));
        var now = Clock.GetUtcNow();
        (touched.LastUsedAt, touched.ExpiresAt).ShouldBe((now, now + DeviceSessionStore.LIFETIME));
    }

    private HttpClient BearerClient(string secret)
    {
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secret);
        return client;
    }

    private async Task<DeviceSession> StoredAsync(string secret)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<IDeviceSessions>().FindBySecretAsync(secret)).ShouldNotBeNull();
    }

    private static async Task<string?> ErrorOfAsync(HttpResponseMessage response)
        => (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString();
}
