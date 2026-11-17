import { useNavigationType } from 'react-router-dom';

// Component tests only: shows how the router's last navigation happened (POP, PUSH or REPLACE), since MemoryRouter
// has no back button to prove a replace with.
export function NavigationTypeProbe() {
  return <output data-testid="navigation-type">{useNavigationType()}</output>;
}
