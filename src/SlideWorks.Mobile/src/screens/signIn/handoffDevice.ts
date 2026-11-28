import * as Crypto from 'expo-crypto';
import * as Device from 'expo-device';
import { deviceNameFrom } from '../../signIn/handoff';

// What the handoff needs from the phone: random bytes, SHA-256 and the device's name.

export const randomBytes = (count: number): Uint8Array => Crypto.getRandomBytes(count);

export const sha256Base64 = (data: string): Promise<string> =>
  Crypto.digestStringAsync(Crypto.CryptoDigestAlgorithm.SHA256, data, {
    encoding: Crypto.CryptoEncoding.BASE64,
  });

export const deviceName = (): string =>
  deviceNameFrom({ deviceName: Device.deviceName, modelName: Device.modelName });
