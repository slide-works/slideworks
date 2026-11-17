namespace SlideWorks.WebApi.Realtime.Presenting;

// Which kind of window or device joined a presentation (P1). On the wire as "presenter", "audience" or "phone".
public enum PresentationRole
{
    Presenter,
    Audience,
    Phone,
}
