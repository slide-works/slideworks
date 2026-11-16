using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Realtime;

namespace SlideWorks.WebApi.Tests.Realtime;

// IDeckEvents as the app registers it: per-deck and all-deck subscriptions, order, and a failing handler.
[TestFixture]
[CancelAfter(30000)]
public class DeckEventsBusTests : ApiTestFixture
{
    private IDeckEvents Bus => Factory.Services.GetRequiredService<IDeckEvents>();

    [Test]
    public async Task Subscribe_WhenGivenADeck_ShouldDeliverOnlyThatDecksEventsInOrder()
    {
        var (deck, other) = (Guid.NewGuid(), Guid.NewGuid());
        var received = Channel.CreateUnbounded<DeckEvent>();
        using var subscription = Bus.Subscribe(deck, e => received.Writer.WriteAsync(e).AsTask());

        await Bus.PublishAsync(new DeckChanged(other));
        var first = new DeckChanged(deck);
        var second = new DeckChanged(deck);
        await Bus.PublishAsync(first);
        await Bus.PublishAsync(second);

        (await received.Reader.ReadAsync()).ShouldBeSameAs(first);
        (await received.Reader.ReadAsync()).ShouldBeSameAs(second);
    }

    [Test]
    public async Task Subscribe_WhenHandlerThrows_ShouldKeepDelivering()
    {
        var deck = Guid.NewGuid();
        var received = Channel.CreateUnbounded<DeckEvent>();
        var calls = 0;
        using var subscription = Bus.Subscribe(deck, e =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                throw new InvalidOperationException("first one fails");
            }
            return received.Writer.WriteAsync(e).AsTask();
        });

        await Bus.PublishAsync(new DeckChanged(deck));
        var second = new DeckChanged(deck);
        await Bus.PublishAsync(second);

        (await received.Reader.ReadAsync()).ShouldBeSameAs(second);
    }

    [Test]
    public async Task Dispose_WhenSubscriptionDisposed_ShouldStopDelivering()
    {
        var deck = Guid.NewGuid();
        var disposedReceived = Channel.CreateUnbounded<DeckEvent>();
        var witness = Channel.CreateUnbounded<DeckEvent>();
        var disposed = Bus.Subscribe(deck, e => disposedReceived.Writer.WriteAsync(e).AsTask());
        using var kept = Bus.Subscribe(deck, e => witness.Writer.WriteAsync(e).AsTask());

        disposed.Dispose();
        await Bus.PublishAsync(new DeckChanged(deck));

        await witness.Reader.ReadAsync();
        disposedReceived.Reader.TryRead(out _).ShouldBeFalse();
    }
}
