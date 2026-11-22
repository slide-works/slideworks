using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SlideWorks.WebApi.Features.Templates.Assets;
using SlideWorks.WebApi.Features.Templates.Data;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Infrastructure.Mcp;

namespace SlideWorks.WebApi.Features.Templates.Mcp;

// upload_asset: starts a one-time upload for a template the caller owns. The bytes never pass through the model: the agent
// PUTs the file to the returned address. The right to serve a font is never confirmed here, whatever the agent says.
[McpServerToolType]
public sealed class UploadAssetTool
{
    private static readonly string[] KINDS = ["font", "logo", "art", "layout"];

    [McpServerTool(Name = "upload_asset")]
    [Description("Start the upload of one file for a template draft you own: a font (WOFF2), a logo or art (SVG, PNG or WebP), or a custom layout. Returns upload_url, a curl line to run, the kind and when the address expires. Run the curl line, one file after another: the address works once, and the upload answers { assetId, kind, fileName, contentType, bytes, meta }. Put that assetId in the manifest with update_template. You cannot confirm font rights: a person ticks them in the template's editor before it can be published. A layout's file is one JSON document { markup, css, slots }. A layout must then be listed in the template's customLayouts as { name, file } with update_template, using the assetId as file. The template's owner approves the layout in the app before the template can be published. If the upload refuses it, the answer carries the error code and message, plus each finding's path, code and message when there are any.")]
    public Task<CallToolResult> UploadAsset(
        ITemplateAssets assets,
        ICurrentActor actor,
        [Description("The template's `tpl_…` id, as create_template or list_templates gives it.")] string template,
        [Description("What the file is: font, logo, art or layout.")] string kind,
        [Description("The file's name, as a label for people (up to 200 characters), and the name curl reads on your side.")] string filename)
        => McpToolErrors.Run(async () =>
        {
            var parsed = KindOf(kind);
            var started = await assets.StartUploadAsync(actor, template, parsed, filename, rightsConfirmed: false);
            return new UploadAssetResult(started.UploadUrl, $"curl -T {Quote(filename)} \"{started.UploadUrl}\"", KINDS[(int)parsed], started.ExpiresLabel);
        });

    private static TemplateAssetKind KindOf(string kind)
    {
        var index = Array.FindIndex(KINDS, k => string.Equals(k, kind?.Trim(), StringComparison.OrdinalIgnoreCase));
        return index < 0
            ? throw new BadRequestException($"kind: must be {string.Join(", ", KINDS[..^1])} or {KINDS[^1]}")
            : (TemplateAssetKind)index;
    }

    // Left as it is when a shell would read it as one word, else in single quotes. A name that starts with a dash would be
    // read as an option, so it is written as ./name.
    private static string Quote(string name)
    {
        var safe = name.Length > 0 && name.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-' or '/');
        if (safe)
        {
            return name[0] == '-' ? $"./{name}" : name;
        }
        return $"'{name.Replace("'", "'\\''")}'";
    }
}
