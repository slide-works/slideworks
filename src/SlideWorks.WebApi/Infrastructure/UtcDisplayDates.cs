using Agoda.IoC.Core;
using SlideWorks.Core;

namespace SlideWorks.WebApi.Infrastructure;

// TODO: replace with a version that formats in the requesting person's time zone.
[RegisterPerRequest]
public sealed class UtcDisplayDates(TimeProvider clock) : IDisplayDates
{
    public string Ago(DateTimeOffset at) => DisplayDate.Ago(at, clock.GetUtcNow());

    public string Format(DateTimeOffset at) => DisplayDate.Format(at);
}
