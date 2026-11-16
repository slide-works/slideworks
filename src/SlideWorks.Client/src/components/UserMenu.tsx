import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { api } from '../api/client';
import type { MeView } from '../api/types';

function initials(name: string): string {
  return name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((word) => word[0].toUpperCase())
    .join('');
}

// The signed-in person's initials in the top bar. Opening it shows who is signed
// in and a sign-out button, which ends the cookie session (POST /api/signout) and
// goes to /signin.
export function UserMenu({ me }: { me: MeView }) {
  const navigate = useNavigate();
  const [open, setOpen] = useState(false);
  const [failed, setFailed] = useState(false);

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
    <div className="relative">
      <button
        type="button"
        title={me.name}
        aria-label={`Account: ${me.name}`}
        aria-expanded={open}
        data-testid="user-menu-button"
        onClick={() => setOpen((value) => !value)}
        className="grid h-7 w-7 place-items-center rounded-full bg-accent-soft text-[11px] font-semibold text-accent-dark"
      >
        {initials(me.name)}
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
