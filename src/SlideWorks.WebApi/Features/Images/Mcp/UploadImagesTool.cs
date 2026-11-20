using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Mcp;

namespace SlideWorks.WebApi.Features.Images.Mcp;

[McpServerToolType]
public sealed class UploadImagesTool
{
    [McpServerTool(Name = ImageUploads.MANY_TOOL)]
    [Description("Start uploading several images to a deck you own, in one call. files is a list of {name, contentType}, up to 40. Returns every image id and its one-time uploadUrl: PUT each file's raw bytes to its uploadUrl (no auth header) within expiresInSeconds, then use the id in an image slot. PNG, JPEG, WebP or GIF up to 10 MB each; the bytes must match contentType. SVG is not accepted.")]
    public Task<CallToolResult> UploadImages(
        ImageUploads images,
        [Description("The deck's id.")] string deck,
        [Description("The files to upload: [{name, contentType}], from 1 to 40. name is the file name, such as story-map.png, up to 200 characters. contentType is image/png, image/jpeg, image/webp or image/gif.")] JsonElement files)
        => McpToolErrors.Run(() => images.CreateManyAsync(deck, Read(files), ImageUploads.MANY_TOOL));

    private static List<ImageUploadRequest> Read(JsonElement files)
    {
        if (files.ValueKind != JsonValueKind.Array)
        {
            throw new BadRequestException("files: a list of {name, contentType}");
        }

        var list = new List<ImageUploadRequest>();
        var index = 0;
        foreach (var file in files.EnumerateArray())
        {
            if (file.ValueKind != JsonValueKind.Object)
            {
                throw new BadRequestException($"files[{index}]: an object with name and contentType");
            }

            string? name = null;
            string? contentType = null;
            foreach (var property in file.EnumerateObject())
            {
                switch (property.Name)
                {
                    case "name":
                        name = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : throw new BadRequestException($"files[{index}].name: must be text");
                        break;
                    case "contentType":
                        contentType = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : throw new BadRequestException($"files[{index}].contentType: must be text");
                        break;
                    default:
                        throw new BadRequestException($"files[{index}].{property.Name}: unknown field");
                }
            }

            list.Add(new ImageUploadRequest(name, contentType));
            index++;
        }

        return list;
    }
}
