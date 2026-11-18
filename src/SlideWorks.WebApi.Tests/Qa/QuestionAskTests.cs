using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Polls;
using SlideWorks.Core.Qa;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Polls;
using SlideWorks.WebApi.Features.Polls.Contracts;
using SlideWorks.WebApi.Features.Qa.Contracts;
using SlideWorks.WebApi.Features.Qa.Data;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Qa;

// A guest asking (Q4) through POST /api/join/{code}/questions and the real QuestionAsking, over a live deck from QaSeed whose
// Q&A round the real QaRounds opened at Go live. The room on screen comes from FakeAudienceRoom, the asker's upvote goes to
// FakeRoundAnswerWriter and the publish to FakeQuestionPublisher; the deck's review and names settings are rows from
// QaRowSeed. The app, and so every fake, is shared by the fixture: each test seeds a deck and guests of its own.
[TestFixture]
public class QuestionAskTests : QaTestFixture
{
    private FakeAudienceRoom Room => Factory.Services.GetRequiredService<FakeAudienceRoom>();

    private FakeRoundAnswerWriter Writer => Factory.Services.GetRequiredService<FakeRoundAnswerWriter>();

    private FakeQuestionPublisher Publisher => Factory.Services.GetRequiredService<FakeQuestionPublisher>();

    protected override void ConfigureQaServices(IServiceCollection services)
    {
        FakeAudienceRoom.AddTo(services);
        FakeRoundAnswerWriter.AddTo(services);
        FakeQuestionPublisher.AddTo(services);
    }

    [Test]
    public async Task AskAsync_WhenAccepted_ShouldStoreTheQuestionWithTheAskersUpvote()
    {
        var seeded = await ArrangeAsync();
        await using var guest = await JoinAsync(seeded);
        await QaRowSeed.QuestionAsync(seeded.RoundId, "What do you ship first?");
        await QaRowSeed.VoteAsync(seeded.RoundId, guest.GuestId, 1);

        var response = await guest.AskAsync(seeded.Code, new AskRequest("  How   do you split\n a big change? ", " Priya "));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var question = body.GetProperty("question");
        question.GetProperty("number").GetInt32().ShouldBe(2);
        question.GetProperty("text").GetString().ShouldBe("How do you split a big change?");
        question.GetProperty("name").GetString().ShouldBe("Priya");
        question.GetProperty("age").GetString().ShouldBe("now");
        question.GetProperty("state").GetString().ShouldBe("open");
        question.GetProperty("votes").GetInt32().ShouldBe(1);
        question.GetProperty("onScreen").GetBoolean().ShouldBeFalse();
        question.GetProperty("yours").GetBoolean().ShouldBeTrue();
        body.GetProperty("mine").GetProperty("picks").EnumerateArray().Select(p => p.GetInt32()).ShouldBe([1, 2]);
        body.GetProperty("mine").GetProperty("revision").GetInt32().ShouldBe(2);
        var stored = (await QuestionsAsync(seeded.RoundId))[1];
        stored.GuestId.ShouldBe(guest.GuestId);
        stored.Name.ShouldBe("Priya");
        stored.State.ShouldBe(QuestionState.Open);
        stored.OnScreen.ShouldBeFalse();
        (await OptionTextsAsync(seeded.RoundId)).ShouldBe(["What do you ship first?", "How do you split a big change?"]);
        var write = Writer.WritesFor(seeded.RoundId.ToString()).ShouldHaveSingleItem();
        write.GuestId.ShouldBe(guest.GuestId);
        write.Picks.ShouldBe([0, 1]);
        write.ExpectedRevision.ShouldBe(1);
        write.Round.ShouldBe(new PollRoundRef(seeded.DeckId, PollKind.Qa, null, null, seeded.SessionId.ToString(), seeded.RoundId.ToString(), 2));
        Publisher.PublishesOf(seeded.DeckId).ShouldBe(1);
    }

    [Test]
    public async Task AskAsync_WhenReviewOn_ShouldBeWaiting()
    {
        var seeded = await ArrangeAsync(QaSettings.Default with { Review = true });
        await using var guest = await JoinAsync(seeded);

        var response = await guest.AskAsync(seeded.Code, new AskRequest("How do you split a big change?", null));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        body.GetProperty("question").GetProperty("state").GetString().ShouldBe("waiting");
        body.GetProperty("question").GetProperty("name").ValueKind.ShouldBe(JsonValueKind.Null);
        (await QuestionsAsync(seeded.RoundId)).ShouldHaveSingleItem().State.ShouldBe(QuestionState.Waiting);
    }

