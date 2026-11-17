using System.Text.Json;
using SlideWorks.Core.Polls;

namespace SlideWorks.UnitTest.Polls;

// The wire names SW-9 added to the poll enums (Q2): a poll's kind and the pick with no maximum.
[TestFixture]
public class PollWireNamesTests
{
    [TestCase(PollKind.Slide, "\"slide\"")]
    [TestCase(PollKind.Qa, "\"qa\"")]
    public void Serialize_WhenPollKind_ShouldUseItsWireName(PollKind kind, string expected)
    {
        JsonSerializer.Serialize(kind).ShouldBe(expected);
    }

    [Test]
    public void Serialize_WhenPollPickIsAny_ShouldUseItsWireName()
    {
        JsonSerializer.Serialize(PollPick.Any).ShouldBe("\"any\"");
    }
}
