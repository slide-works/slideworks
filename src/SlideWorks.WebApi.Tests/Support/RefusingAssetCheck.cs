using SlideWorks.WebApi.Features.Templates;
using SlideWorks.WebApi.Features.Templates.Assets;
using SlideWorks.WebApi.Features.Templates.Data;

namespace SlideWorks.WebApi.Tests.Support;

// An IAssetCheck for one kind of file that answers what a test tells it to: Refuse makes every check from then on
// refuse with that code (and findings), Accept makes it accept again, with a content type and meta the test may set.
// Until a test says otherwise it accepts every file as bytes of no type. It counts the bytes of the last file it was
// handed, so a test can see the check was given the whole file. Add it with
// services.AddSingleton<IAssetCheck>(check): the last check registered for a kind is the one that runs. The app, and
// so this check, is shared by every test in a fixture: a test that makes it refuse calls Accept when it is done.
public sealed class RefusingAssetCheck(TemplateAssetKind kind) : IAssetCheck
{
    private readonly Lock _gate = new();
    private AssetCheckResult _answer = AssetCheckResult.Accepted(AssetContentTypes.UNKNOWN);
    private long _lastLength = -1;

    public TemplateAssetKind Kind { get; } = kind;

    public void Refuse(string code, params (string Name, string Value)[] values) => Answer(AssetCheckResult.Refused(code, values));

    public void Refuse(string code, IReadOnlyList<TemplateRefusalDetail> errors, params (string Name, string Value)[] values)
        => Answer(AssetCheckResult.Refused(code, errors, values));

    public void Accept(string contentType = AssetContentTypes.UNKNOWN, string? metaJson = null) => Answer(AssetCheckResult.Accepted(contentType, metaJson));

    // The length of the last file checked, -1 when none has been.
    public long LastLength()
    {
        lock (_gate)
        {
            return _lastLength;
        }
    }

    public async Task<AssetCheckResult> CheckAsync(Stream content, string fileName, CancellationToken ct)
    {
        using var copy = new MemoryStream();
        await content.CopyToAsync(copy, ct);
        lock (_gate)
        {
            _lastLength = copy.Length;
            return _answer;
        }
    }

    private void Answer(AssetCheckResult answer)
    {
        lock (_gate)
        {
            _answer = answer;
        }
    }
}
