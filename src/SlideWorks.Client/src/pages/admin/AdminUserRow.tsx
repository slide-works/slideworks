import type { AdminUserRow as AdminUserRowData } from '../../api/types';
import { Avatar } from '../../components/Avatar';
import { AdminUserActions, type AdminAction } from './AdminUserActions';

type Props = {
  user: AdminUserRowData;
  disabled: boolean;
  error: string | null;
  onAction(action: AdminAction): void;
};

const ACCESS_STYLE = {
  pending: 'bg-warn-soft text-warn',
  approved: 'bg-ok-soft text-ok',
  rejected: 'bg-bad-soft text-bad',
} as const;

// Pending rows have no decision yet, so they only say when the request came in. Decided rows say when and by whom;
// a null changedBy is the system.
function whenLine(user: AdminUserRowData): string {
  if (user.access === 'pending') return `requested ${user.requestedLabel}`;
  if (user.changedLabel === null) return `requested ${user.requestedLabel}`;
  return user.changedBy === null ? user.changedLabel : `${user.changedLabel} by ${user.changedBy}`;
}

// One person on /admin/users.
export function AdminUserRow({ user, disabled, error, onAction }: Props) {
  return (
    <tr
      data-testid="admin-user-row"
      data-user-id={user.id}
      tabIndex={-1}
      className="border-b border-edge align-top outline-none last:border-b-0"
    >
      <td className="px-3 py-2.5">
        <div className="flex items-center gap-2.5">
          <Avatar avatar={user.avatar} testId="admin-user-avatar" />
          <div className="grid">
            <span data-testid="admin-user-name" className="font-semibold text-ink">
              {user.name}
              {user.isSelf && <span className="font-normal text-ink-faint"> (you)</span>}
            </span>
            <span data-testid="admin-user-email" className="text-ink-muted">
              {user.email}
            </span>
          </div>
        </div>
      </td>
      <td className="px-3 py-2.5">
        <span
          data-testid="admin-user-access"
          className={`rounded-full px-2 py-0.5 text-xs font-semibold capitalize ${ACCESS_STYLE[user.access]}`}
        >
          {user.access}
        </span>
        {user.isAdmin && (
          <span
            data-testid="admin-user-admin"
            className="ml-1.5 rounded-full bg-accent-soft px-2 py-0.5 text-xs font-semibold text-accent-dark"
          >
            Admin
          </span>
        )}
      </td>
      <td
        data-testid="admin-user-when"
        className="px-3 py-2.5 font-mono text-[12.5px] whitespace-nowrap text-ink-muted"
      >
        {whenLine(user)}
      </td>
      <td className="px-3 py-2">
        <AdminUserActions user={user} disabled={disabled} onAction={onAction} />
        {error && (
          <p
            role="alert"
            data-testid="admin-user-error"
            className="mt-1.5 text-right text-meta text-bad"
          >
            {error}
          </p>
        )}
      </td>
    </tr>
  );
}
