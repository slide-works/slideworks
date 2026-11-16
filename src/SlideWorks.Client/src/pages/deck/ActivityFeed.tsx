import type { ActivityRow } from '../../api/types';

type ActivityFeedProps = {
  rows: ActivityRow[];
  // On the owner's page, a person's change is the owner's own (people change
  // only their own decks, through the browser), so it reads "You".
  isOwner: boolean;
};

function initials(name: string): string {
  return name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((word) => word[0].toUpperCase())
    .join('');
}

// A key for each row that survives a re-read: rows have no id, and new rows
// arrive at the top, so a row is keyed by what it says plus how many identical
// rows come before it counting from the oldest. The time label is left out, as
// it changes on every re-read.
function rowKeys(rows: ActivityRow[]): string[] {
  const seen = new Map<string, number>();
  const oldestFirst = [...rows].reverse().map((row) => {
    const content = [row.actorKind, row.actor, row.via, row.tool ?? '', row.summary].join('|');
    const occurrence = (seen.get(content) ?? 0) + 1;
    seen.set(content, occurrence);
    return `${content}#${occurrence}`;
  });
  return oldestFirst.reverse();
}

// The deck's change log, newest first: "Claude added slide 16 · 2 min ago · via MCP".
export function ActivityFeed({ rows, isOwner }: ActivityFeedProps) {
  const keys = rowKeys(rows);
  return (
    <section data-testid="deck-activity" className="panel grid gap-3 p-4">
      <h3 className="text-xs font-semibold tracking-wide text-ink-muted uppercase">Activity</h3>
      {rows.length === 0 ? (
        <p className="text-meta text-ink-faint">Nothing yet.</p>
      ) : (
        <ul className="grid gap-3">
          {rows.map((row, index) => {
            const agent = row.actorKind === 'agent';
            const actor = !agent && isOwner ? 'You' : row.actor;
            return (
              <li key={keys[index]} data-testid="activity-row" className="flex gap-2.5 text-meta">
                <span
                  aria-hidden
                  className={`grid h-6 w-6 shrink-0 place-items-center rounded-md text-[10px] font-semibold ${
                    agent ? 'bg-accent-soft text-accent-dark' : 'bg-panel-2 text-ink-muted'
                  }`}
                >
                  {agent ? 'AI' : initials(row.actor)}
                </span>
                <span className="min-w-0">
                  <span className="text-ink">
                    <b className="font-semibold">{actor}</b> {row.summary}
                  </span>
                  <span className="sr-only"> · </span>
                  <span className="block text-xs text-ink-muted">
                    {row.atLabel} · {row.via === 'mcp' ? 'via MCP' : 'in the browser'}
                    {row.tool !== null && (
                      <>
                        {' '}
                        <code className="font-mono">{row.tool}</code>
                      </>
                    )}
                  </span>
                </span>
              </li>
            );
          })}
        </ul>
      )}
    </section>
  );
}
