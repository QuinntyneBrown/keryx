import { Injectable } from '@angular/core';
import {
  type ApiClientDto,
  type ApiClientPage,
  type ApiClientsApi,
  type CreateApiClientRequest,
  type CreatedApiClientDto,
} from '@kx/api';
import { antiforgeryToken } from '../session/antiforgery';
import { readJson } from '../session/read-json';

/** The products that may call the consumer API, over fetch with the staff session's cookie (ADR 0005). */
@Injectable()
export class HttpApiClientsApi implements ApiClientsApi {
  list(page: number, signal: AbortSignal): Promise<ApiClientPage> {
    return readJson<ApiClientPage>(`/admin/v1/api-clients?page=${page}&page_size=50`, signal);
  }

  async create(request: CreateApiClientRequest, signal: AbortSignal): Promise<CreatedApiClientDto> {
    return readJson<CreatedApiClientDto>('/admin/v1/api-clients', signal, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'X-XSRF-TOKEN': await antiforgeryToken(signal),
      },
      body: JSON.stringify(request),
    });
  }

  async revoke(id: string, signal: AbortSignal): Promise<ApiClientDto> {
    const revoked = await readJson<{ client: ApiClientDto }>(
      `/admin/v1/api-clients/${encodeURIComponent(id)}/revoke`,
      signal,
      { method: 'POST', headers: { 'X-XSRF-TOKEN': await antiforgeryToken(signal) } },
    );
    return revoked.client;
  }
}
