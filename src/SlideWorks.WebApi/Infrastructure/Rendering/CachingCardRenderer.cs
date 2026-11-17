using System.Collections.Concurrent;
using System.Text.Json;
using Agoda.IoC.Core;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Features.Images;

namespace SlideWorks.WebApi.Infrastructure.Rendering;

// The one ICardRenderer (L9), in front of IRenderEngine. A deck's card is keyed by RenderKey (revision, resolved look
// and sharing, read through DeckReader.ReadForRenderAsync) and kept on the data volume at
// renders/<deck>/card-<revision>-<lookHash>-<sharing>.png; writing a new card deletes that deck's older ones. Concurrent
// requests for one key share one render, and a failed render is never kept, so the next request tries again. The
// generic card is rendered at most once per process and kept in memory. An unknown, private or named-viewer deck gets
// the generic card and never starts a deck render.
[RegisterSingleton(For = typeof(ICardRenderer))]
public sealed class CachingCardRenderer(IRenderEngine engine, IServiceScopeFactory scopes, IConfiguration configuration) : ICardRenderer
{
    // ImageStore's default too: the data volume.
    private const string DEFAULT_DATA_PATH = "/data";
    private const string RENDERS_FOLDER = "renders";
    private const string CARD_PREFIX = "card-";
    private const string PNG = ".png";

    private readonly ConcurrentDictionary<string, Lazy<Task<byte[]>>> _inFlight = new();
    private readonly Lock _genericLock = new();
    private Task<byte[]>? _generic;

    // Only a link deck's card is ever rendered (L9, D12). Callers already send other decks to the generic card; this is
    // the second line, so an unknown, private or named-viewer deck never starts a deck render here either.
    public async Task<RenderedCard> RenderCardAsync(string deckId, CancellationToken ct)
    {
        var key = await LinkDeckKeyOfAsync(deckId);
        if (key == null)
        {
            return await RenderGenericCardAsync(ct);
        }
        var path = PathFor(deckId, key);
        var cached = await ReadCachedAsync(path, ct);
        if (cached != null)
        {
            return new RenderedCard(cached, key);
        }
        var render = _inFlight.GetOrAdd(key, k => new Lazy<Task<byte[]>>(() => RenderAndStoreAsync(deckId, k, path)));
        return new RenderedCard(await UntilDoneOrCancelledAsync(render.Value, ct), key);
    }

    public async Task<RenderedCard> RenderGenericCardAsync(CancellationToken ct)
    {
        Task<byte[]> generic;
        lock (_genericLock)
        {
            generic = _generic ??= engine.RenderGenericCardAsync(CancellationToken.None);
        }
        try
        {
            return new RenderedCard(await UntilDoneOrCancelledAsync(generic, ct), RenderKey.GENERIC);
        }
        catch when (generic.IsFaulted || generic.IsCanceled)
        {
            lock (_genericLock)
            {
                if (_generic == generic)
                {
                    _generic = null;
                }
            }
            throw;
        }
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

    // The deck's RenderKey when its sharing is link; null for an unknown deck or any other sharing.
    private async Task<string?> LinkDeckKeyOfAsync(string deckId)
    {
        await using var scope = scopes.CreateAsyncScope();
        DeckForRender deck;
        try
        {
            deck = await scope.ServiceProvider.GetRequiredService<DeckReader>().ReadForRenderAsync(deckId);
        }
        catch (NotFoundException)
        {
            return null;
        }
        if (deck.Sharing != DeckSharing.Link)
        {
            return null;
        }
        return RenderKey.For(
            deckId,
            checked((int)deck.Revision),
            JsonSerializer.Serialize(deck.Play.Look, JsonSerializerOptions.Web),
            deck.Sharing.ToString().ToLowerInvariant());
    }

    // The render is shared by every caller of this key, so no one caller's cancellation stops it; the engine's own
    // timeout bounds it.
    private async Task<byte[]> RenderAndStoreAsync(string deckId, string key, string path)
    {
        try
        {
            var png = await engine.RenderCardAsync(deckId, CancellationToken.None);
            await StoreAsync(path, png);
            return png;
        }
        finally
        {
            _inFlight.TryRemove(key, out _);
        }
    }

    private static async Task StoreAsync(string path, byte[] png)
    {
        var folder = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(folder);
        var temp = Path.Combine(folder, $".{Guid.NewGuid():N}.tmp");
        await File.WriteAllBytesAsync(temp, png);
        File.Move(temp, path, overwrite: true);
        foreach (var older in Directory.EnumerateFiles(folder, $"{CARD_PREFIX}*{PNG}").Where(file => file != path))
        {
            File.Delete(older);
        }
    }

    // <deckId>:<revision>:<lookHash>:<sharing> becomes renders/<deckId>/card-<revision>-<lookHash>-<sharing>.png. The
    // deck id has been found in the database by then, and the path must still stay under the renders folder.
    private string PathFor(string deckId, string key)
    {
        var root = Path.GetFullPath(Path.Combine(configuration[ImageStore.DATA_PATH_SETTING] ?? DEFAULT_DATA_PATH, RENDERS_FOLDER));
        var path = Path.GetFullPath(Path.Combine(root, deckId, CARD_PREFIX + string.Join('-', key.Split(':').Skip(1)) + PNG));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("A card path must stay under the renders folder.");
        }
        return path;
    }
}
