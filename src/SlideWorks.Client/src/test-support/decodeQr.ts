import type { Locator, Page } from 'playwright/test';
import jsQR from 'jsqr';

// Screenshots the QR code and reads it back the way a phone would; null if nothing decodes. The PNG becomes RGBA in the
// page (a canvas), so no PNG decoder is needed here. jsqr is a dev dependency, used only by tests.
export async function decodeQr(page: Page, qr: Locator): Promise<string | null> {
  const png = (await qr.screenshot()).toString('base64');
  const { width, height, pixels } = await page.evaluate(async (data) => {
    const bitmap = await createImageBitmap(
      await (await fetch(`data:image/png;base64,${data}`)).blob(),
    );
    const canvas = document.createElement('canvas');
    canvas.width = bitmap.width;
    canvas.height = bitmap.height;
    const context = canvas.getContext('2d')!;
    context.drawImage(bitmap, 0, 0);
    const image = context.getImageData(0, 0, bitmap.width, bitmap.height);
    return { width: image.width, height: image.height, pixels: Array.from(image.data) };
  }, png);
  return jsQR(Uint8ClampedArray.from(pixels), width, height)?.data ?? null;
}
