using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.Qa;

namespace SlideWorks.WebApi.Tests.Qa;

// The Q&A seams (Q5) as the app registers them, with no test doubles: each resolves, with the lifetime Q5 fixes. The seams
// that use the database are per request; the publisher, reactions, the seq source and the revisions are one instance for
// the app. The vote pump's lifetime is asserted with the pump.
[TestFixture]
public class QaSeamTests : ApiTestFixture
{
    [Test]
    public async Task PerRequestSeams_WhenAppStarts_ShouldResolveOncePerScope()
    {
        await using var first = Factory.Services.CreateAsyncScope();
        await using var second = Factory.Services.CreateAsyncScope();
        var seams = new[] { typeof(IQuestionAsking), typeof(IGuestQuestions), typeof(IQuestionModeration), typeof(IQaSettings), typeof(IQaRounds), typeof(IAudienceRoom) };

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
        var seams = new[] { typeof(IQuestionPublisher), typeof(IReactions), typeof(QaSeq), typeof(QaRevisions) };

        foreach (var seam in seams)
        {
            scope.ServiceProvider.GetRequiredService(seam).ShouldBeSameAs(Factory.Services.GetRequiredService(seam), seam.Name);
        }
    }
}
