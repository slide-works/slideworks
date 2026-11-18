namespace SlideWorks.UnitTest.Slides;

[TestFixture]
public class LayoutsDocTests
{
    [Test]
    public void Docs_WhenCatalogueChanges_ShouldMatchTheGeneratedFiles()
    {
        var files = LayoutsDoc.Files().ToDictionary(f => f.Key, f => f.Value.ReplaceLineEndings("\n"));
        var folder = ContractFiles.PathOf(LayoutsDoc.Folder);
        if (Environment.GetEnvironmentVariable("UPDATE_LAYOUTS_DOC") == "1")
        {
            Directory.CreateDirectory(folder);
            foreach (var gone in FilesOnDisk(folder).Where(name => !files.ContainsKey(name)))
            {
                File.Delete(ContractFiles.PathOf(gone));
            }

            foreach (var (name, text) in files)
            {
                File.WriteAllText(ContractFiles.PathOf(name), text);
            }
        }

        var wrong = files
            .Where(f => !File.Exists(ContractFiles.PathOf(f.Key)))
            .Select(f => $"src/contracts/{f.Key} is missing")
            .Concat(files
                .Where(f => File.Exists(ContractFiles.PathOf(f.Key)) && File.ReadAllText(ContractFiles.PathOf(f.Key)).ReplaceLineEndings("\n") != f.Value)
                .Select(f => $"src/contracts/{f.Key} is out of date"))
            .Concat(FilesOnDisk(folder)
                .Where(name => !files.ContainsKey(name))
                .Select(name => $"src/contracts/{name} has no layout in the catalogue"))
            .ToList();

        wrong.ShouldBeEmpty("rerun this test with UPDATE_LAYOUTS_DOC=1 and commit the files");
    }

    // The files in src/contracts/layouts, named the way LayoutsDoc.Files names them.
    private static IEnumerable<string> FilesOnDisk(string folder) =>
        Directory.Exists(folder)
            ? Directory.EnumerateFiles(folder).Select(path => $"{LayoutsDoc.Folder}/{Path.GetFileName(path)}").Order(StringComparer.Ordinal)
            : [];
}
