using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.SupportTests;

[TestFixture]
public class CallCounterTests
{
    [Test]
    public async Task WhenCalledAsync_WhenRecordedAfterTheWaitStarts_ShouldComplete()
    {
        var counter = new CallCounter();
        var wait = counter.WhenCalledAsync("deck", 2, TimeSpan.FromSeconds(5));

        counter.Record("deck");
        counter.Record("deck", failed: true);
        await wait;

        counter.CallsFor("deck").ShouldBe(2);
        counter.FailedFor("deck").ShouldBe(1);
        counter.CallsFor("other").ShouldBe(0);
    }

    [Test]
    public async Task WhenCalledAsync_WhenTooFewCalls_ShouldThrowTimeout()
    {
        var counter = new CallCounter();
        counter.Record("deck");

        var thrown = await Should.ThrowAsync<TimeoutException>(() => counter.WhenCalledAsync("deck", 2, TimeSpan.FromMilliseconds(50)));

        thrown.Message.ShouldContain("1 of 2");
    }

    [Test]
    public async Task WhenCalledAsync_WhenItTimesOut_ShouldNotKeepTheWaiter()
    {
        var counter = new CallCounter();

        await Should.ThrowAsync<TimeoutException>(() => counter.WhenCalledAsync("deck", 1, TimeSpan.FromMilliseconds(50)));

        counter.PendingWaits.ShouldBe(0);
    }
}
