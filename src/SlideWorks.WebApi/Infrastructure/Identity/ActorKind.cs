namespace SlideWorks.WebApi.Infrastructure.Identity;

// Who is acting: a person in the browser, or an agent acting for a presenter over MCP. Its names are the values of the
// SlideWorksClaims.ACTOR_KIND claim.
public enum ActorKind
{
    Person,
    Agent,
}
