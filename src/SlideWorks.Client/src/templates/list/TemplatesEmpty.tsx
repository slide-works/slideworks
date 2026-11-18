// The Yours group before the person has a template of their own.
export function TemplatesEmpty({ onNew }: { onNew(): void }) {
  return (
    <div data-testid="templates-empty" className="panel grid justify-items-center gap-1 px-6 py-10">
      <p className="font-semibold text-ink">No templates of your own yet</p>
      <p className="text-meta text-ink-muted">
        Make one to give your decks your fonts, logo and colours.
      </p>
      <button type="button" className="btn-primary mt-3" aria-haspopup="dialog" onClick={onNew}>
        New template
      </button>
    </div>
  );
}
