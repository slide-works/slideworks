namespace SlideWorks.Core.Polls;

// Who sees a round's counts (V5, D4: hidden means hidden). The one place the rule lives: the server sends counts only to
// whoever this says may see them, and the client reads the result as roomSeesCounts.
public static class PollVisibility
{
    // The room (the audience group and the big screen) sees the counts when results are live, or the presenter has
    // revealed them, or the round has closed.
    public static bool RoomSees(PollResults results, bool revealed, bool closed) =>
        results == PollResults.Live || revealed || closed;

    // One guest sees the counts when the room does, or when results show after answering and this guest has answered.
    public static bool GuestSees(bool roomSees, PollResults results, bool hasAnswered) =>
        roomSees || (results == PollResults.AfterAnswer && hasAnswered);
}
