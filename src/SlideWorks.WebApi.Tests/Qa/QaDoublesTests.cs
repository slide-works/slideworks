using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Qa;
using SlideWorks.WebApi.Features.Qa;
using SlideWorks.WebApi.Features.Qa.Contracts;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Qa;

// Agent 0's eight Q&A doubles, as a later task uses them: AddTo makes each one the app's seam, a test sets what it answers
// and reads what it was called with through the seam, and Reset forgets it. Each test uses keys of its own, since the app,
// and so each double, is shared by the fixture.
[TestFixture]
public class QaDoublesTests : ApiTestFixture
{
    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        FakeQuestionAsking.AddTo(services);
        FakeGuestQuestions.AddTo(services);
        FakeQuestionModeration.AddTo(services);
        FakeQaSettings.AddTo(services);
        FakeQaRounds.AddTo(services);
        FakeAudienceRoom.AddTo(services);
        FakeQuestionPublisher.AddTo(services);
        FakeReactions.AddTo(services);
    };

    private static readonly QaRoom _room = new("round-1", QaRoomQuestions.Closed, false, true, 9);

    [Test]
    public async Task FakeQuestionAsking_WhenSet_ShouldAnswerThroughTheSeamAndRecordTheCall()
    {
        var fake = Factory.Services.GetRequiredService<FakeQuestionAsking>();
        var code = NewKey();
        fake.Set(code, new AskResult.Limited(42));
        await using var scope = Factory.Services.CreateAsyncScope();
        var seam = scope.ServiceProvider.GetRequiredService<IQuestionAsking>();

        var result = await seam.AskAsync(code, "guest-a", new AskRequest("Why?", null));

        result.ShouldBe(new AskResult.Limited(42));
        fake.CallsFor(code).ShouldBe([("guest-a", new AskRequest("Why?", null))]);
        fake.Reset();
        fake.CallsFor(code).ShouldBeEmpty();
        await Should.ThrowAsync<InvalidOperationException>(() => seam.AskAsync(code, "guest-a", new AskRequest("Why?", null)));
    }

    [Test]
    public async Task FakeGuestQuestions_WhenSet_ShouldAnswerThroughTheSeamAndRecordTheCalls()
    {
        var fake = Factory.Services.GetRequiredService<FakeGuestQuestions>();
        var code = NewKey();
        var view = new GuestQuestionsView(_room, [], new MyVotes([], 0));
        fake.SetView(code, view);
        fake.SetVote(code, new VoteResult.Voted([1, 3], 2));
        await using var scope = Factory.Services.CreateAsyncScope();
        var seam = scope.ServiceProvider.GetRequiredService<IGuestQuestions>();
        var request = new VoteRequest("round-1", [1, 3], 1);

        var voted = await seam.VoteAsync(code, "guest-b", request);
        var read = await seam.ViewAsync(code, "guest-b");

        voted.ShouldBeOfType<VoteResult.Voted>().Revision.ShouldBe(2);
        read.ShouldBeSameAs(view);
        fake.VotesFor(code).ShouldBe([("guest-b", request)]);
        fake.ViewsFor(code).ShouldBe(["guest-b"]);
        fake.Reset();
        (await seam.ViewAsync(code, "guest-b")).ShouldBeNull();
    }

    [Test]
    public async Task FakeQuestionModeration_WhenSetOrRefusing_ShouldAnswerThroughTheSeam()
    {
        var fake = Factory.Services.GetRequiredService<FakeQuestionModeration>();
        var deck = NewKey();
        var view = new QaControlView(5, false, null, QaSettings.Default, null, [], new QaCounts(0, 0, 0, 0), null, [], ReactionCounts.Zero);
        fake.Set(deck, view);
        await using var scope = Factory.Services.CreateAsyncScope();
        var seam = scope.ServiceProvider.GetRequiredService<IQuestionModeration>();

        var hidden = await seam.HideAsync(deck, 3);
        fake.RefuseWith("wrong_state");
        var refusal = await Should.ThrowAsync<ConflictException>(() => seam.ShowAsync(deck, 4));
        var control = await seam.ControlAsync(deck);

        hidden.ShouldBeSameAs(view);
        refusal.Message.ShouldBe("wrong_state");
        control.ShouldBeSameAs(view);
        fake.CallsFor(deck).ShouldBe(["Hide 3"]);
        fake.Reset();
        await Should.ThrowAsync<InvalidOperationException>(() => seam.ControlAsync(deck));
    }

    [Test]
    public async Task FakeQaSettings_WhenPatched_ShouldKeepTheOtherFieldsAndRecordTheChange()
    {
        var fake = Factory.Services.GetRequiredService<FakeQaSettings>();
        var deck = NewKey();
        await using var scope = Factory.Services.CreateAsyncScope();
        var seam = scope.ServiceProvider.GetRequiredService<IQaSettings>();

        var updated = await seam.UpdateAsync(deck, new QaSettingsPatch(null, true, null, null, QaShowOn.QaSlide));
        var slide = await seam.SetSlideAsync(deck, 3, new QaSlidePatch(false, null));
        fake.FailWith(new NotFoundException("No slide 9"));
        await Should.ThrowAsync<NotFoundException>(() => seam.SetSlideAsync(deck, 9, new QaSlidePatch(false, null)));

        updated.ShouldBe(QaSettings.Default with { Review = true, ShowOn = QaShowOn.QaSlide });
        (await seam.GetAsync(deck)).ShouldBe(updated);
        slide.ShouldBe(new QaSlideSwitch(false, true));
        fake.SwitchOf(deck, 3).ShouldBe(slide);
        fake.CallsFor(deck).ShouldBe(["Update", "SetSlide 3"]);
        fake.Reset();
        (await seam.GetAsync(deck)).ShouldBe(QaSettings.Default);
    }

    [Test]
    public async Task FakeQaRounds_WhenCalled_ShouldRecordEachCall()
    {
        var fake = Factory.Services.GetRequiredService<FakeQaRounds>();
        var deck = NewKey();
        var session = Guid.NewGuid();
        await using var scope = Factory.Services.CreateAsyncScope();
        var seam = scope.ServiceProvider.GetRequiredService<IQaRounds>();

        await seam.OpenAsync(deck, session);
        await seam.EnsureAsync(deck);
        await seam.CloseAsync(deck, session);

        fake.CallsFor(deck).ShouldBe([$"Open {session}", "Ensure", $"Close {session}"]);
        fake.Reset();
        fake.CallsFor(deck).ShouldBeEmpty();
    }

    [Test]
    public async Task FakeAudienceRoom_WhenSet_ShouldAnswerThroughTheSeamAndCountReads()
    {
        var fake = Factory.Services.GetRequiredService<FakeAudienceRoom>();
        var deck = NewKey();
        fake.Set(deck, _room);
        await using var scope = Factory.Services.CreateAsyncScope();
        var seam = scope.ServiceProvider.GetRequiredService<IAudienceRoom>();

        var room = await seam.ForDeckAsync(deck);

        room.ShouldBe(_room);
        fake.ReadsOf(deck).ShouldBe(1);
        fake.Reset();
        (await seam.ForDeckAsync(deck)).ShouldBeNull();
    }

    [Test]
    public async Task FakeQuestionPublisher_WhenFailing_ShouldRecordAndThrow()
    {
        var fake = Factory.Services.GetRequiredService<FakeQuestionPublisher>();
        var deck = NewKey();
        var seam = Factory.Services.GetRequiredService<IQuestionPublisher>();

        await seam.PublishAsync(deck);
        fake.FailWith(new InvalidOperationException("publish failed"));
        await Should.ThrowAsync<InvalidOperationException>(() => seam.PublishAsync(deck));

        fake.PublishesOf(deck).ShouldBe(2);
        fake.Reset();
        await seam.PublishAsync(deck);
        fake.PublishesOf(deck).ShouldBe(1);
    }

    [Test]
    public async Task FakeReactions_WhenSet_ShouldAnswerThroughTheSeamAndRecordTheCalls()
    {
        var fake = Factory.Services.GetRequiredService<FakeReactions>();
        var code = NewKey();
        var session = Guid.NewGuid();
        var totals = new ReactionCounts(1, 2, 3, 4, 5);
        fake.SetTap(code, new ReactionResult.Refused(GuestQaErrors.REACTIONS_OFF));
        fake.SetTotals(session, totals);
        var seam = Factory.Services.GetRequiredService<IReactions>();

        var tap = await seam.TapAsync(code, "guest-c", "clap");
        await seam.EndSessionAsync(session);

        tap.ShouldBe(new ReactionResult.Refused(GuestQaErrors.REACTIONS_OFF));
        (await seam.TotalsAsync(session)).ShouldBe(totals);
        fake.TapsFor(code).ShouldBe([("guest-c", "clap")]);
        fake.EndsOf(session).ShouldBe(1);
        fake.Reset();
        (await seam.TapAsync(code, "guest-c", "clap")).ShouldBeOfType<ReactionResult.Counted>();
        (await seam.TotalsAsync(session)).ShouldBe(ReactionCounts.Zero);
    }

    private static string NewKey() => Guid.NewGuid().ToString("N");
}
