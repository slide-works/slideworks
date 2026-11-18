using Agoda.IoC.Core;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Templates;

// TODO: returns a sample until the real service lands: import reads the bundle into a draft, export writes the draft out.
// Import reads nothing and answers one fixed id; export answers an empty zip named sample.zip.
[RegisterPerRequest(For = typeof(ITemplateBundles))]
public sealed class StubTemplateBundles : ITemplateBundles
{
    private const string SAMPLE_ID = "tpl_sample000001";

    // A zip with no entries is its end-of-central-directory record alone: the signature, then eighteen zero bytes.
    private static readonly byte[] EMPTY_ZIP = [0x50, 0x4B, 0x05, 0x06, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];

    public Task<string> ImportAsync(ICurrentActor actor, Stream zip, CancellationToken ct) => Task.FromResult(SAMPLE_ID);

    public Task<TemplateBundleFile> ExportAsync(ICurrentActor actor, string id, CancellationToken ct)
        => Task.FromResult(new TemplateBundleFile(new MemoryStream(EMPTY_ZIP, writable: false), "sample.zip"));
}
