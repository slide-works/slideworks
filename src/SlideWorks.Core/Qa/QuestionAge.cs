namespace SlideWorks.Core.Qa;

// How old a question is, as the label the server sends with it (Q4, D22), so the phone never needs a clock: "now" under a
// minute, then whole minutes ("2 min") under an hour, then whole hours ("1 h"). A time ahead of now (clock skew) is "now".
public static class QuestionAge
{
    public static string Label(DateTimeOffset askedAt, DateTimeOffset now)
    {
        var age = now - askedAt;
        if (age < TimeSpan.FromMinutes(1))
        {
            return "now";
        }

        return age < TimeSpan.FromHours(1) ? $"{(int)age.TotalMinutes} min" : $"{(int)age.TotalHours} h";
    }
}
