using System.Text;
using Agoda.IoC.Core;

namespace SlideWorks.WebApi.Features.Accounts.Avatars;

[RegisterSingleton(For = typeof(IAvatars))]
public class Avatars : IAvatars
{
    private const int MAX_INITIALS = 2;

    public AvatarView For(User user) => new(
        InitialsOf(user.Name),
        user.AvatarColour ?? AvatarColours.PickFor(user.Id),
        user.PhotoId != null ? $"/api/people/{user.Id}/photo/{user.PhotoId}" : user.PictureUrl);

    // The first letter of the first two words, upper-cased; one letter for a one-word name.
    private static string InitialsOf(string name)
    {
        var initials = new StringBuilder();
        foreach (var word in name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Take(MAX_INITIALS))
        {
            initials.Append(word.EnumerateRunes().First().ToString().ToUpperInvariant());
        }
        return initials.ToString();
    }
}
