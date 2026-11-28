import { ApiProblem } from '@kx/api';

/**
 * Sends one request to the admin API with the staff session's cookie and reads its JSON. A
 * refusal rejects with its `ApiProblem`; a request that never reached the API rejects with
 * status 0, unless it was cancelled, which rejects with the abort.
 */
export async function readJson<T>(
  url: string,
  signal: AbortSignal,
  init: RequestInit = {},
): Promise<T> {
  let response: Response;
  try {
    response = await fetch(url, { ...init, credentials: 'include', cache: 'no-store', signal });
  } catch (error) {
    if (signal.aborted) throw error;
    throw new ApiProblem(0);
  }
  if (!response.ok) throw await problemOf(response);
  return (await response.json()) as T;
}

/** The ProblemDetails of a refusal, or just its status when the body is not one. */
export async function problemOf(response: Response): Promise<ApiProblem> {
  const body = await response.json().catch(() => null);
  const code = typeof body?.code === 'string' ? body.code : undefined;
  const reference = typeof body?.trace_id === 'string' ? body.trace_id : undefined;
  const errors = body?.errors && typeof body.errors === 'object' ? body.errors : undefined;
  const runId = typeof body?.run_id === 'string' ? body.run_id : undefined;
  return new ApiProblem(response.status, code, reference, errors, runId);
}
