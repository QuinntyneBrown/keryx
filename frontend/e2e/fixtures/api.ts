/**
 * The API screen's answers (docs/mocks/pages/api/default.html): five clients with their usage
 * over the last 24 hours, and the whole API's usage. The clock is OVERVIEW_NOW, 9:46 AM in
 * Toronto, so the last calls read as the mock shows them.
 */

export interface ClientUsage {
  requests_24h?: number;
  errors_24h?: number;
  error_rate?: number;
  last_call_at?: string | null;
}

export function apiClient(
  id: string,
  product: string,
  prefix: string,
  scopes: string[],
  revoked = false,
  usage: ClientUsage = {},
): Record<string, unknown> {
  return {
    id,
    product_name: product,
    key_prefix: prefix,
    scopes,
    created_at: '2026-11-20T14:00:00Z',
    revoked_at: revoked ? '2026-11-21T09:00:00Z' : null,
    requests_24h: 0,
    errors_24h: 0,
    error_rate: 0,
    last_call_at: null,
    ...usage,
  };
}

/** The mock's five clients, newest first. */
export const USAGE_CLIENTS = [
  apiClient(
    'cli_mobile',
    'Keryx Mobile (iOS & Android)',
    'kx_live_7Hq2',
    ['articles:read', 'events:read'],
    false,
    {
      requests_24h: 48_210,
      errors_24h: 10,
      error_rate: 0.0002,
      last_call_at: '2026-11-28T14:45:40Z',
    },
  ),
  apiClient(
    'cli_newsletter',
    'GTA Faith & Fitness newsletter',
    'kx_live_c91L',
    ['articles:read'],
    false,
    {
      requests_24h: 96,
      error_rate: 0,
      last_call_at: '2026-11-28T11:15:00Z',
    },
  ),
  apiClient(
    'cli_widget',
    'Kingdom Strong website widget',
    'kx_live_Pz0e',
    ['articles:read'],
    false,
    {
      requests_24h: 3_402,
      errors_24h: 14,
      error_rate: 0.004,
      last_call_at: '2026-11-28T14:44:00Z',
    },
  ),
  apiClient(
    'cli_temple',
    'Temple Fit partner app',
    'kx_live_4mWb',
    ['articles:read', 'sources:read'],
    false,
    {
      requests_24h: 1_118,
      error_rate: 0,
      last_call_at: '2026-11-28T14:35:00Z',
    },
  ),
  apiClient('cli_staging', 'Old staging key', 'kx_test_aa03', ['articles:read'], true, {
    last_call_at: '2026-02-02T15:00:00Z',
  }),
];

/** The whole API's usage as the mock shows it. */
export const USAGE = {
  base_url: 'https://api.keryx.ca/v1',
  requests_24h: 52_826,
  p95_latency_ms: 84,
  error_rate: 0.0005,
};

/** No request in the last 24 hours. */
export const NO_USAGE = { ...USAGE, requests_24h: 0, p95_latency_ms: null, error_rate: 0 };

export const pageOf = (items: unknown[], total = items.length) => ({
  items,
  total,
  page: 1,
  page_size: 50,
});
