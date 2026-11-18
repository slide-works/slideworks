using System.Buffers;
using System.Security.Cryptography;
using Agoda.IoC.Core;
using SlideWorks.WebApi.Features.Images;
using SlideWorks.WebApi.Features.Templates.Data;

namespace SlideWorks.WebApi.Features.Templates.Assets;

// A template's files on the data volume (T6): <SLIDEWORKS_DATA_PATH, default /data>/templates/<templateId>/<assetId>.
// A path is built only from a template's id and a file's content hash, each checked character by character, never
// from a file name or anything else a caller sends. A file arrives in a temp file under templates/.uploads, is hashed
// and counted as it is copied, and is moved into place only once it has been checked. A stored file is never replaced.
[RegisterSingleton]
public class TemplateAssetStore(IConfiguration configuration)
{
    private const string DEFAULT_DATA_PATH = "/data";
    private const string TEMPLATES_FOLDER = "templates";
    private const string TEMP_FOLDER = ".uploads";
    private const int BUFFER_SIZE = 81920;

    // Where an asset's file lives.
    public string PathFor(string templateId, string assetId)
    {
        var root = Root();
        var path = Path.GetFullPath(Path.Combine(root, CheckTemplateId(templateId), CheckAssetId(assetId)));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("A template file's path must stay under the templates folder.");
        }
        return path;
    }

    // Copies content to a new temp file and answers it with its size and SHA-256. Null once the content passes
    // maxBytes: the temp file is gone and the rest of the content is left unread. The temp file is also deleted when
    // the copy fails. The caller deletes the temp file it is handed.
    public async Task<TempAsset?> WriteTempAsync(Stream content, long maxBytes, CancellationToken ct)
    {
        var folder = Path.Combine(Root(), TEMP_FOLDER);
        Directory.CreateDirectory(folder);
        var temp = Path.Combine(folder, Guid.NewGuid().ToString("N"));
        var buffer = ArrayPool<byte>.Shared.Rent(BUFFER_SIZE);
        var complete = false;
        try
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long size = 0;
            await using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, BUFFER_SIZE, useAsync: true))
            {
                int read;
                while ((read = await content.ReadAsync(buffer.AsMemory(0, BUFFER_SIZE), ct)) > 0)
                {
                    size += read;
                    if (size > maxBytes)
                    {
                        return null;
                    }
                    hash.AppendData(buffer, 0, read);
                    await file.WriteAsync(buffer.AsMemory(0, read), ct);
                }
                // On disk before the rename, so a power loss can't leave a short file behind a stored asset.
                await file.FlushAsync(ct);
                file.Flush(flushToDisk: true);
            }
            complete = true;
            return new TempAsset(temp, size, Convert.ToHexStringLower(hash.GetHashAndReset()));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            if (!complete)
            {
                File.Delete(temp);
            }
        }
    }

    // Moves a checked temp file to the asset's place. True when this call put the file there; false when a file was
    // already there, which is left as it is: its name is its content's hash, so it holds the same bytes.
    public bool Commit(string tempPath, string templateId, string assetId)
    {
        var path = PathFor(templateId, assetId);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (File.Exists(path))
        {
            return false;
        }
        File.Move(tempPath, path, overwrite: false);
        return true;
    }

    public void Delete(string templateId, string assetId) => File.Delete(PathFor(templateId, assetId));

    public bool Exists(string templateId, string assetId) => File.Exists(PathFor(templateId, assetId));

    private string Root()
        => Path.GetFullPath(Path.Combine(configuration[ImageStore.DATA_PATH_SETTING] is { Length: > 0 } dataPath ? dataPath : DEFAULT_DATA_PATH, TEMPLATES_FOLDER));

    // "tpl_" plus base62, made by the server. Anything else never names a folder.
    private static string CheckTemplateId(string id)
    {
        if (!id.StartsWith(TemplateRecord.ID_PREFIX, StringComparison.Ordinal) || !id.All(c => char.IsAsciiLetterOrDigit(c) || c == '_'))
        {
            throw new InvalidOperationException("A template id is server-generated.");
        }
        return id;
    }

    // The first 32 lower-case hex characters of a SHA-256. Anything else never names a file.
    private static string CheckAssetId(string id)
    {
        if (id.Length != TemplateAsset.ID_LENGTH || !id.All(char.IsAsciiHexDigitLower))
        {
            throw new InvalidOperationException("An asset id is the start of a content hash.");
        }
        return id;
    }
}
