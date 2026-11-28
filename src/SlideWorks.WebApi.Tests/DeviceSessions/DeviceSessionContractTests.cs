using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.DeviceSessions;
using SlideWorks.WebApi.Tests.Realtime;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.DeviceSessions;

// The device-session seams other tasks build on (K4, K5, K7): the row seed keeps only the secret's hash, and HubReceiver
// carries a guest id as the hub's guest query. The store's behaviour is proven through the routes that use it.
[TestFixture]
public class DeviceSessionContractTests : ApiTestFixture
{
    [Test]
    public async Task DeviceSessionRowSeed_WhenSeeded_ShouldStoreOnlyTheHash()
    {
        var (session, secret) = await new DeviceSessionRowSeed(Factory.Services).CreateAsync(TestUser.Alex);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.DeviceSessions.AsNoTracking().SingleAsync(s => s.Id == session.Id);
        secret.ShouldMatch("^swd_[A-Za-z0-9_-]{43}$");
        stored.SecretHash.ShouldBe(DeviceSecrets.Hash(secret));
        stored.SecretHash.ShouldMatch("^[0-9a-f]{64}$");
        (await db.DeviceSessions.AnyAsync(s => s.SecretHash == secret || s.DeviceName == secret)).ShouldBeFalse();
        stored.DeviceName.ShouldBe(DeviceSessionRowSeed.DEVICE_NAME);
        stored.EndedAt.ShouldBeNull();
        stored.ExpiresAt.ShouldBe(stored.CreatedAt + TimeSpan.FromDays(90));
        (await scope.ServiceProvider.GetRequiredService<IDeviceSessions>().FindBySecretAsync(secret)).ShouldNotBeNull().Id.ShouldBe(session.Id);
    }

    [Test]
    public async Task HubReceiver_WhenGivenGuestQuery_ShouldPutItOnTheUrl()
    {
        var guest = Guid.NewGuid().ToString("N");

        await using var receiver = await HubReceiver.StartAsync(Factory, new HubReceiverOptions { Guest = guest });

        receiver.Url.AbsolutePath.ShouldBe("/hubs/deck");
        receiver.Url.Query.ShouldBe($"?guest={guest}");
        receiver.Connection.State.ShouldBe(HubConnectionState.Connected);
    }
}
