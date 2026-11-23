using SlideWorks.WebApi.Infrastructure.Mail;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Sharing.Email;

// A FakeMailSender that also tells a waiting test when a message arrived, so a test waits on the send itself, with a
// timeout only as the failure path, rather than on a fixed delay.
public sealed class SignallingMailSender(FakeMailSender inner) : IMailSender
{
    private readonly Lock _gate = new();
    private TaskCompletionSource _changed = NewSignal();

    public async Task SendAsync(MailMessageModel message, CancellationToken cancellationToken = default)
    {
        await inner.SendAsync(message, cancellationToken);
        Signal();
    }

    // Returns once done() is true, or after the timeout (the caller's own assertion then fails with what it sees).
    public async Task WaitUntilAsync(Func<bool> done, TimeSpan timeout)
    {
        using var cancel = new CancellationTokenSource(timeout);
        var timedOut = new TaskCompletionSource();
        await using var registration = cancel.Token.Register(() => timedOut.TrySetResult());
        while (true)
        {
            Task changed;
            lock (_gate)
            {
                changed = _changed.Task;
            }
            if (done() || timedOut.Task.IsCompleted)
            {
                return;
            }
            await Task.WhenAny(changed, timedOut.Task);
        }
    }

    private void Signal()
    {
        lock (_gate)
        {
            _changed.TrySetResult();
            _changed = NewSignal();
        }
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
