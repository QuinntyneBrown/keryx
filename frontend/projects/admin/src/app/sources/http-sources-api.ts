import { Injectable } from '@angular/core';
import {
  ApiProblem,
  type NewSource,
  type Region,
  type SourceDetail,
  type SourcePage,
  type SourceQuery,
  type SourcesApi,
} from '@kx/api';
import { antiforgeryToken } from '../session/antiforgery';

/** The source catalogue over fetch, with the staff session's cookie (ADR 0005). */
@Injectable()
export class HttpSourcesApi implements SourcesApi {
  async list(query: SourceQuery, signal: AbortSignal): Promise<SourcePage> {
    const search = new URLSearchParams({
      page: String(query.page),
      page_size: String(query.pageSize),
    });
    if (query.kind) search.set('kind', query.kind);
    return this.read<SourcePage>(`/admin/v1/sources?${search}`, signal);
  }

  async regions(signal: AbortSignal): Promise<Region[]> {
    return (await this.read<{ items: Region[] }>('/admin/v1/regions', signal)).items;
  }

  async create(source: NewSource, signal: AbortSignal): Promise<SourceDetail> {
    const token = await antiforgeryToken(signal);
    return this.read<SourceDetail>('/admin/v1/sources', signal, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', 'X-XSRF-TOKEN': token },
      body: JSON.stringify(source),
    });
  }

  private async read<T>(url: string, signal: AbortSignal, init: RequestInit = {}): Promise<T> {
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

/** The ProblemDetails of a refusal, or just its status when the body is not one. */
export async function problemOf(response: Response): Promise<ApiProblem> {
  const body = await response.json().catch(() => null);
  const code = typeof body?.code === 'string' ? body.code : undefined;
  const reference = typeof body?.trace_id === 'string' ? body.trace_id : undefined;
  const errors = body?.errors && typeof body.errors === 'object' ? body.errors : undefined;
  return new ApiProblem(response.status, code, reference, errors);
}
