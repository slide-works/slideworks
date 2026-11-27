namespace SlideWorks.WebApi.Tests.Support;

// The MCP tools the server registers, in ordinal order. This is the one list a new tool is added to; every test that
// needs the tool names reads it. McpToolListTests.ToolList_WhenServerLists_ShouldMatchExpectedTools is the test that fails
// until the list is updated.
public static class ExpectedTools
{
    public static IReadOnlyList<string> Names { get; } =
    [
        "add_poll_option", "add_slide", "add_slides", "check_deck", "close_open_text", "close_poll", "close_word_cloud", "create_deck", "create_template",
        "delete_slide", "get_deck", "get_deck_url", "get_poll_results", "get_questions", "get_template", "list_blocks", "list_decks", "list_layouts",
        "list_palettes", "list_templates", "move_slide", "open_open_text", "open_poll", "open_word_cloud", "preview_deck", "publish_template", "set_look", "set_notes", "set_parts", "set_sharing", "set_talk_length", "set_title", "update_poll", "update_qa",
        "update_slide", "update_template", "upload_asset", "upload_image", "upload_images", "validate_slide",
    ];
}
