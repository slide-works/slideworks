namespace SlideWorks.WebApi.Tests.Identity.Approval;

// What ActorProbeController saw of ICurrentActor. Access is the UserAccess name, or null.
public sealed record ActorDto(bool IsSignedIn, string? Email, string Kind, string? Access, bool IsAdmin);
