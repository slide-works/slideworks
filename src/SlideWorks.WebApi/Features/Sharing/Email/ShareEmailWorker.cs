#pragma warning disable S6667 // D15: the exception text can hold the recipient address, so it is deliberately never logged
using System.Threading.Channels;
using MailKit.Net.Smtp;
using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Accounts;
using SlideWorks.WebApi.Features.Sharing.Limits;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Infrastructure.Mail;

namespace SlideWorks.WebApi.Features.Sharing.Email;

// Sends the share emails (C8, D15) from an in-process queue, one notice at a time after the share has committed. For each
// address it skips one that has unsubscribed or already got EmailsPerRecipientPerDay emails in 24 hours from all sharers
// (counted from ShareEmailRow), and a notice from a sharer who is not approved or whose deck is gone. A send that fails is
// logged without the message and dropped: no retry, and the share is never touched. The queue is in memory, so a restart
// drops what is waiting; it is unbounded because only a committed share adds to it, and sharing is rate limited.
[RegisterSingleton(Concrete = true)]
[RegisterSingleton(For = typeof(IHostedService), Factory = typeof(Seam))]
public sealed class ShareEmailWorker(
    IServiceScopeFactory scopes,
    IMailSender sender,
    SharingLimits limits,
    TimeProvider clock,
    ILogger<ShareEmailWorker> logger) : IHostedService, IDisposable
{
    private static readonly TimeSpan DAY = TimeSpan.FromHours(24);

    private readonly Channel<ShareNotice> _queue = Channel.CreateUnbounded<ShareNotice>(new UnboundedChannelOptions { SingleReader = true });
    private readonly CancellationTokenSource _stopping = new();
    private Task _loop = Task.CompletedTask;

    public void Enqueue(ShareNotice notice)
    {
        if (notice.Added.Count > 0 && !_queue.Writer.TryWrite(notice))
        {
            logger.LogWarning("Share email for deck {DeckId} dropped: the queue has stopped", notice.DeckId);
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _loop = Task.Run(() => RunAsync(_stopping.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _queue.Writer.TryComplete();
        await _stopping.CancelAsync();
        await _loop;
    }

    public void Dispose() => _stopping.Dispose();

    private async Task RunAsync(CancellationToken stopping)
    {
        try
        {
            await foreach (var notice in _queue.Reader.ReadAllAsync(stopping))
            {
                try
                {
                    await SendNoticeAsync(notice, stopping);
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !stopping.IsCancellationRequested)
                {
                    logger.LogError("Share emails for deck {DeckId} failed and were dropped ({ExceptionType})", notice.DeckId, ex.GetType().Name);
                }
            }
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
            // Stopping: what is still queued is dropped with the process.
        }
    }

    private async Task SendNoticeAsync(ShareNotice notice, CancellationToken stopping)
    {
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<AppDbContext>();
        var sharer = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == notice.SharerId, stopping);
        var deck = await db.Decks.AsNoTracking().SingleOrDefaultAsync(d => d.Id == notice.DeckId, stopping);
        if (sharer is not { Access: UserAccess.Approved } || deck == null)
        {
            logger.LogWarning("Share email for deck {DeckId} skipped: the sharer is not approved or the deck is gone", notice.DeckId);
            return;
        }
        var suppressions = services.GetRequiredService<IEmailSuppressions>();
        var links = services.GetRequiredService<IUnsubscribeLinks>();
        var deckUrl = services.GetRequiredService<IPublicUrls>().Deck(deck.Id, deck.Slug);
        foreach (var recipient in notice.Added.Select(ViewerEmails.Normalize).Distinct(StringComparer.Ordinal))
        {
            if (await suppressions.ContainsAsync(recipient))
            {
                continue;
            }
            var since = clock.GetUtcNow() - DAY;
            if (await db.ShareEmails.CountAsync(r => r.RecipientEmail == recipient && r.SentAt >= since, stopping) >= limits.EmailsPerRecipientPerDay)
            {
                continue;
            }
            var message = ShareEmailTemplate.Build(recipient, sharer.Name, sharer.Email, deck.Title, deckUrl, links.For(recipient));
            try
            {
                await sender.SendAsync(message, stopping);
                db.ShareEmails.Add(new ShareEmailRow { Id = Guid.NewGuid(), RecipientEmail = recipient, SharerId = sharer.Id, DeckId = deck.Id, SentAt = clock.GetUtcNow() });
                await db.SaveChangesAsync(stopping);
            }
            catch (SmtpCommandException ex)
            {
                // D15: logged and dropped, no retry. Never the exception or its message: MailKit puts the address in them.
                logger.LogWarning("Share email for deck {DeckId} was not sent: {ErrorCode} {StatusCode}", deck.Id, ex.ErrorCode, (int)ex.StatusCode);
                db.ChangeTracker.Clear();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning("Share email for deck {DeckId} was not sent: {ExceptionType}", deck.Id, ex.GetType().Name);
                db.ChangeTracker.Clear();
            }
        }
    }

    // Resolves the hosted service to the app's one worker, so the queue the notifier fills is the queue the loop reads.
    public sealed class Seam : IComponentFactory<IHostedService>
    {
        public IHostedService Build(IComponentResolver c) => c.Resolve<ShareEmailWorker>();
    }
}
