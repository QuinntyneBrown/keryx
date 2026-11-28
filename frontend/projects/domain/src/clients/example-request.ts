/** The placeholder a consumer replaces with its key; a real key never appears in an example. */
export const KEY_PLACEHOLDER = 'kx_live_…';

/**
 * The `curl` line of an endpoint's example (L2-067 AC4): the request after the base address,
 * then the authorization header with the placeholder key. Before the base address is known (the
 * usage did not load), the request is written from the root, as `/v1/…`.
 */
export function exampleRequest(baseUrl: string | null, request: string): string {
  const base = (baseUrl ?? '/v1').replace(/\/+$/, '');
  return `curl ${base}/${request} \\\n  -H "Authorization: Bearer ${KEY_PLACEHOLDER}"`;
}
