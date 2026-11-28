import { Injectable } from '@angular/core';
import {
  type NewSource,
  type Region,
  type SourceDetail,
  type SourceKind,
  type SourcePage,
  type SourceTestResult,
  type SourceQuery,
  type SourcesApi,
} from '@kx/api';
import { antiforgeryToken } from '../session/antiforgery';
import { readJson } from '../session/read-json';

/** The source catalogue over fetch, with the staff session's cookie (ADR 0005). */
@Injectable()
export class HttpSourcesApi implements SourcesApi {
  async list(query: SourceQuery, signal: AbortSignal): Promise<SourcePage> {
    const search = new URLSearchParams({
      page: String(query.page),
      page_size: String(query.pageSize),
    });
    if (query.kind) search.set('kind', query.kind);
    return readJson<SourcePage>(`/admin/v1/sources?${search}`, signal);
  }

  async regions(signal: AbortSignal): Promise<Region[]> {
    return (await readJson<{ items: Region[] }>('/admin/v1/regions', signal)).items;
  }

  async testAddress(
    kind: SourceKind,
    address: string,
    signal: AbortSignal,
  ): Promise<SourceTestResult> {
    const token = await antiforgeryToken(signal);
    return readJson<SourceTestResult>('/admin/v1/sources/test', signal, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', 'X-XSRF-TOKEN': token },
      body: JSON.stringify({ kind, address }),
    });
  }

  async testSource(id: string, signal: AbortSignal): Promise<SourceTestResult> {
    const token = await antiforgeryToken(signal);
    return readJson<SourceTestResult>(`/admin/v1/sources/${encodeURIComponent(id)}/test`, signal, {
      method: 'POST',
      headers: { 'X-XSRF-TOKEN': token },
    });
  }

  async create(source: NewSource, signal: AbortSignal): Promise<SourceDetail> {
    const token = await antiforgeryToken(signal);
    return readJson<SourceDetail>('/admin/v1/sources', signal, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', 'X-XSRF-TOKEN': token },
      body: JSON.stringify(source),
    });
  }
}
