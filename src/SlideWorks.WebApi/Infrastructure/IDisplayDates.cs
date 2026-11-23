namespace SlideWorks.WebApi.Infrastructure;

// The absolute dates in labels (change logs, lists, the admin list), formatted for the person making the request.
// Slide footer dates (DisplayDate.Day) and template labels are not here: they stay UTC.
public interface IDisplayDates
{
    // How long ago at was, as of now: "just now", "2 min ago", "Yesterday", then a date.
    string Ago(DateTimeOffset at);

    // An absolute date and time label.
    string Format(DateTimeOffset at);
}
