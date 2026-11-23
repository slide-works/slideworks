namespace SlideWorks.WebApi.Features.Sharing.Email;

public interface IUnsubscribeLinks
{
    // The absolute unsubscribe link for an address: <public url>/unsubscribe#t=<token>.
    string For(string email);

    // The address a token names, or null when it is not one this server made.
    Task<string?> ReadAsync(string token);
}
