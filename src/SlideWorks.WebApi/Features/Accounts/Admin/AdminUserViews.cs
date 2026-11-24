using Agoda.IoC.Core;
using SlideWorks.WebApi.Features.Accounts.Avatars;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Accounts.Admin;

// Builds the admin-users screen (S5) from IUserAccounts, in its list order, with labels formatted by IDisplayDates.
[RegisterPerRequest]
public class AdminUserViews(IUserAccounts accounts, ICurrentActor actor, IDisplayDates dates, IAvatars avatars)
{
    public async Task<AdminUsersView> BuildAsync()
    {
        var users = await accounts.ListAsync();
        var names = users.ToDictionary(u => u.Id, u => u.Name);
        var rows = users.Select(u => new AdminUserRow(
            u.Id,
            u.Name,
            u.Email,
            u.PictureUrl,
            avatars.For(u),
            u.Access.ToString().ToLowerInvariant(),
            u.IsAdmin,
            u.Id == actor.PresenterId,
            dates.Ago(u.CreatedAt),
            u.AccessChangedAt is { } changedAt ? dates.Ago(changedAt) : null,
            u.AccessChangedBy is { } by && names.TryGetValue(by, out var name) ? name : null)).ToList();
        return new AdminUsersView(
            rows,
            PendingCount: users.Count(u => u.Access == UserAccess.Pending),
            AdminCount: users.Count(u => u.IsAdmin));
    }
}
