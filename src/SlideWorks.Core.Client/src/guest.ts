import { requestWithHeaders } from './client';
import type { AudienceStateSummary } from './realtime/shapes';

// Guest identity by header (contract K7). The app holds the same opaque guest id the web keeps in the sw_guest cookie and
// sends it as a header (on /hubs/deck, as the guest query).

export const GUEST_HEADER = 'X-SlideWorks-Guest';
// 'app' on a join asks the server to issue the guest id in GUEST_HEADER instead of a cookie.
export const CLIENT_HEADER = 'X-SlideWorks-Client';

const GUEST_ID = /^[0-9a-f]{32}$/;

// The server counts anything else as no guest.
export function isGuestId(value: string): boolean {
  return GUEST_ID.test(value);
}

// The app's structural subset of GET /api/join/{code}'s GuestView (src/contracts/screens/guest.json).
export type JoinSummary = { deckTitle: string; presenterName: string; state: AudienceStateSummary };

export type JoinResult = { view: JoinSummary; issuedGuestId: string | null };

// Joins the talk with that code as a guest of that origin. issuedGuestId is the id the server issued in GUEST_HEADER,
// or null when it issued none (the request already carried a valid id, or the host is the web, which gets a cookie).
export async function getJoin(origin: string, code: string): Promise<JoinResult> {
  const { body, headers } = await requestWithHeaders<JoinSummary>(
    'GET',
    `/api/join/${encodeURIComponent(code)}`,
    undefined,
    { as: 'guest', origin },
  );
  return { view: body, issuedGuestId: headers.get(GUEST_HEADER) };
}
