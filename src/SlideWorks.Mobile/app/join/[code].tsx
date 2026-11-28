import { useLocalSearchParams } from 'expo-router';
import { JoinScreen } from '../../src/screens/join/JoinScreen';

export default function Join() {
  const { code, server } = useLocalSearchParams<{ code: string; server?: string }>();
  return <JoinScreen code={code} server={server} />;
}
