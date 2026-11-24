using System.Buffers;
using Agoda.IoC.Core;
using SlideWorks.WebApi.Features.Images;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Accounts.Profile;

// Photo files on the data volume (D4): <SLIDEWORKS_DATA_PATH, default /data>/avatars/<userId>/<photoId>. Paths are built
// only from the server's own ids. An upload goes to a temp file under avatars/.uploads and is moved into place once it is
// complete, so a failed write leaves no file behind. Removing a photo keeps its file.
[RegisterSingleton]
public class AvatarFiles(IConfiguration configuration)
{
    // 5 MB, the most a photo may weigh.
    public const long MAX_BYTES = 5 * 1024 * 1024;

    private const string DEFAULT_DATA_PATH = "/data";
    private const string AVATARS_FOLDER = "avatars";
    private const string TEMP_FOLDER = ".uploads";
    private const int BUFFER_SIZE = 81920;

    public string PathFor(Guid userId, string photoId)
    {
        var root = Root();
        var path = Path.GetFullPath(Path.Combine(root, userId.ToString("D"), CheckId(photoId)));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("A photo path must stay under the avatars folder.");
        }
        return path;
    }

    // Copies body to a new temp file, refusing it with PayloadTooLargeException once it passes MAX_BYTES. Returns the temp
    // file's path. The temp file is deleted if the copy fails.
    public async Task<string> WriteTempAsync(Stream body, CancellationToken cancellationToken)
    {
        var folder = Path.Combine(Root(), TEMP_FOLDER);
        Directory.CreateDirectory(folder);
        var temp = Path.Combine(folder, Guid.NewGuid().ToString("N"));
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
                        throw new PayloadTooLargeException($"A photo may be at most {MAX_BYTES / (1024 * 1024)} MB");
                    }
                    await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
                await file.FlushAsync(cancellationToken);
                file.Flush(flushToDisk: true);
            }
            return temp;
        }
        catch
        {
            File.Delete(temp);
            throw;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    // Moves a checked temp file to the photo's place. It never replaces an existing file.
    public string Commit(string tempPath, Guid userId, string photoId)
    {
        var path = PathFor(userId, photoId);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.Move(tempPath, path, overwrite: false);
        return path;
    }

    // The photo's file opened for reading, or null when there is no such file.
    public Stream? Open(Guid userId, string photoId)
    {
        var path = PathFor(userId, photoId);
        return File.Exists(path) ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, BUFFER_SIZE, useAsync: true) : null;
    }

    private string Root()
        => Path.GetFullPath(Path.Combine(configuration[ImageStore.DATA_PATH_SETTING] is { Length: > 0 } dataPath ? dataPath : DEFAULT_DATA_PATH, AVATARS_FOLDER));

    private static string CheckId(string id)
    {
        if (id.Length == 0 || !id.All(char.IsAsciiLetterOrDigit))
        {
            throw new InvalidOperationException("Photo ids are server-generated base62.");
        }
        return id;
    }
}
