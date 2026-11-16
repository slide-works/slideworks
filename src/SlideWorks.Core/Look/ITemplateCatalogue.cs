namespace SlideWorks.Core.Look;

// The templates a deck can use (L3). TemplateCatalogue holds the built-ins; tests swap in a catalogue
// with a locking template.
public interface ITemplateCatalogue
{
    IReadOnlyList<Template> All { get; }

    Template Default { get; }

    Template? Find(string name);
}
