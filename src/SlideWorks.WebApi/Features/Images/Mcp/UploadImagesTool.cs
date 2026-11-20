using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SlideWorks.WebApi.Infrastructure.Mcp;

namespace SlideWorks.WebApi.Features.Images.Mcp;

[McpServerToolType]
public sealed class UploadImagesTool
{
    [McpServerTool(Name = ImageUploads.MANY_TOOL)]
    [Description("Start uploading up to 30 images to a deck you own in one call. Returns, for each file in order, its image id and a one-time uploadUrl: PUT the raw image bytes to each (no auth header needed) within expiresInSeconds, then use the ids in image slots ({ \"image\": \"<id>\", \"alt\": \"...\" }) with add_slide or update_slide. PNG, JPEG, WebP or GIF up to 10 MB each; the bytes must match contentType. SVG is not accepted. If any file is refused, nothing is started.")]
    public Task<CallToolResult> UploadImages(
        ImageUploads images,
        [Description("The deck's id.")] string deck,
        [Description("1 to 30 files, each { name, contentType }.")] UploadImagesFile[] files)
        => McpToolErrors.Run(() => images.CreateManyAsync(deck, files, ImageUploads.MANY_TOOL));
}
