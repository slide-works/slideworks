import { useRef, useState } from 'react';
import type { DeckAccessLevel, SharingLevel, SharingOwner } from '../../api/types';
import { Avatar } from '../Avatar';
import { FULL_EMAIL, PeopleField } from './PeopleField';
import { PersonRow, type PersonEntry } from './PersonRow';

type PeopleSectionProps = {
  owner: SharingOwner;
  people: PersonEntry[];
  level: SharingLevel;
  error: string | null;
  pending: string;
  onPending(text: string): void;
  onChange(people: PersonEntry[]): void;
};

// The People section (D26): a count with the owner included, the owner's row first, the field, one row each.
export function PeopleSection({
  owner,
  people,
  level,
  error,
  pending,
  onPending,
  onChange,
}: PeopleSectionProps) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [notice, setNotice] = useState('');
  const newCount = people.filter((person) => person.isNew).length;

  const add = () => {
    const email = pending.trim().toLowerCase();
    if (!FULL_EMAIL.test(email)) return;
    onPending('');
    const known = email === owner.email.toLowerCase() || people.some((p) => p.email === email);
    setNotice(known ? `${email} is already on the list.` : '');
    if (!known) {
      onChange([...people, { email, name: null, avatar: null, access: 'read', isNew: true }]);
    }
    inputRef.current?.focus();
  };

  const setAccess = (email: string, access: DeckAccessLevel) =>
    onChange(people.map((person) => (person.email === email ? { ...person, access } : person)));

  const remove = (email: string) => {
    setNotice('');
    onChange(people.filter((person) => person.email !== email));
    inputRef.current?.focus();
  };

  return (
    <section aria-labelledby="sharing-people-heading" className="grid gap-2 px-[18px] pb-3">
      <h3 id="sharing-people-heading" className="text-sm font-semibold text-ink">
        People{' '}
        <span data-testid="sharing-people-count" className="font-normal text-ink-muted">
          {people.length + 1}
        </span>
      </h3>
      <PeopleField value={pending} inputRef={inputRef} onChange={onPending} onAdd={add} />
      {notice && (
        <p role="status" data-testid="sharing-person-notice" className="text-meta text-ink-muted">
          {notice}
        </p>
      )}
      {error && (
        <p role="alert" data-testid="sharing-error" className="text-meta text-bad">
          {error}
        </p>
      )}
      <ul aria-label="People on this deck" className="divide-y divide-edge">
        <li data-testid="sharing-owner" className="flex items-center gap-2.5 py-1.5">
          <Avatar avatar={owner.avatar} testId="sharing-owner-avatar" />
          <div className="min-w-0 flex-1 text-meta">
            <b className="block truncate text-ink">{owner.name} (you)</b>
            <span className="block truncate text-ink-muted">{owner.email}</span>
          </div>
          <span data-testid="sharing-owner-label" className="text-meta text-ink-muted">
            Owner
          </span>
        </li>
        {people.map((person) => (
          <PersonRow
            key={person.email}
            person={person}
            paused={level === 'private'}
            onAccess={(access) => setAccess(person.email, access)}
            onRemove={() => remove(person.email)}
          />
        ))}
      </ul>
      {newCount > 0 && (
        <p data-testid="sharing-new-note" className="text-meta text-ink-muted">
          Saving emails {newCount} new {newCount === 1 ? 'person' : 'people'}.
        </p>
      )}
    </section>
  );
}
