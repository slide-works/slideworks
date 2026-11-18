import {
  useCallback,
  useMemo,
  useRef,
  useState,
  type ComponentType,
  type KeyboardEvent,
} from 'react';
import { Link, useParams, useSearchParams } from 'react-router-dom';
import { bundleUrl } from '../bundle';
import { TemplateShareDialog } from '../TemplateShareDialog';
import { TEMPLATES_TEST_IDS, editorTabButtonTestId } from '../testIds';
import type { EditorTab, TemplateManifest } from '../types';
import { useTemplateDraft, type ChangeOutcome } from '../useTemplateDraft';
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
  if (key === 'ArrowRight' || key === 'ArrowDown') return OFFERED[at === last ? 0 : at + 1];
  if (key === 'ArrowLeft' || key === 'ArrowUp') return OFFERED[at === 0 ? last : at - 1];
  if (key === 'Home') return OFFERED[0];
  if (key === 'End') return OFFERED[last];
  return null;
}

const panelId = (tab: EditorTab) => `template-editor-panel-${tab}`;
const tabId = (tab: EditorTab) => `template-editor-tab-${tab}`;

const STALE = 'Someone else changed this template. Reload to see their changes.';
const NOT_SAVED = 'Could not save. Your change is still here.';

// Why the last change did not save, for the alert under the header.
type Failure =
  | { kind: 'stale' }
  | { kind: 'retry'; patch: Partial<TemplateManifest> }
  | { kind: 'refused'; message: string };

// /templates/:id/edit. Keyed by the template's id, so that moving to another template starts a
// new editor: a save still in flight for the old one can never put its state on the new one.
export function TemplateEditor() {
  const { id = '' } = useParams();
  return <OpenEditor key={id} id={id} />;
}

