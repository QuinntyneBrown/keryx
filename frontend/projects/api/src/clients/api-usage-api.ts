import { InjectionToken } from '@angular/core';

/** The whole consumer API's usage over the last 24 hours (L2-037 AC3); every figure a number. */
export interface ApiUsageDto {
  /** The consumer API's public address, ending in /v1. */
  base_url: string;
  requests_24h: number;
  /** Null when there were no requests. */
  p95_latency_ms: number | null;
  /** Errors over requests, from 0 to 1; 0 with no requests. */
  error_rate: number;
}

/** The API screen's usage figures (docs/detailed-designs/consumer-api/report-api-usage). */
export interface ApiUsageApi {
  get(signal: AbortSignal): Promise<ApiUsageDto>;
}

export const API_USAGE_API = new InjectionToken<ApiUsageApi>('Keryx API usage');
