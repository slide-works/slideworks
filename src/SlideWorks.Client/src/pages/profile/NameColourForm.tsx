import type { ProfileIdentity } from '../../api/profile';
import { Avatar } from '../../components/Avatar';

// The Identity panel's name and colour. TODO: make the name editable, add the colour options and a Save button
// (PUT /api/me/profile); for now it shows what is stored.
export function NameColourForm({ identity }: { identity: ProfileIdentity }) {
  return (
    <div data-testid="profile-identity" className="flex items-center gap-4">
      <Avatar avatar={identity.avatar} size="lg" testId="profile-avatar" />
      <div className="grid">
        <span data-testid="profile-name" className="text-sm font-semibold text-ink">
          {identity.name}
        </span>
        <span data-testid="profile-email" className="text-meta text-ink-muted">
          {identity.email}
        </span>
        <span data-testid="profile-colour" className="text-meta text-ink-muted">
          {identity.colour ?? 'No colour chosen'}
        </span>
      </div>
    </div>
  );
}
