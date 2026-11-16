using Agoda.IoC.Core;
using SlideWorks.WebApi.Features.OAuth.Stores;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Accounts.Admin;

// The admin's changes to who may use the server (S5). Each maps the AccountChange to the API's errors and returns the
// whole refreshed screen. Revoke ends the person's agent tokens first, so a failure between the two steps can't leave
// tokens that come back to life when they are approved again.
[RegisterPerRequest]
public class AdminUserCommands(IUserAccounts accounts, IAgentTokens agentTokens, ICurrentActor actor, AdminUserViews views)
{
    private const string NOT_FOUND = "User not found";
    private const string LAST_ADMIN = "The last admin can't be removed";

    public Task<AdminUsersView> ListAsync() => views.BuildAsync();

    public Task<AdminUsersView> ApproveAsync(Guid id)
        => ChangeAsync(accounts.ApproveAsync(id, actor.PresenterId), "Only a pending or rejected person can be approved");

    public Task<AdminUsersView> RejectAsync(Guid id)
        => ChangeAsync(accounts.RejectAsync(id, actor.PresenterId), "Only a pending person can be rejected");

    public Task<AdminUsersView> MakeAdminAsync(Guid id)
        => ChangeAsync(accounts.MakeAdminAsync(id, actor.PresenterId), "Only an approved person who isn't an admin can be made an admin");

    public Task<AdminUsersView> RemoveAdminAsync(Guid id)
        => ChangeAsync(accounts.RemoveAdminAsync(id, actor.PresenterId), "That person isn't an admin");

    public async Task<AdminUsersView> RevokeAsync(Guid id)
    {
        if (await accounts.FindAsync(id) == null)
        {
            throw new NotFoundException(NOT_FOUND);
        }
        await agentTokens.RevokeAllForPresenterAsync(id);
        return await ChangeAsync(accounts.RevokeAsync(id, actor.PresenterId), "Only an approved person can be revoked");
    }

    private async Task<AdminUsersView> ChangeAsync(Task<AccountChange> change, string notAllowed)
    {
        switch (await change)
        {
            case AccountChange.NotFound:
                throw new NotFoundException(NOT_FOUND);
            case AccountChange.LastAdmin:
                throw new ConflictException(LAST_ADMIN);
            case AccountChange.NotAllowed:
                throw new ConflictException(notAllowed);
            default:
                return await views.BuildAsync();
        }
    }
}
