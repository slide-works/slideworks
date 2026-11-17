namespace SlideWorks.WebApi.Infrastructure.Identity;

// The links people scan or type to join a talk (J1). They come from configuration, never from a request's Host header or a
// forwarded one: SLIDEWORKS_JOIN_URL when it is set, otherwise SLIDEWORKS_PUBLIC_URL with a /j path.
public interface IJoinUrls
{
    // {JOIN_URL}/{code}, or with no join URL {PUBLIC_URL}/j/{code}.
    string JoinUrl(string code);

    // {JOIN_URL}/join, or with no join URL {PUBLIC_URL}/join.
    string KeypadUrl();

    // What a slide prints: the join host, such as slideworks.live, or the public host with its /j path.
    string HostLabel();
}
