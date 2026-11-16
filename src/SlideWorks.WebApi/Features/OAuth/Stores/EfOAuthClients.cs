using System.Text.Json;
using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.OAuth.Stores;

// Clients from dynamic client registration, one OAuthClientRow each. The redirect URIs are kept as a JSON array.
[RegisterPerRequest(For = typeof(IOAuthClients))]
public class EfOAuthClients(AppDbContext db, TimeProvider clock) : IOAuthClients
{
    public async Task<OAuthClient> RegisterAsync(ClientRegistration registration)
    {
        var row = new OAuthClientRow
        {
            ClientId = SecretTokens.New(),
            ClientName = registration.ClientName,
            RedirectUrisJson = JsonSerializer.Serialize(registration.RedirectUris),
            CreatedAt = clock.GetUtcNow(),
        };
        db.OAuthClients.Add(row);
        await db.SaveChangesAsync();
        return ToClient(row);
    }

    public async Task<OAuthClient?> FindAsync(string clientId)
    {
        var row = await db.OAuthClients.AsNoTracking().SingleOrDefaultAsync(c => c.ClientId == clientId);
        return row == null ? null : ToClient(row);
    }

    private static OAuthClient ToClient(OAuthClientRow row)
        => new(row.ClientId, row.ClientName, JsonSerializer.Deserialize<string[]>(row.RedirectUrisJson) ?? [], row.CreatedAt);
}
