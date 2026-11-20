namespace SlideWorks.Core.Slides;

// A link to one YouTube video. The text is stored as it was sent; the player never puts that text in
// an iframe. It reads the video id and a start time out of it, then builds the embed address itself.
// The same rules run in the client, against src/contracts/youtube-cases.json.
public static class YouTubeVideo
{
    public const int ID_LENGTH = 11;

    public const int MAX_START_SECONDS = 24 * 60 * 60;

    private static readonly HashSet<string> _hosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "youtube.com",
        "www.youtube.com",
        "m.youtube.com",
        "music.youtube.com",
        "youtube-nocookie.com",
        "www.youtube-nocookie.com",
        "youtu.be",
        "www.youtu.be",
    };

    private static readonly HashSet<string> _shortHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "youtu.be",
        "www.youtu.be",
    };

    // The id and the start time, in seconds, or false when the text is not one YouTube video.
    public static bool TryRead(string text, out string id, out int startSeconds)
    {
        id = "";
        startSeconds = 0;
        if (LinkSlot.Problem(text) is not null)
        {
            return false;
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || !uri.IsAbsoluteUri || !_hosts.Contains(uri.Host))
        {
            return false;
        }

        // The path as it was written. .NET decodes %2D in a path and a browser keeps it, so the id is read
        // from the raw text, which both sides see the same way.
        var segments = RawPath(text).Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (!TryId(uri, segments, out id))
        {
            return false;
        }

        startSeconds = StartOf(uri);
        return true;
    }

    private static bool TryId(Uri uri, string[] segments, out string id)
    {
        id = "";
        if (_shortHosts.Contains(uri.Host))
        {
            return segments.Length == 1 && IsId(segments[0], out id);
        }

        if (segments.Length == 1 && segments[0] == "watch")
        {
            return IsId(Query(uri, "v"), out id);
        }

        if (segments.Length == 2
            && segments[0] is "embed" or "shorts" or "live" or "v")
        {
            return IsId(segments[1], out id);
        }

        return false;
    }

    private static bool IsId(string? value, out string id)
    {
        id = "";
        if (value is null || value.Length != ID_LENGTH)
        {
            return false;
        }

        if (!value.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-'))
        {
            return false;
        }

        id = value;
        return true;
    }

    // start= wins over t=, matching the query string's own first-of-each and YouTube's embed parameter.
    private static int StartOf(Uri uri)
    {
        var raw = Query(uri, "start") ?? Query(uri, "t");
        return raw is null ? 0 : ParseTime(raw);
    }

    // A bare number of seconds, or YouTube's 1h2m3s. Anything else is no start time, and the video still plays.
    private static int ParseTime(string raw)
    {
        if (raw.Length == 0 || raw.Length > 32)
        {
            return 0;
        }

        if (raw.All(char.IsAsciiDigit))
        {
            return long.TryParse(raw, out var seconds) ? Clamp(seconds) : 0;
        }

        long total = 0;
        var index = 0;
        var any = false;
        while (index < raw.Length)
        {
            var start = index;
            while (index < raw.Length && char.IsAsciiDigit(raw[index]))
            {
                index++;
            }

            if (index == start || index >= raw.Length)
            {
                return 0;
            }

            var unit = raw[index++];
            if (unit is not ('h' or 'm' or 's'))
            {
                return 0;
            }

            if (!long.TryParse(raw.AsSpan(start, index - start - 1), out var number))
            {
                return 0;
            }

            any = true;
            total += unit switch
            {
                'h' => number * 3600,
                'm' => number * 60,
                _ => number,
            };
            if (total >= MAX_START_SECONDS)
            {
                return MAX_START_SECONDS;
            }
        }

        return any ? Clamp(total) : 0;
    }

    private static int Clamp(long seconds)
    {
        if (seconds <= 0)
        {
            return 0;
        }

        if (seconds >= MAX_START_SECONDS)
        {
            return MAX_START_SECONDS;
        }

        return (int)seconds;
    }

    // The first value of the named query parameter, decoded the way a browser decodes it, or null when it is absent.
    private static string? Query(Uri uri, string name)
    {
        var query = uri.Query;
        if (query.Length <= 1)
        {
            return null;
        }

        foreach (var part in query[1..].Split('&'))
        {
            if (part.Length == 0)
            {
                continue;
            }

            var eq = part.IndexOf('=');
            var rawKey = eq < 0 ? part : part[..eq];
            if (!string.Equals(Decode(rawKey), name, StringComparison.Ordinal))
            {
                continue;
            }

            return Decode(eq < 0 ? "" : part[(eq + 1)..]);
        }

        return null;
    }

    private static string RawPath(string text)
    {
        var scheme = text.IndexOf("://", StringComparison.Ordinal);
        if (scheme < 0)
        {
            return "";
        }

        var slash = text.IndexOf('/', scheme + 3);
        if (slash < 0)
        {
            return "";
        }

        var end = text.IndexOfAny(['?', '#'], slash);
        return end < 0 ? text[slash..] : text[slash..end];
    }

    private static string Decode(string raw)
    {
        try
        {
            return Uri.UnescapeDataString(raw.Replace('+', ' '));
        }
        catch (UriFormatException)
        {
            return "";
        }
    }
}
