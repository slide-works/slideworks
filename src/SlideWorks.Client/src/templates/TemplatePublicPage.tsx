import { TEMPLATES_TEST_IDS } from './testIds';

// TODO: a placeholder. /t/:id, the page a template's share link opens (its look, its purpose and
// "Use this template", which goes to /decks?template=<id>), replaces it.
export function TemplatePublicPage() {
  return (
    <section data-testid={TEMPLATES_TEST_IDS.publicPage}>
      <h1 className="text-lg font-bold tracking-tight text-ink">Template</h1>
    </section>
  );
}
