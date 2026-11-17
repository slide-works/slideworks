using System.Text.Json;
using System.Text.Json.Serialization;

namespace SlideWorks.WebApi.Features.Templates.Views;

// PUT /api/templates/{id}: the whole manifest (T3) and the revision the caller read.
public sealed record UpdateTemplateRequest([property: JsonRequired] JsonElement Manifest, [property: JsonRequired] int Revision);