    [Test]
    public async Task AskAsync_WhenFourthInAMinute_ShouldBeLimitedWithRetryAfter()
    {
        var seeded = await ArrangeAsync();
        await using var guest = await JoinAsync(seeded);
        for (var i = 1; i <= 3; i++)
        {
            (await guest.AskAsync(seeded.Code, new AskRequest($"Question {i}?", null))).StatusCode.ShouldBe(HttpStatusCode.Created);
        }
        Clock.Advance(TimeSpan.FromSeconds(20));

        var response = await guest.AskAsync(seeded.Code, new AskRequest("Question 4?", null));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        body.GetProperty("error").GetString().ShouldBe("too_many_questions");
        body.GetProperty("retryAfter").GetInt32().ShouldBe(40);
        (await QuestionsAsync(seeded.RoundId)).Count.ShouldBe(3);
    }

    [Test]
    public async Task AskAsync_WhenNamesOffAndNameGiven_ShouldBeRefused()
    {
        var seeded = await ArrangeAsync(QaSettings.Default with { Names = false });
        await using var guest = await JoinAsync(seeded);

        var response = await guest.AskAsync(seeded.Code, new AskRequest("How do you split a big change?", "Priya"));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        body.GetProperty("error").GetString().ShouldBe("name: not allowed");
        (await QuestionsAsync(seeded.RoundId)).ShouldBeEmpty();
        (await OptionTextsAsync(seeded.RoundId)).ShouldBeEmpty();
        Writer.WritesFor(seeded.RoundId.ToString()).ShouldBeEmpty();
    }

    [Test]
    public async Task AskAsync_WhenSlideSwitchOff_ShouldBeRefusedAndWriteNothing()
    {
        var seeded = await ArrangeAsync(slideSwitch: new QaSlideSwitch(false, true));
        await using var guest = await JoinAsync(seeded);

        var response = await guest.AskAsync(seeded.Code, new AskRequest("How do you split a big change?", null));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        body.GetProperty("error").GetString().ShouldBe("questions_off_here");
        (await QuestionsAsync(seeded.RoundId)).ShouldBeEmpty();
        (await OptionTextsAsync(seeded.RoundId)).ShouldBeEmpty();
        Writer.WritesFor(seeded.RoundId.ToString()).ShouldBeEmpty();
        Publisher.PublishesOf(seeded.DeckId).ShouldBe(0);
    }

    [TestCase(QaQuestions.Closed, "questions_closed")]
    [TestCase(QaQuestions.Off, "questions_off")]
    public async Task AskAsync_WhenTheDeckRefuses_ShouldAnswerItsCodeAndWriteNothing(QaQuestions questions, string error)
    {
        var seeded = await ArrangeAsync(QaSettings.Default with { Questions = questions });
        await using var guest = await JoinAsync(seeded);

        var response = await guest.AskAsync(seeded.Code, new AskRequest("How do you split a big change?", null));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        body.GetProperty("error").GetString().ShouldBe(error);
        (await QuestionsAsync(seeded.RoundId)).ShouldBeEmpty();
    }

    [Test]
    public async Task AskAsync_WhenTwoGuestsAskAtOnce_ShouldGiveEachItsOwnNumber()
    {
        var seeded = await ArrangeAsync();
        var guests = new List<GuestClient>();
        for (var i = 0; i < 6; i++)
        {
            guests.Add(await JoinAsync(seeded));
        }

        try
        {
            var responses = await Task.WhenAll(guests.Select(g => g.AskAsync(seeded.Code, new AskRequest($"From {g.GuestId}?", null))));
            var bodies = await Task.WhenAll(responses.Select(r => r.Content.ReadFromJsonAsync<JsonElement>()));

            responses.Select(r => r.StatusCode).ShouldAllBe(status => status == HttpStatusCode.Created);
            bodies.Select(b => b.GetProperty("question").GetProperty("number").GetInt32()).Order().ShouldBe([1, 2, 3, 4, 5, 6]);
            var stored = await QuestionsAsync(seeded.RoundId);
            stored.Select(q => q.Position).ShouldBe([0, 1, 2, 3, 4, 5]);
            stored.Select(q => q.GuestId).ShouldBe(guests.Select(g => g.GuestId), ignoreOrder: true);
            foreach (var body in bodies)
            {
                var number = body.GetProperty("question").GetProperty("number").GetInt32();
                var asker = stored[number - 1].GuestId;
                (await OptionTextsAsync(seeded.RoundId))[number - 1].ShouldBe($"From {asker}?");
            }
        }
        finally
        {
            foreach (var guest in guests)
            {
                await guest.DisposeAsync();
            }
        }
    }

