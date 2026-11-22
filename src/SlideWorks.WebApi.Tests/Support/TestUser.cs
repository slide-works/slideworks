namespace SlideWorks.WebApi.Tests.Support;

// The people tests act as. Each named person has a stable Google subject, email and name, so a test can sign in as the
// same person twice and find what it made. Sam, Alex and Mira are approved on first sign-in (TestPeople), and Ada is the
// admin through SLIDEWORKS_ADMIN_EMAIL, which the test hosts set to her email. NewPending() makes a fresh person nobody
// approves, for a test that changes someone's access. Anonymous is nobody: a test that uses it does not sign in.
public sealed class TestUser
{
    // NewPending() people's emails sit under this domain, so the test scheme knows them from the email alone.
    public const string PENDING_DOMAIN = "pending.test";

    // NewApproved() people's emails sit under this domain; TestPeople approves them on first sign-in, as it does Sam.
    public const string APPROVED_DOMAIN = "approved.test";

    private const string SUBJECT_PREFIX = "test-google-sub-";

    public static readonly TestUser Sam = new(SUBJECT_PREFIX + "sam", "sam@team.example", "Sam Rivera");

    public static readonly TestUser Alex = new(SUBJECT_PREFIX + "alex", "alex@team.example", "Alex Chen");

    public static readonly TestUser Mira = new(SUBJECT_PREFIX + "mira", "mira@team.example", "Mira Okafor");

    public static readonly TestUser Ada = new(SUBJECT_PREFIX + "ada", "ada@team.example", "Ada Brooks");

    public static readonly TestUser Anonymous = new("", "", "Anonymous");

    // Everyone with a fixed identity, so a sign-in by email can find them.
    public static readonly IReadOnlyList<TestUser> Named = [Sam, Alex, Mira, Ada];

    private TestUser(string googleSubject, string email, string name)
    {
        GoogleSubject = googleSubject;
        Email = email;
        Name = name;
    }

    public string GoogleSubject { get; }

    public string Email { get; }

    public string Name { get; }

    public bool IsAnonymous => ReferenceEquals(this, Anonymous);

    public bool IsPending => Email.EndsWith("@" + PENDING_DOMAIN, StringComparison.Ordinal);

    // A fresh person with a unique email under PENDING_DOMAIN, who stays Pending until a test changes that.
    public static TestUser NewPending() => FromEmail($"pat-{Guid.NewGuid():N}@{PENDING_DOMAIN}");

    public bool IsFreshApproved => Email.EndsWith("@" + APPROVED_DOMAIN, StringComparison.Ordinal);

    // A fresh person with a unique email under APPROVED_DOMAIN, approved on first sign-in. No other test acts as them, so a
    // journey that asserts an empty state ("nothing live", "no decks") does not depend on what the shared people did.
    public static TestUser NewApproved() => FromEmail($"ana-{Guid.NewGuid():N}@{APPROVED_DOMAIN}");

    // The named person with that email, or a NewPending() or NewApproved() person rebuilt from their email alone (an acceptance browser
    // sends only the email). Null for any other email.
    public static TestUser? Find(string email)
    {
        var named = Named.SingleOrDefault(p => string.Equals(p.Email, email, StringComparison.OrdinalIgnoreCase));
        if (named != null)
        {
            return named;
        }
        var fresh = FromEmail(email.Trim().ToLowerInvariant());
        return fresh.IsPending || fresh.IsFreshApproved ? fresh : null;
    }

    public override string ToString() => Name;

    // The subject and name both come from the email, so the same email is always the same person.
    private static TestUser FromEmail(string email)
        => new(SUBJECT_PREFIX + email, email, email.Split('@')[0]);
}
