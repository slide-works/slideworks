using System.Text.Json;
using SlideWorks.WebApi.Features.Templates.Data;

namespace SlideWorks.WebApi.Features.Templates.Assets;

// What storing a template's file answers (T6): { assetId, kind, fileName, contentType, bytes, meta }. Meta is the
// object the kind's check stored, {} when it stored none.
public sealed record StoredAsset(string AssetId, TemplateAssetKind Kind, string FileName, string ContentType, long Bytes, JsonElement Meta);
