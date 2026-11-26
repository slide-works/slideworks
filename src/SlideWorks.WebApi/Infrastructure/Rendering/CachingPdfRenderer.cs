using System.Collections.Concurrent;
using System.Text.Json;
using Agoda.IoC.Core;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Features.Images;
using SlideWorks.WebApi.Features.Render.Print;

namespace SlideWorks.WebApi.Infrastructure.Rendering;

// The one IPdfRenderer (P5, D7, D13), in front of IRenderEngine.RenderPdfAsync. A deck's PDF is keyed by PdfRenderKey
// (revision, newest session, and a hash of the whole print view) and kept on the data volume at
// renders/<deck>/pdf-<revision>-<session>-<viewHash>.pdf; writing a new PDF deletes that deck's older PDFs, never its
// cards. Concurrent requests for one key share one render, and a failed render is never kept, so the next request tries
// again. Engine exceptions pass through unchanged.
[RegisterSingleton(For = typeof(IPdfRenderer))]
public sealed class CachingPdfRenderer(IRenderEngine engine, IServiceScopeFactory scopes, IConfiguration configuration) : IPdfRenderer
{
    // ImageStore's default too: the data volume.
    private const string DEFAULT_DATA_PATH = "/data";
    private const string RENDERS_FOLDER = "renders";
    private const string PDF_PREFIX = "pdf-";
    private const string PDF = ".pdf";

    private readonly ConcurrentDictionary<string, Lazy<Task<byte[]>>> _inFlight = new();

    public async Task<byte[]> RenderAsync(string deckId, CancellationToken ct)
    {
        var key = await KeyOfAsync(deckId, ct);
        var path = PathFor(deckId, key);
        var cached = await ReadCachedAsync(path, ct);
        if (cached != null)
        {
            return cached;
        }
        var render = _inFlight.GetOrAdd(key, k => new Lazy<Task<byte[]>>(() => RenderAndStoreAsync(deckId, k, path)));
        return await UntilDoneOrCancelledAsync(render.Value, ct);
    }

    // The revision, the newest session and the whole print view, read in one scope.
    private async Task<string> KeyOfAsync(string deckId, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var revision = (await services.GetRequiredService<DeckReader>().ReadForRenderAsync(deckId)).Revision;
        var session = await services.GetRequiredService<PrintSessions>().NewestAsync(deckId, ct);
        var view = await services.GetRequiredService<PrintViews>().ViewAsync(deckId, ct);
        return PdfRenderKey.For(deckId, revision, session, JsonSerializer.Serialize(view, JsonSerializerOptions.Web));
    }

    // A shared render keeps going for its other callers when one caller gives up; that caller stops waiting.
    private static async Task<byte[]> UntilDoneOrCancelledAsync(Task<byte[]> shared, CancellationToken ct)
    {
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using (ct.Register(() => cancelled.TrySetResult()))
        {
            if (await Task.WhenAny(shared, cancelled.Task) == shared)
            {
                return await shared;
            }
        }
        throw new OperationCanceledException(ct);
    }

    // Another key's write for the same deck can delete the file between finding and reading it, so a missing file is
    // a miss.
    private static async Task<byte[]?> ReadCachedAsync(string path, CancellationToken ct)
    {
        try
        {
            return await File.ReadAllBytesAsync(path, ct);
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
    }

    // The render is shared by every caller of this key, so no one caller's cancellation stops it; the engine's own
    // timeout bounds it.
    private async Task<byte[]> RenderAndStoreAsync(string deckId, string key, string path)
    {
        try
        {
            var pdf = await engine.RenderPdfAsync(deckId, CancellationToken.None);
            await StoreAsync(path, pdf);
            return pdf;
        }
        finally
        {
            _inFlight.TryRemove(key, out _);
        }
    }

    private static async Task StoreAsync(string path, byte[] pdf)
    {
        var folder = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(folder);
        var temp = Path.Combine(folder, $".{Guid.NewGuid():N}.tmp");
        await File.WriteAllBytesAsync(temp, pdf);
        File.Move(temp, path, overwrite: true);
        foreach (var older in Directory.EnumerateFiles(folder, $"{PDF_PREFIX}*{PDF}").Where(file => file != path))
        {
            File.Delete(older);
        }
    }

    // <deckId>:<revision>:<session>:<viewHash> becomes renders/<deckId>/pdf-<revision>-<session>-<viewHash>.pdf. The deck
    // id has been found in the database by then, and the path must still stay under the renders folder.
    private string PathFor(string deckId, string key)
    {
        var root = Path.GetFullPath(Path.Combine(configuration[ImageStore.DATA_PATH_SETTING] ?? DEFAULT_DATA_PATH, RENDERS_FOLDER));
        var path = Path.GetFullPath(Path.Combine(root, deckId, PDF_PREFIX + string.Join('-', key.Split(':').Skip(1)) + PDF));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("A PDF path must stay under the renders folder.");
        }
        return path;
    }
}
