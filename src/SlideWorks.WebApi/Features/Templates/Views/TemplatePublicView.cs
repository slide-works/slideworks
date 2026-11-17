using SlideWorks.WebApi.Features.Look.Views;

namespace SlideWorks.WebApi.Features.Templates.Views;

// GET /api/templates/{id}/public: the template's page at /t/{id} (src/contracts/screens/template-public.json). Look is
// the published version's look, as a deck on it would get.
public sealed record TemplatePublicView(string Id, string Name, string Owner, string Purpose, LookView Look);
