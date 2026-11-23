import type { ProfileIdentity } from '../../api/profile';

// The photo line of the Identity panel. TODO: add the file input and Remove photo (POST and DELETE /api/me/photo);
// for now it says which picture is shown.
export function PhotoControl({ identity }: { identity: ProfileIdentity }) {
  let picture = 'Initials';
  if (identity.hasPhoto) {
    picture = 'Your photo';
  } else if (identity.avatar.pictureUrl !== null) {
    picture = 'Your Google picture';
  }
  return (
    <p data-testid="profile-photo-source" className="text-meta text-ink-muted">
      Picture: {picture}
    </p>
  );
}
