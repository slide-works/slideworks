using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using SlideWorks.WebApi.Tests.Mcp;

namespace SlideWorks.WebApi.Tests.Images;

// Images made the way an agent makes them: upload_image over MCP, then a PUT of the bytes to the upload URL with no
// cookie and no Origin.
public static class ImageSeed
{
    public static readonly byte[] PNG_SIGNATURE = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    // Bytes that start as the type says and run to length; the server checks only the magic bytes.
    public static byte[] Bytes(string contentType, int length = 256)
    {
        byte[] header = contentType switch
        {
            "image/png" => PNG_SIGNATURE,
            "image/jpeg" => [0xFF, 0xD8, 0xFF, 0xE0],
            "image/gif" => "GIF89a"u8.ToArray(),
            "image/webp" => [.. "RIFF"u8, 0x24, 0x00, 0x00, 0x00, .. "WEBPVP8 "u8],
            _ => throw new ArgumentException($"No sample bytes for {contentType}.", nameof(contentType)),
        };
        var bytes = new byte[Math.Max(length, header.Length)];
        Random.Shared.NextBytes(bytes);
        header.CopyTo(bytes, 0);
        return bytes;
    }

    // The upload_image result; fails the test on a tool error.
    public static async Task<JsonElement> StartAsync(McpTestClient agent, string deck, string name = "story-map.png", string contentType = "image/png")
    {
        var (isError, json) = await agent.CallToolAsync("upload_image", new { deck, name, contentType });
        isError.ShouldBeFalse(json.ToString());
        return json;
    }

    // PUTs bytes to an upload URL as an agent would: no sign-in, no Origin.
    public static async Task<HttpResponseMessage> PutAsync(WebApplicationFactory<ApiMarker> factory, string uploadUrl, byte[] bytes, string? contentType = "image/png")
    {
        using var http = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var content = new ByteArrayContent(bytes);
        if (contentType != null)
        {
            content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        }
        return await http.PutAsync(uploadUrl, content);
    }

    // upload_image plus the PUT; returns the image id, ready for an image slot.
    public static async Task<string> UploadAsync(McpTestClient agent, WebApplicationFactory<ApiMarker> factory, string deck, byte[] bytes, string contentType = "image/png", string name = "story-map.png")
    {
        var started = await StartAsync(agent, deck, name, contentType);
        using var put = await PutAsync(factory, started.GetProperty("uploadUrl").GetString()!, bytes, contentType);
        put.StatusCode.ShouldBe(HttpStatusCode.NoContent, await put.Content.ReadAsStringAsync());
        return started.GetProperty("image").GetString()!;
    }

    // The client's fixture image (K11), a real PNG.
    public static byte[] SamplePng()
    {
        var root = Path.GetDirectoryName(Path.GetDirectoryName(Support.JsonShape.ScreensDirectory()))!;
        return File.ReadAllBytes(Path.Combine(root, "SlideWorks.Client", "src", "test-support", "fixtures", "sample-image.png"));
    }
}
