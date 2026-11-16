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
// generic card is rendered at most once per process and kept in memory. Callers send unknown, private and unlisted
// decks to the generic card; an unknown deck id here is "Deck not found" and never starts a render.
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

    public async Task<RenderedCard> RenderCardAsync(string deckId, CancellationToken ct)
    {
        var key = await KeyOfAsync(deckId);
        var path = PathFor(deckId, key);
        if (File.Exists(path))
        {
            return new RenderedCard(await File.ReadAllBytesAsync(path, ct), key);
        }
        var render = _inFlight.GetOrAdd(key, k => new Lazy<Task<byte[]>>(() => RenderAndStoreAsync(deckId, k, path)));
        return new RenderedCard(await render.Value, key);
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
            return new RenderedCard(await generic, RenderKey.GENERIC);
        }
        catch
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

    private async Task<string> KeyOfAsync(string deckId)
    {
        await using var scope = scopes.CreateAsyncScope();
        var deck = await scope.ServiceProvider.GetRequiredService<DeckReader>().ReadForRenderAsync(deckId);
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
