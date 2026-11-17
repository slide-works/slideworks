using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.OAuth.Stores;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.OAuth.Contracts;

// K10 / D20: the stored form of a code or token is the lower-case hex SHA-256 of its UTF-8 bytes, computed here, not by
// the app's own helper. Every token and code already issued is looked up by that hash, so a change to the algorithm or
// the encoding would silently sign every agent out and void every pending code; this fails CI instead.
[TestFixture]
public class OAuthSecretHashFormatContractTests : OAuthContractFixture
{
    [Test]
    public async Task Stores_WhenTokensAndACodeAreIssued_ShouldKeepTheLowerCaseHexSha256OfEach()
    {
        var (client, presenterId, tokens) = await IssueTokensAsync();
        var code = await UseAsync<IOAuthCodes, string>(c => c.IssueAsync(client.ClientId, presenterId, REDIRECT, CHALLENGE, SCOPE, Resource));

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tokenHashes = await db.AgentTokens.AsNoTracking().Where(t => t.ClientId == client.ClientId).Select(t => t.TokenHash).ToListAsync();
        var codeHashes = await db.OAuthCodes.AsNoTracking().Where(c => c.ClientId == client.ClientId).Select(c => c.CodeHash).ToListAsync();

        tokenHashes.ShouldBe([Sha256Hex(tokens.AccessToken), Sha256Hex(tokens.RefreshToken)], ignoreOrder: true);
        codeHashes.ShouldBe([Sha256Hex(code)]);
        tokenHashes.Concat(codeHashes).ShouldAllBe(hash => hash.Length == 64 && hash.All(ch => char.IsAsciiHexDigitLower(ch) || char.IsAsciiDigit(ch)));
    }

    // FIPS 180-2's "abc" vector, so the expected form is pinned to the spec as well as to this test's own computation.
    [Test]
    public void Sha256Hex_WhenGivenTheFipsVector_ShouldMatchTheSpec()
    {
        Sha256Hex("abc").ShouldBe("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
    }

    private static string Sha256Hex(string secret)
        => string.Concat(SHA256.HashData(Encoding.UTF8.GetBytes(secret)).Select(b => b.ToString("x2", System.Globalization.CultureInfo.InvariantCulture)));
}
