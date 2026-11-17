namespace SlideWorks.LoadTest;

// The harness's one source of chance: when in the window a voter answers and which option it picks. Neither is a secret, so
// the system generator is right and S2245 (which is about security tokens) does not apply.
public static class Dice
{
#pragma warning disable S2245
    public static int Next(int below) => Random.Shared.Next(below);

    public static double NextDouble() => Random.Shared.NextDouble();
#pragma warning restore S2245
}
