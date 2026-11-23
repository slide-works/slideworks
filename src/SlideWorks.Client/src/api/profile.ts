import type { AvatarView } from './types';

// GET /api/me/profile and the answer of every profile write (src/contracts/screens/profile.json, C3).
// colour is the key the person chose, or null while they chose none (avatar.colour is the one shown).
export type ProfileIdentity = {
  name: string;
  email: string;
  avatar: AvatarView;
  colour: string | null;
  colours: string[];
  hasPhoto: boolean;
};

export type TimeZoneOption = { id: string; label: string };

export type ProfileAccount = {
  timeZone: string | null;
  timeZones: TimeZoneOption[];
  isAdmin: boolean;
  adminUrl: string;
};

// id is the token family id; connectedLabel says "Unknown" for families from before the time was kept.
export type ProfileAgent = {
  id: string;
  clientName: string;
  connectedLabel: string;
  lastUsedLabel: string | null;
};

export type ProfileView = {
  identity: ProfileIdentity;
  account: ProfileAccount;
  agents: ProfileAgent[];
};
