import { ApiError, api } from '@slideworks/core/client';
import type { PresentationStateSummary } from '@slideworks/core/realtime/shapes';
import { useQuery } from '@tanstack/react-query';
import { useEffect, useState } from 'react';
import { ActivityIndicator, StyleSheet, Text, View } from 'react-native';
import { useAppRuntime } from '../../platform/appRuntime';
import { signedInKey } from '../../queries/queryClient';
import { useTheme, type Theme } from '../../theme';
import { deviceLabel, deviceTimeZone } from './deviceLabel';
import { bearerFor, followPresentation, type PresentationStatus } from './presentationConnection';
import { talkTestIds } from './testIds';

// The narrow part of GET /api/decks/{id}/presenter (src/contracts/screens/presenter.json) this screen reads.
type PresenterTitle = { title: string };

// A signed-in person's deck: its title and the current slide number, live over the deck hub with the device bearer.
export function TalkScreen({ deckId }: { deckId: string }) {
  const theme = useTheme();
  const styles = stylesFor(theme);
  const { session, platform } = useAppRuntime();
  const presenter = useQuery({
    queryKey: signedInKey('presenter', deckId),
    queryFn: () => api.get<PresenterTitle>(`/api/decks/${encodeURIComponent(deckId)}/presenter`),
  });
  const [status, setStatus] = useState<PresentationStatus>('connecting');
  const [state, setState] = useState<PresentationStateSummary | null>(null);

  useEffect(() => {
    setState(null);
    return followPresentation(
      {
        deckId,
        clientId: crypto.randomUUID(),
        label: deviceLabel(),
        timeZone: deviceTimeZone(),
      },
      bearerFor(session, () => platform.apiOrigin()),
      { status: setStatus, state: setState },
    );
  }, [deckId, session, platform]);

  const notFound = presenter.error instanceof ApiError && presenter.error.status === 404;
  if (notFound || status === 'refused') {
    return (
      <View testID={talkTestIds.root} style={[styles.screen, styles.centred]}>
        <Text testID={talkTestIds.notYours} style={styles.message}>
          This talk isn&apos;t yours or no longer exists.
        </Text>
      </View>
    );
  }

  const title = presenter.data?.title;
  return (
    <View testID={talkTestIds.root} style={[styles.screen, styles.centred]}>
      {title !== undefined && (
        <Text testID={talkTestIds.title} style={styles.title}>
          {title}
        </Text>
      )}
      {state !== null && status !== 'connecting' ? (
        <Text testID={talkTestIds.slide} style={styles.slide}>
          Slide {state.slide}
        </Text>
      ) : (
        <ActivityIndicator testID={talkTestIds.connecting} color={theme.colors.accent} />
      )}
      {status === 'reconnecting' && (
        <Text testID={talkTestIds.reconnecting} style={styles.message}>
          Reconnecting…
        </Text>
      )}
    </View>
  );
}

function stylesFor({ colors, space }: Theme) {
  return StyleSheet.create({
    screen: { flex: 1, backgroundColor: colors.background, padding: space.lg },
    centred: { alignItems: 'center', justifyContent: 'center', gap: space.md },
    title: { color: colors.text, fontSize: 22, fontWeight: '700', textAlign: 'center' },
    slide: { color: colors.text, fontSize: 48, fontWeight: '800' },
    message: { color: colors.muted, fontSize: 16, textAlign: 'center' },
  });
}
