using System.Reflection;

namespace SlideWorks.WebApi.Features.Templates.Views;

// The template screen samples of src/contracts/screens, which SlideWorks.WebApi.csproj embeds for the stub services:
// a change to a sample file changes what its stub answers.
// TODO: goes, with its csproj lines, when the real services replace the stubs.
public static class TemplateSamples
{
    public const string TEMPLATES = "templates.json";
    public const string EDITOR = "template-editor.json";
    public const string SHARING = "template-sharing.json";
    public const string PUBLIC = "template-public.json";
    public const string LAYOUT_REVIEW = "layout-review.json";

    // A sample as its view record, read the way the wire is written.
    public static T Read<T>(string file)
    {
        var resource = "SlideWorks.WebApi.Features.Templates.Samples." + file;
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"{resource} is not embedded in the WebApi assembly.");
        using var reader = new StreamReader(stream);
        return TemplateViewJson.Read<T>(reader.ReadToEnd());
    }
}
