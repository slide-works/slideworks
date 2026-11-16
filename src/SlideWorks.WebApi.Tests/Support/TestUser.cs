namespace SlideWorks.WebApi.Tests.Support;

// The people tests act as. Each has a stable Google subject, email and name, so a test can sign in as the same person
// twice and find what it made. Anonymous is nobody: a test that uses it does not sign in.
public sealed class TestUser
{
    public static readonly TestUser Sam = new("test-google-sub-sam", "sam@team.example", "Sam Rivera");

    public static readonly TestUser Alex = new("test-google-sub-alex", "alex@team.example", "Alex Chen");

    public static readonly TestUser Mira = new("test-google-sub-mira", "mira@team.example", "Mira Okafor");

    public static readonly TestUser Anonymous = new("", "", "Anonymous");

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

    public override string ToString() => Name;
}
