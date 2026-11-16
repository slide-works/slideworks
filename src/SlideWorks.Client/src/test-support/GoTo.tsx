import { useState, type ReactNode } from 'react';
import { useNavigate } from 'react-router-dom';

// Component tests only: an in-app navigation to any path, as a link click
// would do, so a spec can move a mounted page to another route (MemoryRouter
// has no address bar). Fill goto-path, then click goto.
export function GoTo({ children }: { children: ReactNode }) {
  const navigate = useNavigate();
  const [path, setPath] = useState('');
  return (
    <>
      {children}
      <input
        data-testid="goto-path"
        aria-label="Path"
        value={path}
        onChange={(event) => setPath(event.target.value)}
      />
      <button type="button" data-testid="goto" onClick={() => void navigate(path)}>
        Go
      </button>
    </>
  );
}
