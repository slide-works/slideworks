namespace SlideWorks.WebApi.Infrastructure.Identity;

// The claim types on every SlideWorks principal, for people (cookie session) and agents (bearer token) alike.
public static class SlideWorksClaims
{
    // The User.Id of the person, or of the presenter an agent acts for.
    public const string PRESENTER_ID = "slideworks:presenter_id";

    public const string EMAIL = "slideworks:email";

    public const string NAME = "slideworks:name";

    // An agent's OAuth client_name, such as "Claude". Absent for a person.
    public const string AGENT_CLIENT_NAME = "slideworks:agent_client_name";

    // "Person" or "Agent".
    public const string ACTOR_KIND = "slideworks:actor_kind";
}
