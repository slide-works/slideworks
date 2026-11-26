import { Navigate, Outlet, useRoutes, type RouteObject } from 'react-router-dom';
import { App } from './App';
import { GuestRoute } from './guest/GuestRoute';
import { JoinCodePage } from './guest/JoinCodePage';
import { AdminUsersPage } from './pages/AdminUsersPage';
import { CardPage } from './pages/CardPage';
import { ConsentPage } from './pages/ConsentPage';
import { DeckPage } from './pages/DeckPage';
import { DecksPage } from './pages/DecksPage';
import { PendingPage } from './pages/PendingPage';
import { PlayerPage } from './pages/PlayerPage';
import { PresenterPage } from './pages/PresenterPage';
import { ProfilePage } from './pages/ProfilePage';
import { RemotePickPage } from './pages/RemotePickPage';
import { PrintPage } from './pages/PrintPage';
import { RenderPage } from './pages/RenderPage';
import { SignInPage } from './pages/SignInPage';
import { SpeakerPage } from './pages/SpeakerPage';
import { UnsubscribePage } from './pages/UnsubscribePage';
import { SignedInGate } from './SignedInGate';
import { TemplateEditor } from './templates/editor/TemplateEditor';
import { LayoutReviewPage } from './templates/LayoutReviewPage';
import { TemplatePublicPage } from './templates/TemplatePublicPage';
import { TemplatesPage } from './templates/TemplatesPage';

// K12. The player, sign-in and pending pages sit outside the auth gate (App); everything else
// is for a signed-in person. So do the render and share-card pages (SW-2 L9), which a
// headless browser opens with a render token instead of a sign-in. The presenter window and the phone's pages (P8)
// are for a signed-in person too, but fill the screen, so they sit behind the same gate without the Shell.
const routes: RouteObject[] = [
  { path: '/signin', element: <SignInPage /> },
  { path: '/pending', element: <PendingPage /> },
  // Where a share email's unsubscribe link leads: open without a sign-in, the token in the fragment is the credential.
  { path: '/unsubscribe', element: <UnsubscribePage /> },
  { path: '/d/:id/:slug', element: <PlayerPage /> },
  { path: '/d/:id', element: <PlayerPage /> },
  // The guest pages (SW-5 J5): anyone with a join code, no sign-in. /j/{code} is what both hosts serve; the bare /{code}
  // is for the join host. A segment that is not six digits is not a code.
  { path: '/join', element: <JoinCodePage /> },
  { path: '/j/:code', element: <GuestRoute /> },
  { path: '/:code', element: <GuestRoute /> },
  { path: '/render/:id/:n', element: <RenderPage /> },
  { path: '/print/:id', element: <PrintPage /> },
  { path: '/card/generic', element: <CardPage /> },
  { path: '/card/:id', element: <CardPage /> },
  // A template's page, where its share link leads: open without a sign-in, as the player is.
  { path: '/t/:id', element: <TemplatePublicPage /> },
  {
    element: <SignedInGate>{(me) => <Outlet context={me} />}</SignedInGate>,
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
      { path: 'templates', element: <TemplatesPage /> },
      { path: 'templates/:id/edit', element: <TemplateEditor /> },
      { path: 'templates/:id/layouts/:name', element: <LayoutReviewPage /> },
      { path: 'profile', element: <ProfilePage /> },
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
