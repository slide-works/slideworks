import { TEMPLATES_TEST_IDS } from './testIds';

// TODO: a placeholder. /templates/:id/layouts/:name, where the owner reviews and approves a
// custom layout, replaces it.
export function LayoutReviewPage() {
  return (
    <section data-testid={TEMPLATES_TEST_IDS.layoutReviewPage}>
      <h1 className="text-lg font-bold tracking-tight text-ink">Review layout</h1>
    </section>
  );
}
