namespace SlideWorks.WebApi.Infrastructure.Identity.Google;

// Where sign-in sends the person afterwards. Only a local path is kept, with its query string and fragment: it starts with
// a single '/', the next character is neither '/' nor '\' (a browser reads both as the start of another host), and every
// character is printable ASCII (0x20 to 0x7E): no control characters, and nothing Kestrel would refuse in the Location
// header, which would turn a crafted link into a 500 after sign-in. Anything else, such as https://evil.example, //evil.example or /\evil.example, goes home. The
// value arrives already URL-decoded once, so %2F%2F is refused as //, and anything still encoded stays inside the path.
public static class ReturnTo
{
    public const string HOME = "/";

    public static string LocalOrHome(string? value)
        => IsLocalPath(value) ? value! : HOME;

    private static bool IsLocalPath(string? value)
        => value is { Length: > 0 }
            && value[0] == '/'
            && (value.Length == 1 || (value[1] != '/' && value[1] != '\\'))
            && value.All(c => c is >= ' ' and <= '~');
}
