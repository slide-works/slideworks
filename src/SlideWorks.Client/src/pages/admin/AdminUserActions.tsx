import { useEffect, useRef, useState } from 'react';
import type { AdminUserRow } from '../../api/types';

export type AdminAction = 'approve' | 'reject' | 'make-admin' | 'remove-admin' | 'revoke';

type Props = { user: AdminUserRow; disabled: boolean; onAction(action: AdminAction): void };

type ActionButtonProps = {
  testId: string;
  label: string;
  name: string;
  disabled: boolean;
  buttonRef?: React.Ref<HTMLButtonElement>;
  onClick(): void;
};

// The visible text is the verb; the accessible name adds whose row it is.
function ActionButton({ testId, label, name, disabled, buttonRef, onClick }: ActionButtonProps) {
  return (
    <button
      type="button"
      ref={buttonRef}
      data-testid={testId}
      aria-label={`${label} ${name}`}
      disabled={disabled}
      className="btn-ghost"
      onClick={onClick}
    >
      {label}
    </button>
  );
}

// The buttons for one person, by access state: pending has Approve and Reject, rejected has Approve, and approved
// has Make admin or Remove admin, and Revoke. Revoke asks first, in place: the confirm step takes focus on Cancel,
// and Cancel hands it back to Revoke. `disabled` is true while any action is in flight.
export function AdminUserActions({ user, disabled, onAction }: Props) {
  const [confirming, setConfirming] = useState(false);
  const revokeRef = useRef<HTMLButtonElement>(null);
  const cancelRef = useRef<HTMLButtonElement>(null);
  const cancelled = useRef(false);
  const { name } = user;

  useEffect(() => {
    if (confirming) cancelRef.current?.focus();
    else if (cancelled.current) {
      cancelled.current = false;
      revokeRef.current?.focus();
    }
  }, [confirming]);

  if (confirming) {
    return (
      <div className="flex items-center justify-end gap-2">
        <span className="text-meta text-ink-muted">Revoke access?</span>
        <button
          type="button"
          data-testid="admin-revoke-confirm"
          aria-label={`Revoke ${name}`}
          disabled={disabled}
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
          name={`revoking ${name}`}
          disabled={false}
          buttonRef={cancelRef}
          onClick={() => {
            cancelled.current = true;
            setConfirming(false);
          }}
        />
      </div>
    );
  }

  const button = (testId: string, label: string, action: AdminAction) => (
    <ActionButton
      testId={testId}
      label={label}
      name={name}
      disabled={disabled}
      onClick={() => onAction(action)}
    />
  );

  return (
    <div className="flex items-center justify-end gap-2">
      {user.access !== 'approved' && button('admin-approve', 'Approve', 'approve')}
      {user.access === 'pending' && button('admin-reject', 'Reject', 'reject')}
      {user.access === 'approved' && (
        <>
          {user.isAdmin
            ? button('admin-remove-admin', 'Remove admin', 'remove-admin')
            : button('admin-make-admin', 'Make admin', 'make-admin')}
          <ActionButton
            testId="admin-revoke"
            label="Revoke"
            name={name}
            disabled={disabled}
            buttonRef={revokeRef}
            onClick={() => setConfirming(true)}
          />
        </>
      )}
    </div>
  );
}
