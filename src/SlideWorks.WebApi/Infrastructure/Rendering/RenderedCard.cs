namespace SlideWorks.WebApi.Infrastructure.Rendering;

// A rendered share card: the PNG bytes and the RenderKey they were rendered for, which callers use as the ETag.
public sealed record RenderedCard(byte[] Png, string Key);
