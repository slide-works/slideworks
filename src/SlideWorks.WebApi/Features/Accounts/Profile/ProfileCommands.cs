using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Accounts.Avatars;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Accounts.Profile;

[RegisterPerRequest]
public sealed class ProfileCommands(AppDbContext db) : IProfileCommands
{
    private const int MAX_NAME_LENGTH = 60;

    public async Task SaveAsync(Guid userId, string? name, string? colour)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length is 0 or > MAX_NAME_LENGTH)
        {
            throw new BadRequestException($"Your name must be 1 to {MAX_NAME_LENGTH} characters.");
        }
        if (colour is null || !AvatarColours.Keys.Contains(colour))
        {
            throw new BadRequestException("That colour is not one of the choices.");
        }
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId) ?? throw new NotFoundException("The signed-in person no longer exists.");
        user.Name = trimmed;
        user.AvatarColour = colour;
        user.NameSetByPerson = true;
        await db.SaveChangesAsync();
    }
}
