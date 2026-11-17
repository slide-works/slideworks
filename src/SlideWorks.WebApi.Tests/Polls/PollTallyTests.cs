using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Polls;
using SlideWorks.WebApi.Features.Polls;
using SlideWorks.WebApi.Features.Polls.Contracts;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Polls;

// The real in-memory tally (V5, D16), with rounds and answers written by PollRowSeed, as the answer writer would have
// committed them, and ApplyAsync called as the writer calls it after its commit. The app's tally is a singleton shared by
// the fixture, so every test seeds a deck and a round of its own and reads only that round.
[TestFixture]
[CancelAfter(60000)]
public class PollTallyTests : PollTestFixture
{
    private const int OPTIONS = 3;

    private IPollTallies Tallies => Factory.Services.GetRequiredService<IPollTallies>();

    [Test]
    public async Task Tally_WhenAnAnswerIsReplaced_ShouldMoveTheCount()
    {
        var (round, roundRef) = await OpenRoundAsync();
        await Tallies.ReadAsync(round, OPTIONS);

        await Rows.AnswerAsync(Guid.Parse(round), "guest-a", 0);
        await Tallies.ApplyAsync(roundRef, [], [0]);
        var first = await Tallies.ReadAsync(round, OPTIONS);
        await Rows.AnswerAsync(Guid.Parse(round), "guest-a", 2);
        await Tallies.ApplyAsync(roundRef, [0], [2]);
        var replaced = await Tallies.ReadAsync(round, OPTIONS);

        first.Answered.ShouldBe(1);
        first.Counts.ShouldBe([1, 0, 0]);
        replaced.Answered.ShouldBe(1);
        replaced.Counts.ShouldBe([0, 0, 1]);
    }

    [Test]
    public async Task Tally_WhenReadAfterRestart_ShouldMatchTheRows()
    {
        var (round, _) = await OpenRoundAsync();
        var id = Guid.Parse(round);
        await Rows.AnswerAsync(id, "guest-a", 0);
        await Rows.AnswerAsync(id, "guest-b", 1);
        await Rows.AnswerAsync(id, "guest-c", 1);
        await Rows.AnswerAsync(id, "guest-c", 2);

        var read = await Tallies.ReadAsync(round, OPTIONS);

        read.Answered.ShouldBe(3);
        read.Counts.ShouldBe([1, 1, 1]);
    }

    // The first touch after a restart is the writer's ApplyAsync for an answer that is already in the rows it loads.
    [Test]
    public async Task Tally_WhenTheFirstTouchIsAnApply_ShouldCountTheAnswerOnce()
    {
        var (round, roundRef) = await OpenRoundAsync();
        var id = Guid.Parse(round);
        await Rows.AnswerAsync(id, "guest-a", 1);
        await Rows.AnswerAsync(id, "guest-b", 1);

        await Tallies.ApplyAsync(roundRef, [], [1]);
        var afterLoad = await Tallies.ReadAsync(round, OPTIONS);
        await Rows.AnswerAsync(id, "guest-c", 0);
        await Tallies.ApplyAsync(roundRef, [], [0]);
        var afterNext = await Tallies.ReadAsync(round, OPTIONS);

        afterLoad.Answered.ShouldBe(2);
        afterLoad.Counts.ShouldBe([0, 2, 0]);
        afterNext.Answered.ShouldBe(3);
        afterNext.Counts.ShouldBe([1, 2, 0]);
    }

    [Test]
    public async Task Tally_WhenAnOptionIsAdded_ShouldPadWithZero()
    {
        var (round, roundRef) = await OpenRoundAsync();
        await Rows.AnswerAsync(Guid.Parse(round), "guest-a", 2);
        await Tallies.ReadAsync(round, OPTIONS);

        var padded = await Tallies.ReadAsync(round, OPTIONS + 1);
        await Rows.AnswerAsync(Guid.Parse(round), "guest-b", 3);
        await Tallies.ApplyAsync(roundRef with { OptionCount = OPTIONS + 1 }, [], [3]);
        var picked = await Tallies.ReadAsync(round, OPTIONS + 1);

        padded.Answered.ShouldBe(1);
        padded.Counts.ShouldBe([0, 0, 1, 0]);
        picked.Answered.ShouldBe(2);
        picked.Counts.ShouldBe([0, 0, 1, 1]);
    }

    // Pick several: a guest is one answer however many options it picks.
    [Test]
    public async Task Tally_WhenAGuestPicksSeveral_ShouldCountTheGuestOnce()
    {
        var (round, roundRef) = await OpenRoundAsync();
        await Tallies.ReadAsync(round, OPTIONS);

        await Rows.AnswerAsync(Guid.Parse(round), "guest-a", 0, 2);
        await Tallies.ApplyAsync(roundRef, [], [0, 2]);
        await Rows.AnswerAsync(Guid.Parse(round), "guest-a", 1, 2);
        await Tallies.ApplyAsync(roundRef, [0, 2], [1, 2]);
        var read = await Tallies.ReadAsync(round, OPTIONS);

        read.Answered.ShouldBe(1);
        read.Counts.ShouldBe([0, 1, 1]);
    }

