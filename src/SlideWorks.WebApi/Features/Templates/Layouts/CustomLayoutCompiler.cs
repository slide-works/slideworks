using Agoda.IoC.Core;
using SlideWorks.Core.Look.CustomLayouts;

namespace SlideWorks.WebApi.Features.Templates.Layouts;

// The pipeline of Core over its five stages. The stages keep their names when they become real, so this file does not
// change with them.
[RegisterSingleton(For = typeof(ICustomLayoutCompiler))]
public sealed class CustomLayoutCompiler : ICustomLayoutCompiler
{
    private readonly SlotSchemaCompiler _schema = new();
    private readonly CustomLayoutPipeline _pipeline;

    public CustomLayoutCompiler()
    {
        _pipeline = new CustomLayoutPipeline(new MarkupSanitiser(), new CssSanitiser(), _schema, new LayoutCrossCheck(), new LayoutSamples());
    }

    public CompileResult Compile(string markup, string css, string slotsJson, string scope) => _pipeline.Compile(markup, css, slotsJson, scope);

    public CompiledLayout Read(string storedJson) => CompiledLayoutJson.Read(storedJson, _schema);
}
