import { InjectionToken } from '@angular/core';

/** What a key may read (L2-035), in the order "New API key" lists them. */
export type ApiScope = 'articles:read' | 'events:read' | 'sources:read';

/** A client as staff read it: never its key, only the key's first 12 characters (L2-035 AC2). */
export interface ApiClientDto {
  id: string;
  product_name: string;
  key_prefix: string;
  scopes: ApiScope[];
  created_at: string;
  revoked_at: string | null;
}

/** One page of clients, newest first. */
export interface ApiClientPage {
  items: ApiClientDto[];
  total: number;
  page: number;
  page_size: number;
}

export interface CreateApiClientRequest {
  product_name: string;
  scopes: ApiScope[];
}

/** A new client and its full key, which this response alone carries. */
export interface CreatedApiClientDto {
  client: ApiClientDto;
  key: string;
}

/**
 * The products that may call the consumer API (docs/detailed-designs/consumer-api/manage-api-keys).
 * It rejects with an `ApiProblem` whose `code` a screen branches on (`client_name_taken`, `client_not_found`).
 */
export interface ApiClientsApi {
  list(page: number, signal: AbortSignal): Promise<ApiClientPage>;
  create(request: CreateApiClientRequest, signal: AbortSignal): Promise<CreatedApiClientDto>;
  revoke(id: string, signal: AbortSignal): Promise<ApiClientDto>;
}

export const API_CLIENTS_API = new InjectionToken<ApiClientsApi>('Keryx API clients');
