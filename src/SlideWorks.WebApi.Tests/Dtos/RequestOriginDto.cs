namespace SlideWorks.WebApi.Tests.Dtos;

// The scheme and host the app saw a request arrive on, after its own middleware ran (SessionProbeController).
public record RequestOriginDto(string Scheme, string Host);
