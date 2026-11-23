using System.Buffers;
using System.Collections.Concurrent;
using Agoda.IoC.Core;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Images;

// Image files on the data volume (D14): <SLIDEWORKS_DATA_PATH, default /data>/images/<deckId>/<imageId>. Paths are built
// only from the server's own ids, never from an image's name or anything else a client sends. An upload is written to a
// temp file under images/.uploads first and moved into place only once it is complete and checked, so a failed write
// leaves no file behind.
[RegisterSingleton]
public class ImageStore(IConfiguration configuration)
{
    // 10 MB, the most an image may weigh (K11).
    public const long MAX_BYTES = 10 * 1024 * 1024;

    public const string DATA_PATH_SETTING = "SLIDEWORKS_DATA_PATH";

    private const string DEFAULT_DATA_PATH = "/data";
    private const string IMAGES_FOLDER = "images";
    private const string TEMP_FOLDER = ".uploads";
    private const int BUFFER_SIZE = 81920;

    // The temp files a request is writing right now, by file name. One process serves a data folder, so memory is enough.
    // ImageSweeper leaves these alone however old they look: a slow request body keeps its file open for as long as it
    // takes to arrive.
    private readonly ConcurrentDictionary<string, byte> _writing = new(StringComparer.Ordinal);

    // Where the image's file lives once its upload has completed.
    public string PathFor(string deckId, string imageId)
    {
        var images = ImagesRoot();
        var path = Path.GetFullPath(Path.Combine(images, CheckId(deckId), CheckId(imageId)));
        if (!path.StartsWith(images + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("An image path must stay under the images folder.");
        }
        return path;
    }

    // The path stored on the image row, relative to the data folder.
    public static string RelativePathFor(string deckId, string imageId) => $"{IMAGES_FOLDER}/{deckId}/{imageId}";

    // The folder holding temp files of uploads in progress. It may not exist yet.
    public string TempFolderPath() => Path.Combine(ImagesRoot(), TEMP_FOLDER);

    // Whether a request is writing the temp file with this name right now.
    public bool IsWriting(string tempFileName) => _writing.ContainsKey(tempFileName);

    // Copies body to a new temp file, refusing it with PayloadTooLargeException once it passes MAX_BYTES. Returns the
    // temp file's path and its size. The temp file is deleted if the copy fails.
    public async Task<(string Path, long Size)> WriteTempAsync(Stream body, CancellationToken cancellationToken)
    {
        var folder = TempFolderPath();
        Directory.CreateDirectory(folder);
        var name = Guid.NewGuid().ToString("N");
        var temp = Path.Combine(folder, name);
        _writing[name] = 0;
        var buffer = ArrayPool<byte>.Shared.Rent(BUFFER_SIZE);
        try
        {
            long size = 0;
            await using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, BUFFER_SIZE, useAsync: true))
            {
                int read;
                while ((read = await body.ReadAsync(buffer.AsMemory(0, BUFFER_SIZE), cancellationToken)) > 0)
                {
                    size += read;
                    if (size > MAX_BYTES)
                    {
                        throw new PayloadTooLargeException(TooLargeMessage);
                    }
                    await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
                // On disk before the rename, so a power loss can't leave a short file behind an upload marked complete.
                await file.FlushAsync(cancellationToken);
                file.Flush(flushToDisk: true);
            }
            return (temp, size);
        }
        catch
        {
            File.Delete(temp);
            throw;
        }
        finally
        {
            _writing.TryRemove(name, out _);
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public static string TooLargeMessage => $"An image may be at most {MAX_BYTES / (1024 * 1024)} MB";

    // The first bytes of a file, enough for ImageTypes.Matches.
    public static async Task<byte[]> HeaderOfAsync(string path, CancellationToken cancellationToken)
    {
        var header = new byte[ImageTypes.HEADER_LENGTH];
        await using var file = File.OpenRead(path);
        var length = await file.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken);
        return header[..length];
    }

    // Moves a checked temp file to the image's place and returns that path. It never replaces an existing file.
    public string Commit(string tempPath, string deckId, string imageId)
    {
        var path = PathFor(deckId, imageId);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.Move(tempPath, path, overwrite: false);
        return path;
    }

    private string ImagesRoot()
        => Path.GetFullPath(Path.Combine(configuration[DATA_PATH_SETTING] is { Length: > 0 } dataPath ? dataPath : DEFAULT_DATA_PATH, IMAGES_FOLDER));

    // Deck and image ids are base62, plus the "img_" prefix on an image id. Anything else never names a folder or file.
    private static string CheckId(string id)
    {
        if (id.Length == 0 || !id.All(c => char.IsAsciiLetterOrDigit(c) || c == '_'))
        {
            throw new InvalidOperationException("Image and deck ids are server-generated base62.");
        }
        return id;
    }
}
