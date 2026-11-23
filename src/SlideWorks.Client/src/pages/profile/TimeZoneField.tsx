import type { ProfileAccount } from '../../api/profile';

// The time zone line of the Account panel. TODO: make it a select that saves (PUT /api/me/time-zone) and fills an empty
// zone from the browser once; for now it shows the stored zone.
export function TimeZoneField({ account }: { account: ProfileAccount }) {
  const label = account.timeZones.find((zone) => zone.id === account.timeZone)?.label;
  return (
    <p data-testid="profile-time-zone" className="text-meta text-ink">
      {account.timeZone === null ? 'Not set' : (label ?? account.timeZone)}
    </p>
  );
}
