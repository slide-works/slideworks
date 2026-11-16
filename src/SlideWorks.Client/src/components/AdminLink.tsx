import { Link } from 'react-router-dom';
import type { MeView } from '../api/types';

// The top bar's link to /admin/users, for admins only. While requests are waiting it carries their count.
export function AdminLink({ me }: { me: MeView }) {
  if (!me.isAdmin) return null;
  return (
    <Link
      to="/admin/users"
      data-testid="admin-link"
      className="flex items-center gap-1.5 rounded-md px-2.5 py-1 text-meta font-medium text-ink-muted no-underline hover:bg-panel-2 hover:text-ink"
    >
      Admin
      {me.pendingRequests > 0 && (
        <span
          data-testid="admin-link-badge"
          aria-label={`${me.pendingRequests} waiting`}
          className="rounded-full bg-accent px-1.5 font-mono text-[11px] leading-4 text-white"
        >
          {me.pendingRequests}
        </span>
      )}
    </Link>
  );
}
