using System.Reflection;
using System.Text.RegularExpressions;
using SlideWorks.Core.Qa;
using SlideWorks.WebApi.Features.Qa.Contracts;

namespace SlideWorks.WebApi.Tests.Guards;

// Rules about Q&A that hold whatever the services do (Q4, Q5, Q7): only QuestionPublisher names questionTally and
// questionControl in its code, only Reactions names the reactions event (AudienceEventsRelay may name all three, since it
// routes them; each record's own file and AudienceEvent.cs declare them), and no Q&A record has a member named like a
// guest's id. Naming the record at all is the rule, comments aside, so a target-typed new(...) can't slip past it. The
// member rule covers every record in the Q&A contracts and the Core types they hold, the presenter's and the report's too,
// since none of them may say which guest asked or upvoted.
[TestFixture]
public class QaGuardTests
{
    private static readonly Regex _guestIdName = new("guest|voter|cookie|participant|asker", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex _questionEvent = new(@"\bQuestion(Tally|Control)\b", RegexOptions.CultureInvariant);

    private static readonly Regex _reactionsEvent = new(@"\bReactionsBurst\b", RegexOptions.CultureInvariant);

    private static readonly Regex _comment = new(@"/\*.*?\*/|//[^\n]*", RegexOptions.Singleline | RegexOptions.CultureInvariant);

    private static readonly string[] _mayNameQuestionEvents = ["QuestionPublisher.cs", "AudienceEventsRelay.cs", "QuestionTally.cs", "QuestionControl.cs", "AudienceEvent.cs"];

    private static readonly string[] _mayNameReactionsEvents = ["Reactions.cs", "AudienceEventsRelay.cs", "ReactionsBurst.cs", "AudienceEvent.cs"];

    [Test]
    public void QuestionEvents_ShouldBeNamedOnlyByQuestionPublisher()
    {
        var files = ProductionSource.Matching(["**/*.cs"]);

        files.ShouldNotBeEmpty();
        files.Where(file => !_mayNameQuestionEvents.Contains(Path.GetFileName(file), StringComparer.Ordinal) && NamesQuestionEvent(File.ReadAllText(file)))
            .ShouldBeEmpty();
    }

    [Test]
    public void ReactionsEvents_ShouldBeNamedOnlyByReactions()
    {
        var files = ProductionSource.Matching(["**/*.cs"]);

        files.ShouldNotBeEmpty();
        files.Where(file => !_mayNameReactionsEvents.Contains(Path.GetFileName(file), StringComparer.Ordinal) && NamesReactionsEvent(File.ReadAllText(file)))
            .ShouldBeEmpty();
    }

    // The event rules bite: each of these is a file that makes, publishes or holds a Q&A event itself.
    [TestCase("await events.PublishAsync(new QuestionTally(deckId, round, 4, []));")]
    [TestCase("var control = new QuestionControl (deckId, view);")]
    [TestCase("public sealed class Pump(IAudienceEvents events) { QuestionTally Make() => new(\"d\", \"r\", 1, []); }")]
    [TestCase("public sealed class Pump { QuestionControl Make() => new(\"d\", view); }")]
    public void QuestionGuard_WhenSourceNamesAQuestionEvent_ShouldFlagIt(string source)
    {
        NamesQuestionEvent(source).ShouldBeTrue();
    }

    [TestCase("await events.PublishAsync(new ReactionsBurst(deckId, 3, counts));")]
    [TestCase("public sealed class Taps(IAudienceEvents events) { ReactionsBurst Make() => new(\"d\", 1, ReactionCounts.Zero); }")]
    [TestCase("public sealed class Taps { ReactionsBurst Make() => new(\"d\", 1, ReactionCounts.Zero); }")]
    public void ReactionsGuard_WhenSourceNamesAReactionsEvent_ShouldFlagIt(string source)
    {
        NamesReactionsEvent(source).ShouldBeTrue();
    }

    // And they leave alone what the Q&A tasks must write: a call to the publisher, a view that holds the presenter's control
    // view, and a comment that names an event.
    [TestCase("public sealed class QuestionModeration(IQuestionPublisher publisher) { Task Go() => publisher.PublishAsync(deckId); QaControlView? View; }")]
    [TestCase("public sealed class Pump(IQuestionPublisher publisher) { Task Go() => publisher.PublishAsync(deckId); }")]
    [TestCase("// The publisher sends QuestionTally and QuestionControl; Reactions sends ReactionsBurst.\npublic sealed class Pump { }")]
    [TestCase("/* QuestionTally, QuestionControl,\n   ReactionsBurst */ public sealed class Pump { }")]
    public void QuestionGuard_WhenSourceOnlyCallsThePublisherOrComments_ShouldNotFlagIt(string source)
    {
        NamesQuestionEvent(source).ShouldBeFalse();
        NamesReactionsEvent(source).ShouldBeFalse();
    }

    [Test]
    public void QaRecords_ShouldHaveNoMemberNamedLikeAGuestId()
    {
        var seen = new HashSet<Type>();
        var found = new List<string>();

        foreach (var type in typeof(GuestQuestionsView).Assembly.GetTypes().Where(t => t.IsPublic && t.Namespace == typeof(GuestQuestionsView).Namespace))
        {
            Walk(type, seen, found);
        }

        seen.ShouldContain(typeof(GuestQuestion));
        seen.ShouldContain(typeof(QuestionTally));
        seen.ShouldContain(typeof(QaRoom));
        found.ShouldBeEmpty();
    }

    // The member rule bites on each word.
    [TestCase("GuestId")]
    [TestCase("VoterIds")]
    [TestCase("CookieValue")]
    [TestCase("Participant")]
    [TestCase("Asker")]
    public void MemberGuard_WhenANameSaysWhoAsked_ShouldFlagIt(string member)
    {
        _guestIdName.IsMatch(member).ShouldBeTrue();
    }

    private static bool NamesQuestionEvent(string source) => _questionEvent.IsMatch(_comment.Replace(source, " "));

    private static bool NamesReactionsEvent(string source) => _reactionsEvent.IsMatch(_comment.Replace(source, " "));

    // Every public property of the record and of every Q&A record it holds (in the contracts or in Core), by name.
    private static void Walk(Type type, HashSet<Type> seen, List<string> found)
    {
        if ((type.Namespace != typeof(GuestQuestionsView).Namespace && type.Namespace != typeof(QaRoom).Namespace) || !seen.Add(type))
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
        foreach (var nested in type.GetNestedTypes(BindingFlags.Public))
        {
            Walk(nested, seen, found);
        }
    }
}
