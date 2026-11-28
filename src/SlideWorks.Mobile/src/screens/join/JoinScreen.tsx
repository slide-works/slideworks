import { ActivityIndicator, StyleSheet, Text, View } from 'react-native';
import { HOSTED_SERVER } from '../../links';
import { useTheme, type Theme } from '../../theme';
import { NO_SESSION_LEAD, REFUSAL_ADVICE, spacedCode } from './refusals';
import { joinTestIds } from './testIds';
import { useGuestTalk } from './useGuestTalk';

// Join: joins the talk with that code as a guest of server (the hosted one unless a link names another) and shows its
// title and live slide number. Signed in or not, the guest id is what proves who joined.
export function JoinScreen({ code, server }: { code: string; server?: string }) {
  const theme = useTheme();
  const styles = stylesFor(theme);
  const talk = useGuestTalk(server || HOSTED_SERVER, code);

  if (talk.status === 'failed') throw talk.error;

  return (
    <View testID={joinTestIds.root} style={styles.screen}>
      {talk.status === 'joining' && (
        <ActivityIndicator testID={joinTestIds.joining} color={theme.colors.accent} />
      )}
      {talk.status === 'refused' &&
        (talk.refusal === 'no_session' ? (
          <Text testID={joinTestIds.noSession} style={styles.refusal}>
            {NO_SESSION_LEAD}
            <Text style={styles.strong}>{spacedCode(code)}</Text>. {REFUSAL_ADVICE.no_session}
          </Text>
        ) : (
          <Text testID={joinTestIds.tooManyAttempts} style={styles.refusal}>
            {REFUSAL_ADVICE.too_many_attempts}
          </Text>
        ))}
      {talk.status === 'joined' && (
        <>
          <Text testID={joinTestIds.title} style={styles.title}>
            {talk.view.deckTitle}
          </Text>
          {talk.state.phase === 'lobby' && (
            <Text testID={joinTestIds.lobby} style={styles.body}>
              Waiting for the talk to start.
            </Text>
          )}
          {talk.state.phase === 'live' && talk.state.current && (
            <Text testID={joinTestIds.slide} style={styles.slide}>
              Slide {talk.state.current.index} of {talk.state.current.count}
            </Text>
          )}
          {talk.state.phase === 'ended' && (
            <Text testID={joinTestIds.ended} style={styles.body}>
              The talk has ended.
            </Text>
          )}
          {talk.offline && talk.state.phase !== 'ended' && (
            <Text testID={joinTestIds.offline} style={styles.muted}>
              Reconnecting…
            </Text>
          )}
        </>
      )}
    </View>
  );
}

function stylesFor({ colors, space }: Theme) {
  return StyleSheet.create({
    screen: { flex: 1, padding: space.lg, gap: space.md, backgroundColor: colors.background },
    title: { fontSize: 24, fontWeight: '700', color: colors.text },
    slide: { fontSize: 20, fontWeight: '600', color: colors.text },
    body: { fontSize: 16, color: colors.text },
    muted: { color: colors.muted },
    refusal: { fontSize: 16, color: colors.bad },
    strong: { fontWeight: '700' },
  });
}
