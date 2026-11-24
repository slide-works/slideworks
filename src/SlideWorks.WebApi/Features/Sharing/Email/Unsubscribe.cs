using Agoda.IoC.Core;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Sharing.Email;

[RegisterPerRequest(For = typeof(IUnsubscribe))]
public sealed class Unsubscribe(IUnsubscribeLinks links, IEmailSuppressions suppressions) : IUnsubscribe
{
    public async Task UnsubscribeAsync(string token)
    {
        var email = await links.ReadAsync(token) ?? throw new BadRequestException("token: this link didn't work");
        await suppressions.AddAsync(email);
    }
}
