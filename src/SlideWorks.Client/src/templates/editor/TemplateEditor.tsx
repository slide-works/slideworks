import type { ComponentType, KeyboardEvent } from 'react';
import { useParams, useSearchParams } from 'react-router-dom';
import { TEMPLATES_TEST_IDS, editorTabButtonTestId } from '../testIds';
import type { EditorTab } from '../types';
import { useTemplateDraft } from '../useTemplateDraft';
import { EditorPreview } from './EditorPreview';
import { PublishChecklist } from './PublishChecklist';
import { EDITOR_TABS, TAB_PARAM, tabOf, type EditorTabProps } from './tabProps';
import { BasicsTab } from './tabs/BasicsTab';
import { FontsTab } from './tabs/FontsTab';
import { LayoutsTab } from './tabs/LayoutsTab';
import { LogoArtTab } from './tabs/LogoArtTab';
import { PaletteTab } from './tabs/PaletteTab';

// TODO: the Custom layouts tab stays hidden until custom layouts ship; then delete this flag.
const CUSTOM_LAYOUTS = false;

const TAB_CONTENT: { [T in EditorTab]: ComponentType<EditorTabProps> } = {
  basics: BasicsTab,
  fonts: FontsTab,
  'logo-art': LogoArtTab,
  palette: PaletteTab,
  layouts: LayoutsTab,
};

const TABS = EDITOR_TABS.filter(({ tab }) => CUSTOM_LAYOUTS || tab !== 'layouts');
const OFFERED = TABS.map(({ tab }) => tab);

// The tab an arrow, Home or End key moves to from `from`: the arrows wrap round the ends.
function tabForKey(key: string, from: EditorTab): EditorTab | null {
  const at = OFFERED.indexOf(from);
  const last = OFFERED.length - 1;
  if (key === 'ArrowRight') return OFFERED[at === last ? 0 : at + 1];
  if (key === 'ArrowLeft') return OFFERED[at === 0 ? last : at - 1];
  if (key === 'Home') return OFFERED[0];
  if (key === 'End') return OFFERED[last];
  return null;
}

const panelId = (tab: EditorTab) => `template-editor-panel-${tab}`;
const tabId = (tab: EditorTab) => `template-editor-tab-${tab}`;

// /templates/:id/edit: the template editor's frame. It holds the one useTemplateDraft, the tab
// list and the open tab (the `tab` search parameter; `basics` without one), and hands every tab
// and the publish checklist the same EditorTabProps. The tab bar is the ARIA tabs pattern: only
// the open tab is in the Tab order, and Left, Right, Home and End move to a tab and open it.
export function TemplateEditor() {
  const { id = '' } = useParams();
  const [search, setSearch] = useSearchParams();
  const { load, saving, saveError, change, reload } = useTemplateDraft(id);
  const open = tabOf(search, OFFERED);
  const Tab = TAB_CONTENT[open];

  const show = (tab: EditorTab) =>
    setSearch(
      (current) => {
        const next = new URLSearchParams(current);
        next.set(TAB_PARAM, tab);
        return next;
      },
      { replace: true },
    );

  const onTabKey = (event: KeyboardEvent<HTMLButtonElement>, from: EditorTab) => {
    const next = tabForKey(event.key, from);
    if (next === null) return;
    event.preventDefault();
    show(next);
    document.getElementById(tabId(next))?.focus();
  };

  const draft = load.kind === 'loaded' ? load.draft : null;
  const props = draft && { draft, change, reload, saving };

  return (
    <section data-testid={TEMPLATES_TEST_IDS.editor} className="grid gap-5">
      <h1 className="text-lg font-bold tracking-tight text-ink">
        {draft ? `Edit ${draft.name}` : 'Edit template'}
      </h1>

      {load.kind === 'failed' && (
        <p
          role="alert"
          data-testid={TEMPLATES_TEST_IDS.editorError}
          className="panel px-4 py-3 text-meta text-bad"
        >
          {load.message}
        </p>
      )}
      {saveError && (
        <p
          role="alert"
          data-testid={TEMPLATES_TEST_IDS.editorSaveError}
          className="panel px-4 py-3 text-meta text-bad"
        >
          {saveError.message}
        </p>
      )}

      {props && (
        <>
          <div
            role="tablist"
            aria-label={`Sections of ${props.draft.name}`}
            data-testid={TEMPLATES_TEST_IDS.editorTabs}
            className="flex gap-1 border-b border-edge"
          >
            {TABS.map(({ tab, label }) => (
              <button
                key={tab}
                type="button"
                role="tab"
                id={tabId(tab)}
                aria-selected={tab === open}
                aria-controls={panelId(tab)}
                tabIndex={tab === open ? 0 : -1}
                onKeyDown={(event) => onTabKey(event, tab)}
                data-testid={editorTabButtonTestId(tab)}
                className={`rounded-t-md px-3 py-1.5 text-meta font-medium ${
                  tab === open ? 'bg-panel-2 text-ink' : 'text-ink-muted hover:text-ink'
                }`}
                onClick={() => show(tab)}
              >
                {label}
              </button>
            ))}
          </div>
          <div
            role="tabpanel"
            id={panelId(open)}
            aria-labelledby={tabId(open)}
            data-testid={TEMPLATES_TEST_IDS.editorPanel}
          >
            <Tab {...props} />
          </div>
          <EditorPreview draft={props.draft} />
          <PublishChecklist {...props} />
        </>
      )}
    </section>
  );
}
