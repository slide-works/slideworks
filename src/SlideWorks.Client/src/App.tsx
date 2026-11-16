import { useEffect } from 'react';
import { Navigate, useNavigate } from 'react-router-dom';
import { getToken, setOnUnauthorized } from './api/client';
import { Shell } from './Shell';

// Auth gate around the shell: no stored token (or any 401 from the API) sends
// the user to the token screen.
export function App() {
  const navigate = useNavigate();

  useEffect(() => {
    setOnUnauthorized(() => navigate('/login'));
  }, [navigate]);

  if (!getToken()) {
    return <Navigate to="/login" replace />;
  }

  return <Shell />;
}
