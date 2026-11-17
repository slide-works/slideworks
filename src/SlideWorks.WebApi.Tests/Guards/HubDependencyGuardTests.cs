using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Realtime;

namespace SlideWorks.WebApi.Tests.Guards;

// Under long polling a hub call runs after the request that opened it has finished, so the request's caller is anonymous
// and its HttpContext is null inside a hub call (SW-205 review). The hub reads its caller from Context.User and passes it
// on, so nothing the hub resolves, however deep, may depend on ICurrentActor or IHttpContextAccessor.
[TestFixture]
public class HubDependencyGuardTests : ApiTestFixture
{
    private static readonly Type[] _forbidden = [typeof(ICurrentActor), typeof(IHttpContextAccessor)];

    [Test]
    public void DeckHub_WhenResolved_ShouldNotDependOnTheRequestCallerAnywhereInItsGraph()
    {
        using var scope = Factory.Services.CreateScope();
        var constructor = typeof(DeckHub).GetConstructors().Single();
        var visited = new HashSet<Type>();
        var offenders = new List<string>();

        foreach (var parameter in constructor.GetParameters())
        {
            Walk(scope.ServiceProvider, parameter.ParameterType, typeof(DeckHub), visited, offenders);
        }

        visited.ShouldContain(typeof(AudienceRoster));
        offenders.ShouldBeEmpty();
    }

    // Visits the registered implementation of a service type and the services its constructor takes, in SlideWorks code. A
    // framework type is a leaf, unless it is one of the forbidden ones.
    private static void Walk(IServiceProvider services, Type serviceType, Type requiredBy, HashSet<Type> visited, List<string> offenders)
    {
        if (_forbidden.Contains(serviceType))
        {
            offenders.Add($"{requiredBy.Name} -> {serviceType.Name}");
            return;
        }
        var implementation = services.GetService(serviceType)?.GetType();
        if (implementation?.Namespace?.StartsWith("SlideWorks", StringComparison.Ordinal) != true || !visited.Add(implementation))
        {
            return;
        }
        foreach (var constructor in implementation.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
        {
            foreach (var parameter in constructor.GetParameters())
            {
                Walk(services, parameter.ParameterType, implementation, visited, offenders);
            }
        }
    }
}
