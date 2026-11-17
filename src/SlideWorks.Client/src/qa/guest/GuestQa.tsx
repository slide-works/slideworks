import { useState, type ReactNode } from 'react';
import type { AudienceState } from '../../guest/audienceEvents';
import type { AudienceStatus } from '../../guest/useAudience';
import { GuestTabs, type GuestTab } from './GuestTabs';
import { QuestionsLive } from './QuestionsLive';
import { ReactionsLive } from './ReactionsLive';

export type GuestQaProps = {
  code: string;
  state: AudienceState;
  status: AudienceStatus;
  presenter: string;
  // SW-5's following page under the header: the Slide tab.
  children: ReactNode;
};

// The guest page's Q&A and reactions (Q8): holds the tab showing, gives the parts the phone's questions and reactions,
// and lays the tabs over SW-5's following page. GuestFollowing renders it around the slide, progress and activity.
export function GuestQa({ code, state, status, presenter, children }: GuestQaProps) {
  const [tab, setTab] = useState<GuestTab>('slide');
  const room = state.room;
  const shown = room !== null && room.questions !== 'off' ? tab : 'slide';
  return (
    <QuestionsLive code={code} room={room} status={status} showing={shown === 'questions'}>
      <ReactionsLive code={code} room={room} status={status}>
        <GuestTabs
          tab={shown}
          onTab={setTab}
          activity={state.activity}
          room={room}
          presenter={presenter}
        >
          {children}
        </GuestTabs>
      </ReactionsLive>
    </QuestionsLive>
  );
}
