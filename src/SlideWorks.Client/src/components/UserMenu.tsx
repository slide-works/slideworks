import { useEffect, useRef, useState } from 'react';
import type { FocusEvent, KeyboardEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { api } from '../api/client';
import type { AvatarView, MeView } from '../api/types';
import { Avatar } from './Avatar';

const PROFILE_SAVED = 'slideworks:profile-saved';

type ProfileSaved = { name: string; avatar: AvatarView };

// The profile page tells the menu when the person saved a new name or picture, so the top bar shows it without a reload.
export function announceProfileSaved(name: string, avatar: AvatarView): void {
  window.dispatchEvent(new CustomEvent<ProfileSaved>(PROFILE_SAVED, { detail: { name, avatar } }));
}

// The signed-in person's initials in the top bar. Opening it shows who is signed
// in and a list: Profile, People (admin) for admins, and Sign out, which ends the cookie
// session (POST /api/signout) and goes to /signin. Opening moves focus to the first item; Escape closes it and
// returns focus to the button; a click outside or tabbing out of it closes it.
export function UserMenu({ me: loaded }: { me: MeView }) {
  const [saved, setSaved] = useState<ProfileSaved | null>(null);
  const me = saved === null ? loaded : { ...loaded, ...saved };
  const navigate = useNavigate();
  const [open, setOpen] = useState(false);
  const [failed, setFailed] = useState(false);
  const rootRef = useRef<HTMLDivElement>(null);
  const buttonRef = useRef<HTMLButtonElement>(null);
  const firstItemRef = useRef<HTMLAnchorElement>(null);

  useEffect(() => {
    const onSaved = (event: Event) => setSaved((event as CustomEvent<ProfileSaved>).detail);
    window.addEventListener(PROFILE_SAVED, onSaved);
    return () => window.removeEventListener(PROFILE_SAVED, onSaved);
  }, []);

  useEffect(() => {
    if (!open) {
      return;
    }
    firstItemRef.current?.focus();
    function closeOnOutsidePointer(event: PointerEvent) {
      if (!rootRef.current?.contains(event.target as Node)) {
        setOpen(false);
      }
    }
    document.addEventListener('pointerdown', closeOnOutsidePointer);
    return () => document.removeEventListener('pointerdown', closeOnOutsidePointer);
  }, [open]);

  function closeOnEscape(event: KeyboardEvent) {
    if (open && event.key === 'Escape') {
      event.preventDefault();
      setOpen(false);
      buttonRef.current?.focus();
    }
  }

  // Focus moving to something outside the menu (Tab past the last item) closes it.
  // A null target is a click on something that can't take focus; the outside-click
  // listener decides that one, so a click on the panel's text keeps it open.
  function closeOnFocusLeaving(event: FocusEvent) {
    const next = event.relatedTarget;
    if (next instanceof Node && !rootRef.current?.contains(next)) {
      setOpen(false);
    }
  }

  async function signOut() {
    setFailed(false);
    try {
      await api.post('/api/signout');
      navigate('/signin', { replace: true });
    } catch {
      setFailed(true);
    }
  }

  return (
    <div ref={rootRef} className="relative" onKeyDown={closeOnEscape} onBlur={closeOnFocusLeaving}>
      <button
        ref={buttonRef}
        type="button"
        title={me.name}
        aria-label={`Account: ${me.name}`}
        aria-expanded={open}
        data-testid="user-menu-button"
        onClick={() => setOpen((value) => !value)}
        className="rounded-full"
      >
        <Avatar avatar={me.avatar} testId="user-menu-avatar" />
      </button>
      {open && (
        <div
          data-testid="user-menu-panel"
          className="absolute right-0 mt-2 grid w-56 gap-2 rounded-panel border border-edge bg-panel p-3 shadow-lg"
        >
          <div className="grid">
            <span className="text-meta font-semibold text-ink">{me.name}</span>
            <span className="truncate text-meta text-ink-muted">{me.email}</span>
          </div>
          <Link
            ref={firstItemRef}
            to="/profile"
            className="btn-ghost"
            data-testid="user-menu-profile"
            onClick={() => setOpen(false)}
          >
            Profile
          </Link>
          {me.isAdmin && (
            <Link
              to="/admin/users"
              className="btn-ghost"
              data-testid="user-menu-people"
              onClick={() => setOpen(false)}
            >
              People (admin)
            </Link>
          )}
          <button
            type="button"
            className="btn-ghost"
            data-testid="user-menu-signout"
            onClick={signOut}
          >
            Sign out
          </button>
          {failed && (
            <p role="alert" data-testid="user-menu-error" className="text-meta text-ink-muted">
              Couldn’t sign out. Try again.
            </p>
          )}
        </div>
      )}
    </div>
  );
}
