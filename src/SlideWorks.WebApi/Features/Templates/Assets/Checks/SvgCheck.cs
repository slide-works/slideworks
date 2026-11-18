using System.Text;
using System.Xml;
using Agoda.IoC.Core;
using SlideWorks.WebApi.Features.Templates.Data;

namespace SlideWorks.WebApi.Features.Templates.Assets;

// The check of a logo or a piece of art (D6): the bytes are an SVG, a PNG or a WebP, found by their content (the file
// name and the type the uploader claims prove nothing). Both kinds share the one set of rules; only the kind differs.
// Nothing is ever rewritten: a file is accepted exactly as uploaded or refused.
public abstract class ImageCheck : IAssetCheck
{
    public abstract TemplateAssetKind Kind { get; }

    public async Task<AssetCheckResult> CheckAsync(Stream content, string fileName, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        return RasterRules.Check(bytes) ?? SvgRules.Check(bytes);
    }
}

[RegisterSingleton(For = typeof(IAssetCheck), OfCollection = true)]
public sealed class LogoCheck : ImageCheck
{
    public override TemplateAssetKind Kind => TemplateAssetKind.Logo;
}

[RegisterSingleton(For = typeof(IAssetCheck), OfCollection = true)]
public sealed class ArtCheck : ImageCheck
{
    public override TemplateAssetKind Kind => TemplateAssetKind.Art;
}

// The SVG rules. The bytes are read by XmlReader, which decides the encoding (a BOM, an XML declaration, UTF-16 with or
// without one), with DTD processing prohibited and no resolver, so nothing is fetched and no entity expands. Every node
// is walked and the first finding refuses the file. Nothing is cleaned: an SVG that would be safe with a part removed
// is still refused. No regular expression reads the markup; the only text matching is on single attribute values and
// the text of style elements, after the reader has decoded them.
public static class SvgRules
{
    public const string SVG_NAMESPACE = "http://www.w3.org/2000/svg";
    private const string XHTML_NAMESPACE = "http://www.w3.org/1999/xhtml";
    private const string MATHML_NAMESPACE = "http://www.w3.org/1998/Math/MathML";

    private static readonly string[] FOREIGN_ELEMENTS = ["foreignobject", "iframe", "embed", "object"];
    private static readonly string[] SCRIPT_ELEMENTS = ["script", "handler", "listener"];
    private static readonly string[] ANIMATIONS = ["set", "animate", "animatetransform", "animatemotion"];
    private static readonly string[] STYLE_BANS = ["@import", "image-set(", "src:", "expression(", "src(", "image(", "cross-fade("];

    public static AssetCheckResult Check(byte[] bytes)
    {
        try
        {
            var finding = Walk(bytes, DtdProcessing.Prohibit);
            return finding == null ? AssetCheckResult.Accepted(AssetContentTypes.SVG) : AssetCheckResult.Refused(finding);
        }
        catch (XmlException e)
        {
            // Prohibited DTD processing stops the reader at a DOCTYPE with an exception; a second pass that stops at the
            // DOCTYPE node tells that apart from a file that is not XML.
            if (HasDoctype(bytes))
            {
                return AssetCheckResult.Refused(TemplateErrorCodes.SVG_EXTERNAL_REFERENCE);
            }
            // Bytes that are not XML at all (the reader fails on its first character) are not an image.
            return e.LineNumber <= 1 && e.LinePosition <= 1
                ? AssetCheckResult.Refused(TemplateErrorCodes.NOT_AN_IMAGE)
                : AssetCheckResult.Refused(TemplateErrorCodes.SVG_MALFORMED);
        }
        catch (Exception e) when (e is DecoderFallbackException or ArgumentException or InvalidOperationException)
        {
            return AssetCheckResult.Refused(TemplateErrorCodes.SVG_MALFORMED);
        }
    }

