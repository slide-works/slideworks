namespace SlideWorks.WebApi.Infrastructure.Identity;

// The email being saved already belongs to a person who signs in with another Google subject.
public sealed class EmailLinkedException() : Exception("The email belongs to a person with another Google subject.")
{
}
