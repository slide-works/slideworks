using System.Text.Json.Serialization;

namespace SlideWorks.WebApi.Features.OAuth.Authorize;

public sealed record ConsentDecisionRequest([property: JsonRequired] bool Approve);
