namespace SlideWorks.WebApi.Features.OAuth.ConnectedAgents;

// One live token family of a person: the client's name, when it connected ("Unknown" for families from before the
// connect time was kept) and when it last made a request, both as labels.
public sealed record ConnectedAgent(Guid FamilyId, string ClientName, string Connected, string? LastUsed);
