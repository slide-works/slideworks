namespace SlideWorks.WebApi.Realtime;

// How many guests connected now read the deck in one language (SW-886 K11): on the wire {"code": "th", "count": 9}.
public sealed record ReadingInCount(string Code, int Count);
