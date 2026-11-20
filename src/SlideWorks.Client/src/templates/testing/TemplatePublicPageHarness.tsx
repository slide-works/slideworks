import { Route, Routes } from 'react-router-dom';
import { LocationProbe } from '../../test-support/LocationProbe';
import { TemplatePublicPage } from '../TemplatePublicPage';

// Component tests only: the page on its route, with the router's location shown beside it.
export function TemplatePublicPageHarness() {
  return (
    <LocationProbe>
      <Routes>
        <Route path="/t/:id" element={<TemplatePublicPage />} />
        <Route path="*" element={<p>elsewhere</p>} />
      </Routes>
    </LocationProbe>
  );
}
