namespace SlideWorks.UnitTest.Look.CustomLayouts;

// The five schemas of design/sw-50-custom-templates/gaps-c.md section 8, as the contract reads them: each
// has a description, and horizon names its steps.
public static class FiveSchemas
{
    public const string AGENT_LANES = """
        { "name": "agent-lanes", "description": "Parallel agent lanes with a progress meter each.", "surface": "either", "steps": "lanes",
          "slots": {
            "kicker":   { "type": "text", "max": 40, "optional": true },
            "headline": { "type": "text", "max": 80 },
            "lanes":    { "type": "list", "min": 2, "max": 4, "of": {
              "who":      { "type": "text", "max": 8 },
              "task":     { "type": "text", "max": 44 },
              "progress": { "type": "number", "min": 0, "max": 100 },
              "state":    { "type": "enum", "values": ["running", "waiting"] },
              "status":   { "type": "text", "max": 10 } } },
            "lead":     { "type": "text", "max": 160, "optional": true },
            "levels":   { "type": "levels" } } }
        """;

    public const string DIAL = """
        { "name": "dial", "description": "A dial with a needle at a value.", "surface": "either",
          "slots": {
            "kicker":   { "type": "text", "max": 40, "optional": true },
            "headline": { "type": "text", "max": 80 },
            "label":    { "type": "text", "max": 24, "optional": true },
            "value":    { "type": "number", "min": 0, "max": 100 },
            "from":     { "type": "text", "max": 20 },
            "to":       { "type": "text", "max": 20 },
            "text":     { "type": "text", "max": 220, "optional": true } } }
        """;

    public const string CODE_MOCK = """
        { "name": "code-mock", "description": "A code editor mock-up with ghost suggestions.", "surface": "either",
          "slots": {
            "kicker":   { "type": "text", "max": 40, "optional": true },
            "headline": { "type": "text", "max": 80 },
            "file":     { "type": "text", "max": 30, "optional": true },
            "lines":    { "type": "list", "min": 1, "max": 6, "of": {
              "code":  { "type": "text", "max": 56 },
              "ghost": { "type": "text", "max": 30, "optional": true } } },
            "hint":     { "type": "text", "max": 80, "optional": true },
            "facts":    { "type": "facts", "optional": true },
            "levels":   { "type": "levels" } } }
        """;

    public const string CHAT_MOCK = """
        { "name": "chat-mock", "description": "A chat between a person and a bot.", "surface": "either", "steps": "messages",
          "slots": {
            "kicker":   { "type": "text", "max": 40, "optional": true },
            "headline": { "type": "text", "max": 80 },
            "messages": { "type": "list", "min": 1, "max": 4, "of": {
              "from": { "type": "enum", "values": ["me", "bot"] },
              "text": { "type": "text", "max": 120 },
              "code": { "type": "text", "max": 80, "optional": true } } },
            "hint":     { "type": "text", "max": 80, "optional": true },
            "facts":    { "type": "facts", "optional": true },
            "levels":   { "type": "levels" } } }
        """;

    public const string HORIZON = """
        { "name": "horizon", "description": "Rows of segments along a horizon, with ticks.", "surface": "light", "steps": "rows",
          "slots": {
            "kicker":   { "type": "text", "max": 40, "optional": true },
            "headline": { "type": "text", "max": 80 },
            "rows":     { "type": "list", "min": 1, "max": 3, "of": {
              "label": { "type": "text", "max": 20 },
              "sub":   { "type": "text", "max": 24, "optional": true },
              "segments": { "type": "list", "min": 1, "max": 4, "of": {
                "text":  { "type": "text", "max": 90 },
                "span":  { "type": "number", "min": 1, "max": 100 },
                "fill":  { "type": "enum", "values": ["solid", "hatched", "faint", "none"] } } } } },
            "ticks":    { "type": "list", "min": 2, "max": 6, "of": { "type": "text", "max": 12 } } } }
        """;

    public static IReadOnlyList<string> All { get; } = [AGENT_LANES, DIAL, CODE_MOCK, CHAT_MOCK, HORIZON];
}
