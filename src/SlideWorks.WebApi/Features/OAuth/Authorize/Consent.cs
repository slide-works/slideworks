using Agoda.IoC.Core;
using SlideWorks.WebApi.Features.OAuth.Stores;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.OAuth.Authorize;

// The presenter's answer to an authorize request. Showing it reads the request without using it up
// (IOAuthRequests.FindAsync, which applies the same rules as ConsumeAsync). Answering consumes it, once: allow issues a
// code bound to everything the request carried, deny sends access_denied. Either way the client gets its state back.
[RegisterPerRequest(Concrete = true)]
public class Consent(IOAuthRequests requests, IOAuthClients clients, IOAuthCodes codes, ICurrentActor actor)
{
    public async Task<ConsentView> GetAsync(string requestId)
    {
        var pending = await requests.FindAsync(requestId, actor.PresenterId!.Value) ?? throw new ConsentGoneException();
        var client = await clients.FindAsync(pending.ClientId) ?? throw new ConsentGoneException();
        return ConsentView.From(client, pending);
    }

    public async Task<ConsentDecisionView> DecideAsync(string requestId, ConsentDecisionRequest decision)
    {
        var pending = await requests.ConsumeAsync(requestId, actor.PresenterId!.Value) ?? throw new ConsentGoneException();
        if (!decision.Approve)
        {
            return new ConsentDecisionView(OAuthRedirect.To(pending.RedirectUri, ("error", "access_denied"), ("state", pending.State)));
        }
        var code = await codes.IssueAsync(pending.ClientId, pending.PresenterId, pending.RedirectUri, pending.CodeChallenge, pending.Scope, pending.Resource);
        return new ConsentDecisionView(OAuthRedirect.To(pending.RedirectUri, ("code", code), ("state", pending.State)));
    }
}
