namespace SlideWorks.WebApi.Features.Accounts.Admin;

// GET /api/admin/users: every account, pending first, then approved, then rejected (src/contracts/screens/admin-users.json).
public sealed record AdminUsersView(IReadOnlyList<AdminUserRow> Users, int PendingCount, int AdminCount);
