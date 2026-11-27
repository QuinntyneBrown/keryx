import { Injectable } from '@angular/core';
import {
  ApiProblem,
  type ApiClientDto,
  type ApiClientPage,
  type ApiClientsApi,
  type CreateApiClientRequest,
  type CreatedApiClientDto,
} from '@kx/api';
import { antiforgeryToken } from '../session/antiforgery';
import { problemOf } from '../sources/http-sources-api';

/** The products that may call the consumer API, over fetch with the staff session's cookie (ADR 0005). */
@Injectable()
export class HttpApiClientsApi implements ApiClientsApi {
  list(page: number, signal: AbortSignal): Promise<ApiClientPage> {
    return this.send<ApiClientPage>(`/admin/v1/api-clients?page=${page}&page_size=50`, signal);
  }

  async create(request: CreateApiClientRequest, signal: AbortSignal): Promise<CreatedApiClientDto> {
    return this.send<CreatedApiClientDto>('/admin/v1/api-clients', signal, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'X-XSRF-TOKEN': await antiforgeryToken(signal),
      },
      body: JSON.stringify(request),
    });
  }

  async revoke(id: string, signal: AbortSignal): Promise<ApiClientDto> {
    const revoked = await this.send<{ client: ApiClientDto }>(
      `/admin/v1/api-clients/${encodeURIComponent(id)}/revoke`,
      signal,
      { method: 'POST', headers: { 'X-XSRF-TOKEN': await antiforgeryToken(signal) } },
    );
    return revoked.client;
  }

  private async send<T>(url: string, signal: AbortSignal, init: RequestInit = {}): Promise<T> {
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
}
