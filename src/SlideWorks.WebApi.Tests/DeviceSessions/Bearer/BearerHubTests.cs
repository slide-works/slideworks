using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.DeviceSessions;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Tests.Realtime;
using SlideWorks.WebApi.Tests.Sharing;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.DeviceSessions.Bearer;

// The device bearer on /hubs (K5): a phone's secret in the access_token query signs the connection in as its person, and
// ending the session drops the open connection. The decks are private, so joining one proves the connection is signed in.
[TestFixture]
public class BearerHubTests : ApiTestFixture
{
    private readonly List<HubReceiver> _receivers = [];

    private DeviceSessionRowSeed Seed => new(Factory.Services);

    [TearDown]
    public async Task DisconnectAsync()
    {
        foreach (var receiver in _receivers)
        {
            await receiver.DisposeAsync();
        }
        _receivers.Clear();
    }

    [Test]
    public async Task Hub_WhenBearerInQuery_ShouldJoinDeck()
    {
        var deck = await SharingApi.CreateDeckAsync(Client, "Phone deck");
        var (_, secret) = await Seed.CreateAsync(TestUser.Sam);
        var phone = await StartAsync(new HubReceiverOptions { AccessToken = secret });

        await phone.Connection.InvokeAsync("JoinDeck", deck);
        await Factory.Services.GetRequiredService<IDeckEvents>().PublishAsync(new DeckChanged(deck));

        (await phone.NextAsync()).GetProperty("deckId").GetString().ShouldBe(deck);
    }

    [Test]
    public async Task Hub_WhenSessionEnded_ShouldDropOpenConnection()
    {
        var (session, secret) = await Seed.CreateAsync(TestUser.Sam);
        var phone = await StartAsync(new HubReceiverOptions { Bearer = secret });
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        phone.Connection.Closed += _ =>
        {
            closed.TrySetResult();
            return Task.CompletedTask;
        };

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IDeviceSessions>().EndAsync(session.Id, DeviceSessionEndReason.WebSignOut);
        }

        (await Task.WhenAny(closed.Task, Task.Delay(TimeSpan.FromSeconds(5)))).ShouldBe(closed.Task, "the server should drop the connection");
        phone.Connection.State.ShouldBe(HubConnectionState.Disconnected);
    }

    private async Task<HubReceiver> StartAsync(HubReceiverOptions options)
    {
        var receiver = await HubReceiver.StartAsync(Factory, options);
        _receivers.Add(receiver);
        return receiver;
    }
}
