import { View } from 'react-native';
import { homeTestIds } from '../src/screens/home/testIds';

// TODO: the Home (Join a talk, Sign in) screen replaces this stub and keeps its root test id.
export default function Home() {
  return <View testID={homeTestIds.root} />;
}
