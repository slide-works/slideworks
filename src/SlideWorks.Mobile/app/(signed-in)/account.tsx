import { View } from 'react-native';
import { accountTestIds } from '../../src/screens/account/testIds';

// TODO: the Account (name, email, server, version, Sign out) screen replaces this stub and keeps its root test id.
export default function Account() {
  return <View testID={accountTestIds.root} />;
}
