using SlideWorks.LoadTest;

LoadOptions options;
try
{
    options = LoadOptions.Parse(args);
}
catch (ArgumentException e)
{
    await Console.Error.WriteLineAsync($"{e.Message}\n{LoadOptions.USAGE}");
    return 2;
}

using var cancel = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancel.Cancel();
};
try
{
    var report = await LoadRun.RunAsync(options, Console.Out, cancel.Token);
    Console.WriteLine(report.Table());
    Console.WriteLine(report.Json());
    return 0;
}
catch (OperationCanceledException)
{
    await Console.Error.WriteLineAsync("Cancelled.");
    return 130;
}
catch (Exception e) when (e is InvalidOperationException or HttpRequestException)
{
    await Console.Error.WriteLineAsync(e.Message);
    return 1;
}
