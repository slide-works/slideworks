import { useLocation, useParams } from 'react-router-dom';

// /card/:id and /card/generic: the 1200×630 share card the server screenshots
// (SW-2 L9). Outside the sign-in gate and with no realtime subscription. A
// deck's card needs the render token, which comes only from the fragment
// (#token=…); the generic card needs no deck and no token.
//
// TODO: a shell; the share card (GET /api/render/{id}/card with the
// X-Render-Token header, the generic card, data-ready once fonts and images
// load) replaces it.
export function CardPage() {
  const { id } = useParams();
  const token = new URLSearchParams(useLocation().hash.slice(1)).get('token');
  const generic = id === undefined;
  return (
    <div
      data-testid="share-card"
      data-deck-id={id}
      data-generic={generic ? 'true' : 'false'}
      data-error={generic || token ? undefined : 'Render token missing'}
      style={{ width: 1200, height: 630 }}
    />
  );
}
