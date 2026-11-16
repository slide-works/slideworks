namespace SlideWorks.WebApi.Infrastructure.Identity;

// What an IUserAccounts change did.
public enum AccountChange
{
    Changed,

    // No user has that id.
    NotFound,

    // The user is not in a state the change starts from, including already being in the state it ends in.
    NotAllowed,

    // The change would leave no admin.
    LastAdmin,
}
