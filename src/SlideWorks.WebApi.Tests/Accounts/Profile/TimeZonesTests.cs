using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.Accounts.Profile;

namespace SlideWorks.WebApi.Tests.Accounts.Profile;

[TestFixture]
public class TimeZonesTests : ApiTestFixture
{
    [Test]
    public void List_WhenCalled_ShouldIncludeUtcAndEuropeLondon()
    {
        using var scope = Factory.Services.CreateScope();

        var ids = scope.ServiceProvider.GetRequiredService<ITimeZones>().List().Select(zone => zone.Id).ToList();

        ids.ShouldContain("UTC");
        ids.ShouldContain("Europe/London");
    }
}
