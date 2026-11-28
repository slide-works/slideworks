import { useMemo } from 'react';
import { AudienceOpenResponsesContext } from '../openResponses/useOpenResponse';
import { AudienceQuestionsContext } from '../qa/guest/AudienceQuestionsContext';
import { AudiencePollsContext } from './AudiencePollsContext';
import { GuestErrorBoundary } from './GuestErrorBoundary';
import { GuestScreens } from './GuestScreens';
import { GuestLanguageContext } from './language/GuestLanguageContext';
import { initialLanguage, useGuestLanguage } from './language/useGuestLanguage';
import type { GuestView } from './guestView';
import { useAudience } from './useAudience';
import type { Retry } from './useRetry';

function Screens({ code, view: initial, onRetry }: Props) {
  // Where the phone starts: a pick kept for this session, else its own language when the deck has it (D3).
  const start = useMemo(() => initialLanguage(code, initial.translations), [code, initial]);
  const { view, state, status, polls, questions, openResponses, setLanguage } = useAudience(
    code,
    initial,
    undefined,
    start?.code ?? null,
  );
  const language = useGuestLanguage(code, view.translations, state.language, start, setLanguage);
  return (
    <GuestLanguageContext value={state.language?.code ?? null}>
      <AudiencePollsContext value={polls}>
        <AudienceQuestionsContext value={questions}>
          <AudienceOpenResponsesContext value={openResponses}>
            <GuestScreens
              view={view}
              state={state}
              status={status}
              onRetry={onRetry}
              language={language}
            />
          </AudienceOpenResponsesContext>
        </AudienceQuestionsContext>
      </AudiencePollsContext>
    </GuestLanguageContext>
  );
}

type Props = { code: string; view: GuestView; onRetry: Retry };

// A loaded guest view with its realtime state. The hook starts from the view the GET returned and hands back the view to
// show, so a re-read inside it reaches the screens. An unexpected failure in the hook lands on the boundary.
export function GuestLive(props: Props) {
  return (
    <GuestErrorBoundary onRetry={props.onRetry}>
      <Screens {...props} />
    </GuestErrorBoundary>
  );
}