// The editor's frame. It holds the one useTemplateDraft, the tab list and the open tab (the `tab`
// search parameter; `basics` without one), and hands every tab and the publish checklist the same
// EditorTabProps. The tab bar is the ARIA tabs pattern: only the open tab is in the Tab order, and
// Left, Right, Up, Down, Home and End move to a tab and open it.
function OpenEditor({ id }: { id: string }) {
  const [search, setSearch] = useSearchParams();
  const { load, saving, saveError, change, reload } = useTemplateDraft(id);
  const [failure, setFailure] = useState<Failure | null>(null);
  // What the preview shows ahead of the server, until each save is read back.
  const [optimistic, setOptimistic] = useState<Partial<TemplateManifest>>({});
  const [sharing, setSharing] = useState(false);
  const stale = useRef(false);
  // A change that failed with no answer from the server: sent again under the next change.
  const failed = useRef<Partial<TemplateManifest> | null>(null);
  const counter = useRef(0);
  const newest = useRef<Partial<Record<string, number>>>({});
  const shareButton = useRef<HTMLButtonElement>(null);
  const open = tabOf(search, OFFERED);
  const Tab = TAB_CONTENT[open];
  const loaded = load.kind === 'loaded' ? load.draft : null;

  // Every tab saves through this. After a stale revision nothing more is sent until Reload.
  const save = useCallback(
    async (asked: Partial<TemplateManifest>): Promise<ChangeOutcome> => {
      if (stale.current) return { ok: false, code: 'stale_revision', message: STALE };
      const patch = failed.current === null ? asked : { ...failed.current, ...asked };
      counter.current += 1;
      const mine = counter.current;
      const keys = Object.keys(patch);
      for (const key of keys) newest.current[key] = mine;
      if (failed.current === null) setFailure(null);
      setOptimistic((current) => ({ ...current, ...patch }));
      const forget = (all: boolean) =>
        setOptimistic((current) => {
          const next: Record<string, unknown> = { ...current };
          for (const key of keys) if (all || newest.current[key] === mine) delete next[key];
          return next as Partial<TemplateManifest>;
        });
      const outcome = await change(patch);
      if (outcome.ok) {
        failed.current = null;
        setFailure(null);
        forget(false);
      } else if (outcome.code === 'stale_revision') {
        failed.current = null;
        stale.current = true;
        setFailure({ kind: 'stale' });
        forget(true);
      } else if (outcome.code === null) {
        failed.current = patch;
        setFailure({ kind: 'retry', patch });
      } else {
        failed.current = null;
        setFailure({ kind: 'refused', message: outcome.message });
        forget(false);
      }
      return outcome;
    },
    [change],
  );

  const reloadAfterStale = async () => {
    await reload();
    stale.current = false;
    setFailure(null);
  };

  const preview = useMemo(
    () => loaded && { ...loaded, manifest: { ...loaded.manifest, ...optimistic } },
    [loaded, optimistic],
  );

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
    // Alt+Left is the browser's Back: a key with a modifier is not ours.
    if (event.altKey || event.ctrlKey || event.metaKey) return;
    const next = tabForKey(event.key, from);
    if (next === null) return;
    event.preventDefault();
    show(next);
    document.getElementById(tabId(next))?.focus();
  };

  if (load.kind === 'loading')
    return (
      <section data-testid={TEMPLATES_TEST_IDS.editor}>
        <p role="status" className="text-meta text-ink-muted">
          Loading the template…
        </p>
      </section>
    );

  if (load.kind === 'failed')
    return (
      <section data-testid={TEMPLATES_TEST_IDS.editor} className="grid justify-items-start gap-3">
        <p
          role="alert"
          data-testid={TEMPLATES_TEST_IDS.editorError}
          className="panel px-4 py-3 text-meta text-bad"
        >
          {load.notFound ? 'This template is not here, or it is not yours.' : load.message}
        </p>
        <Link to="/templates" className="btn-ghost no-underline">
          Back to your templates
        </Link>
      </section>
    );

  const draft = loaded;
  if (draft === null || preview === null) return null;
  const props = { draft, change: save, reload, saving };
  const alert =
    failure?.kind === 'stale'
      ? STALE
      : failure?.kind === 'retry'
        ? NOT_SAVED
        : (failure?.message ?? (saveError?.code === null ? saveError.message : null));
  const state = saving ? 'Saving…' : alert !== null ? 'Not saved' : 'Saved';

  return (
    <section data-testid={TEMPLATES_TEST_IDS.editor} className="grid gap-5">
      <header className="flex flex-wrap items-center gap-3">
        <h1 className="text-lg font-bold tracking-tight text-ink">{draft.name}</h1>
        <span data-testid="template-editor-status" className="chip text-meta">
          {draft.status === 'draft'
            ? 'Draft'
            : draft.status === 'archived'
              ? 'Archived'
              : `Published · v${draft.version}`}
        </span>
        <output data-testid="template-editor-save-state" className="text-meta text-ink-muted">
          {state}
        </output>
        <div className="ml-auto flex gap-2">
          <a href={bundleUrl(id)} download className="btn-ghost no-underline">
            Download bundle
          </a>
          <button
            ref={shareButton}
            type="button"
            className="btn-ghost"
            data-testid="template-editor-share"
            onClick={() => setSharing(true)}
          >
            Share
          </button>
        </div>
      </header>

      {alert !== null && (
        <div
          role="alert"
          data-testid={TEMPLATES_TEST_IDS.editorSaveError}
          className="panel flex flex-wrap items-center gap-3 px-4 py-3 text-meta text-bad"
        >
          <p>{alert}</p>
          {failure?.kind === 'stale' && (
            <button type="button" className="btn-ghost" onClick={() => void reloadAfterStale()}>
              Reload
            </button>
          )}
          {failure?.kind === 'retry' && (
            <button type="button" className="btn-ghost" onClick={() => void save(failure.patch)}>
              Retry
            </button>
          )}
        </div>
      )}

      <div className="grid items-start gap-5 lg:grid-cols-[11rem_minmax(0,1fr)_minmax(0,1fr)]">
        <div
          role="tablist"
          aria-label={`Sections of ${draft.name}`}
          data-testid={TEMPLATES_TEST_IDS.editorTabs}
          className="flex gap-1 lg:flex-col"
        >
          {TABS.map(({ tab, label }) => (
            <button
              key={tab}
              type="button"
              role="tab"
              id={tabId(tab)}
              aria-selected={tab === open}
              aria-controls={tab === open ? panelId(tab) : undefined}
              tabIndex={tab === open ? 0 : -1}
              onKeyDown={(event) => onTabKey(event, tab)}
              data-testid={editorTabButtonTestId(tab)}
              className={`rounded-md px-3 py-1.5 text-left text-meta font-medium ${
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
          tabIndex={0}
          data-testid={TEMPLATES_TEST_IDS.editorPanel}
        >
          <Tab {...props} />
        </div>
        <div className="grid gap-5">
          <EditorPreview draft={preview} />
          <PublishChecklist {...props} />
        </div>
      </div>

      {sharing && (
        <TemplateShareDialog
          templateId={id}
          name={draft.name}
          onClose={() => {
            setSharing(false);
            shareButton.current?.focus();
          }}
        />
      )}
    </section>
  );
}
