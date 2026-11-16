using System.Security.Cryptography;

namespace SlideWorks.WebApi.Tests.Realtime;

// Deck ids shaped like the real ones (K9, D13): 12 random base62 characters, case-sensitive.
public static class TestDeckIds
{
    private const string ALPHABET = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    public static string New() => RandomNumberGenerator.GetString(ALPHABET, 12);
}
