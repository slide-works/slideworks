import { View } from 'react-native';
import { signInTestIds } from '../src/screens/signIn/testIds';

// TODO: the Sign in (server address, server check, Sign in with Google) screen replaces this stub and keeps its root test id.
export default function SignIn() {
  return <View testID={signInTestIds.root} />;
}
