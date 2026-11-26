namespace SlideWorks.WebApi.Tests.Render;

// Counts IPrintQaResults.ReadAsync calls; WhenReads gives the task that completes once that many more calls have come.
public sealed class QaReadCounter
{
    private readonly object _lock = new();
    private int _count;
    private int _target = int.MaxValue;
    private TaskCompletionSource _reached = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Record()
    {
        lock (_lock)
        {
            _count++;
            if (_count >= _target)
            {
                _reached.TrySetResult();
            }
        }
    }

    public Task WhenReads(int more)
    {
        lock (_lock)
        {
            _target = _count + more;
            _reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            return _reached.Task;
        }
    }
}
