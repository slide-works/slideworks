using System.IO.Compression;
using SlideWorks.Core.Look.Manifest;

namespace SlideWorks.WebApi.Features.Templates.Bundles;

// An opened bundle that has passed every check on its entries' names and headers, before any entry is read: the entry
// count, plain names, no duplicate, no symlink and no entry whose header already claims more than the bundle may hold.
// The entries are then read one at a time with the bytes counted as they come (a header is never trusted), each inside
// its limit, and Read adds every byte to a running total that may not pass TemplateLimits.MAX_BUNDLE_BYTES.
// Nothing here writes anywhere.
internal sealed class BundleArchive
{
    // A deflate stream of zeros is about 1000 to 1: a file that unpacks to more than 100 times its packed size, and is
    // not small, is a bomb. Real fonts, images and markup pack at a tenth of that.
    private const int MAX_RATIO = 100;
    private const int RATIO_FROM_BYTES = 64 * 1024;
    private const int BUFFER_SIZE = 81920;
    private const int UNIX_TYPE_MASK = 0xF000;
    private const int UNIX_SYMLINK = 0xA000;
    private const int WINDOWS_REPARSE_POINT = 0x400;

    private readonly Dictionary<string, ZipArchiveEntry> _files;
    private long _total;

    private BundleArchive(Dictionary<string, ZipArchiveEntry> files) => _files = files;

    public IReadOnlyCollection<string> Paths => _files.Keys;

    public bool Has(string path) => _files.ContainsKey(path);

    public static BundleArchive Open(ZipArchive zip)
    {
        if (zip.Entries.Count > TemplateLimits.MAX_BUNDLE_ENTRIES)
        {
            throw TemplateRefusal.Of(TemplateErrorCodes.BUNDLE_TOO_LARGE);
        }
        var files = new Dictionary<string, ZipArchiveEntry>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long declared = 0;
        foreach (var entry in zip.Entries)
        {
            var isFolder = entry.FullName.EndsWith('/');
            var name = isFolder ? entry.FullName[..^1] : entry.FullName;
            if (!BundleNames.IsPlainPath(name))
            {
                throw Invalid("an entry's name is not a relative path of plain names");
            }
            if (!seen.Add(entry.FullName))
            {
                throw Invalid("the same name is used by more than one entry");
            }
            if (IsLink(entry))
            {
                throw Invalid("it holds a link, and a bundle holds only files");
            }
            if (isFolder)
            {
                if (entry.Length != 0)
                {
                    throw Invalid("a folder entry holds data");
                }
                continue;
            }
            declared += entry.Length;
            if (declared > TemplateLimits.MAX_BUNDLE_BYTES || IsBomb(entry.Length, entry.CompressedLength))
            {
                throw TemplateRefusal.Of(TemplateErrorCodes.BUNDLE_TOO_LARGE);
            }
            files.Add(entry.FullName, entry);
        }
        return new BundleArchive(files);
    }

    // The entry's bytes, counted as they are unpacked. An entry that passes maxBytes, a total that passes the bundle's
    // limit, or one that unpacks to far more than it packs, is bundle_too_large.
    public async Task<byte[]> ReadAsync(string path, long maxBytes, CancellationToken ct)
    {
        var entry = _files[path];
        using var result = new MemoryStream();
        var buffer = new byte[BUFFER_SIZE];
        try
        {
            await using var stream = await entry.OpenAsync(ct);
            int read;
            while ((read = await stream.ReadAsync(buffer, ct)) > 0)
            {
                _total += read;
                if (result.Length + read > maxBytes || _total > TemplateLimits.MAX_BUNDLE_BYTES)
                {
                    throw TemplateRefusal.Of(TemplateErrorCodes.BUNDLE_TOO_LARGE);
                }
                await result.WriteAsync(buffer.AsMemory(0, read), ct);
            }
        }
        catch (Exception e) when (e is InvalidDataException or NotSupportedException)
        {
            throw Invalid("an entry can't be unpacked");
        }
        if (IsBomb(result.Length, entry.CompressedLength))
        {
            throw TemplateRefusal.Of(TemplateErrorCodes.BUNDLE_TOO_LARGE);
        }
        return result.ToArray();
    }

    public static TemplateRefusedException Invalid(string reason, string? path = null)
        => TemplateRefusal.Of(TemplateErrorCodes.BUNDLE_INVALID, path, ("reason", reason));

    private static bool IsBomb(long length, long packed) => length > RATIO_FROM_BYTES && length / Math.Max(packed, 1) > MAX_RATIO;

    private static bool IsLink(ZipArchiveEntry entry)
        => ((entry.ExternalAttributes >>> 16) & UNIX_TYPE_MASK) == UNIX_SYMLINK || (entry.ExternalAttributes & WINDOWS_REPARSE_POINT) != 0;
}
