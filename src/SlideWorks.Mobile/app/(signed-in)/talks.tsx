import { View } from 'react-native';
import { talksTestIds } from '../../src/screens/talks/testIds';

// TODO: the Talks (the signed-in person's decks) screen replaces this stub and keeps its root test id.
export default function Talks() {
  return <View testID={talksTestIds.root} />;
}
