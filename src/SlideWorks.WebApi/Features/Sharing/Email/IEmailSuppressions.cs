namespace SlideWorks.WebApi.Features.Sharing.Email;

public interface IEmailSuppressions
{
    // Suppresses the address. Suppressing one already suppressed changes nothing.
    Task AddAsync(string email);

    Task<bool> ContainsAsync(string email);
}
