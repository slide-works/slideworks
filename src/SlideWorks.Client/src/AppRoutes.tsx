import { Navigate, useRoutes, type RouteObject } from 'react-router-dom';
import { App } from './App';
import { ConsentPage } from './pages/ConsentPage';
import { DeckPage } from './pages/DeckPage';
import { DecksPage } from './pages/DecksPage';
import { PlayerPage } from './pages/PlayerPage';
import { SignInPage } from './pages/SignInPage';

// K12. The player and sign-in sit outside the auth gate (App); everything else
// is for a signed-in person.
const routes: RouteObject[] = [
  { path: '/signin', element: <SignInPage /> },
  { path: '/d/:id/:slug', element: <PlayerPage /> },
  { path: '/d/:id', element: <PlayerPage /> },
  {
    element: <App />,
    children: [
      { index: true, element: <Navigate to="/decks" replace /> },
      { path: 'decks', element: <DecksPage /> },
      { path: 'decks/:id', element: <DeckPage /> },
      { path: 'oauth/consent', element: <ConsentPage /> },
      { path: '*', element: <Navigate to="/decks" replace /> },
    ],
  },
];

// The full route table, separate from main.tsx so page-level component tests
// can mount the exact routing (and App shell) the real app uses.
export function AppRoutes() {
  return useRoutes(routes);
}
