import { useSearchParams } from 'react-router-dom';

// /oauth/consent?request=<id>: a signed-in presenter allows or denies an agent.
// TODO: shell; the OAuth consent page replaces it.
export function ConsentPage() {
  const [params] = useSearchParams();
  return <section data-testid="consent-page" data-request={params.get('request') ?? ''} />;
}
