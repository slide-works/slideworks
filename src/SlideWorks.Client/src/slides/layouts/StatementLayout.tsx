import type { LayoutProps } from '../slots';

// The statement slide.
// TODO: draws only its statement; the real statement layout replaces it.
export function StatementLayout({ slots }: LayoutProps<'statement'>) {
  return (
    <div data-testid="layout-statement" className="sw-layout">
      <h2 data-testid="statement-text">{slots.statement}</h2>
    </div>
  );
}
