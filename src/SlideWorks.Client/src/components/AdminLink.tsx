import { Link } from 'react-router-dom';
import type { MeView } from '../api/types';

// The top bar's link to /admin/users, for admins only. While requests are waiting it carries their count.
export function AdminLink({ me }: { me: MeView }) {
  if (!me.isAdmin) return null;
  return (
    <Link
      to="/admin/users"
      data-testid="admin-link"
      className="inline-block rounded-md px-2.5 py-1 text-meta font-medium text-ink-muted no-underline hover:bg-panel-2 hover:text-ink"
    >
      {me.pendingRequests > 0 ? (
        <>
          <span aria-hidden>Admin</span>
          <span className="sr-only">
            Admin, {me.pendingRequests} pending {me.pendingRequests === 1 ? 'request' : 'requests'}
          </span>
          <span
            aria-hidden
            data-testid="admin-link-badge"
            className="ml-1.5 rounded-full bg-accent px-1.5 font-mono text-[11px] leading-4 text-white"
          >
            {me.pendingRequests}
          </span>
        </>
      ) : (
        'Admin'
      )}
    </Link>
  );
}
