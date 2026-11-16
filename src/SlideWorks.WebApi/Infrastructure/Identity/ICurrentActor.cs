namespace SlideWorks.WebApi.Infrastructure.Identity;

// Who is calling, for the current request (K1). It reads the SlideWorksClaims on the request's principal, so it is the
// same for a person signed in by cookie and for an agent signed in by bearer token.
public interface ICurrentActor
{
    // False for an anonymous caller, such as a viewer opening a deck shared by link.
    bool IsSignedIn { get; }

    // The person's User.Id, or for an agent the presenter it acts for. Null when anonymous.
    Guid? PresenterId { get; }

    // The person's email, or for an agent its presenter's. Null when anonymous.
    string? Email { get; }

    // The person's name, or for an agent its OAuth client_name, such as "Claude". Null when anonymous.
    string? Name { get; }

    ActorKind Kind { get; }

    // Web for a person, Mcp for an agent.
    ActorVia Via { get; }
}
