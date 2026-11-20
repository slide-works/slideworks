using System.Text.Json;
using SlideWorks.Core.Slides;

namespace SlideWorks.UnitTest.Slides;

// The YouTube link is a security boundary: its text is stored, and the player builds an embed address
// from the id it reads. Every case of the shared case file runs here, and the client's check runs the same file.
[TestFixture]
public class YouTubeVideoTests
{
    [Test]
    public void TryRead_WhenRunOverTheContractCases_ShouldAgreeWithEachOne()
    {
        using var file = JsonDocument.Parse(File.ReadAllText(ContractFiles.PathOf("youtube-cases.json")));
        var cases = file.RootElement.GetProperty("cases").EnumerateArray().ToList();

        cases.Count.ShouldBeGreaterThanOrEqualTo(40);
        cases.ShouldContain(item => item.GetProperty("id").ValueKind == JsonValueKind.Null);
        cases.ShouldContain(item => item.GetProperty("id").ValueKind == JsonValueKind.String);
        foreach (var item in cases)
        {
            var value = item.GetProperty("value").GetString()!;
            var read = YouTubeVideo.TryRead(value, out var id, out var start);

            if (item.GetProperty("id").ValueKind == JsonValueKind.Null)
            {
                read.ShouldBeFalse(value);
            }
            else
            {
                read.ShouldBeTrue(value);
                id.ShouldBe(item.GetProperty("id").GetString(), value);
                start.ShouldBe(item.GetProperty("start").GetInt32(), value);
            }
        }
    }
}
