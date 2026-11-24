using System.Security.Cryptography;
using Agoda.IoC.Core;
using Microsoft.AspNetCore.DataProtection;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Sharing.Email;

// The token is a Data Protection payload of the normalised address. Protect(string) already returns base64url, and the token
// rides in the URL fragment so it never reaches a server log. It does not expire: it can only suppress the address it names,
// and the link must still work from an old email.
[RegisterPerRequest]
public sealed class UnsubscribeLinks(IPublicUrls urls, IDataProtectionProvider protection) : IUnsubscribeLinks
{
    private const string PURPOSE = "share-unsubscribe";

    public string For(string email) => $"{urls.Base}/unsubscribe#t={Protector().Protect(ViewerEmails.Normalize(email))}";

    public Task<string?> ReadAsync(string token)
    {
        try
        {
            return Task.FromResult<string?>(Protector().Unprotect(token));
        }
        catch (CryptographicException)
        {
            return Task.FromResult<string?>(null);
        }
    }

    private IDataProtector Protector() => protection.CreateProtector(PURPOSE);
}
