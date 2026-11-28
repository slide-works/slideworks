using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.DeviceSessions;
using SlideWorks.WebApi.Tests.Support;
using DeviceSessionStore = SlideWorks.WebApi.Features.DeviceSessions.DeviceSessions;

namespace SlideWorks.WebApi.Tests.DeviceSessions;

// A person's signed-in devices from the web (K4): GET lists their live sessions newest first, none marked current for a
// cookie caller, and DELETE ends one of their own as webSignOut. Each test signs in its own fresh people, so the rows one
// test seeds never show in another's list.
[TestFixture]
public class DeviceListTests : ApiTestFixture
{
    private CapturingLoggerProvider _logs = null!;

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
        _logs = CapturingLoggerProvider.Replace<DeviceSessionStore>(services);

    private DeviceSessionRowSeed Seed => new(Factory.Services);

    [Test]
    public async Task List_WhenCookie_ShouldListLiveDevicesNewestFirstWithNoneCurrent()
    {
        var person = TestUser.NewApproved();
        using var client = ClientFor(person);
        await Seed.CreateAsync(person, "Older phone");
        Clock.Advance(TimeSpan.FromMinutes(5));
        var (ended, _) = await Seed.CreateAsync(person, "Ended phone");
        Clock.Advance(TimeSpan.FromMinutes(5));
        await Seed.CreateAsync(person, "Newer phone");
        await Seed.CreateAsync(TestUser.NewApproved(), "Someone else's phone");
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            (await scope.ServiceProvider.GetRequiredService<IDeviceSessions>().EndAsync(ended.Id, DeviceSessionEndReason.AppSignOut)).ShouldBeTrue();
        }

        var devices = (await client.GetFromJsonAsync<JsonElement>("/api/auth/device-sessions")).GetProperty("devices").EnumerateArray().ToList();

        devices.Select(d => d.GetProperty("deviceName").GetString()).ShouldBe(["Newer phone", "Older phone"]);
        devices.ShouldAllBe(d => !d.GetProperty("current").GetBoolean());
    }

    [Test]
    public async Task Revoke_WhenOwnDevice_ShouldEndItAsWebSignOut()
    {
        var person = TestUser.NewApproved();
        using var client = ClientFor(person);
        var (session, _) = await Seed.CreateAsync(person);

        using var response = await client.DeleteAsync($"/api/auth/device-sessions/{session.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        _logs.Lines.ShouldContain(line => line.EndsWith($"Device session {session.Id} ended: webSignOut", StringComparison.Ordinal));
        (await client.GetFromJsonAsync<JsonElement>("/api/auth/device-sessions")).GetProperty("devices").GetArrayLength().ShouldBe(0);
    }

    [Test]
    public async Task Revoke_WhenAnotherPersonsDevice_ShouldReturn404()
    {
        var owner = TestUser.NewApproved();
        var (session, _) = await Seed.CreateAsync(owner);
        using var stranger = ClientFor(TestUser.NewApproved());
        using var ownerClient = ClientFor(owner);

        using var response = await stranger.DeleteAsync($"/api/auth/device-sessions/{session.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).ShouldBe("""{"error":"Not found"}""");
        (await ownerClient.GetFromJsonAsync<JsonElement>("/api/auth/device-sessions")).GetProperty("devices").EnumerateArray()
            .Select(d => d.GetProperty("id").GetString()).ShouldBe([session.Id.ToString()]);
    }
}
