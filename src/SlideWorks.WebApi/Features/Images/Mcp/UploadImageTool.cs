using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SlideWorks.WebApi.Infrastructure.Mcp;

namespace SlideWorks.WebApi.Features.Images.Mcp;

[McpServerToolType]
public sealed class UploadImageTool
{
    [McpServerTool(Name = ImageUploads.TOOL)]
    [Description("Start uploading an image to a deck you own. Returns the image's id and a one-time uploadUrl: PUT the raw image bytes to it (no auth header needed) within expiresInSeconds, then use the id in an image slot ({ \"image\": \"<id>\", \"alt\": \"...\" }) with add_slide or update_slide. PNG, JPEG, WebP or GIF up to 10 MB; the bytes must match contentType. SVG is not accepted.")]
    public Task<CallToolResult> UploadImage(
        ImageUploads images,
        [Description("The deck's id.")] string deck,
        [Description("The image's file name, such as story-map.png, up to 200 characters. Shown in the deck's change log only.")] string name,
        [Description("image/png, image/jpeg, image/webp or image/gif.")] string contentType)
        => McpToolErrors.Run(() => images.CreateAsync(deck, name, contentType, ImageUploads.TOOL));
}
