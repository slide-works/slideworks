namespace SlideWorks.WebApi.Features.OAuth.ConnectedAgents;

public interface IConnectedAgents
{
    // The agents acting for this person, one per live token family.
    Task<IReadOnlyList<ConnectedAgent>> ListAsync(Guid presenterId);

    // Revokes the family. False when it is not this person's or is already revoked.
    Task<bool> DisconnectAsync(Guid presenterId, Guid familyId);
}
