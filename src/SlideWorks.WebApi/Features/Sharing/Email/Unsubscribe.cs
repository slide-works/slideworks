using Agoda.IoC.Core;

namespace SlideWorks.WebApi.Features.Sharing.Email;

// TODO: read the token with IUnsubscribeLinks, answer 400 for a bad one and add the address to IEmailSuppressions.
[RegisterPerRequest(For = typeof(IUnsubscribe))]
public sealed class Unsubscribe : IUnsubscribe
{
    public Task UnsubscribeAsync(string token) => throw new NotImplementedException("Unsubscribe is not built yet.");
}
