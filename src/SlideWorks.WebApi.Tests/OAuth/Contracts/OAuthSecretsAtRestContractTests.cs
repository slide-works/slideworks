using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.OAuth.Stores;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.OAuth.Contracts;

// K10 / D20: codes and tokens are stored only as hashes. The raw secrets must not be anywhere in the app's database file
// (or its journal), whatever table or column a store puts them in. The in-memory stores write nothing there, so this only
// bites once a store keeps its rows in the database; the email control proves the bytes read are the live database.
[TestFixture]
public class OAuthSecretsAtRestContractTests : OAuthContractFixture
{
    [Test]
    public async Task Stores_WhenCodesAndTokensAreIssuedAndUsed_ShouldKeepNoRawSecretInTheDatabase()
    {
        var presenterId = await PresenterAsync(TestUser.Sam);
        var client = await RegisterClientAsync();
        var redeemedCode = await IssueCodeAsync(client.ClientId, presenterId);
        var unusedCode = await IssueCodeAsync(client.ClientId, presenterId);
        var grant = await UseAsync<IOAuthCodes, CodeGrant?>(c => c.RedeemAsync(redeemedCode, client.ClientId, REDIRECT, VERIFIER, Resource));
        var tokens = await UseAsync<IAgentTokens, AgentTokenPair>(t => t.IssueAsync(grant.ShouldNotBeNull()));
        var rotated = await UseAsync<IAgentTokens, AgentTokenPair?>(t => t.RefreshAsync(tokens.RefreshToken, client.ClientId));
        rotated.ShouldNotBeNull();

        var stored = await DatabaseBytesAsTextAsync();

        stored.Contains(TestUser.Sam.Email, StringComparison.Ordinal).ShouldBeTrue("control: the person's email should be in the database file read");
        var secrets = new Dictionary<string, string>
        {
            ["redeemed code"] = redeemedCode,
            ["unused code"] = unusedCode,
            ["first access token"] = tokens.AccessToken,
            ["first refresh token (used)"] = tokens.RefreshToken,
            ["rotated access token"] = rotated.AccessToken,
            ["rotated refresh token"] = rotated.RefreshToken,
        };
        secrets.Where(secret => stored.Contains(secret.Value, StringComparison.Ordinal)).Select(secret => secret.Key).ShouldBeEmpty();
    }

    private Task<string> IssueCodeAsync(string clientId, Guid presenterId)
        => UseAsync<IOAuthCodes, string>(c => c.IssueAsync(clientId, presenterId, REDIRECT, CHALLENGE, SCOPE, Resource));

    // The database file and any journal beside it, byte for byte (Latin-1 keeps each byte as one char), so a secret
    // stored as text in any table shows up as its ASCII bytes.
    private async Task<string> DatabaseBytesAsTextAsync()
    {
        var dbPath = Factory.Services.GetRequiredService<IConfiguration>()["SLIDEWORKS_DB_PATH"].ShouldNotBeNull();
        var text = new StringBuilder();
        foreach (var path in new[] { dbPath, $"{dbPath}-wal", $"{dbPath}-journal" }.Where(File.Exists))
        {
            await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var bytes = new MemoryStream();
            await file.CopyToAsync(bytes);
            text.Append(Encoding.Latin1.GetString(bytes.ToArray()));
        }
        return text.ToString();
    }
}
