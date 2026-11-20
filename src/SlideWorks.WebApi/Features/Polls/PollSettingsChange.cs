namespace SlideWorks.WebApi.Features.Polls;

// update_poll's arguments as the tool bound them: each is null when left out, which keeps the poll's current value. The
// three enums come as their wire names, as set_sharing's level does. Correct 0 clears the correct answer, since null
// means left out.
public sealed record PollSettingsChange(string? Pick, int? Max, string? Results, int? Correct, string? Opens, string? Answers = null);
