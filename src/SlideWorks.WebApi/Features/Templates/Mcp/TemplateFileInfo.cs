namespace SlideWorks.WebApi.Features.Templates.Mcp;

// One stored file of a template draft as get_template lists it. Asset is the id the manifest names the file by; Kind is
// font, logo, art or layout.
public sealed record TemplateFileInfo(string Asset, string Kind, string FileName, int Bytes, bool RightsConfirmed);
