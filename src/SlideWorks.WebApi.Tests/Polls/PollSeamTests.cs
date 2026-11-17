using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.Polls;

namespace SlideWorks.WebApi.Tests.Polls;

// The poll seams (V5) as the app registers them, with no test doubles: each resolves, with the lifetime V5 fixes. The
// database-backed seams are per request; the tally, the publisher and the seq source are one instance for the app.
[TestFixture]
public class PollSeamTests : ApiTestFixture
{
    [Test]
    public async Task PerRequestSeams_WhenAppStarts_ShouldResolveOncePerScope()
    {
        await using var first = Factory.Services.CreateAsyncScope();
        await using var second = Factory.Services.CreateAsyncScope();
        var seams = new[] { typeof(IPolls), typeof(IPollArrival), typeof(IPollAnswers), typeof(IAudienceActivity) };

        foreach (var seam in seams)
        {
            var inFirst = first.ServiceProvider.GetRequiredService(seam);
            first.ServiceProvider.GetRequiredService(seam).ShouldBeSameAs(inFirst, seam.Name);
            second.ServiceProvider.GetRequiredService(seam).ShouldNotBeSameAs(inFirst, seam.Name);
        }
    }

    [Test]
    public async Task SingletonSeams_WhenAppStarts_ShouldResolveOnceForTheApp()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var seams = new[] { typeof(IPollTallies), typeof(IPollPublisher), typeof(PollSeq) };

        foreach (var seam in seams)
        {
            scope.ServiceProvider.GetRequiredService(seam).ShouldBeSameAs(Factory.Services.GetRequiredService(seam), seam.Name);
        }
    }
}
