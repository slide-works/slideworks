# Layouts and inline blocks

These files are generated from `LayoutCatalogue`: do not edit them by hand, rerun `UPDATE_LAYOUTS_DOC=1 dotnet test src/SlideWorks.UnitTest --filter "FullyQualifiedName~LayoutsDoc"`.

A slide is a layout plus slots (K2). Slot text is plain text: Markdown and HTML are not interpreted,
and line breaks are allowed only where a slot says so. Every slide also has `part` (1-based, or null)
and `notes`, which are set next to the slots, not inside them. A JSON null counts as an absent slot.
Error paths name the failing field with 0-based indexes, such as `practice.do[2].text: required`.

- [`title`](layouts/title.md)
- [`divider`](layouts/divider.md)
- [`bullets`](layouts/bullets.md)
- [`two-column`](layouts/two-column.md)
- [`practice`](layouts/practice.md)
- [`compare`](layouts/compare.md)
- [`big-number`](layouts/big-number.md)
- [`quote`](layouts/quote.md)
- [`image`](layouts/image.md)
- [`video`](layouts/video.md)
- [`table`](layouts/table.md)
- [`chart`](layouts/chart.md)
- [`gantt`](layouts/gantt.md)
- [`numbered-cards`](layouts/numbered-cards.md)
- [`rules`](layouts/rules.md)
- [`cards`](layouts/cards.md)
- [`statement`](layouts/statement.md)
- [`links`](layouts/links.md)
- [`stack`](layouts/stack.md)
- [`bars`](layouts/bars.md)
- [`flow`](layouts/flow.md)
- [`levels`](layouts/levels.md)
- [`columns-list`](layouts/columns-list.md)
- [`timeline`](layouts/timeline.md)
- [`loop`](layouts/loop.md)
- [`close`](layouts/close.md)
- [`join`](layouts/join.md)
- [`poll`](layouts/poll.md)
- [`qa`](layouts/qa.md)
- [`word-cloud`](layouts/word-cloud.md)
- [`open-text`](layouts/open-text.md)

Inline blocks: [`_blocks`](layouts/_blocks.md)
