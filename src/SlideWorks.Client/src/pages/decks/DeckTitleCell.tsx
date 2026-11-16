import { Link } from 'react-router-dom';

type DeckTitleCellProps = { title: string; subtitle: string | null; to: string };

// The deck's title, linked, with its subtitle underneath.
export function DeckTitleCell({ title, subtitle, to }: DeckTitleCellProps) {
  return (
    <td className="px-3 py-1.5">
      <Link
        to={to}
        data-testid="deck-title"
        className="font-semibold text-ink no-underline hover:text-accent"
      >
        {title}
      </Link>
      {subtitle && <small className="block text-xs text-ink-faint">{subtitle}</small>}
    </td>
  );
}