    // Each concurrent read sees whole changes only: with first answers of one pick each, the counts always add up to
    // Answered, and the end result counts every answer once.
    [Test]
    public async Task Tally_WhenAppliesAndReadsRunConcurrently_ShouldCountEachAnswerOnce()
    {
        const int answers = 300;
        var (round, roundRef) = await OpenRoundAsync();
        await Tallies.ReadAsync(round, OPTIONS);

        var applies = Enumerable.Range(0, answers).Select(i => Task.Run(() => Tallies.ApplyAsync(roundRef, [], [i % OPTIONS])));
        var reads = Enumerable.Range(0, answers).Select(_ => Task.Run(() => Tallies.ReadAsync(round, OPTIONS))).ToList();
        await WithinAsync(Task.WhenAll(applies));
        var seen = await WithinAsync(Task.WhenAll(reads));
        var end = await Tallies.ReadAsync(round, OPTIONS);

        seen.ShouldAllBe(read => read.Counts.Sum() == read.Answered);
        end.Answered.ShouldBe(answers);
        end.Counts.ShouldBe([answers / OPTIONS, answers / OPTIONS, answers / OPTIONS]);
    }

    // Many first touches race to load an unloaded round: they share one load, so each reads the rows once and an answer
    // applied after them is added once on top.
    [Test]
    public async Task Tally_WhenFirstTouchesRace_ShouldLoadTheRowsOnce()
    {
        var (round, roundRef) = await OpenRoundAsync();
        var id = Guid.Parse(round);
        for (var guest = 0; guest < 5; guest++)
        {
            await Rows.AnswerAsync(id, $"guest-{guest}", guest % OPTIONS);
        }

        var racing = await WithinAsync(Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(() => Tallies.ReadAsync(round, OPTIONS)))));
        await Rows.AnswerAsync(id, "guest-late", 2);
        await Tallies.ApplyAsync(roundRef, [], [2]);
        var after = await Tallies.ReadAsync(round, OPTIONS);

        racing.ShouldAllBe(read => read.Answered == 5 && read.Counts.SequenceEqual(new[] { 2, 2, 1 }));
        after.Answered.ShouldBe(6);
        after.Counts.ShouldBe([2, 2, 2]);
    }

    // A closed round is answered from its rows on every read, and later answers to it are never kept.
    [Test]
    public async Task Tally_WhenTheRoundIsClosed_ShouldAnswerFromTheRows()
    {
        var (round, roundRef) = await OpenRoundAsync();
        var id = Guid.Parse(round);
        await Rows.AnswerAsync(id, "guest-a", 1);
        await Rows.CloseAsync(id, 4);

        await Tallies.ApplyAsync(roundRef, [], [0]);
        var read = await Tallies.ReadAsync(round, OPTIONS);

        read.Answered.ShouldBe(1);
        read.Counts.ShouldBe([0, 1, 0]);
    }

    // A draft round (no OpenedAt, as a discarded draft also has) never counts, and an unknown round reads as no answers.
    [Test]
    public async Task Tally_WhenTheRoundIsADraftOrUnknown_ShouldReadAsNoAnswers()
    {
        var poll = await SeedPollAsync();
        var draft = await Rows.RoundAsync(poll.SlideId, poll.SessionId, PollPhase.Draft);

        var drafted = await Tallies.ReadAsync(draft.Id.ToString(), OPTIONS);
        var unknown = await Tallies.ReadAsync(Guid.NewGuid().ToString(), OPTIONS);

        drafted.Answered.ShouldBe(0);
        drafted.Counts.ShouldBe([0, 0, 0]);
        unknown.Answered.ShouldBe(0);
        unknown.Counts.ShouldBe([0, 0, 0]);
    }

    // ApplyAsync awaits every Changed handler with the round; one that throws neither fails the answer nor stops the next.
    [Test]
    public async Task Apply_WhenAHandlerThrows_ShouldStillCallTheOthers()
    {
        var (round, roundRef) = await OpenRoundAsync();
        await Tallies.ReadAsync(round, OPTIONS);
        var called = new List<PollRoundRef>();
        using var failing = Tallies.Changed(changed => changed.RoundId == round ? throw new InvalidOperationException("handler failed") : Task.CompletedTask);
        using var recording = Tallies.Changed(changed =>
        {
            if (changed.RoundId == round)
            {
                lock (called)
                {
                    called.Add(changed);
                }
            }
            return Task.CompletedTask;
        });

        await Tallies.ApplyAsync(roundRef, [], [1]);
        var read = await Tallies.ReadAsync(round, OPTIONS);

        called.ShouldBe([roundRef]);
        read.Answered.ShouldBe(1);
    }

    private static async Task WithinAsync(Task task)
    {
        if (await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(20))) != task)
        {
            throw new TimeoutException("The tally did not finish within 20 seconds.");
        }
        await task;
    }

    private static async Task<T> WithinAsync<T>(Task<T> task)
    {
        await WithinAsync((Task)task);
        return await task;
    }

    // An open round on the test's own poll, and the PollRoundRef the answer writer would pass for it.
    private async Task<(string Round, PollRoundRef Ref)> OpenRoundAsync()
    {
        var poll = await SeedPollAsync();
        var round = await Rows.RoundAsync(poll.SlideId, poll.SessionId);
        var id = round.Id.ToString();
        return (id, new PollRoundRef(poll.DeckId, poll.Slide, poll.SlideId, poll.SessionId.ToString(), id, OPTIONS));
    }
}
