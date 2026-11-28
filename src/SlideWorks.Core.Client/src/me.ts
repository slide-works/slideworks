// GET /api/me (src/contracts/screens/me.json). Moved from the SPA's api/types.ts, which re-exports them.

// How a person is shown everywhere. pictureUrl is the uploaded photo, else the
// Google picture, else null.
export type AvatarView = {
  initials: string;
  colour: string;
  pictureUrl: string | null;
};

// GET /api/me
export type MeView = {
  email: string;
  name: string;
  pictureUrl: string | null;
  avatar: AvatarView;
  timeZone: string | null;
  access: 'pending' | 'approved';
  isAdmin: boolean;
  pendingRequests: number;
  adminMissing: boolean;
};
