import { useLocalSearchParams } from 'expo-router';
import { TalkScreen } from '../../../src/screens/talk/TalkScreen';

export default function Talk() {
  const { id } = useLocalSearchParams<{ id: string }>();
  return <TalkScreen deckId={id} />;
}
