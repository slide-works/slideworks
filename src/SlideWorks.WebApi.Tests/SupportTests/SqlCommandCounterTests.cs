using Microsoft.EntityFrameworkCore;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Tests.Support.ApiOnly;

namespace SlideWorks.WebApi.Tests.SupportTests;

[TestFixture]
public class SqlCommandCounterTests : ApiTestFixture
{
    private readonly SqlCommandCounter _counter = new();

    protected override Action<IServiceCollection>? ConfigureTestServices => _counter.AddTo;

    [Test]
    public async Task Count_WhenOneQueryRuns_ShouldBeOneAndKeepItsText()
    {
        _counter.Reset();

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Decks.CountAsync();
        }

        _counter.Count.ShouldBe(1);
        _counter.Texts.Single().ShouldContain("Decks");
    }

    [Test]
    public async Task Reset_WhenCommandsRan_ShouldForgetThem()
    {
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Decks.CountAsync();
        }
        _counter.Count.ShouldBeGreaterThan(0);

        _counter.Reset();

        _counter.Count.ShouldBe(0);
        _counter.Texts.ShouldBeEmpty();
    }
}
