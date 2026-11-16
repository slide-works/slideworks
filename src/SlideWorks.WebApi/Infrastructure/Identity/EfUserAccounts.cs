using System.Linq.Expressions;
using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Accounts;

namespace SlideWorks.WebApi.Infrastructure.Identity;

// Each change is a single UPDATE whose WHERE holds every rule, including "another admin exists" for the changes that
// take admin away. SQLite runs one writer at a time, so a check can't go stale between reading and writing. When no
// row changes, the user is read back only to say why.
[RegisterPerRequest(For = typeof(IUserAccounts))]
public class EfUserAccounts(AppDbContext db, TimeProvider clock) : IUserAccounts
{
    public async Task<IReadOnlyList<User>> ListAsync()
        => await db.Users.AsNoTracking()
            .OrderBy(u => u.Access == UserAccess.Pending ? 0 : u.Access == UserAccess.Approved ? 1 : 2)
            .ThenBy(u => u.Access == UserAccess.Pending ? "" : u.Name)
            .ThenBy(u => u.CreatedAt)
            .ThenBy(u => u.Id)
            .ToListAsync();

    public Task<User?> FindAsync(Guid id) => db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id);

    public Task<int> PendingCountAsync() => db.Users.CountAsync(u => u.Access == UserAccess.Pending);

    public Task<bool> AnyAdminAsync() => db.Users.AnyAsync(u => u.IsAdmin);

    public Task<AccountChange> ApproveAsync(Guid id, Guid? by)
        => ChangeAsync(id, by, u => u.Access == UserAccess.Pending || u.Access == UserAccess.Rejected, UserAccess.Approved, isAdmin: null);

    public Task<AccountChange> RejectAsync(Guid id, Guid? by)
        => ChangeAsync(id, by, u => u.Access == UserAccess.Pending, UserAccess.Rejected, isAdmin: null);

    public Task<AccountChange> RevokeAsync(Guid id, Guid? by)
        => ChangeAsync(
            id,
            by,
            u => u.Access == UserAccess.Approved && (!u.IsAdmin || db.Users.Any(other => other.IsAdmin && other.Id != id)),
            UserAccess.Rejected,
            isAdmin: false,
            lastAdmin: u => u.Access == UserAccess.Approved && u.IsAdmin);

    public Task<AccountChange> MakeAdminAsync(Guid id, Guid? by)
        => ChangeAsync(id, by, u => u.Access == UserAccess.Approved && !u.IsAdmin, access: null, isAdmin: true);

    public Task<AccountChange> RemoveAdminAsync(Guid id, Guid? by)
        => ChangeAsync(
            id,
            by,
            u => u.IsAdmin && db.Users.Any(other => other.IsAdmin && other.Id != id),
            access: null,
            isAdmin: false,
            lastAdmin: u => u.IsAdmin);

    private async Task<AccountChange> ChangeAsync(
        Guid id,
        Guid? by,
        Expression<Func<User, bool>> allowedFrom,
        UserAccess? access,
        bool? isAdmin,
        Func<User, bool>? lastAdmin = null)
    {
        var now = clock.GetUtcNow();
        var changed = await db.Users
            .Where(u => u.Id == id)
            .Where(allowedFrom)
            .ExecuteUpdateAsync(set =>
            {
                if (access is { } newAccess)
                {
                    set.SetProperty(u => u.Access, newAccess);
                }
                if (isAdmin is { } newIsAdmin)
                {
                    set.SetProperty(u => u.IsAdmin, newIsAdmin);
                }
                set.SetProperty(u => u.AccessChangedAt, now).SetProperty(u => u.AccessChangedBy, by);
            });
        if (changed == 1)
        {
            return AccountChange.Changed;
        }
        var user = await FindAsync(id);
        if (user == null)
        {
            return AccountChange.NotFound;
        }
        return lastAdmin != null && lastAdmin(user) ? AccountChange.LastAdmin : AccountChange.NotAllowed;
    }
}
