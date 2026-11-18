using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.Templates.Layouts;

namespace SlideWorks.WebApi.Tests.Support;

// ITemplateLayouts for tests that need a template with custom layouts before the store exists. Add gives a version of a
// template a layout (version 0 is the draft), kept in the order added, as the real one keeps the manifest's order.
// ApproveAsync writes nothing: it records the call and answers what Outcome set last (Approved until a test sets one).
// The app, and so this fake, is shared by every test in a fixture: each test uses template ids of its own, and a test
// that calls Outcome puts it back when it is done. Add it with FakeTemplateLayouts.AddTo.
public sealed class FakeTemplateLayouts : ITemplateLayouts
{
    private readonly Lock _gate = new();
    private readonly List<(string TemplateId, int Version, TemplateLayout Layout)> _layouts = [];
    private readonly List<(string TemplateId, string Name, LayoutReport Report)> _approvals = [];
    private ApproveOutcome _outcome = ApproveOutcome.Approved;

    public static void AddTo(IServiceCollection services)
        => services.AddSingleton<FakeTemplateLayouts>().AddSingleton<ITemplateLayouts>(provider => provider.GetRequiredService<FakeTemplateLayouts>());

    public void Add(string templateId, int version, TemplateLayout layout)
    {
        lock (_gate)
        {
            _layouts.Add((templateId, version, layout));
        }
    }

    // What every ApproveAsync answers from now on.
    public void Outcome(ApproveOutcome outcome)
    {
        lock (_gate)
        {
            _outcome = outcome;
        }
    }

    // The ApproveAsync calls made for a template, in order.
    public IReadOnlyList<(string Name, LayoutReport Report)> ApprovalsFor(string templateId)
    {
        lock (_gate)
        {
            return [.. _approvals.Where(call => call.TemplateId == templateId).Select(call => (call.Name, call.Report))];
        }
    }

    public Task<IReadOnlyList<TemplateLayout>> ForVersionAsync(string templateId, int version, CancellationToken ct)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<TemplateLayout>>([.. _layouts.Where(held => held.TemplateId == templateId && held.Version == version).Select(held => held.Layout)]);
        }
    }

    public Task<ApproveOutcome> ApproveAsync(string templateId, string name, LayoutReport report, CancellationToken ct)
    {
        lock (_gate)
        {
            _approvals.Add((templateId, name, report));
            return Task.FromResult(_outcome);
        }
    }
}
