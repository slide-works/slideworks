using SlideWorks.WebApi.Features.Accounts;

namespace SlideWorks.WebApi.Infrastructure.Identity;

// Who may use SlideWorks (S1): the admin's view of people and the changes an admin makes to their access. Each change
// stamps AccessChangedAt and AccessChangedBy (by is the admin's User.Id, or null for the system), and is one conditional
// update, so two admins acting at once can't leave the server without an admin. It never touches agent tokens.
public interface IUserAccounts
{
    // Pending first (oldest request first), then approved, then rejected (each by name).
    Task<IReadOnlyList<User>> ListAsync();

    Task<User?> FindAsync(Guid id);

    Task<int> PendingCountAsync();

    Task<bool> AnyAdminAsync();

    // Pending or Rejected → Approved.
    Task<AccountChange> ApproveAsync(Guid id, Guid? by);

    // Pending → Rejected.
    Task<AccountChange> RejectAsync(Guid id, Guid? by);

    // Approved → Rejected, and no longer admin. LastAdmin for the only admin.
    Task<AccountChange> RevokeAsync(Guid id, Guid? by);

    // An approved person who is not admin → admin.
    Task<AccountChange> MakeAdminAsync(Guid id, Guid? by);

    // Admin → not admin. LastAdmin for the only admin.
    Task<AccountChange> RemoveAdminAsync(Guid id, Guid? by);
}
