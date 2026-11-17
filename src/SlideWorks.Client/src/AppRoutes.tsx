import { Navigate, Outlet, useRoutes, type RouteObject } from 'react-router-dom';
import { App } from './App';
import { AdminUsersPage } from './pages/AdminUsersPage';
import { CardPage } from './pages/CardPage';
import { ConsentPage } from './pages/ConsentPage';
import { DeckPage } from './pages/DeckPage';
import { DecksPage } from './pages/DecksPage';
import { PendingPage } from './pages/PendingPage';
import { PlayerPage } from './pages/PlayerPage';
import { PresenterPage } from './pages/PresenterPage';
import { RemotePickPage } from './pages/RemotePickPage';
import { RenderPage } from './pages/RenderPage';
import { SignInPage } from './pages/SignInPage';
import { SpeakerPage } from './pages/SpeakerPage';
import { SignedInGate } from './SignedInGate';

// K12. The player, sign-in and pending pages sit outside the auth gate (App); everything else
// is for a signed-in person. So do the render and share-card pages (SW-2 L9), which a
// headless browser opens with a render token instead of a sign-in. The presenter window and the phone's pages (P8)
// are for a signed-in person too, but fill the screen, so they sit behind the same gate without the Shell.
const routes: RouteObject[] = [
  { path: '/signin', element: <SignInPage /> },
  { path: '/pending', element: <PendingPage /> },
  { path: '/d/:id/:slug', element: <PlayerPage /> },
  { path: '/d/:id', element: <PlayerPage /> },
  { path: '/render/:id/:n', element: <RenderPage /> },
  { path: '/card/generic', element: <CardPage /> },
  { path: '/card/:id', element: <CardPage /> },
  {
    element: <SignedInGate>{() => <Outlet />}</SignedInGate>,
    children: [
      { path: 'decks/:id/present', element: <PresenterPage /> },
      { path: 'remote', element: <RemotePickPage /> },
      { path: 'remote/:id', element: <SpeakerPage /> },
    ],
  },
  {
    element: <App />,
    children: [
      { index: true, element: <Navigate to="/decks" replace /> },
      { path: 'decks', element: <DecksPage /> },
      { path: 'decks/:id', element: <DeckPage /> },
      { path: 'admin/users', element: <AdminUsersPage /> },
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