    private static bool HasDoctype(byte[] bytes)
    {
        try
        {
            // Parse reports the DOCTYPE node; the pass ends there, before any entity is read, and with no resolver nothing
            // outside the file is opened.
            using var reader = Open(bytes, DtdProcessing.Parse);
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.DocumentType)
                {
                    return true;
                }
            }
        }
        catch (Exception e) when (e is XmlException or DecoderFallbackException or ArgumentException or InvalidOperationException)
        {
            // The file is refused for being malformed, not for its DOCTYPE.
        }
        return false;
    }

    private static XmlReader Open(byte[] bytes, DtdProcessing dtd)
        => XmlReader.Create(new MemoryStream(bytes), new XmlReaderSettings
        {
            DtdProcessing = dtd,
            XmlResolver = null,
            IgnoreComments = true,
            MaxCharactersFromEntities = 1,
            CloseInput = true,
        });

    // Null when the file is clean, otherwise the code of the first finding.
    private static string? Walk(byte[] bytes, DtdProcessing dtd)
    {
        using var reader = Open(bytes, dtd);
        var sawRoot = false;
        var styleDepth = -1;
        var styleText = new StringBuilder();
        while (reader.Read())
        {
            switch (reader.NodeType)
            {
                case XmlNodeType.DocumentType:
                case XmlNodeType.ProcessingInstruction:
                case XmlNodeType.EntityReference:
                    return TemplateErrorCodes.SVG_EXTERNAL_REFERENCE;
                case XmlNodeType.Element:
                    if (!sawRoot)
                    {
                        sawRoot = true;
                        if (!IsSvgRoot(reader))
                        {
                            return TemplateErrorCodes.SVG_MALFORMED;
                        }
                    }
                    var element = CheckElement(reader);
                    if (element != null)
                    {
                        return element;
                    }
                    if (IsNamed(reader.LocalName, "style") && !reader.IsEmptyElement && styleDepth < 0)
                    {
                        styleDepth = reader.Depth;
                        styleText.Clear();
                    }
                    break;
                case XmlNodeType.Text or XmlNodeType.CDATA or XmlNodeType.Whitespace or XmlNodeType.SignificantWhitespace:
                    if (styleDepth >= 0)
                    {
                        styleText.Append(reader.Value);
                    }
                    break;
                case XmlNodeType.EndElement when reader.Depth == styleDepth:
                    styleDepth = -1;
                    if (CheckStyle(styleText.ToString()) is { } style)
                    {
                        return style;
                    }
                    break;
            }
        }
        return sawRoot ? null : TemplateErrorCodes.SVG_MALFORMED;
    }

    private static bool IsSvgRoot(XmlReader reader)
        => reader.LocalName == "svg" && reader.NamespaceURI == SVG_NAMESPACE;

    private static bool IsNamed(string name, string expected) => string.Equals(name, expected, StringComparison.OrdinalIgnoreCase);

    // The element's own name, then every one of its attributes.
    private static string? CheckElement(XmlReader reader)
    {
        var name = reader.LocalName;
        if (SCRIPT_ELEMENTS.Any(n => IsNamed(name, n)))
        {
            return TemplateErrorCodes.SVG_SCRIPT;
        }
        // HTML and MathML elements load and navigate (img, meta refresh, form, video); no editor export puts them
        // outside a foreignObject, so they are refused whatever their name. Other namespaces (Inkscape, RDF) pass.
        if (reader.NamespaceURI is XHTML_NAMESPACE or MATHML_NAMESPACE || FOREIGN_ELEMENTS.Any(n => IsNamed(name, n)))
        {
            return TemplateErrorCodes.SVG_FOREIGN_OBJECT;
        }
        var animates = ANIMATIONS.Any(n => IsNamed(name, n));
        if (!reader.MoveToFirstAttribute())
        {
            return null;
        }
        do
        {
            var finding = CheckAttribute(reader, animates);
            if (finding != null)
            {
                reader.MoveToElement();
                return finding;
            }
        }
        while (reader.MoveToNextAttribute());
        reader.MoveToElement();
        return null;
    }

    private static string? CheckAttribute(XmlReader reader, bool onAnimation)
    {
        var local = reader.LocalName;
        var value = reader.Value;
        if (local.StartsWith("on", StringComparison.OrdinalIgnoreCase))
        {
            return TemplateErrorCodes.SVG_SCRIPT;
        }
        var squeezed = Squeeze(value);
        if (squeezed.Contains("javascript:", StringComparison.Ordinal))
        {
            return TemplateErrorCodes.SVG_SCRIPT;
        }
        if (onAnimation && IsNamed(local, "attributeName")
            && LocalPart(value.Trim()) is var target
            && (IsNamed(target, "href") || target.StartsWith("on", StringComparison.OrdinalIgnoreCase)))
        {
            return TemplateErrorCodes.SVG_SCRIPT;
        }
        if (IsNamed(local, "href") && !value.StartsWith('#'))
        {
            return TemplateErrorCodes.SVG_EXTERNAL_REFERENCE;
        }
        if (reader.Prefix == "xml" && local == "base")
        {
            return TemplateErrorCodes.SVG_EXTERNAL_REFERENCE;
        }
        // Any attribute may be read as CSS (a presentation attribute, style), so all of them get the style rules, and
        // a backslash (a CSS escape, never decoded here) is refused in every one.
        return value.Contains('\\') ? TemplateErrorCodes.SVG_EXTERNAL_REFERENCE : CheckStyle(squeezed);
    }

    private static string? CheckStyle(string css)
    {
        if (css.Contains('\\'))
        {
            return TemplateErrorCodes.SVG_EXTERNAL_REFERENCE;
        }
        var squeezed = Squeeze(css);
        if (STYLE_BANS.Any(ban => squeezed.Contains(ban, StringComparison.Ordinal)))
        {
            return TemplateErrorCodes.SVG_EXTERNAL_REFERENCE;
        }
        for (var at = squeezed.IndexOf("url(", StringComparison.Ordinal); at >= 0; at = squeezed.IndexOf("url(", at + 1, StringComparison.Ordinal))
        {
            var after = at + 4;
            while (after < squeezed.Length && squeezed[after] is '"' or '\'')
            {
                after++;
            }
            if (after >= squeezed.Length || squeezed[after] != '#')
            {
                return TemplateErrorCodes.SVG_EXTERNAL_REFERENCE;
            }
        }
        return null;
    }

    private static string LocalPart(string name)
    {
        var colon = name.IndexOf(':');
        return colon < 0 ? name : name[(colon + 1)..];
    }

    // Without whitespace and control characters, lower-cased: how a browser reads a scheme or a CSS function.
    private static string Squeeze(string value)
        => new([.. value.Where(c => !char.IsWhiteSpace(c) && !char.IsControl(c)).Select(char.ToLowerInvariant)]);
}
