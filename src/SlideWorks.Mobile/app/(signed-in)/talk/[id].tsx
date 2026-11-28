import { View } from 'react-native';
import { talkTestIds } from '../../../src/screens/talk/testIds';

// TODO: the Talk (deck title and live slide number) screen replaces this stub and keeps its root test id.
export default function Talk() {
  return <View testID={talkTestIds.root} />;
}
