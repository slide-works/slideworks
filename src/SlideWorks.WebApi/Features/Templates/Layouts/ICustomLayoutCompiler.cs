using SlideWorks.Core.Look.CustomLayouts;

namespace SlideWorks.WebApi.Features.Templates.Layouts;

// The app's one way into the custom-layout contract of Core (T10): compiling an upload and reading a stored layout.
public interface ICustomLayoutCompiler
{
    // The compiled layout, or every error found ({path, code, message}); it never throws for what an author wrote.
    // scope is 8 lower-case hex characters, the first 8 of the upload's asset id: the server writes every class of the
    // compiled CSS and tree as cl-<scope>-<layout name>__<class>. A scope of any other shape throws.
    CompileResult Compile(string markup, string css, string slotsJson, string scope);

    // A layout from its stored form (CompiledLayoutJson). Bad stored data throws StoredLayoutRefusedException,
    // JsonException or InvalidOperationException and nothing else: a caller that reads several layouts catches the three
    // for each one, leaves that layout out and logs it. The caller, not Read, compares the tree's scope with the first 8
    // characters of the asset id.
    CompiledLayout Read(string storedJson);
}
