namespace SlideWorks.WebApi.Features.Sharing.Email;

public interface IUnsubscribe
{
    // Reads the token, adds its address to the suppression list. A token that is not ours or has expired is a BadRequestException.
    Task UnsubscribeAsync(string token);
}
