import { setOnPendingApproval } from '../api/client';
import { AdminUsersPage } from '../pages/AdminUsersPage';

// Mounts the admin page with the client's pending handler turned into a no-op: the real one leaves for /pending,
// which would end the page under test.
export function PendingHandlerSilenced() {
  setOnPendingApproval(() => {});
  return <AdminUsersPage />;
}
