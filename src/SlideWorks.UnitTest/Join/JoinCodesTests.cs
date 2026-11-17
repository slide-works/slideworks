using System.Security.Cryptography;
using SlideWorks.Core.Join;

namespace SlideWorks.UnitTest.Join;

[TestFixture]
public class JoinCodesTests
{
    [Test]
    public void JoinCodesNew_WhenDrawnManyTimes_ShouldStayInRange()
    {
        using var random = RandomNumberGenerator.Create();

        for (var i = 0; i < 20000; i++)
        {
            var code = JoinCodes.New(random);

            code.Length.ShouldBe(6);
            code.ShouldAllBe(c => char.IsAsciiDigit(c));
            int.Parse(code).ShouldBeInRange(100000, 999999);
        }
    }

    [Test]
    public void JoinCodesDisplay_WhenSixDigits_ShouldGroupInThrees()
    {
        JoinCodes.Display("482913").ShouldBe("482 913");
    }
}
