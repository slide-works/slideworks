using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity.Gate;

namespace SlideWorks.WebApi.Features.Accounts.Admin;

// Admins decide who may use the server (S5). Every action answers with the whole refreshed admin-users view.
[Route("api/admin/users")]
[Authorize(Policy = AdminPolicy.NAME)]
public class AdminUsersController(AdminUserCommands commands) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<AdminUsersView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    public Task<AdminUsersView> List() => commands.ListAsync();

    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType<AdminUsersView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public Task<AdminUsersView> Approve(Guid id) => commands.ApproveAsync(id);

    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType<AdminUsersView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public Task<AdminUsersView> Reject(Guid id) => commands.RejectAsync(id);

    [HttpPost("{id:guid}/make-admin")]
    [ProducesResponseType<AdminUsersView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public Task<AdminUsersView> MakeAdmin(Guid id) => commands.MakeAdminAsync(id);

    [HttpPost("{id:guid}/remove-admin")]
    [ProducesResponseType<AdminUsersView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public Task<AdminUsersView> RemoveAdmin(Guid id) => commands.RemoveAdminAsync(id);

    [HttpPost("{id:guid}/revoke")]
    [ProducesResponseType<AdminUsersView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public Task<AdminUsersView> Revoke(Guid id) => commands.RevokeAsync(id);
}
