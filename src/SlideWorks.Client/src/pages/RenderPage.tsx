import { useLocation, useParams } from 'react-router-dom';

// /render/:id/:n: one slide at 1920×1080 with every step revealed and no player
// UI, for an agent to screenshot (SW-2 L9, D21). Outside the sign-in gate and
// with no realtime subscription. The render token comes only from the fragment
// (#token=…), never the query string, so it never reaches the server in a URL.
//
// TODO: a shell; the render page (session cookie, GET /api/render/{id} with the
// X-Render-Token header, the slide, data-ready once fonts and images load) replaces it.
export function RenderPage() {
  const { id, n } = useParams();
  const token = new URLSearchParams(useLocation().hash.slice(1)).get('token');
  return (
    <div
      data-testid="render-slide"
      data-deck-id={id}
      data-slide={n}
      data-error={token ? undefined : 'Render token missing'}
      style={{ width: 1920, height: 1080 }}
    />
  );
}
