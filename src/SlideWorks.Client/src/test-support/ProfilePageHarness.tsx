import { useEffect, useState } from 'react';
import { api } from '../api/client';
import type { ProfileView } from '../api/profile';
import { ProfilePage } from '../pages/ProfilePage';

// Component tests only, and no spec mounts it yet: the profile page beside an output that shows the first read's agent
// count ("none" before it arrives). A spec that wants to check a write shows up in what the server holds can extend it.
export function ProfilePageHarness() {
  const [count, setCount] = useState('none');
  useEffect(() => {
    void api
      .get<ProfileView>('/api/me/profile')
      .then((profile) => setCount(String(profile.agents.length)));
  }, []);
  return (
    <>
      <output data-testid="harness-agent-count">{count}</output>
      <ProfilePage />
    </>
  );
}
