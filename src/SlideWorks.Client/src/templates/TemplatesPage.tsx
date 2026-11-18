import { TEMPLATES_TEST_IDS } from './testIds';
import type { TemplateRow } from './types';
import { useTemplates } from './useTemplates';

// TODO: a placeholder. The templates page (its three tables, their row actions and "New
// template") replaces the bare lists; the list comes from the one useTemplates either way.
export function TemplatesPage() {
  const { load } = useTemplates();
  const view = load.kind === 'loaded' ? load.templates : null;

  return (
    <section data-testid={TEMPLATES_TEST_IDS.page} className="grid gap-5">
      <h1 className="text-lg font-bold tracking-tight text-ink">Templates</h1>

      {load.kind === 'failed' && (
        <p
          role="alert"
          data-testid={TEMPLATES_TEST_IDS.error}
          className="panel px-4 py-3 text-meta text-bad"
        >
          {load.message}
        </p>
      )}

      {view && (
        <>
          <Rows label="Your templates" rows={view.mine} />
          <Rows label="Shared with you" rows={view.shared} />
          <Rows label="Built in" rows={view.builtIn} />
        </>
      )}
    </section>
  );
}

function Rows({ label, rows }: { label: string; rows: TemplateRow[] }) {
  if (rows.length === 0) return null;
  return (
    <ul aria-label={label} className="grid gap-1">
      {rows.map((row) => (
        <li key={row.id} data-testid={TEMPLATES_TEST_IDS.row} className="text-meta text-ink">
          <span data-testid={TEMPLATES_TEST_IDS.rowName}>{row.name}</span>
        </li>
      ))}
    </ul>
  );
}
