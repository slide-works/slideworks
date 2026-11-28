import { Stack } from 'expo-router';

// The signed-in routes. The root layout gates this group on the device session; Account opens as a sheet.
export default function SignedInLayout() {
  return (
    <Stack>
      <Stack.Screen name="talks" />
      <Stack.Screen name="talk/[id]" />
      <Stack.Screen name="account" options={{ presentation: 'formSheet' }} />
    </Stack>
  );
}
