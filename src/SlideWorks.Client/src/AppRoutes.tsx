import { useRoutes, type RouteObject } from 'react-router-dom';
import { App } from './App';
import { LoginPage } from './pages/LoginPage';
import { NotesPage } from './pages/NotesPage';

const routes: RouteObject[] = [
  { path: '/login', element: <LoginPage /> },
  { element: <App />, children: [{ index: true, element: <NotesPage /> }] },
];

// The full route table, separate from main.tsx so page-level component tests
// can mount the exact routing (and App shell) the real app uses.
export function AppRoutes() {
  return useRoutes(routes);
}
