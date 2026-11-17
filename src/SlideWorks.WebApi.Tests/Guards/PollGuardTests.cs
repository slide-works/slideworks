using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using SlideWorks.Core.Slides;
using SlideWorks.WebApi.Features.Polls.Contracts;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Guards;

// Rules about polls that hold whatever the services do (V1, V5, D5, D10): what a guest can be sent never names a guest, the
// poll layout's slots are only the question and the options, only PollPublisher publishes poll events, and the activity
// allow-list the privacy tests use is poll-activity.json's.
[TestFixture]
public class PollGuardTests
{
    // The records a guest's phone receives: the GET body, the 409 body, the 200 body and the events on the audience group.
    private static readonly Type[] _guestReceives = [typeof(GuestPollView), typeof(StaleAnswer), typeof(MyAnswer), typeof(PollTally), typeof(PollActivity)];

    private static readonly Regex _guestIdName = new("guest|voter|cookie|participant", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex _pollEvent = new(@"\bPoll(Tally|Control)\b", RegexOptions.CultureInvariant);

    private static readonly Regex _publishesPollEvent = new(@"PublishAsync\(\s*new\s+Poll(Tally|Control)\b", RegexOptions.CultureInvariant);

    // PollPublisher publishes them; AudienceEventsRelay only forwards what reached the bus to the hub.
    private static readonly string[] _mayPublishPollEvents = ["PollPublisher.cs", "AudienceEventsRelay.cs"];

    [Test]
    public void GuestRecords_ShouldHaveNoMemberNamedLikeAGuestId()
    {
        var seen = new HashSet<Type>();
        var found = new List<string>();

        foreach (var type in _guestReceives)
        {
            Walk(type, seen, found);
        }

        seen.ShouldContain(typeof(PollActivity));
        found.ShouldBeEmpty();
    }

    [Test]
    public void PollLayout_ShouldHaveOnlyTheQuestionAndTheOptionsAsSlots()
    {
        var poll = LayoutCatalogue.Find("poll");

        poll.ShouldNotBeNull();
        poll.Slots.Fields.Select(f => f.Name).ShouldBe(["question", "options"], ignoreOrder: true);
    }

    [Test]
    public void PollEvents_ShouldBePublishedOnlyByPollPublisher()
    {
        var files = ProductionSource.Matching(["**/*.cs"]).Where(f => !_mayPublishPollEvents.Contains(Path.GetFileName(f), StringComparer.Ordinal)).ToList();

        files.ShouldNotBeEmpty();
        files.Where(file =>
        {
            var source = File.ReadAllText(file);
            return _publishesPollEvent.IsMatch(source) || (source.Contains("IAudienceEvents", StringComparison.Ordinal) && _pollEvent.IsMatch(source));
        }).ShouldBeEmpty();
    }

    [Test]
    public void PollActivityKeys_ShouldBeThePollActivityContractsKeys()
    {
        using var contract = JsonDocument.Parse(File.ReadAllText(Path.Combine(JsonShape.ScreensDirectory(), "poll-activity.json")));
        var samples = contract.RootElement.EnumerateObject().Select(p => p.Value).ToList();

        samples.ShouldNotBeEmpty();
        samples.ShouldAllBe(sample => PollActivityKeys.Of(sample).OrderBy(k => k, StringComparer.Ordinal).SequenceEqual(PollActivityKeys.All.OrderBy(k => k, StringComparer.Ordinal)));
    }

    // Every public property of the record and of every contract record it holds, by name.
    private static void Walk(Type type, HashSet<Type> seen, List<string> found)
    {
        if (type.Namespace != typeof(PollActivity).Namespace || !seen.Add(type))
        {
            return;
        }
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (_guestIdName.IsMatch(property.Name))
            {
                found.Add($"{type.Name}.{property.Name}");
            }
            var held = property.PropertyType.IsGenericType ? property.PropertyType.GetGenericArguments() : [property.PropertyType];
            foreach (var inner in held)
            {
                Walk(Nullable.GetUnderlyingType(inner) ?? inner, seen, found);
            }
        }
    }
}
