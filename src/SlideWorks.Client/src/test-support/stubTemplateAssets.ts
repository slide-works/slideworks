import type { Page, Route } from 'playwright/test';
import type { TemplateAsset } from '../templates/types';

type Answer = { status: number; json?: unknown } | 'network-down';

export type AssetStubOptions = {
  // How each file's PUT is answered, by file name; a file it names nothing for is stored.
  put?: (fileName: string) => Answer | undefined;
  // How the start of an upload is answered, by file name.
  start?: (fileName: string) => Answer | undefined;
  rights?: Answer;
};

const IMAGE_BODY =
  '<svg xmlns="http://www.w3.org/2000/svg" width="120" height="32"><rect width="120" height="32" fill="#2f5bea"/></svg>';

const contentTypeOf = (fileName: string) =>
  fileName.endsWith('.woff2')
    ? 'font/woff2'
    : fileName.endsWith('.svg')
      ? 'image/svg+xml'
      : fileName.endsWith('.webp')
        ? 'image/webp'
        : 'image/png';

// The asset the stub stores for a started upload, as the view lists it afterwards.
export function storedAsset(
  templateId: string,
  assetId: string,
  kind: TemplateAsset['kind'],
  fileName: string,
  rightsConfirmed = false,
): TemplateAsset {
  return {
    assetId,
    kind,
    fileName,
    contentType: contentTypeOf(fileName),
    bytes: 3,
    meta: kind === 'font' ? { avgCharEm: 0.56 } : {},
    rightsConfirmed,
    url: `/api/templates/${templateId}/assets/${assetId}`,
  };
}

// Answers the template file routes at the network boundary: start an upload, the PUT of the bytes,
// confirming font rights, and serving a stored file (a small image). The returned log is the one
// ordered record of what the page asked: `start <kind> <file> rights=<bool>`, `put <file> <bytes>
// <cookie?>`, `rights`. Register it after stubTemplatesApi.
export async function stubTemplateAssets(
  page: Page,
  templateId: string,
  options: AssetStubOptions = {},
): Promise<string[]> {
  const log: string[] = [];
  const started = new Map<string, { kind: TemplateAsset['kind']; fileName: string }>();
  const respond = (route: Route, answer: Answer) =>
    answer === 'network-down'
      ? route.abort('connectionrefused')
      : answer.json === undefined
        ? route.fulfill({ status: answer.status })
        : route.fulfill({ status: answer.status, json: answer.json });

  await page.route(`**/api/templates/${templateId}/assets/*`, (route) => {
    if (route.request().method() !== 'GET') return route.fallback();
    return route.fulfill({ contentType: 'image/svg+xml', body: IMAGE_BODY });
  });
  await page.route(`**/api/templates/${templateId}/assets/font-rights`, (route) => {
    log.push('rights');
    return respond(route, options.rights ?? { status: 200, json: { confirmed: 1 } });
  });
  await page.route(`**/api/templates/${templateId}/assets`, (route) => {
    const body = route.request().postDataJSON() as {
      kind: TemplateAsset['kind'];
      fileName: string;
      rightsConfirmed: boolean;
    };
    log.push(`start ${body.kind} ${body.fileName} rights=${body.rightsConfirmed}`);
    const answer = options.start?.(body.fileName);
    if (answer !== undefined) return respond(route, answer);
    const token = `tok${started.size}`;
    started.set(token, { kind: body.kind, fileName: body.fileName });
    return route.fulfill({
      status: 201,
      json: {
        uploadUrl: `http://localhost/api/template-uploads/${token}`,
        expiresLabel: 'in 10 minutes',
      },
    });
  });
  await page.route('**/api/template-uploads/*', async (route) => {
    const request = route.request();
    const token = new URL(request.url()).pathname.split('/').pop() ?? '';
    const upload = started.get(token);
    const cookie = (await request.allHeaders()).cookie ?? 'no-cookie';
    log.push(
      `${request.method().toLowerCase()} ${upload?.fileName ?? 'unknown'} ${request.postDataBuffer()?.length ?? 0} ${cookie}`,
    );
    if (upload === undefined)
      return respond(route, { status: 404, json: { error: 'Upload not found' } });
    const answer = options.put?.(upload.fileName);
    if (answer !== undefined) return respond(route, answer);
    return route.fulfill({
      status: 200,
      json: {
        assetId: `new-${token}`,
        kind: upload.kind,
        fileName: upload.fileName,
        contentType: contentTypeOf(upload.fileName),
        bytes: 3,
        meta: upload.kind === 'font' ? { avgCharEm: 0.56 } : {},
      },
    });
  });
  return log;
}
