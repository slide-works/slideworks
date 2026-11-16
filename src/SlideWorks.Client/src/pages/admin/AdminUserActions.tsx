import { useState } from 'react';
import type { AdminUserRow } from '../../api/types';

export type AdminAction = 'approve' | 'reject' | 'make-admin' | 'remove-admin' | 'revoke';

type Props = { user: AdminUserRow; busy: boolean; onAction(action: AdminAction): void };

function ActionButton({
  testId,
  label,
  busy,
  onClick,
}: {
  testId: string;
  label: string;
  busy: boolean;
  onClick(): void;
}) {
  return (
    <button
      type="button"
      data-testid={testId}
      disabled={busy}
      className="btn-ghost"
      onClick={onClick}
    >
      {label}
    </button>
  );
}

// The buttons for one person, by access state: pending has Approve and Reject, rejected has Approve, and approved
// has Make admin or Remove admin, and Revoke. Revoke asks first, in place.
export function AdminUserActions({ user, busy, onAction }: Props) {
  const [confirming, setConfirming] = useState(false);

  if (confirming) {
    return (
      <div className="flex items-center justify-end gap-2">
        <span className="text-meta text-ink-muted">Revoke access?</span>
        <button
          type="button"
          data-testid="admin-revoke-confirm"
          disabled={busy}
          className="btn-ghost border-bad text-bad"
          onClick={() => {
            setConfirming(false);
            onAction('revoke');
          }}
        >
          Revoke
        </button>
        <ActionButton
          testId="admin-revoke-cancel"
          label="Cancel"
          busy={false}
          onClick={() => setConfirming(false)}
        />
      </div>
    );
  }

  return (
    <div className="flex items-center justify-end gap-2">
      {user.access !== 'approved' && (
        <ActionButton
          testId="admin-approve"
          label="Approve"
          busy={busy}
          onClick={() => onAction('approve')}
        />
      )}
      {user.access === 'pending' && (
        <ActionButton
          testId="admin-reject"
          label="Reject"
          busy={busy}
          onClick={() => onAction('reject')}
        />
      )}
      {user.access === 'approved' && (
        <>
          {user.isAdmin ? (
            <ActionButton
              testId="admin-remove-admin"
              label="Remove admin"
              busy={busy}
              onClick={() => onAction('remove-admin')}
            />
          ) : (
            <ActionButton
              testId="admin-make-admin"
              label="Make admin"
              busy={busy}
              onClick={() => onAction('make-admin')}
            />
          )}
          <ActionButton
            testId="admin-revoke"
            label="Revoke"
            busy={busy}
            onClick={() => setConfirming(true)}
          />
        </>
      )}
    </div>
  );
}
