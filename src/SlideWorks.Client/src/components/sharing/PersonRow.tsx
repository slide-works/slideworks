import type { AvatarView, DeckAccessLevel } from '../../api/types';
import { Avatar } from '../Avatar';

export type PersonEntry = {
  email: string;
  name: string | null;
  avatar: AvatarView | null;
  access: DeckAccessLevel;
  isNew: boolean;
};

type PersonRowProps = {
  person: PersonEntry;
  paused: boolean;
  onAccess(access: DeckAccessLevel): void;
  onRemove(): void;
};

// One person on the list: their name and avatar when they have an account, else the bare email (D24), with
// an access select and Remove. Each control's name includes the email.
export function PersonRow({ person, paused, onAccess, onRemove }: PersonRowProps) {
  return (
    <li data-testid="sharing-person" className="flex items-center gap-2.5 py-1.5">
      {person.avatar ? (
        <Avatar avatar={person.avatar} testId="sharing-person-avatar" />
      ) : (
        <span aria-hidden className="inline-block h-7 w-7 shrink-0 rounded-full bg-panel-2" />
      )}
      <div className="min-w-0 flex-1 text-meta">
        {person.name !== null ? (
          <>
            <b data-testid="sharing-person-name" className="block truncate text-ink">
              {person.name}
            </b>
            <span data-testid="sharing-person-email" className="block truncate text-ink-muted">
              {person.email}
            </span>
          </>
        ) : (
          <span data-testid="sharing-person-email" className="block truncate text-ink">
            {person.email}
          </span>
        )}
      </div>
      {person.isNew && (
        <span
          data-testid="sharing-person-new"
          className="rounded-full bg-violet-50 px-2 py-0.5 text-xs font-medium text-violet-700"
        >
          New
        </span>
      )}
      {paused && (
        <span data-testid="sharing-person-paused" className="text-xs text-ink-muted">
          paused
        </span>
      )}
      <select
        data-testid="sharing-person-access"
        aria-label={`Access for ${person.email}`}
        className="rounded-md border border-edge-strong bg-panel px-1.5 py-1 text-meta text-ink"
        value={person.access}
        onChange={(event) => onAccess(event.target.value === 'write' ? 'write' : 'read')}
      >
        <option value="read">Can view</option>
        <option value="write">Can edit</option>
      </select>
      <button
        type="button"
        data-testid="sharing-person-remove"
        aria-label={`Remove ${person.email}`}
        className="btn-ghost"
        onClick={onRemove}
      >
        Remove
      </button>
    </li>
  );
}
