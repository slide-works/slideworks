import { redirectToSignIn } from './client';

export const DOWNLOAD_FALLBACK_ERROR = "Couldn't make the PDF. Try again.";

// Fetches a binary file from the API with the session cookie and saves it as `filename` through an object URL and a
// temporary link. A refusal's { error } text is thrown as is; a network failure or a body without `error` throws the
// client fallback. A 401 sends the person to sign in, as every other API call does.
export async function downloadFile(path: string, filename: string): Promise<void> {
  let response: Response;
  try {
    response = await fetch(path, { method: 'GET', credentials: 'same-origin' });
  } catch {
    throw new Error(DOWNLOAD_FALLBACK_ERROR);
  }
  if (!response.ok) {
    if (response.status === 401) redirectToSignIn(window.location);
    throw new Error(await errorText(response));
  }
  const url = URL.createObjectURL(await response.blob());
  const link = document.createElement('a');
  link.href = url;
  link.download = filename;
  document.body.appendChild(link);
  link.click();
  link.remove();
  // Firefox and Safari can cancel the download if the URL is revoked in the same tick as the click.
  setTimeout(() => URL.revokeObjectURL(url), 0);
}

async function errorText(response: Response): Promise<string> {
  try {
    const body: unknown = await response.json();
    const error = (body as { error?: unknown } | null)?.error;
    return typeof error === 'string' && error !== '' ? error : DOWNLOAD_FALLBACK_ERROR;
  } catch {
    return DOWNLOAD_FALLBACK_ERROR;
  }
}
