import { vi } from 'vitest';
import samplePngDataUrl from './fixtures/sample-image.png?inline';
import { SAMPLE_IMAGE_ID } from './fixtures/layoutSlots';

function samplePng(): Response {
  const bytes = Uint8Array.from(atob(samplePngDataUrl.split(',')[1]), (c) => c.charCodeAt(0));
  return new Response(bytes, { status: 200, headers: { 'Content-Type': 'image/png' } });
}

// Fakes the network at fetch itself, so the client under test is the real one.
// Every call answers with `status` and `body`, except the sample image, which
// /api/images/img_sample0001 always serves.
export function stubFetch(status: number, body?: unknown) {
  const fetchMock = vi.fn();
  fetchMock.mockImplementation(async (input: RequestInfo | URL) =>
    String(input).endsWith(`/api/images/${SAMPLE_IMAGE_ID}`)
      ? samplePng()
      : new Response(body === undefined ? null : JSON.stringify(body), { status }),
  );
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}
