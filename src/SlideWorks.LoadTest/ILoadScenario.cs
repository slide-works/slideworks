namespace SlideWorks.LoadTest;

// What a run plays (--scenario): it checks the app is ready, makes a guest per slot, and reports what the guests measured and
// whether the gate held. LoadRun owns the joins, the window and the presenter; the scenario owns what a guest does.
public interface ILoadScenario
{
    // The verb for the progress line: "answering" in "joined 20 of 20 in 1.2 s; answering over 10 s".
    string Action { get; }

    // Checks the app is serving what the scenario drives and reads what the guests are counted from. Throws
    // InvalidOperationException, which exits 1, when a run would only measure refusals.
    Task PrepareAsync(CancellationToken ct);

    ILoadGuest CreateGuest(Uri hubBase);

    // Valid once the guests have finished.
    IRunReport Report();
}
