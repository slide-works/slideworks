using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using SlideWorks.WebApi.Features.Templates;

namespace SlideWorks.WebApi.Tests.Templates;

// The contract over the real service, on a real database with no uploaded template in it: what it answers about the
// built-ins is what every ITemplates answers. What it adds for uploaded templates is in TemplatesDatabaseTests.
[TestFixture]
public class TemplatesDatabaseContractTests : TemplatesContractTests
{
    private readonly List<AsyncServiceScope> _scopes = [];
    private string _dbPath = null!;
    private WebApplicationFactory<ApiMarker> _factory = null!;

    [OneTimeSetUp]
    public void CreateApp()
    {
        _dbPath = TestAppFactory.NewDatabasePath();
        _factory = TestAppFactory.Create(_dbPath, new FakeTimeProvider(DateTimeOffset.FromUnixTimeSeconds(TimeProvider.System.GetUtcNow().ToUnixTimeSeconds())));
    }

    [OneTimeTearDown]
    public async Task DisposeApp()
    {
        foreach (var scope in _scopes)
        {
            await scope.DisposeAsync();
        }
        await _factory.DisposeAsync();
        TestAppFactory.DeleteDatabase(_dbPath);
    }

    protected override ITemplates Create()
    {
        var scope = _factory.Services.CreateAsyncScope();
        _scopes.Add(scope);
        return scope.ServiceProvider.GetRequiredService<ITemplates>();
    }

    [Test]
    public void Create_WhenResolvedFromTheHost_ShouldBeTheDatabaseService()
    {
        var templates = Create();

        templates.ShouldBeOfType<SlideWorks.WebApi.Features.Templates.Templates>();
    }
}
