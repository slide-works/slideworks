using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.Templates;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Tests.Support;

// ITemplateBundles for tests of the bundle routes and their callers: import answers the id a test sets with AnswerImport
// and keeps the bytes it was sent, export answers the bytes and file name set with AnswerExport, and both throw what a test
// hands to Refuse. Calls are recorded in order as "Import <email>" and "Export <id> <email>"; a refused call records
// nothing. Add it with FakeTemplateBundles.Register. The app, and so this fake, is shared by every test in a fixture: a
// test that sets an answer or a refusal calls Reset when it is done.
public sealed class FakeTemplateBundles : ITemplateBundles
{
    public const string DEFAULT_ID = "tpl_fake00000001";
    public const string DEFAULT_FILE_NAME = "fake.zip";

    private readonly Lock _gate = new();
    private readonly List<string> _calls = [];
    private readonly List<byte[]> _imports = [];
    private string _id = DEFAULT_ID;
    private byte[] _export = [];
    private string _fileName = DEFAULT_FILE_NAME;
    private Exception? _refusal;

    public static void Register(IServiceCollection services)
        => services.AddSingleton<FakeTemplateBundles>().AddSingleton<ITemplateBundles>(provider => provider.GetRequiredService<FakeTemplateBundles>());

    public void AnswerImport(string id)
    {
        lock (_gate)
        {
            _id = id;
        }
    }

    public void AnswerExport(byte[] content, string fileName)
    {
        lock (_gate)
        {
            _export = content;
            _fileName = fileName;
        }
    }

    // Every call from now on throws this and records nothing; null accepts them again.
    public void Refuse(Exception? refusal)
    {
        lock (_gate)
        {
            _refusal = refusal;
        }
    }

    // Back to the default answers, no refusal and no recorded calls.
    public void Reset()
    {
        lock (_gate)
        {
            _id = DEFAULT_ID;
            _export = [];
            _fileName = DEFAULT_FILE_NAME;
            _refusal = null;
            _calls.Clear();
            _imports.Clear();
        }
    }

    public IReadOnlyList<string> Calls()
    {
        lock (_gate)
        {
            return [.. _calls];
        }
    }

    // The bytes of each imported bundle, in order.
    public IReadOnlyList<byte[]> Imports()
    {
        lock (_gate)
        {
            return [.. _imports];
        }
    }

    public async Task<string> ImportAsync(ICurrentActor actor, Stream zip, CancellationToken ct)
    {
        using var copy = new MemoryStream();
        await zip.CopyToAsync(copy, ct);
        lock (_gate)
        {
            ThrowIfRefused();
            _imports.Add(copy.ToArray());
            _calls.Add($"Import {actor.Email}");
            return _id;
        }
    }

    public Task<TemplateBundleFile> ExportAsync(ICurrentActor actor, string id, CancellationToken ct)
    {
        lock (_gate)
        {
            ThrowIfRefused();
            _calls.Add($"Export {id} {actor.Email}");
            return Task.FromResult(new TemplateBundleFile(new MemoryStream(_export, writable: false), _fileName));
        }
    }

    private void ThrowIfRefused()
    {
        if (_refusal != null)
        {
            throw _refusal;
        }
    }
}
