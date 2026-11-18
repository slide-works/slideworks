import type { EditorTab, TemplateEditorView, TemplateManifest } from '../types';
import type { ChangeOutcome } from '../useTemplateDraft';

// What every editor tab and the publish checklist take (frozen: the tabs are filled against it).
// `change` merges top-level manifest keys and saves through the editor's one useTemplateDraft, and
// resolves to `{ ok: true }` or `{ ok: false, code, message, path? }`: a tab shows `message` next
// to the field at `path` and moves focus there. After a `stale_revision` refusal the draft has
// already been read again. `reload` reads the editor view again (after an upload or a publish).
export type EditorTabProps = {
  draft: TemplateEditorView;
  change(patch: Partial<TemplateManifest>): Promise<ChangeOutcome>;
  reload(): Promise<void>;
  saving: boolean;
};

// The open tab is the `tab` search parameter of /templates/:id/edit.
export const TAB_PARAM = 'tab';
export const DEFAULT_TAB: EditorTab = 'basics';

export const EDITOR_TABS: readonly { tab: EditorTab; label: string }[] = [
  { tab: 'basics', label: 'Basics' },
  { tab: 'fonts', label: 'Fonts' },
  { tab: 'logo-art', label: 'Logo and art' },
  { tab: 'palette', label: 'Palette' },
  { tab: 'layouts', label: 'Custom layouts' },
];

// The tab a search string asks for among the tabs on offer: `basics` when it names none of them.
export function tabOf(search: URLSearchParams, offered: readonly EditorTab[]): EditorTab {
  const asked = search.get(TAB_PARAM);
  return offered.find((tab) => tab === asked) ?? DEFAULT_TAB;
}
