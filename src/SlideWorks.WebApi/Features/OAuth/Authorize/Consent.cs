using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.OAuth.Stores;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.OAuth.Authorize;

// The presenter's answer to an authorize request. Showing it reads the request without using it up (IOAuthRequests has
// no read, so this reads the row with the same rules ConsumeAsync applies). Answering consumes it, once: allow issues a
// code bound to everything the request carried, deny sends access_denied. Either way the client gets its state back.
[RegisterPerRequest(Concrete = true)]
public class Consent(AppDbContext db, IOAuthRequests requests, IOAuthClients clients, IOAuthCodes codes, ICurrentActor actor, TimeProvider clock)
{
    public async Task<ConsentView> GetAsync(string requestId)
    {
        var presenterId = actor.PresenterId!.Value;
        var now = clock.GetUtcNow();
        var request = await db.OAuthRequests.AsNoTracking()
            .Where(r => r.Id == requestId && r.PresenterId == presenterId && r.ConsumedAt == null && r.ExpiresAt > now)
            .Select(r => new { r.ClientId, r.Scope })
            .SingleOrDefaultAsync() ?? throw new ConsentGoneException();
        var client = await clients.FindAsync(request.ClientId) ?? throw new ConsentGoneException();
        return new ConsentView(client.ClientName, request.Scope);
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
