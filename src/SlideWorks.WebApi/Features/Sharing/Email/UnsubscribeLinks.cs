using Agoda.IoC.Core;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Sharing.Email;

// TODO: make the token a Data Protection token of the address and read it back.
[RegisterPerRequest]
public sealed class UnsubscribeLinks(IPublicUrls urls) : IUnsubscribeLinks
{
    public string For(string email) => $"{urls.Base}/unsubscribe#t=stand-in";

    public Task<string?> ReadAsync(string token) => Task.FromResult<string?>(null);
}
