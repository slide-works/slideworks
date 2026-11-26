using System.Collections.Concurrent;
using SlideWorks.WebApi.Features.Checks;
using SlideWorks.WebApi.Features.Templates.Assets;
using SlideWorks.WebApi.Infrastructure.Rendering;

namespace SlideWorks.WebApi.Tests.Support;

// The test double for IRenderEngine (L9), swapped in on every test host by TestAppFactory. It returns a fixed 1200x630
// PNG (or the PDF bytes), records every call (a deck id, "pdf:<deck>" for a PDF, or GENERIC for the generic card), can
// hold PDF renders at a gate, and throws on demand.
public sealed class FakeRenderEngine : IRenderEngine
{
    public const string GENERIC = "generic";
    public const int WIDTH = 1200;
    public const int HEIGHT = 630;

    // The PNG signature and an IHDR chunk naming 1200x630 (0x04B0 by 0x0276); the server never decodes the rest.
    public static readonly byte[] PNG =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x04, 0xB0, 0x00, 0x00, 0x02, 0x76,
        0x08, 0x06, 0x00, 0x00, 0x00,
    ];

    private readonly ConcurrentQueue<string> _calls = new();

    // Every call so far, in order: the deck id, or GENERIC.
    public IReadOnlyList<string> Calls => [.. _calls];

    // When set, every render throws this instead of returning the PNG.
    public Exception? ThrowOnRender { get; set; }

    // The raw width MeasureFontAsync answers: by default the built-in body font's, so a test font reads 0.56 em.
    public double MeasuredRaw { get; set; } = FontMeasure.REFERENCE;

    // When set, every font measure throws this instead of answering MeasuredRaw.
    public Exception? ThrowOnMeasure { get; set; }

    private int _measureCalls;

    public int MeasureCalls => Volatile.Read(ref _measureCalls);

    public Task<double> MeasureFontAsync(byte[] woff2, CancellationToken ct)
    {
        Interlocked.Increment(ref _measureCalls);
        return ThrowOnMeasure == null ? Task.FromResult(MeasuredRaw) : Task.FromException<double>(ThrowOnMeasure);
    }

    public void ThrowUnavailable() => ThrowOnRender = new RendererUnavailableException("Rendering isn't available");

    public void ThrowTimeout() => ThrowOnRender = new TimeoutException("Rendering timed out");

    public Task<byte[]> RenderCardAsync(string deckId, CancellationToken ct) => Render(deckId);

    // Fixed bytes of a PDF: the header the server checks for, and nothing the server decodes.
    public static readonly byte[] PDF =
    [
        0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x37, 0x0A,
        0x25, 0x66, 0x61, 0x6B, 0x65, 0x0A,
    ];

    // Records "pdf:<deck>", and throws ThrowOnRender when set, as every render does.
    public async Task<byte[]> RenderPdfAsync(string deckId, CancellationToken ct)
    {
        _calls.Enqueue($"pdf:{deckId}");
        if (Volatile.Read(ref _pdfGate) is { } gate)
        {
            _pdfHeld?.TrySetResult();
            await gate.Task;
        }
        return await (ThrowOnRender == null ? Task.FromResult(PDF.ToArray()) : Task.FromException<byte[]>(ThrowOnRender));
    }

    private TaskCompletionSource? _pdfGate;
    private TaskCompletionSource? _pdfHeld;

    // Completed once a PDF render has reached the gate of the latest HoldPdfRenders (call that first).
    public Task PdfRenderHeld => Volatile.Read(ref _pdfHeld)?.Task ?? Task.FromException(new InvalidOperationException("Call HoldPdfRenders first."));

    // Holds every PDF render that starts from now on, after its call is recorded, until the handle is disposed (or Reset).
    public IDisposable HoldPdfRenders()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Volatile.Write(ref _pdfHeld, new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
        Volatile.Write(ref _pdfGate, gate);
        return new PdfHold(this, gate);
    }

    private void ReleasePdfRenders(TaskCompletionSource gate)
    {
        Interlocked.CompareExchange(ref _pdfGate, null, gate);
        gate.TrySetResult();
    }

    private sealed class PdfHold(FakeRenderEngine engine, TaskCompletionSource gate) : IDisposable
    {
        public void Dispose() => engine.ReleasePdfRenders(gate);
    }

    public Task<byte[]> RenderGenericCardAsync(CancellationToken ct) => Render(GENERIC);

    private readonly ConcurrentDictionary<int, IReadOnlyList<SlideFinding>> _findings = new();
    private readonly ConcurrentDictionary<int, Exception> _failures = new();

    // The findings every render of this slide number carries (in any deck).
    public void FindingsFor(int slide, params SlideFinding[] findings) => _findings[slide] = findings;

    // Back to rendering every slide cleanly with no findings, for the next test of a shared fixture.
    public void Reset()
    {
        ThrowOnRender = null;
        if (Volatile.Read(ref _pdfGate) is { } gate)
        {
            ReleasePdfRenders(gate);
        }
        _findings.Clear();
        _failures.Clear();
    }

    // Makes every render of this slide number throw error. ThrowOnRender, when set, still wins for all slides.
    public void FailSlide(int slide, Exception error) => _failures[slide] = error;

    public async Task<RenderedSlide> RenderSlideAsync(string deckId, int slide, CancellationToken ct)
    {
        var call = $"slide:{deckId}:{slide}";
        if (_failures.TryGetValue(slide, out var failure) && ThrowOnRender == null)
        {
            _calls.Enqueue(call);
            throw failure;
        }

        return new RenderedSlide(await Render(call), _findings.GetValueOrDefault(slide, []));
    }

    private Task<byte[]> Render(string call)
    {
        _calls.Enqueue(call);
        return ThrowOnRender == null ? Task.FromResult(PNG.ToArray()) : Task.FromException<byte[]>(ThrowOnRender);
    }
}
