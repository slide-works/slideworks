using System.Globalization;
using System.Text.Json;
using SlideWorks.Core.Look;
using SlideWorks.UnitTest.Slides;

namespace SlideWorks.UnitTest.Look;

// src/contracts/card-lift.json is CardLift's values written out, so the client's previews (New deck dialog, template
// editor) lift cards on dark paper as the server's look does (SW-148 D2). Rewrite it with UPDATE_CARD_LIFT=1.
public class CardLiftFileTests
{
    private const string FILE = "card-lift.json";

    [Test]
    public void File_WhenRead_ShouldEqualTheCode()
    {
        var path = ContractFiles.PathOf(FILE);
        if (Environment.GetEnvironmentVariable("UPDATE_CARD_LIFT") == "1")
        {
            File.WriteAllText(path, Text());
        }

        const string hint = "run with UPDATE_CARD_LIFT=1 to rewrite the file";
        File.Exists(path).ShouldBeTrue(hint);
        File.ReadAllText(path).ShouldBe(Text(), hint);
    }

    private static string Text() => string.Create(
        CultureInfo.InvariantCulture,
        $"{{\n  \"dark\": {JsonSerializer.Serialize(CardLift.DARK)},\n  \"darkPaperLuminance\": {CardLift.DARK_PAPER_LUMINANCE:0.0###},\n  \"none\": {JsonSerializer.Serialize(CardLift.NONE)}\n}}\n");
}
