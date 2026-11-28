import { View } from 'react-native';
import { joinTestIds } from '../../src/screens/join/testIds';

// TODO: the Join (guest join and live slide number) screen replaces this stub and keeps its root test id.
export default function Join() {
  return <View testID={joinTestIds.root} />;
}
