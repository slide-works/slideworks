// Test-only: mounts the review page with a stand-in measurement, since a component test cannot
// hand a function that answers at once across the browser boundary.
import { Route, Routes } from 'react-router-dom';
import { LayoutReviewPage } from './LayoutReviewPage';

export function StandInReview({ overflow, contrast }: { overflow: string[]; contrast: string[] }) {
  return (
    <Routes>
      <Route
        path="/templates/:id/layouts/:name"
        element={
          <LayoutReviewPage measureOverflow={() => overflow} measureContrast={() => contrast} />
        }
      />
    </Routes>
  );
}
