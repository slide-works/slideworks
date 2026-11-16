namespace SlideWorks.UnitTest.Slides;

// The shared contract files under src/contracts, found by walking up from the test binaries to the
// folder that holds the solution.
public static class ContractFiles
{
    public static string PathOf(string name)
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SlideWorks.slnx")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new DirectoryNotFoundException("No SlideWorks.slnx above " + TestContext.CurrentContext.TestDirectory);
        }

        return Path.Combine(directory.FullName, "contracts", name);
    }
}
