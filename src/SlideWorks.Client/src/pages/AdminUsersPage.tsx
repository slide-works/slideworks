import { useEffect, useState } from 'react';
import { ApiError, api } from '../api/client';
import type { AdminUsersView } from '../api/types';
import { AdminUserRow } from './admin/AdminUserRow';
import type { AdminAction } from './admin/AdminUserActions';
import { LedgerHead } from './decks/LedgerHead';

type State =
  | { kind: 'loading' }
  | { kind: 'loaded'; view: AdminUsersView }
  | { kind: 'failed'; message: string; adminsOnly: boolean };

const COLUMNS = [{ label: 'Person' }, { label: 'Access' }, { label: 'Changed' }, { label: '' }];
const UNREACHABLE = "Can't reach SlideWorks";

// /admin/users: everyone who has asked for access, from one GET /api/admin/users (AdminUsersView). Every action is
// a POST that answers with the whole refreshed view, which replaces this one; the server's order is kept.
export function AdminUsersPage() {
  const [state, setState] = useState<State>({ kind: 'loading' });
  const [busyId, setBusyId] = useState<string | null>(null);
  const [rowError, setRowError] = useState<{ id: string; message: string } | null>(null);

  useEffect(() => {
    let active = true;
    api.get<AdminUsersView>('/api/admin/users').then(
      (view) => active && setState({ kind: 'loaded', view }),
      (error: unknown) =>
        active &&
        setState({
          kind: 'failed',
          message: error instanceof ApiError ? error.message : UNREACHABLE,
          adminsOnly: error instanceof ApiError && error.status === 403,
        }),
    );
    return () => {
      active = false;
    };
  }, []);

  async function act(id: string, action: AdminAction) {
    setBusyId(id);
    setRowError(null);
    try {
      const view = await api.post<AdminUsersView>(`/api/admin/users/${id}/${action}`);
      setState({ kind: 'loaded', view });
    } catch (error) {
      setRowError({ id, message: error instanceof ApiError ? error.message : UNREACHABLE });
    } finally {
      setBusyId(null);
    }
  }

  if (state.kind === 'failed') {
    return (
      <section data-testid="admin-users-page" className="grid gap-3">
        <h2 className="text-lg font-bold tracking-tight text-ink">
          {state.adminsOnly ? 'Admins only' : 'People'}
        </h2>
        <p
          role="alert"
          data-testid="admin-users-error"
          className="panel px-4 py-3 text-meta text-bad"
        >
          {state.message}
        </p>
      </section>
    );
  }

  const view = state.kind === 'loaded' ? state.view : null;
  return (
    <section data-testid="admin-users-page" className="grid gap-5">
      <div className="flex flex-wrap items-center gap-3">
        <h2 className="text-lg font-bold tracking-tight text-ink">People</h2>
        {view && (
          <span
            data-testid="admin-pending-count"
            className="rounded-full bg-warn-soft px-2 py-0.5 font-mono text-xs text-warn"
          >
            {view.pendingCount} waiting
          </span>
        )}
      </div>
      {view && (
        <div className="panel overflow-x-auto">
          <table data-testid="admin-users-table" className="w-full border-collapse text-meta">
            <LedgerHead columns={COLUMNS} />
            <tbody>
              {view.users.map((user) => (
                <AdminUserRow
                  key={user.id}
                  user={user}
                  busy={busyId === user.id}
                  error={rowError?.id === user.id ? rowError.message : null}
                  onAction={(action) => void act(user.id, action)}
                />
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}
