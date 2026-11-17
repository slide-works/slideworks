using Microsoft.AspNetCore.Http;
using SlideWorks.WebApi.Features.Live;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Realtime.Presenting;

namespace SlideWorks.WebApi.Tests.Guards;

// The relay runs from SW-4's registry and from deck events, with no request and no caller (S1b). So neither it nor anything
// it resolves may take ICurrentActor or IHttpContextAccessor: they would see nobody, or throw. The graph is walked through
// constructor parameters from the relay and from each implementation it resolves per call.
[TestFixture]
public class AudienceRelayGuardTests
{
    private static readonly Type[] _forbidden = [typeof(ICurrentActor), typeof(IHttpContextAccessor)];

    [Test]
    public void Relay_ShouldNotDependOnTheCurrentActorOrTheHttpContext()
    {
        var assembly = typeof(AudienceRelay).Assembly;
        var roots = new[] { typeof(AudienceRelay), typeof(AudienceFeed), typeof(AudienceRelayObserver), typeof(AudienceSessions) }
            .Concat(assembly.GetTypes().Where(t => t is { IsAbstract: false, IsInterface: false } && (typeof(IPresentations).IsAssignableFrom(t) || typeof(IAudienceSessions).IsAssignableFrom(t))))
            .Distinct()
            .ToList();
        var seen = new HashSet<Type>();
        var found = new List<string>();

        foreach (var root in roots)
        {
            Walk(root, root.Name, assembly, seen, found);
        }

        seen.ShouldContain(typeof(AudienceSessions));
        found.ShouldBeEmpty();
    }

    private static void Walk(Type type, string path, System.Reflection.Assembly assembly, HashSet<Type> seen, List<string> found)
    {
        if (_forbidden.Contains(type))
        {
            found.Add(path);
            return;
        }
        if (type.Assembly != assembly || !seen.Add(type))
        {
            return;
        }
        var implementations = type.IsInterface || type.IsAbstract
            ? assembly.GetTypes().Where(t => t is { IsAbstract: false, IsInterface: false } && type.IsAssignableFrom(t))
            : [type];
        foreach (var implementation in implementations)
        {
            foreach (var parameter in implementation.GetConstructors().SelectMany(c => c.GetParameters()))
            {
                Walk(parameter.ParameterType, $"{path} -> {parameter.ParameterType.Name}", assembly, seen, found);
            }
        }
    }
}
