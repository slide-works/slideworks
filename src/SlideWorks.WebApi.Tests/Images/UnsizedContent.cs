using System.Net;

namespace SlideWorks.WebApi.Tests.Images;

// A request body that doesn't say how long it is, so it goes without a Content-Length, as a streamed upload would.
public sealed class UnsizedContent(byte[] bytes) : HttpContent
{
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(bytes).AsTask();

    protected override bool TryComputeLength(out long length)
    {
        length = 0;
        return false;
    }
}
