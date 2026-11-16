// A label with `hidden` is read by screen readers but not shown, for a column
// whose cells explain themselves (the Present link).
type Column = { label: string; align?: 'right'; hidden?: boolean };

// A ledger table's header row: small uppercase labels on the grey band.
export function LedgerHead({ columns }: { columns: Column[] }) {
  return (
    <thead>
      <tr>
        {columns.map((column, index) => (
          <th
            key={index}
            className={`border-b border-edge bg-panel-2 px-3 py-2 text-[11px] font-semibold tracking-wider whitespace-nowrap text-ink-muted uppercase ${column.align === 'right' ? 'text-right' : 'text-left'}`}
          >
            {column.hidden ? <span className="sr-only">{column.label}</span> : column.label}
          </th>
        ))}
      </tr>
    </thead>
  );
}
