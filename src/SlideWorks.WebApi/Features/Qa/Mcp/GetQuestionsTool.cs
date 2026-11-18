using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SlideWorks.WebApi.Infrastructure.Mcp;

namespace SlideWorks.WebApi.Features.Qa.Mcp;

[McpServerToolType]
public sealed class GetQuestionsTool
{
    [McpServerTool(Name = "get_questions", ReadOnly = true)]
    [Description("Read the questions the room asked in a deck you can edit, for each time the deck went live that ran Q&A, newest session first. Each session gives when it started, how many questions were asked, answered and hidden, and its questions in the order they were asked: the text, the name the asker chose to show (null when anonymous), the state (\"waiting\", \"open\", \"answered\" or \"hidden\"; hidden wins), the votes and when it was asked. state keeps only the questions in that state, and the session's counts stay whole. It reports the stored questions, so it still lists a past session's questions when the deck's questions are now off. Never which guest asked or upvoted. An agent can't approve, hide, answer or show a question; the presenter does.")]
    public Task<CallToolResult> GetQuestions(
        QuestionsReports reports,
        [Description("The deck's id.")] string deck,
        [Description("Only the questions in this state: \"open\", \"waiting\", \"answered\" or \"hidden\".")] string? state = null)
        => McpToolErrors.Run(() => reports.ReadAsync(deck, state));
}
