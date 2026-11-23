import { useEffect, useState } from 'react';
import { api } from '../api/client';
import type { ProfileView } from '../api/profile';
import { ProfilePage } from '../pages/ProfilePage';

// Component tests only: the profile page with a button that re-reads the profile, for the later tasks' tests that
// check a write shows up in what the server holds. Shows the first read's agent count ("none" before).
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