    [Test]
    public async Task AskAsync_WhenGuestNotOnRoster_ShouldBeNoSession()
    {
        var seeded = await ArrangeAsync();
        await using var guest = await GuestClient.ConnectAsync(Factory);

        var response = await guest.AskAsync(seeded.Code, new AskRequest("How do you split a big change?", null));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        body.GetProperty("error").GetString().ShouldBe("no_session");
        (await QuestionsAsync(seeded.RoundId)).ShouldBeEmpty();
    }

    [Test]
    public async Task AskAsync_WhenTheUpvoteIsRefusedByTheWriter_ShouldStillAcceptTheQuestion()
    {
        var seeded = await ArrangeAsync();
        await using var guest = await JoinAsync(seeded);
        Writer.Answer(seeded.RoundId.ToString(), _ => new RoundWrite.Stale());

        var response = await guest.AskAsync(seeded.Code, new AskRequest("How do you split a big change?", null));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        body.GetProperty("question").GetProperty("number").GetInt32().ShouldBe(1);
        body.GetProperty("question").GetProperty("votes").GetInt32().ShouldBe(0);
        body.GetProperty("mine").GetProperty("picks").GetArrayLength().ShouldBe(0);
        body.GetProperty("mine").GetProperty("revision").GetInt32().ShouldBe(0);
        (await QuestionsAsync(seeded.RoundId)).ShouldHaveSingleItem().GuestId.ShouldBe(guest.GuestId);
        Writer.WritesFor(seeded.RoundId.ToString()).ShouldHaveSingleItem();
        Publisher.PublishesOf(seeded.DeckId).ShouldBe(1);
    }

    [Test]
    public async Task AskAsync_WhenRefused_ShouldNotUseUpTheLimit()
    {
        var seeded = await ArrangeAsync();
        await using var guest = await JoinAsync(seeded);
        for (var i = 0; i < 3; i++)
        {
            (await guest.AskAsync(seeded.Code, new AskRequest("   ", null))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        var accepted = new List<HttpStatusCode>();
        for (var i = 1; i <= 3; i++)
        {
            accepted.Add((await guest.AskAsync(seeded.Code, new AskRequest($"Question {i}?", null))).StatusCode);
        }

        accepted.ShouldBe([HttpStatusCode.Created, HttpStatusCode.Created, HttpStatusCode.Created]);
        (await QuestionsAsync(seeded.RoundId)).Count.ShouldBe(3);
    }

    // A live deck with its Q&A round, the deck's settings written as a row when they aren't the defaults, and the room on
    // screen worked out from them and the slide's switch, as the real room does.
    private async Task<SeededQa> ArrangeAsync(QaSettings? settings = null, QaSlideSwitch? slideSwitch = null)
    {
        var seeded = await SeedQaAsync();
        if (settings != null)
        {
            await QaRowSeed.SettingsAsync(seeded.DeckId, settings);
        }
        Room.Set(seeded.DeckId, QaRoomRules.For(settings ?? QaSettings.Default, slideSwitch ?? QaSlideSwitch.On, new QaRoundState(seeded.RoundId.ToString(), 1)));
        return seeded;
    }

    private async Task<GuestClient> JoinAsync(SeededQa seeded)
    {
        var guest = await GuestClient.ConnectAsync(Factory);
        await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", seeded.Code);
        return guest;
    }

    private async Task<List<Question>> QuestionsAsync(Guid roundId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Questions.AsNoTracking().Where(q => q.RoundId == roundId).OrderBy(q => q.Position).ToListAsync();
    }

    private async Task<List<string>> OptionTextsAsync(Guid roundId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.PollOptions.AsNoTracking().Where(o => o.RoundId == roundId).OrderBy(o => o.Position).Select(o => o.Text).ToListAsync();
    }
}
