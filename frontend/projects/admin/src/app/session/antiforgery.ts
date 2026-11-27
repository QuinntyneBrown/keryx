import { ApiProblem } from '@kx/api';

/**
 * A request token for a state-changing admin call: the API answers it to the signed-in session,
 * and the call sends it back as `X-XSRF-TOKEN` (L2-083).
 */
export async function antiforgeryToken(signal: AbortSignal): Promise<string> {
  const response = await fetch('/admin/v1/auth/antiforgery', {
    credentials: 'include',
    cache: 'no-store',
    signal,
  });
  if (!response.ok) throw new ApiProblem(response.status);
  const body = await response.json().catch(() => null);
  if (typeof body?.request_token !== 'string') throw new ApiProblem(400);
  return body.request_token;
}
