import type { ReactNode } from 'react';
import { useLocation } from 'react-router-dom';

// Component tests only: shows the router's location next to the app, so a spec
// can assert where a redirect went (MemoryRouter has no address bar).
export function LocationProbe({ children }: { children: ReactNode }) {
  const location = useLocation();
  return (
    <>
      {children}
      <output data-testid="location">{location.pathname + location.search + location.hash}</output>
    </>
  );
}
