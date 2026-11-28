using System.Text.Json.Serialization;

namespace SlideWorks.WebApi.Features.AppLinks;

public sealed record AssetLinkTargetView(
    [property: JsonPropertyName("namespace")] string Namespace,
    [property: JsonPropertyName("package_name")] string PackageName,
    [property: JsonPropertyName("sha256_cert_fingerprints")] IReadOnlyList<string> Sha256CertFingerprints);
