using Agoda.IoC.Core;
using SlideWorks.Core;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Accounts.Admin;

// Builds the admin-users screen (S5) from IUserAccounts, in its list order, with labels formatted on the server clock.
[RegisterPerRequest]
public class AdminUserViews(IUserAccounts accounts, ICurrentActor actor, TimeProvider clock)
{
    public async Task<AdminUsersView> BuildAsync()
    {
        var users = await accounts.ListAsync();
        var names = users.ToDictionary(u => u.Id, u => u.Name);
        var now = clock.GetUtcNow();
        var rows = users.Select(u => new AdminUserRow(
            u.Id,
            u.Name,
            u.Email,
            u.PictureUrl,
            u.Access.ToString().ToLowerInvariant(),
            u.IsAdmin,
            u.Id == actor.PresenterId,
            DisplayDate.Ago(u.CreatedAt, now),
            u.AccessChangedAt is { } changedAt ? DisplayDate.Ago(changedAt, now) : null,
            u.AccessChangedBy is { } by && names.TryGetValue(by, out var name) ? name : null)).ToList();
        return new AdminUsersView(
            rows,
            PendingCount: users.Count(u => u.Access == UserAccess.Pending),
            AdminCount: users.Count(u => u.IsAdmin));
    }
}
