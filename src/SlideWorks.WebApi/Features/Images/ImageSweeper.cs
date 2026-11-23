using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.WebApi.Data;

namespace SlideWorks.WebApi.Features.Images;

// Cleans up what an upload leaves behind when it never finishes. Once at start and then every Interval, on the injected
// TimeProvider's timer, it removes
//  - files in images/.uploads that no request is writing (ImageStore.IsWriting) and that were last written more than Grace
//    ago: a request that died mid-body, or a server that stopped while one was out, and
//  - image rows whose upload expired more than Grace ago and never completed (UploadedAt null), with their file if one
//    is there. Such a row has no content and can't be on a slide (a slide takes only a completed image), and the upload
//    URL it carried has answered 404 since it expired, so nothing can finish it. The grace keeps a row an upload took
//    its token from just before it expired.
// A completed image is never touched. It logs counts only, never a path.
[RegisterSingleton(For = typeof(IHostedService))]
public sealed class ImageSweeper(ImageStore store, IServiceScopeFactory scopes, TimeProvider time, ILogger<ImageSweeper> logger) : IHostedService, IDisposable
{
    public static readonly TimeSpan Interval = TimeSpan.FromHours(1);
    public static readonly TimeSpan Grace = TimeSpan.FromHours(1);

    private readonly SemaphoreSlim _sweeping = new(1, 1);
    private ITimer? _timer;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await SweepAsync(cancellationToken);
        _timer = time.CreateTimer(state => _ = SweepAsync(CancellationToken.None), null, Interval, Interval);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Dispose();
        _timer = null;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _sweeping.Dispose();
    }

    // Runs on a timer, so nothing it throws may escape: a failed sweep is logged and the next one tries again.
    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        await _sweeping.WaitAsync(cancellationToken);
        try
        {
            var cutoff = time.GetUtcNow() - Grace;
            var files = SweepFiles(cutoff);
            var rows = await SweepRowsAsync(cutoff, cancellationToken);
            if (files + rows > 0)
            {
                logger.LogInformation("Swept {Files} stray upload files and {Rows} expired image uploads", files, rows);
            }
        }
        catch (Exception e) when (e is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(e, "Sweeping stray image uploads failed; the next sweep tries again");
        }
        finally
        {
            _sweeping.Release();
        }
    }

    private int SweepFiles(DateTimeOffset cutoff)
    {
        var folder = store.TempFolderPath();
        if (!Directory.Exists(folder))
        {
            return 0;
        }
        var removed = 0;
        foreach (var path in Directory.EnumerateFiles(folder))
        {
            if (store.IsWriting(Path.GetFileName(path)) || File.GetLastWriteTimeUtc(path) >= cutoff)
            {
                continue;
            }
            try
            {
                File.Delete(path);
                removed++;
            }
            catch (IOException)
            {
                // Still open: its write finishes or fails on its own, and the next sweep looks again.
            }
        }
        return removed;
    }

    private async Task<int> SweepRowsAsync(DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var expired = await db.DeckImages.AsNoTracking()
            .Where(i => i.UploadedAt == null && i.UploadExpiresAt < cutoff)
            .Select(i => new { i.Id, i.DeckId })
            .ToListAsync(cancellationToken);
        var removed = 0;
        foreach (var image in expired)
        {
            // The row goes only if it is still unfinished; a file is deleted only for a row that went.
            var deleted = await db.DeckImages.Where(i => i.Id == image.Id && i.UploadedAt == null).ExecuteDeleteAsync(cancellationToken);
            if (deleted == 0)
            {
                continue;
            }
            removed++;
            var path = store.PathFor(image.DeckId, image.Id);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        return removed;
    }
}
