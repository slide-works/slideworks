import { Navigate, useParams } from 'react-router-dom';
import { GuestPage } from './GuestPage';

const SIX_DIGITS = /^[1-9]\d{5}$/;

// /j/:code and /:code. Only a join code (six digits, the first not 0) reaches GuestPage; any other single segment
// behaves as it did before the bare-code route: it goes to /decks.
export function GuestRoute() {
  const { code = '' } = useParams();
  return SIX_DIGITS.test(code) ? <GuestPage /> : <Navigate to="/decks" replace />;
}
