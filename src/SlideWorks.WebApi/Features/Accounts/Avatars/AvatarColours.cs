namespace SlideWorks.WebApi.Features.Accounts.Avatars;

// The eight colour keys a person's avatar may take.
public static class AvatarColours
{
    public static readonly IReadOnlyList<string> Keys = ["slate", "red", "amber", "green", "teal", "blue", "violet", "pink"];

    // The key picked for someone who chose none: a stable hash of their id, so it never changes between requests or restarts.
    public static string PickFor(Guid userId) => Keys[(int)(BitConverter.ToUInt32(userId.ToByteArray(), 0) % (uint)Keys.Count)];
}
