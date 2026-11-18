namespace SlideWorks.LoadTest;

// What a run found: the table for a person, the JSON line for a script, and the verdict the exit code carries.
public interface IRunReport
{
    bool GatePassed { get; }

    string Table();

    string Json();
}
